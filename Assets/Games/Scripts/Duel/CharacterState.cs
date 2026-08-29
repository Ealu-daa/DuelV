using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterState
{
    public CharacterData data;

    public int currentHP;
    public int currentUltGauge;
    public int currentMaxHP;
    public int currentAttack;
    public int currentDefense;
    public bool isDefending;
    public bool hasActedThisTurn = false;
    public bool everActiveAsForward = false; // マスタリーXP付与用: この試合中に1回でも自チームのFWとして出たか
    public List<StatusEffect> activeEffects = new List<StatusEffect>();
    public int defenseExpiresAtHalfTurn = -1;
    public CharacterState protectedBy;
    public bool isProtectingAlly;
    public CharacterState twinBurdenPartner; // Aegisのツインバーデン: 設定されていれば被弾時にこの相手と半分ずつ分担する
    public List<CatalystInstance> catalysts = new List<CatalystInstance>();
    public bool hasUsedCatalystThisHalfTurn = false;

    // Reaperのソウルアブソーブ用: 誰に・いつ倒されたか、その魂は既に吸収済みか
    public CharacterState defeatedBy;
    public int defeatedAtHalfTurn = -1;
    public bool soulAbsorbed = false;

    // Chronicleのハンティンググラウンド用: 「獲物」として互いにしか攻撃できない相手
    public CharacterState huntBoundTo;

    // Morpheのメタモルフォシス用(常時パッシブ。ウルトでの事前準備は不要)
    public bool isSuspendedAnimation;     // 現在仮死状態か
    public bool hasRevived;               // 仮死状態から生き返った後か(見た目切り替え用。以後ずっとtrueのまま)
    public int morpheMaxHpBonusTotal;     // スキルで積み上げた最大HP増加分の合計(復活時に攻撃力へ変換)

    // UIの2段階選択で選んだ内容を、行動キュー実行時までActor側で一時的に運ぶための橋渡し
    public CharacterState pendingEncoreIncoming;   // Graceのアンコール: 選んだ交代先BK
    public CatalystInstance pendingHandpickChoice; // Lapseのハンドピーク: 選んだ相手のカタリスト

    // Doubleのトランスフォーム用: 攻撃力・防御力が入れ替わっているか
    public bool isTransformed;


    public event Action OnStateChanged; // なんか変わったとき

    public bool IsDefeated => currentHP <= 0;

    public bool swapHandled = false;

    public CharacterState(CharacterData sourceData)
    {
        data = sourceData;
        currentHP = sourceData.maxHP;
        currentMaxHP = sourceData.maxHP;
        currentAttack = sourceData.attack;
        currentDefense = sourceData.defense;
        #if UNITY_EDITOR
                currentUltGauge = sourceData.maxUltGauge; // デバッグ用: 最初からMAX
        #else
                currentUltGauge = 0;
        #endif
    }

    public void NotifyChanged()
    {
        OnStateChanged?.Invoke();
    }

    public bool IsStunned(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "行動不能" && currentHalfTurn <= e.expiresAtHalfTurn);
    }
    public void ResetUltGauge()
    {
        currentUltGauge = 0;
        NotifyChanged();
    }
    /// <summary>
    /// ダメージを受ける。attackerが分かる場合は渡すと反撃/貫通/遺志などの反応が働く。
    /// isReactionDamageは反撃・貫通・遺志などの"反応"としてのダメージであることを示し、
    /// これがtrueの間は反応が連鎖しない(反撃し合う無限ループ防止)。
    /// </summary>
    public void TakeDamage(int amount, CharacterState attacker = null, bool isReactionDamage = false)
    {
        var barrier = activeEffects.Find(e => e.effectName == "バリア");
        if (barrier != null)
        {
            activeEffects.Remove(barrier); // 被弾したら消滅
            NotifyChanged();
            AddUltGauge(1); // 0ダメージでも被弾扱いなのでゲージ+1
            return;
        }

        int remaining = amount;

        // 堅守: 1回受けるダメージをまとめて軽減し、使い切りで消える
        var kenshu = activeEffects.Find(e => e.effectName == "堅守");
        if (kenshu != null)
        {
            remaining = Mathf.Max(0, remaining - kenshu.value);
            activeEffects.Remove(kenshu);
        }

        // シールド: 値の分だけ吸収し、削れても残りがあれば持続する(汎用エフェクト)
        var shield = activeEffects.Find(e => e.effectName == "シールド");
        if (shield != null)
        {
            int absorbed = Mathf.Min(shield.value, remaining);
            shield.value -= absorbed;
            remaining -= absorbed;
            if (shield.value <= 0)
                activeEffects.Remove(shield);
        }

        int actualDamage = isDefending ? Mathf.Max(0, remaining - currentDefense) : remaining;

        // 不屈: 本来死ぬダメージを受けてもHP1で耐える(1回限り、消費で解除)
        var fukutsu = activeEffects.Find(e => e.effectName == "不屈");
        if (fukutsu != null && actualDamage >= currentHP)
        {
            currentHP = 1;
            activeEffects.Remove(fukutsu);
            NotifyChanged();
            AddUltGauge(1);
            ApplyCounterReaction(actualDamage, attacker, isReactionDamage);
            return;
        }

        // メタモルフォシス(常時パッシブ): 本来戦闘不能になるところを、繰り上がらず仮死状態(HP18固定)に移行する
        if (data.id == CharacterId.Morphe && !isSuspendedAnimation && actualDamage >= currentHP)
        {
            EnterSuspendedAnimation();
            NotifyChanged();
            AddUltGauge(1);
            ApplyCounterReaction(actualDamage, attacker, isReactionDamage);
            return;
        }

        int hpBeforeDamage = currentHP;
        currentHP = Mathf.Max(0, currentHP - actualDamage);
        NotifyChanged();
        AddUltGauge(1);

        if (hpBeforeDamage > 0 && currentHP <= 0)
        {
            defeatedBy = attacker;
            defeatedAtHalfTurn = TurnManager.Instance != null ? TurnManager.Instance.currentHalfTurn : -1;
            soulAbsorbed = false;
            ClearSourcedEffects(); // 例: 自分が付与した衰弱スタックなどをリセットする
        }

        ApplyCounterReaction(actualDamage, attacker, isReactionDamage);
        ApplyDeathReactions(actualDamage, hpBeforeDamage, attacker, isReactionDamage);
    }

    // 自分が付与元(source)になっている効果を、戦場全体から探して解除する(例: チェインが死んだら衰弱スタックが消える)
    private void ClearSourcedEffects()
    {
        if (BattleManager.Instance == null) return;

        var everyone = BattleManager.Instance.PlayerTeam.AllCharacters()
            .Concat(BattleManager.Instance.EnemyTeam.AllCharacters());

        foreach (var c in everyone)
        {
            var sourced = c.activeEffects.FindAll(e => e.source == this);
            foreach (var e in sourced)
            {
                StatusEffectBehaviors.InvokeOnExpire(e, c);
                c.activeEffects.Remove(e);
            }
        }
    }

    // 現在の状態に応じて表示すべきスプライトを返す(Morphe以外は常にcharacterSpriteのまま)
    public Sprite GetDisplaySprite()
    {
        if (isSuspendedAnimation && data.suspendedAnimationSprite != null) return data.suspendedAnimationSprite;
        if (hasRevived && data.revivedSprite != null) return data.revivedSprite;
        return data.characterSprite;
    }

    // 仮死状態へ移行する(HP18固定、行動不能を付与して以後の行動を止める)
    private void EnterSuspendedAnimation()
    {
        currentHP = 18;
        isSuspendedAnimation = true;
        activeEffects.Add(new StatusEffect { effectName = "行動不能", expiresAtHalfTurn = int.MaxValue });
    }

    // 仮死中に交代すると、そのまま戦闘不能扱いになる(モルフェ専用)。交代処理側から呼ぶ
    public void ResolveSuspendedSwapOut()
    {
        if (!isSuspendedAnimation) return;

        isSuspendedAnimation = false;
        currentHP = 0;

        var stun = activeEffects.Find(e => e.effectName == "行動不能");
        if (stun != null) activeEffects.Remove(stun);

        ClearSourcedEffects();
        NotifyChanged();
    }

    public void TakeInterceptedDamage(int amount, CharacterState attacker = null, bool isReactionDamage = false)
    {
        var barrier = activeEffects.Find(e => e.effectName == "バリア");
        if (barrier != null)
        {
            activeEffects.Remove(barrier);
            NotifyChanged();
            AddUltGauge(1);
            return;
        }

        int actualDamage = Mathf.Max(0, amount - currentDefense);

        var fukutsu = activeEffects.Find(e => e.effectName == "不屈");
        if (fukutsu != null && actualDamage >= currentHP)
        {
            currentHP = 1;
            activeEffects.Remove(fukutsu);
            NotifyChanged();
            AddUltGauge(1);
            ApplyCounterReaction(actualDamage, attacker, isReactionDamage);
            return;
        }

        if (data.id == CharacterId.Morphe && !isSuspendedAnimation && actualDamage >= currentHP)
        {
            EnterSuspendedAnimation();
            NotifyChanged();
            AddUltGauge(1);
            ApplyCounterReaction(actualDamage, attacker, isReactionDamage);
            return;
        }

        int hpBeforeDamage = currentHP;
        currentHP = Mathf.Max(0, currentHP - actualDamage);
        NotifyChanged();
        AddUltGauge(1);

        if (hpBeforeDamage > 0 && currentHP <= 0)
        {
            defeatedBy = attacker;
            defeatedAtHalfTurn = TurnManager.Instance != null ? TurnManager.Instance.currentHalfTurn : -1;
            soulAbsorbed = false;
            ClearSourcedEffects(); // 例: 自分が付与した衰弱スタックなどをリセットする
        }

        ApplyCounterReaction(actualDamage, attacker, isReactionDamage);
        ApplyDeathReactions(actualDamage, hpBeforeDamage, attacker, isReactionDamage);
    }

    // 反撃/リベンジフィールド/シンクザペイン: 被弾に反応する効果(反応ダメージからは連鎖しない)
    private void ApplyCounterReaction(int actualDamage, CharacterState attacker, bool isReactionDamage)
    {
        if (isReactionDamage) return;

        if (attacker != null && !attacker.IsDefeated)
        {
            if (activeEffects.Exists(e => e.effectName == "反撃"))
            {
                attacker.TakeDamage(currentAttack, this, isReactionDamage: true);
            }

            if (activeEffects.Exists(e => e.effectName == "リベンジフィールド"))
            {
                int counterDamage = Mathf.FloorToInt(attacker.currentAttack / 2f);
                if (counterDamage > 0)
                    attacker.TakeDamage(counterDamage, this, isReactionDamage: true);
            }
        }

        // シンクザペイン: 自分が受けたダメージの半分(切り捨て)をもう一方の味方FWにも分配する
        if (activeEffects.Exists(e => e.effectName == "シンクザペイン") && TurnManager.Instance != null)
        {
            int shared = Mathf.FloorToInt(actualDamage / 2f);
            if (shared > 0)
            {
                var team = TurnManager.Instance.GetTeamOf(this);
                var other = team.forwards.Find(c => c != this && !c.IsDefeated);
                other?.TakeDamage(shared, attacker, isReactionDamage: true);
            }
        }
    }

    // 遺志(自分が倒された時)・貫通(攻撃者が持つ、倒した時の超過ダメージ)を処理する
    private void ApplyDeathReactions(int actualDamage, int hpBeforeDamage, CharacterState attacker, bool isReactionDamage)
    {
        if (isReactionDamage) return;

        bool killedThisHit = hpBeforeDamage > 0 && currentHP <= 0;
        if (!killedThisHit) return;

        var ishi = activeEffects.Find(e => e.effectName == "遺志");
        if (ishi != null && attacker != null && !attacker.IsDefeated)
        {
            attacker.TakeDamage(currentDefense, this, isReactionDamage: true);
        }

        if (attacker != null)
        {
            var kantuu = attacker.activeEffects.Find(e => e.effectName == "貫通");
            if (kantuu != null)
            {
                int overflow = actualDamage - hpBeforeDamage;
                if (overflow > 0 && TurnManager.Instance != null)
                {
                    var team = TurnManager.Instance.GetTeamOf(this);
                    var otherFw = team.forwards.Find(c => c != this && !c.IsDefeated);
                    otherFw?.TakeDamage(overflow, attacker, isReactionDamage: true);
                }
            }
        }
    }
    public void Heal(int amount)
    {
        currentHP = Mathf.Min(currentMaxHP, currentHP + amount);
        NotifyChanged();
    }

    // 効果を付与する共通口。鎖縛中は新規バフ(isDebuff=false)だけ弾く(デバフは通す)
    public void AddEffect(StatusEffect effect)
    {
        if (!effect.isDebuff && IsBuffBlocked(TurnManager.Instance != null ? TurnManager.Instance.currentHalfTurn : 0))
        {
            return;
        }
        activeEffects.Add(effect);
    }

    public bool IsBuffBlocked(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "鎖縛" && currentHalfTurn <= e.expiresAtHalfTurn);
    }

    // Doubleのトランスフォーム: 交代でFWを離れる時に攻撃力・防御力を元に戻す
    public void RevertTransformIfNeeded()
    {
        if (!isTransformed) return;
        (currentAttack, currentDefense) = (currentDefense, currentAttack);
        isTransformed = false;
    }

    // 不屈・遺志: 「使ったら交代しない限り発生するまで持続」の仕様通り、発動前に交代すると消える
    public void ClearSwapResetEffects()
    {
        activeEffects.RemoveAll(e => e.effectName == "不屈" || e.effectName == "遺志");
    }
    public void ActivateDefense(int expireAtHalfTurn)
    {
        isDefending = true;
        defenseExpiresAtHalfTurn = expireAtHalfTurn;
    }

    public void ExpireDefenseIfNeeded(int currentHalfTurn)
    {
        if (isDefending && currentHalfTurn > defenseExpiresAtHalfTurn)
        {
            isDefending = false;
            defenseExpiresAtHalfTurn = -1;
        }
    }

    public void AddUltGauge(int amount)
    {
        currentUltGauge = Mathf.Min(data.maxUltGauge, currentUltGauge + amount);
        NotifyChanged();
    }


    public void MarkAsActed()
    {
        hasActedThisTurn = true;
    }

    public void ResetTurnState()
    {
        hasActedThisTurn = false;
    }
    public bool IsSilenced(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "サイレンス" && currentHalfTurn <= e.expiresAtHalfTurn);
    }
    public bool IsConfused(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "コンフュージョン" && currentHalfTurn <= e.expiresAtHalfTurn);
    }
    // エインのウルト「ネビュラ・トリップ」用: 対象指定効果の対象を選択可能な相手からランダムに差し替える
    public bool IsTargetRandomized(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "ターゲットランダム化" && currentHalfTurn <= e.expiresAtHalfTurn);
    }
    // エインのスキル「トゥ・ザ・ミスト」用: 自分の行動(通常攻撃/スキル/ウルト)が一定確率で不発になる
    public bool IsAccuracyDown(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "命中率低下" && currentHalfTurn <= e.expiresAtHalfTurn);
    }
    public bool IsSwapLocked(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "交代封じ" && currentHalfTurn <= e.expiresAtHalfTurn);
    }
    public bool IsUltSealed(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "ULT封じ" && currentHalfTurn <= e.expiresAtHalfTurn);
    }

    // ウルトが今使えるか(ゲージMAX+封印されていないこと。モルフェは仮死中しか使えない特殊条件を持つ)
    public bool CanUseUltimate(int currentHalfTurn)
    {
        if (currentUltGauge < data.maxUltGauge) return false;
        if (IsUltSealed(currentHalfTurn)) return false;
        if (data.id == CharacterId.Morphe && !isSuspendedAnimation) return false; // 生きている間は使えない
        return true;
    }
    public bool IsDefenseOnly(int currentHalfTurn)
    {
        return activeEffects.Exists(e => e.effectName == "献身" && currentHalfTurn <= e.expiresAtHalfTurn);
    }

    // ハンティンググラウンド中(huntBoundTo設定中)は、拘束したクロニクル/対象以外からは対象にされない
    public bool IsTargetableBy(CharacterState potentialActor)
    {
        return huntBoundTo == null || huntBoundTo == potentialActor;
    }
}
