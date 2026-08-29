using System.Collections.Generic;
using UnityEngine;

public class CpuController : MonoBehaviour
{
    public static CpuController Instance;

    // Lv1: ULTゲージがMAXでなければこの重みで抽選する(通常攻撃/スキル/防御/交代)
    private const float AttackWeight = 40f;
    private const float SkillWeight = 35f;
    private const float DefenseWeight = 15f;
    private const float SwapWeight = 10f;

    // Lv2: 通常攻撃で撃破が狙える相手がいる時、通常攻撃の重みに上乗せするボーナス
    private const float KillSecureBonus = 30f;

    // Lv2: 毎ハーフターン、手持ちの未使用カタリストを使うかどうかの基礎確率
    private const float CatalystUseChance = 0.55f;

    // Lv2: 撃破/対象選定で「僅差」とみなす許容幅(この範囲内はランダムに揺らして固定パターン化を避ける)
    private const int NeediestHpTieBreakRange = 3;
    private const float NeediestRatioTieBreakRange = 0.05f;

    void Awake()
    {
        Instance = this;
    }

    // CPU側のFW2体分の行動をまとめて決定・実行する
    public void ExecuteCpuTurn()
    {
        var cpuTeam = BattleManager.Instance.EnemyTeam;
        var enemyForwards = BattleManager.Instance.PlayerTeam.forwards;
        int currentHalfTurn = TurnManager.Instance.currentHalfTurn;

        // Lv2: インデックスで回す(転身カタリストでこの枠の中身が入れ替わることがあるため、
        // カタリスト使用後に「今この枠に居るキャラ」を改めて取り直す必要がある)
        for (int slot = 0; slot < cpuTeam.forwards.Count; slot++)
        {
            var actor = cpuTeam.forwards[slot];
            if (actor.IsDefeated || actor.hasActedThisTurn) continue;

            // 仮死中(モルフェ)は「行動不能」を持っていてもウルト/交代だけは選べるので、丸ごとのスキップ対象からは外す
            if (actor.IsStunned(currentHalfTurn) && !actor.isSuspendedAnimation)
            {
                actor.MarkAsActed(); // 行動不能なので何もせず終了扱いにする
                continue;
            }

            // Lv2: カタリストは通常行動(攻撃/スキル/防御/交代)とは別枠の行動なので先に判断する
            TryUseCatalyst(actor, currentHalfTurn);

            // 転身で入れ替わっていればここで新しい占有者を取り直す。奇襲の反撃等で
            // 戦闘不能になっていた場合はこの枠の行動を諦める(安全策)
            actor = cpuTeam.forwards[slot];
            if (actor.IsDefeated || actor.hasActedThisTurn) continue;
            if (actor.IsStunned(currentHalfTurn) && !actor.isSuspendedAnimation)
            {
                actor.MarkAsActed();
                continue;
            }

            DecideAndQueueAction(actor, enemyForwards, currentHalfTurn);
        }
    }

    // Lv1相当+Lv2補正: ULTゲージMAXなら確定発動、それ以外は重み付きランダムで1つ選ぶ
    // Lv2: 通常攻撃で撃破が狙える相手がいる時は通常攻撃の重みを底上げする
    private void DecideAndQueueAction(CharacterState actor, List<CharacterState> enemyForwards, int currentHalfTurn)
    {
        if (actor.IsDefenseOnly(currentHalfTurn))
        {
            // 献身中は防御しか選べない
            TurnManager.Instance.QueueAction(actor, ActionType.Defense, null);
            actor.MarkAsActed();
            return;
        }

        if (actor.CanUseUltimate(currentHalfTurn))
        {
            var ultScope = SkillRegistry.GetUltimateTargetScope(actor.data.id);
            var ultTarget = PickTargetForScope(ultScope, actor, enemyForwards);
            TurnManager.Instance.QueueAction(actor, ActionType.Ultimate, ultTarget);
            actor.MarkAsActed();
            return;
        }

        // 仮死中(モルフェ)はウルトか交代しか選べない。ゲージがまだ溜まっていないなら、
        // 交代で見捨てず(=生き返るチャンスを潰さない)、素直にこの半ターンは待つ
        if (actor.isSuspendedAnimation)
        {
            actor.MarkAsActed();
            return;
        }

        var myTeam = TurnManager.Instance.GetTeamOf(actor);
        bool hasAliveBackup = myTeam.backups.Exists(c => !c.IsDefeated) && !actor.IsSwapLocked(currentHalfTurn);
        bool canUseSkill = !actor.IsSilenced(currentHalfTurn);
        bool isHuntBound = actor.huntBoundTo != null && !actor.huntBoundTo.IsDefeated;

        // Lv2: 通常攻撃だけで倒し切れそうな相手がいるなら、確実に狩りに行けるよう通常攻撃を優先させる
        // (防御力までは加味せず「残りHP<=自分の攻撃力」の粗い見積もりだが、撃ち漏らし防止としては十分)
        var neediestEnemy = isHuntBound ? actor.huntBoundTo : PickNeediestEnemy(enemyForwards, actor);
        bool canSecureKill = neediestEnemy != null && neediestEnemy.currentHP <= actor.currentAttack;

        // 選べない選択肢(スキル/交代/[拘束中なら防御])の重みは通常攻撃に繰り上げる
        float attackWeight = AttackWeight + (canUseSkill ? 0f : SkillWeight) + (hasAliveBackup ? 0f : SwapWeight)
            + (isHuntBound ? DefenseWeight : 0f) + (canSecureKill ? KillSecureBonus : 0f);
        float skillWeight = canUseSkill ? SkillWeight : 0f;
        float swapWeight = hasAliveBackup ? SwapWeight : 0f;
        float defenseWeight = isHuntBound ? 0f : DefenseWeight; // ハンティンググラウンド中は防御行動を選べない
        float total = attackWeight + skillWeight + defenseWeight + swapWeight;

        float roll = Random.Range(0f, total);

        if (roll < attackWeight)
        {
            // クロニクルのハンティンググラウンド中は、互いにしか通常攻撃できない
            // Lv2: それ以外は乱数選択ではなく、最もHPが低い(=仕留めやすい)相手を優先する
            var target = isHuntBound ? actor.huntBoundTo : neediestEnemy;
            if (target == null)
            {
                actor.MarkAsActed();
                return;
            }
            TurnManager.Instance.QueueAction(actor, ActionType.Attack, target);
        }
        else if (roll < attackWeight + skillWeight)
        {
            var scope = SkillRegistry.GetSkillTargetScope(actor.data.id);
            var target = PickTargetForScope(scope, actor, enemyForwards);
            TurnManager.Instance.QueueAction(actor, ActionType.Skill, target);
        }
        else if (roll < attackWeight + skillWeight + defenseWeight)
        {
            TurnManager.Instance.QueueAction(actor, ActionType.Defense, null);
        }
        else
        {
            // Lv2: 交代先はランダムではなく、最もHPが高い(万全な)控えを前線に出す
            var aliveBackups = myTeam.backups.FindAll(c => !c.IsDefeated);
            var target = PickHealthiestAlly(aliveBackups);
            TurnManager.Instance.QueueAction(actor, ActionType.Swap, target);
        }

        actor.MarkAsActed();
    }

    // Lv2: 手持ちの未使用カタリストから、使うか/どれを使うかを状況に応じて決めて実行する
    private void TryUseCatalyst(CharacterState actor, int currentHalfTurn)
    {
        // 先行1ターン目はどちらの陣営もサブカード使用不可
        bool isCpuFirst = !TurnManager.Instance.isPlayerFirst;
        if (isCpuFirst && currentHalfTurn == 0) return;
        if (actor.hasUsedCatalystThisHalfTurn) return;
        if (actor.isSuspendedAnimation) return; // 仮死中はウルトか交代しか選べない

        var available = actor.catalysts.FindAll(c => !c.isUsed && !c.IsDisabled(currentHalfTurn));
        if (available.Count == 0) return;

        if (Random.value > CatalystUseChance) return; // 毎ハーフターン必ず使うわけではない

        var myTeam = TurnManager.Instance.GetTeamOf(actor);
        bool allyNeedsHelp = new List<CharacterState>(myTeam.AllCharacters())
            .Exists(c => !c.IsDefeated && c.currentMaxHP > 0 && (float)c.currentHP / c.currentMaxHP < 0.6f);

        // スコープごとの基礎重みに応じて1枚選ぶ(味方が減っていれば治癒/支援系を優先しやすくする)
        CatalystInstance best = null;
        float bestScore = float.MinValue;
        foreach (var instance in available)
        {
            float score = Random.value;
            switch (CatalystRegistry.GetCatalystTargetScope(instance.Id))
            {
                case ActionUI.TargetScope.AllyAny:
                    score += allyNeedsHelp ? 3f : 0.2f;
                    break;
                case ActionUI.TargetScope.AllyForward:
                    score += allyNeedsHelp ? 1.5f : 1f;
                    break;
                case ActionUI.TargetScope.EnemyForward:
                    score += 1.2f;
                    break;
                default: // Self(転身/模倣/奇襲もここ。宣言スコープはSelfだが実対象は個別に選ぶ)
                    score += 1f;
                    break;
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = instance;
            }
        }

        if (best != null) ExecuteCatalystSmart(actor, best);
    }

    // Lv2: カタリストの実対象を、ActionUIの特殊ケース(転身/奇襲/模倣)も踏まえて状況に応じて選ぶ
    private void ExecuteCatalystSmart(CharacterState actor, CatalystInstance instance)
    {
        var myTeam = TurnManager.Instance.GetTeamOf(actor);
        var enemyTeam = myTeam == BattleManager.Instance.EnemyTeam ? BattleManager.Instance.PlayerTeam : BattleManager.Instance.EnemyTeam;

        // 転身: 交代先は最もHPが高い(万全な)控えを選ぶ
        if (instance.Id == CatalystId.Tenshin)
        {
            var target = PickHealthiestAlly(myTeam.backups.FindAll(c => !c.IsDefeated));
            if (target == null) return; // 生存中の控えが居なければ使わない(消費しない)
            CatalystRegistry.ExecuteCatalyst(actor, target, instance);
            return;
        }

        // 奇襲: 自分自身が相手BKを攻撃する。最もHPが低い相手BKを狙う
        if (instance.Id == CatalystId.Kishuu)
        {
            var target = PickNeediestEnemy(enemyTeam.backups, null);
            if (target == null) return;
            CatalystRegistry.ExecuteCatalyst(actor, target, instance);
            return;
        }

        // 模倣: コピー元が無ければ何もせず終える(消費しない)
        if (instance.Id == CatalystId.Mohou)
        {
            var mimicId = CatalystRegistry.GetMohouMimicCandidate(actor);
            if (mimicId == null) return;

            var mimicScope = CatalystRegistry.GetCatalystTargetScope(mimicId.Value);
            var mimicTarget = mimicScope == ActionUI.TargetScope.Self ? actor : PickTargetForScope(mimicScope, actor, enemyTeam.forwards);
            if (mimicTarget == null) return;
            CatalystRegistry.ExecuteCatalyst(actor, mimicTarget, instance);
            return;
        }

        var scope = CatalystRegistry.GetCatalystTargetScope(instance.Id);
        var target2 = scope == ActionUI.TargetScope.Self ? actor : PickTargetForScope(scope, actor, enemyTeam.forwards);
        if (target2 == null) return; // 有効な対象が居なければ使わない(消費しない)
        CatalystRegistry.ExecuteCatalyst(actor, target2, instance);
    }

    // Lv2: TargetScopeに応じてスキル/ウルト/カタリストの対象を選ぶ(状況に応じたスマート選択)
    private CharacterState PickTargetForScope(ActionUI.TargetScope scope, CharacterState actor, List<CharacterState> enemyForwards)
    {
        var myTeam = TurnManager.Instance.GetTeamOf(actor);

        switch (scope)
        {
            case ActionUI.TargetScope.Self:
                return actor;
            case ActionUI.TargetScope.EnemyForward:
                return PickNeediestEnemy(enemyForwards, actor);
            case ActionUI.TargetScope.AllyForward:
                return PickNeediestAlly(myTeam.forwards);
            case ActionUI.TargetScope.EnemyAll:
                return PickNeediestEnemy(new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()), actor);
            case ActionUI.TargetScope.AllyAny:
                return PickNeediestAlly(new List<CharacterState>(myTeam.AllCharacters()));
            default:
                return PickNeediestEnemy(enemyForwards, actor);
        }
    }

    // 生存かつ対象可能な候補の中から、最もHPが低い相手を選ぶ(撃破優先/弱点狩り)。僅差なら乱数で揺らす
    private CharacterState PickNeediestEnemy(List<CharacterState> candidates, CharacterState actor)
    {
        var valid = candidates.FindAll(c => !c.IsDefeated && (actor == null || c.IsTargetableBy(actor)));
        if (valid.Count == 0) return null;

        int minHp = int.MaxValue;
        foreach (var c in valid) if (c.currentHP < minHp) minHp = c.currentHP;

        var lowest = valid.FindAll(c => c.currentHP <= minHp + NeediestHpTieBreakRange);
        return lowest[Random.Range(0, lowest.Count)];
    }

    // 生存中の味方の中から、残りHP%が最も低い(最も助けが必要な)相手を選ぶ。僅差なら乱数で揺らす
    private CharacterState PickNeediestAlly(List<CharacterState> candidates)
    {
        var valid = candidates.FindAll(c => !c.IsDefeated);
        if (valid.Count == 0) return null;

        float minRatio = float.MaxValue;
        foreach (var c in valid)
        {
            float ratio = c.currentMaxHP > 0 ? (float)c.currentHP / c.currentMaxHP : 0f;
            if (ratio < minRatio) minRatio = ratio;
        }

        var lowest = valid.FindAll(c =>
        {
            float ratio = c.currentMaxHP > 0 ? (float)c.currentHP / c.currentMaxHP : 0f;
            return ratio <= minRatio + NeediestRatioTieBreakRange;
        });
        return lowest[Random.Range(0, lowest.Count)];
    }

    // 生存中の候補の中から、最もHPが高い(万全な)キャラを選ぶ(交代先/転身先の選定用)
    private CharacterState PickHealthiestAlly(List<CharacterState> candidates)
    {
        CharacterState best = null;
        int bestHp = -1;
        foreach (var c in candidates)
        {
            if (c.IsDefeated) continue;
            if (c.currentHP > bestHp)
            {
                bestHp = c.currentHP;
                best = c;
            }
        }
        return best;
    }
}
