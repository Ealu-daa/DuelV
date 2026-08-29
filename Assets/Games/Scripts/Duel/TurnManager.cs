using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;
using static SkillRegistry;


public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [SerializeField] private ActionUI actionUI;

    public int currentTurnNumber = 1;      // 表示用のターン数(1ターン=ハーフターン2回)
    public int currentHalfTurn = 0;        // 内部的な手番カウンター(ずっと増え続ける)
    public bool isPlayerFirst = true;
    public bool isPlayerTurnNow;

    private int currentActionIndex = 0;

    private List<QueuedAction> queuedActions = new List<QueuedAction>();
    private Team pendingSwapTeam;
    private int pendingSwapFwSlot;
    private bool pendingSwapFromTick = false;         // 自チームの繰り上げ待ちが、行動キュー処理中ではなくTick後の死亡解決から来たものか
    private bool pendingEnemyDeathSwapFromTick = false; // 相手チーム(オンライン)側の繰り上げ待ちについて同上

    [SerializeField] private CharacterSlotUI[] playerFwSlots;   // サイズ2
    [SerializeField] private CharacterSlotUI[] playerBkSlots;   // サイズ3
    [SerializeField] private CharacterSlotUI[] enemyFwSlots;    // サイズ2
    [SerializeField] private CharacterSlotUI[] enemyBkSlots;    // サイズ3

    [Header("CPU行動の遅延")]
    [SerializeField] private float cpuActionDelay = 1.0f;

    [Header("防御演出")]
    [SerializeField] private Sprite defenseFadeSprite; // キャラ共通の1枚。スキル/ULTのフォールバック(PlayFadeEffect)と同じ仕組みで、防御アクター側のパネルにフェードイン→ホールド→フェードアウトする

    public int PendingEnemyDeathSwapFwSlot { get; private set; } = -1;
    public event Action<int, bool> OnHalfTurnChanged;

    private void Awake()
    {
        Instance = this;
        isPlayerTurnNow = isPlayerFirst;
    }

    private void Start()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnTeamsReady += HandleTeamsReady;
    }

    private void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnTeamsReady -= HandleTeamsReady;
    }

    private void HandleTeamsReady()
    {
        if (isPlayerTurnNow)
        {
            actionUI.TurnStart();
        }
    }

    // 行動選択時にここに積む(即実行しない)
    public void QueueAction(CharacterState actor, ActionType type, CharacterState target)
    {
        // コンフュージョン: 対象指定効果の対象を反転する
        if (target != null && actor.IsConfused(currentHalfTurn))
        {
            target = GetMirroredTarget(actor, target);
        }

        // ターゲットランダム化(エインのネビュラ・トリップ): 対象指定効果の対象を選択可能な相手からランダムに差し替える
        if (target != null && actor.IsTargetRandomized(currentHalfTurn))
        {
            target = GetRandomAliveForward();
        }

        var queued = new QueuedAction { actor = actor, actionType = type, target = target };
        if (target != null)
        {
            var team = GetTeamOf(target);
            int slot = team.forwards.IndexOf(target);
            Debug.Log($"QueueAction: actor={actor.data.characterName}, target={target.data.characterName}, targetTeam={(team == BattleManager.Instance.PlayerTeam ? "Player" : "Enemy")}, slot={slot}");
            if (slot != -1)
            {
                queued.targetTeam = team;
                queued.targetSlot = slot;
            }
        }
        queuedActions.Add(queued);

        // ↓追加: オンライン対戦なら、自分のFWの行動を送信バッファに積む
        if (BattleManager.Instance.IsOnlineMatch)
        {
            OnlineBattleSync.Instance.BufferOutgoingAction(actor, type, target);
        }
    }

    public void OnTurnEndPressed()
    {
        currentActionIndex = 0;
        ProcessNextAction();
    }

    private void ProcessNextAction()
    {
        if (currentActionIndex >= queuedActions.Count)
        {
            FinishTurn();
            return;
        }
        var action = queuedActions[currentActionIndex];
        bool isSelfTargeted = (action.actionType == ActionType.Skill || action.actionType == ActionType.Ultimate)
            && action.target == action.actor;

        if (action.actionType == ActionType.Swap)
        {
            ExecuteAction(action);
            action.actor.NotifyChanged();
            currentActionIndex++;
            ProcessNextAction();
            return;
        }
        if (action.actionType == ActionType.Defense)
        {
            var defenseActorTeam = GetTeamOf(action.actor);
            var defenseActorPanel = GetFwSlots(defenseActorTeam)[defenseActorTeam.forwards.IndexOf(action.actor)].panel;

            SeManager.PlayDefense();
            defenseActorPanel.PlayFadeEffect(defenseFadeSprite, () =>
            {
                ExecuteAction(action);
                action.actor.NotifyChanged();
                currentActionIndex++;
                ProcessNextAction();
            });
            return;
        }
        if (isSelfTargeted)
        {
            var actorTeam = GetTeamOf(action.actor);
            int actorSlot = actorTeam.forwards.IndexOf(action.actor);
            var actorPanel = GetFwSlots(actorTeam)[actorSlot].panel;
            var selfVisualPanels = GetVisualTargetPanels(action, actorPanel, actorPanel);
            PlayActionEffect(action, selfVisualPanels, () =>
            {
                ExecuteAction(action);
                action.actor.NotifyChanged();
                if (BattleManager.Instance.CheckVictoryCondition())
                    return;
                currentActionIndex++;
                ProcessNextAction();
            });
            return;
        }

        if (action.targetSlot != -1)
        {
            action.target = action.targetTeam.forwards[action.targetSlot];
        }
        CharacterState actualDamagedCharacter = action.target;
        if (action.actionType == ActionType.Attack &&
            action.target.protectedBy != null &&
            !action.target.protectedBy.IsDefeated)
        {
            actualDamagedCharacter = action.target.protectedBy;
        }

        var targetTeam = GetTeamOf(actualDamagedCharacter);
        int targetSlot = targetTeam.forwards.IndexOf(actualDamagedCharacter);
        var targetPanel = GetFwSlots(targetTeam)[targetSlot].panel;

        var actorPanelForVisual = GetFwSlots(GetTeamOf(action.actor))[GetTeamOf(action.actor).forwards.IndexOf(action.actor)].panel;
        var visualPanels = GetVisualTargetPanels(action, actorPanelForVisual, targetPanel);

        PlayActionEffect(action, visualPanels, () =>
        {
            ExecuteAction(action);
            action.actor.NotifyChanged();
            actualDamagedCharacter.NotifyChanged();
            if (BattleManager.Instance.CheckVictoryCondition())
                return;
            WaitForHpDisplayThenContinue(targetPanel, action);
        });
    }

    private void WaitForHpDisplayThenContinue(CharacterPanelUI targetPanel, QueuedAction action)
    {
        void OnCaughtUp()
        {
            targetPanel.OnDisplayCaughtUp -= OnCaughtUp;
            AfterActionExecuted(action);
        }

        targetPanel.OnDisplayCaughtUp += OnCaughtUp;
    }

    private void AfterActionExecuted(QueuedAction action)
    {
        var deadCharacter = FindNewlyDeadCharacter();
        if (deadCharacter != null)
        {
            var team = GetTeamOf(deadCharacter);

            bool hasAliveBackup = team.backups.Exists(c => !c.IsDefeated);
            if (!hasAliveBackup)
            {
                currentActionIndex++;
                ProcessNextAction();
                return;
            }

            if (team == BattleManager.Instance.PlayerTeam)
            {
                pendingSwapTeam = team;
                pendingSwapFwSlot = team.forwards.IndexOf(deadCharacter);
                actionUI.ShowDeathSwapSelection(team.backups);
                return;
            }
            else
            {
                if (BattleManager.Instance.IsOnlineMatch)
                {
                    // 相手本人の選択を待つ。届いたらOnlineBattleSyncが繰り上げを適用し、処理を再開する。
                    PendingEnemyDeathSwapFwSlot = team.forwards.IndexOf(deadCharacter);
                    return;
                }
                else
                {
                    RandomSwap(team, deadCharacter);
                }
            }
        }

        currentActionIndex++;
        ProcessNextAction();
    }


    public void OnDeathSwapTargetSelected(int backupIndex)
    {
        var incoming = pendingSwapTeam.backups[backupIndex];
        var outgoing = pendingSwapTeam.forwards[pendingSwapFwSlot];

        pendingSwapTeam.forwards[pendingSwapFwSlot] = incoming;
        pendingSwapTeam.backups.RemoveAt(backupIndex);
        pendingSwapTeam.backups.Add(outgoing);
        outgoing.RevertTransformIfNeeded(); // Doubleのトランスフォームは交代で元に戻る
        outgoing.ClearSwapResetEffects(); // 不屈・遺志は発動前に交代すると消える

        incoming.hasActedThisTurn = false;

        GetFwSlots(pendingSwapTeam)[pendingSwapFwSlot].Bind(incoming);
        RefreshBackupSlots(pendingSwapTeam);

        if (BattleManager.Instance.IsOnlineMatch)
        {
            OnlineBattleSync.Instance.SendDeathSwapChoice(backupIndex);
        }

        pendingSwapTeam = null;

        if (pendingSwapFromTick)
        {
            pendingSwapFromTick = false;
            ResolveDeathsAfterTick();
        }
        else
        {
            currentActionIndex++;
            ProcessNextAction();
        }
    }


    private void FinishTurn()
    {
        queuedActions.Clear();
        StartNewTurn();
        currentHalfTurn++;
        foreach (var c in BattleManager.Instance.PlayerTeam.AllCharacters()
             .Concat(BattleManager.Instance.EnemyTeam.AllCharacters()))
        {
            c.ExpireDefenseIfNeeded(currentHalfTurn);
            c.hasUsedCatalystThisHalfTurn = false;
        }
        isPlayerTurnNow = !isPlayerTurnNow;
        TickStatusEffects();

        // 火傷/残蝕などのDoTでTick中に戦闘不能になったキャラを、次のターンを始める前に解決する
        ResolveDeathsAfterTick();
    }

    /// <summary>
    /// TickStatusEffects後の戦闘不能を解決してから次のターンへ進む。
    /// 行動キュー処理中の死亡繰り上げ(AfterActionExecuted)とは別経路なので、
    /// pendingSwapFromTick/pendingEnemyDeathSwapFromTickで合流先を区別する。
    /// </summary>
    private void ResolveDeathsAfterTick()
    {
        if (BattleManager.Instance.CheckVictoryCondition())
            return;

        var deadCharacter = FindNewlyDeadCharacter();
        if (deadCharacter != null)
        {
            var team = GetTeamOf(deadCharacter);
            bool hasAliveBackup = team.backups.Exists(c => !c.IsDefeated);

            if (hasAliveBackup)
            {
                if (team == BattleManager.Instance.PlayerTeam)
                {
                    pendingSwapTeam = team;
                    pendingSwapFwSlot = team.forwards.IndexOf(deadCharacter);
                    pendingSwapFromTick = true;
                    actionUI.ShowDeathSwapSelection(team.backups);
                    return;
                }
                else
                {
                    if (BattleManager.Instance.IsOnlineMatch)
                    {
                        // 相手本人の選択を待つ。届いたらOnlineBattleSyncが繰り上げを適用し、処理を再開する。
                        PendingEnemyDeathSwapFwSlot = team.forwards.IndexOf(deadCharacter);
                        pendingEnemyDeathSwapFromTick = true;
                        return;
                    }
                    else
                    {
                        RandomSwap(team, deadCharacter);
                    }
                }
            }

            // まだ他に新規の戦闘不能キャラがいないか続けて確認する(FW最大4体なので再帰は有限)
            ResolveDeathsAfterTick();
            return;
        }

        ContinueTurnStartAfterDeaths();
    }

    private void ContinueTurnStartAfterDeaths()
    {
        if (isPlayerTurnNow == isPlayerFirst)
        {
            currentTurnNumber++;
        }

        // 状態が完全に片付いた(死亡繰り上げの選択待ちなどが残っていない)このタイミングで、
        // オンライン対戦ならリロード後の再接続用スナップショットを保存する
        BattleSnapshot.WriteSnapshot();

        OnHalfTurnChanged?.Invoke(currentTurnNumber, isPlayerTurnNow);

        if (!isPlayerTurnNow)
        {
            if (BattleManager.Instance.IsOnlineMatch)
            {
                // 何もしない。相手からの行動は既に送信済みで、ここではその到着を待つだけ。
            }
            else
            {
                StartCoroutine(RunCpuTurnWithDelay());
            }
        }
        if (isPlayerTurnNow)
        {
            actionUI.TurnStart();
        }
    }

    private IEnumerator RunCpuTurnWithDelay()
    {
        yield return new WaitForSeconds(cpuActionDelay);
        CpuController.Instance.ExecuteCpuTurn();
        OnTurnEndPressed();
    }

    private CharacterState FindNewlyDeadCharacter()
    {
        foreach (var c in BattleManager.Instance.PlayerTeam.forwards)
        {
            if (c.IsDefeated && !c.swapHandled)
            {
                c.swapHandled = true;
                SeManager.PlayCharacterDefeated();
                return c;
            }
        }
        foreach (var c in BattleManager.Instance.EnemyTeam.forwards)
        {
            if (c.IsDefeated && !c.swapHandled)
            {
                c.swapHandled = true;
                SeManager.PlayCharacterDefeated();
                return c;
            }
        }
        return null;
    }

    public void ApplyStatusEffect(CharacterState target, string name, int turns, int value)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + turns;

        target.AddEffect(new StatusEffect
        {
            effectName = name,
            expiresAtHalfTurn = expireAt,
            value = value
        });
    }

    private void TickStatusEffects()
    {
        foreach (var character in BattleManager.Instance.PlayerTeam.AllCharacters()
                 .Concat(BattleManager.Instance.EnemyTeam.AllCharacters()))
        {
            // DoT/HoT: 期限が切れていない継続効果を毎ハーフターン適用する(正=回復、負=ダメージ)
            var periodicEffects = character.activeEffects.FindAll(e => e.tickValue != 0 && currentHalfTurn <= e.expiresAtHalfTurn);
            foreach (var effect in periodicEffects)
            {
                if (effect.tickValue > 0)
                    character.Heal(effect.tickValue);
                else
                    character.TakeDamage(-effect.tickValue);
            }

            var expired = character.activeEffects.FindAll(e => currentHalfTurn >= e.expiresAtHalfTurn);
            foreach (var effect in expired)
            {
                Debug.Log($"{character.data.characterName} の {effect.effectName} が期限切れ(currentHalfTurn={currentHalfTurn}, expiresAt={effect.expiresAtHalfTurn})");
                StatusEffectBehaviors.InvokeOnExpire(effect, character);
                character.activeEffects.Remove(effect);
            }
        }
    }

    private void ExecuteAction(QueuedAction action)
    {
        // 命中率低下(エインのトゥ・ザ・ミスト): 通常攻撃/スキル/ウルトが25%の確率で不発になる
        bool isMissable = action.actionType == ActionType.Attack || action.actionType == ActionType.Skill || action.actionType == ActionType.Ultimate;
        if (isMissable && action.actor.IsAccuracyDown(currentHalfTurn) && UnityEngine.Random.value < 0.25f)
        {
            Debug.Log($"MISS: {action.actor.data.characterName}の行動が命中率低下により不発。");
            return;
        }

        switch (action.actionType)
        {
            case ActionType.Attack:
                if (action.target.protectedBy != null && !action.target.protectedBy.IsDefeated)
                {
                    action.target.protectedBy.TakeInterceptedDamage(action.actor.currentAttack, action.actor); // 肩代わり専用メソッド
                }
                else if (action.target.twinBurdenPartner != null && !action.target.twinBurdenPartner.IsDefeated)
                {
                    // ツインバーデン: 奇数なら大きい方をパートナー(イージス)側へ、1回限りで消費
                    int total = action.actor.currentAttack;
                    int partnerShare = Mathf.CeilToInt(total / 2f);
                    int selfShare = total - partnerShare;

                    var partner = action.target.twinBurdenPartner;
                    action.target.twinBurdenPartner = null;
                    partner.twinBurdenPartner = null;

                    action.target.TakeDamage(selfShare, action.actor);
                    partner.TakeDamage(partnerShare, action.actor);
                }
                else
                {
                    action.target.TakeDamage(action.actor.currentAttack, action.actor);
                }
                action.actor.AddUltGauge(2); ;

                Debug.Log($"Attack: {action.actor.data.characterName} attacks {action.target.data.characterName} for {action.actor.currentAttack} damage. Target HP: {action.target.currentHP}/{action.target.currentMaxHP}");

                var emptyFill = action.actor.activeEffects.Find(e => e.effectName == "エンプティフィル");
                if (emptyFill != null)
                {
                    action.actor.currentAttack -= emptyFill.value; // バフを戻す
                    action.actor.activeEffects.Remove(emptyFill);   // 消費して削除
                    action.actor.Heal(8);
                }

                // 狂暴: 持続中の通常攻撃のたびに、味方1体へ反動ダメージ
                var kyobou = action.actor.activeEffects.Find(e => e.effectName == "狂暴");
                if (kyobou != null)
                {
                    var actorTeamForKyobou = GetTeamOf(action.actor);
                    var kyobouAllyCandidates = actorTeamForKyobou.AllCharacters()
                        .Where(c => c != action.actor && !c.IsDefeated).ToList();
                    if (kyobouAllyCandidates.Count > 0)
                    {
                        var recoilTarget = kyobouAllyCandidates[UnityEngine.Random.Range(0, kyobouAllyCandidates.Count)];
                        recoilTarget.TakeDamage(7);
                    }
                }
                break;
            case ActionType.Skill:
                SkillRegistry.ExecuteSkill(action.actor, action.target);
                break;
            case ActionType.Ultimate:
                SkillRegistry.ExecuteUltimate(action.actor, action.target);
                action.actor.ResetUltGauge();
                break;
            case ActionType.Defense:
                action.actor.ActivateDefense(currentHalfTurn + 1);
                break;
            case ActionType.Swap:
                ExecuteSwap(action.actor, action.target);
                break;
        }
    }

    public void ExecuteSwap(CharacterState outgoing, CharacterState incoming)
    {
        SeManager.PlaySwap(); // 通常の交代アクションだけでなく、グレイス/ストームのスキルなど行動キュー外から直接呼ばれる交代もここで一括して鳴る

        var team = GetTeamOf(outgoing);

        int fwSlot = team.forwards.IndexOf(outgoing);
        int backupIndex = team.backups.IndexOf(incoming);

        team.forwards[fwSlot] = incoming;
        team.backups.RemoveAt(backupIndex);
        team.backups.Add(outgoing);
        outgoing.RevertTransformIfNeeded(); // Doubleのトランスフォームは交代で元に戻る
        outgoing.ClearSwapResetEffects(); // 不屈・遺志は発動前に交代すると消える
        outgoing.ResolveSuspendedSwapOut(); // モルフェ: 仮死中に交代すると、そのまま戦闘不能扱いになる

        incoming.hasActedThisTurn = true;   // 交代してきた側はこのターンもう行動不可
        outgoing.hasActedThisTurn = false;  // 下がった側は次にFWに戻った時のためリセット

        GetFwSlots(team)[fwSlot].Bind(incoming);
        RefreshBackupSlots(team);
    }

    private void StartNewTurn()
    {
        BattleManager.Instance.PlayerTeam.ResetAllTurnStates();
        BattleManager.Instance.EnemyTeam.ResetAllTurnStates();
    }

    public Team GetTeamOf(CharacterState character)
    {
        if (BattleManager.Instance.PlayerTeam.forwards.Contains(character))
            return BattleManager.Instance.PlayerTeam;
        return BattleManager.Instance.EnemyTeam;
    }

    private void RandomSwap(Team team, CharacterState deadCharacter)
    {
        if (team.backups.Count == 0) return;

        // 生きてるBKだけを対象にする
        var aliveBackups = team.backups.FindAll(c => !c.IsDefeated);
        if (aliveBackups.Count == 0) return; // 生きてるBKが誰もいなければ繰り上がれない

        int fwSlot = team.forwards.IndexOf(deadCharacter);
        var incoming = aliveBackups[UnityEngine.Random.Range(0, aliveBackups.Count)];

        team.forwards[fwSlot] = incoming;
        team.backups.Remove(incoming); // IndexではなくRemove(要素)で消す(元のindexが変わるため)
        team.backups.Add(deadCharacter);
        deadCharacter.RevertTransformIfNeeded(); // Doubleのトランスフォームは交代で元に戻る
        deadCharacter.ClearSwapResetEffects(); // 不屈・遺志は発動前に交代すると消える

        incoming.hasActedThisTurn = false;

        GetFwSlots(team)[fwSlot].Bind(incoming);
        RefreshBackupSlots(team);
    }
    public void ForceSwap(CharacterState outgoing, CharacterState incoming)
    {
        var team = GetTeamOf(outgoing);
        int fwSlot = team.forwards.IndexOf(outgoing);

        team.forwards[fwSlot] = incoming;
        team.backups.Remove(incoming);
        team.backups.Add(outgoing);
        outgoing.RevertTransformIfNeeded(); // Doubleのトランスフォームは交代で元に戻る
        outgoing.ClearSwapResetEffects(); // 不屈・遺志は発動前に交代すると消える
        outgoing.ResolveSuspendedSwapOut(); // モルフェ: 仮死中に交代すると、そのまま戦闘不能扱いになる

        incoming.hasActedThisTurn = false; // 強制交代なので行動可能
        outgoing.hasActedThisTurn = false;

        GetFwSlots(team)[fwSlot].Bind(incoming);
        RefreshBackupSlots(team);
    }
    private CharacterSlotUI[] GetFwSlots(Team team)
    {
        return team == BattleManager.Instance.PlayerTeam ? playerFwSlots : enemyFwSlots;
    }

    /// <summary>
    /// 通常の攻撃行動のキューを経由しない攻撃(例: グレイスのアンコールで交代して出てきたキャラの攻撃)向けに、
    /// 通常攻撃と同じ演出だけを再生する。ダメージ計算は呼び出し側(SkillRegistryなど)で行う。
    /// </summary>
    public void PlayAttackAnimationOnly(CharacterState attacker, CharacterState target, Action onComplete = null)
    {
        var targetTeam = GetTeamOf(target);
        int targetSlot = targetTeam.forwards.IndexOf(target);
        if (targetSlot == -1)
        {
            onComplete?.Invoke();
            return;
        }

        var targetPanel = GetFwSlots(targetTeam)[targetSlot].panel;
        var animController = attacker.data.attackAnimatorController;
        var clip = attacker.data.attackAnimationClip;

        SeManager.Play(attacker.data.attackSe);

        if (clip == null)
        {
            onComplete?.Invoke();
            return;
        }

        targetPanel.PlayAttackEffect(animController, clip.length, onComplete);
    }

    /// <summary>
    /// カタリスト使用時、使用者のパネル上でそのカタリストのアイコンをフェード表示する(通常の行動キューは経由しない)。
    /// </summary>
    public void PlayCatalystFadeEffect(CharacterState actor, Sprite icon, Action onComplete = null)
    {
        var actorTeam = GetTeamOf(actor);
        int actorSlot = actorTeam.forwards.IndexOf(actor);
        if (actorSlot == -1)
        {
            onComplete?.Invoke();
            return;
        }

        SeManager.PlayCatalystUse();
        GetFwSlots(actorTeam)[actorSlot].panel.PlayFadeEffect(icon, onComplete);
    }

    private CharacterSlotUI[] GetBkSlots(Team team)
    {
        return team == BattleManager.Instance.PlayerTeam ? playerBkSlots : enemyBkSlots;
    }
    private void RefreshBackupSlots(Team team)
    {
        var slots = GetBkSlots(team);
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < team.backups.Count)
            {
                slots[i].Bind(team.backups[i]); // 今その順番にいるキャラをBind
            }
            // elseの場合は何もしない → 最後にそのスロットにいたキャラの表示のまま残る
        }
    }
    private CharacterState GetMirroredTarget(CharacterState actor, CharacterState originalTarget)
    {
        var originalTeam = GetTeamOf(originalTarget);

        var mirroredTeam = (originalTeam == BattleManager.Instance.PlayerTeam)
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        int slot = originalTeam.forwards.IndexOf(originalTarget);
        if (slot == -1 || slot >= mirroredTeam.forwards.Count) return originalTarget;

        return mirroredTeam.forwards[slot];
    }

    // ターゲットランダム化用: 生存中のFW(両陣営とも)からランダムに1体選ぶ
    private CharacterState GetRandomAliveForward()
    {
        var candidates = BattleManager.Instance.PlayerTeam.forwards
            .Concat(BattleManager.Instance.EnemyTeam.forwards)
            .Where(c => !c.IsDefeated)
            .ToList();
        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>オンライン対戦開始時、自分が先攻かどうかを設定する</summary>
    public void ConfigureFirstMover(bool amIFirst)
    {
        isPlayerFirst = amIFirst;
        isPlayerTurnNow = amIFirst;
    }

    /// <summary>相手側の死亡繰り上げが適用された後、処理を再開する(OnlineBattleSyncから呼ぶ)</summary>
    public void ResumeAfterEnemyDeathSwap()
    {
        PendingEnemyDeathSwapFwSlot = -1;

        if (pendingEnemyDeathSwapFromTick)
        {
            pendingEnemyDeathSwapFromTick = false;
            ResolveDeathsAfterTick();
        }
        else
        {
            currentActionIndex++;
            ProcessNextAction();
        }
    }

    /// <summary>相手側からの死亡繰り上げ通知を受けてEnemyTeamの表示を更新する(OnlineBattleSyncから呼ぶ)</summary>
    public void RebindAfterRemoteDeathSwap(Team team, int fwSlot)
    {
        GetFwSlots(team)[fwSlot].Bind(team.forwards[fwSlot]);
        RefreshBackupSlots(team);
    }

    private VisualTargetType GetVisualTargetType(CharacterState actor, ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Ultimate:
                return SkillRegistry.GetUltimateVisualTarget(actor.data.id);
            case ActionType.Skill:
                return SkillRegistry.GetSkillVisualTarget(actor.data.id);
            default: // Attack
                return VisualTargetType.Target;
        }
    }
    private List<CharacterPanelUI> GetVisualTargetPanels(QueuedAction action, CharacterPanelUI actorPanel, CharacterPanelUI targetPanel)
    {
        var visualType = GetVisualTargetType(action.actor, action.actionType);
        var actorTeam = GetTeamOf(action.actor);

        switch (visualType)
        {
            case VisualTargetType.Actor:
                return new List<CharacterPanelUI> { actorPanel };

            case VisualTargetType.OtherAllyForward:
                {
                    var otherAlly = actorTeam.forwards.Find(c => c != action.actor);
                    if (otherAlly == null) return new List<CharacterPanelUI>();
                    int slot = actorTeam.forwards.IndexOf(otherAlly);
                    return new List<CharacterPanelUI> { GetFwSlots(actorTeam)[slot].panel };
                }

            case VisualTargetType.AllAllyForward:
                {
                    var panels = new List<CharacterPanelUI>();
                    foreach (var fw in actorTeam.forwards)
                    {
                        int slot = actorTeam.forwards.IndexOf(fw);
                        panels.Add(GetFwSlots(actorTeam)[slot].panel);
                    }
                    return panels;
                }

            case VisualTargetType.AllEnemyForward:
                {
                    var enemyTeam = actorTeam == BattleManager.Instance.PlayerTeam
                        ? BattleManager.Instance.EnemyTeam
                        : BattleManager.Instance.PlayerTeam;
                    var panels = new List<CharacterPanelUI>();
                    foreach (var fw in enemyTeam.forwards)
                    {
                        int slot = enemyTeam.forwards.IndexOf(fw);
                        panels.Add(GetFwSlots(enemyTeam)[slot].panel);
                    }
                    return panels;
                }

            default: // Target
                return new List<CharacterPanelUI> { targetPanel };
        }
    }

    private void PlayActionEffect(QueuedAction action, List<CharacterPanelUI> panels, Action onComplete)
    {
        if (panels.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        switch (action.actionType)
        {
            case ActionType.Ultimate:
                {
                    SeManager.Play(action.actor.data.ultSe);

                    // モルフェのウルトは演出なし。ExecuteAction側でスプライトが切り替わるだけにする
                    if (action.actor.data.id == CharacterId.Morphe)
                    {
                        onComplete?.Invoke();
                        break;
                    }

                    Sprite characterSprite = action.actor.GetDisplaySprite();
                    Sprite ultFadeSprite = action.actor.data.ultSprite;

                    var actorTeam = GetTeamOf(action.actor);
                    var actorPanel = GetFwSlots(actorTeam)[actorTeam.forwards.IndexOf(action.actor)].panel;

                    actorPanel.PlayUltEffect(characterSprite, ultFadeSprite, panels, onComplete);
                    break;
                }
            case ActionType.Skill:
                {
                    SeManager.Play(action.actor.data.skillSe);

                    int remaining = panels.Count;
                    void OnOnePanelDone() { remaining--; if (remaining <= 0) onComplete?.Invoke(); }

                    var animController = action.actor.data.skillAnimatorController;
                    var clip = action.actor.data.skillAnimationClip;

                    if (animController != null && clip != null)
                    {
                        foreach (var panel in panels)
                            panel.PlayAttackEffect(animController, clip.length, OnOnePanelDone);
                    }
                    else
                    {
                        Sprite fadeSprite = action.actor.data.skillSprite;
                        foreach (var panel in panels)
                            panel.PlayFadeEffect(fadeSprite, OnOnePanelDone);
                    }
                    break;
                }
            default: // Attack
                {
                    SeManager.Play(action.actor.data.attackSe);

                    int remaining = panels.Count;
                    void OnOnePanelDone() { remaining--; if (remaining <= 0) onComplete?.Invoke(); }

                    var animController = action.actor.data.attackAnimatorController;
                    var clip = action.actor.data.attackAnimationClip;
                    foreach (var panel in panels)
                        panel.PlayAttackEffect(animController, clip.length, OnOnePanelDone);
                    break;
                }
        }
    }
}