using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Xml.Serialization;

public class ActionUI : MonoBehaviour
{

    [SerializeField] private  List<GameObject> actionTargetButtons = new List<GameObject>();
    [SerializeField] private List<TextMeshProUGUI> actionTargetText = new List<TextMeshProUGUI>();
    [SerializeField] private Transform RightCharacterButton;
    [SerializeField] private Transform LeftCharacterButton;


    [Header("UIs")]
    //0:Normal 1:Gray 2:None
    [SerializeField] private Sprite[] right;
    [SerializeField] private Sprite[] left;
    [SerializeField] private Image rightImage;
    [SerializeField] private Image leftImage;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button leftButton;
    [SerializeField] private GameObject rightObj;
    [SerializeField] private GameObject leftObj;
    [SerializeField] private TextMeshProUGUI rightText;
    [SerializeField] private TextMeshProUGUI leftText;

    [SerializeField] private Sprite[] UI_BG;
    [SerializeField] private Sprite[] UI_Action_BG;
    [SerializeField] private Sprite[] UI_Catalyst_BG;
    [SerializeField] private Image UI_BG_Image;
    [SerializeField] private Image[] UI_Action_BG_Image;
    [SerializeField] private Image UI_Catalyst_BG_Image;
    [SerializeField] private GameObject UI_BG_Obj;
    [SerializeField] private GameObject[] UI_Action_BG_Obj;
    [SerializeField] private GameObject UI_Catalyst_BG_Obj;

    [SerializeField] private GameObject catalystButtonPrefab; // Button + TextMeshProUGUIをアタッチしたプレハブ
    [SerializeField] private Transform catalystButtonParent;  // 生成先の親(縦/横に並ぶLayout Group推奨)

    [Header("対象選択ラベル")]
    [SerializeField] private TextMeshProUGUI targetScopeLabelText; // 対象選択中に「味方」「敵」「味方カタリスト」等を表示(未設定なら表示なし)

    [Header("降参")]
    [SerializeField] private GameObject surrenderConfirmPanel; // 「本当に降参しますか？」の確認パネル(はい/いいえボタンをEditor側でOnConfirmed/OnCancelledに接続)

    [Header("行動制限時間")]
    [SerializeField] private float turnTimeLimitSeconds = 30f; // 0以下にすると無効(タイムアウトなし)
    [SerializeField] private TextMeshProUGUI timeLimitText;    // HUDに表示する残り秒数(未設定なら表示なし)
    [SerializeField] private float timeLimitWarningThreshold = 10f; // これ以下になったら警告色にする
    [SerializeField] private Color timeLimitNormalColor = Color.white;
    [SerializeField] private Color timeLimitWarningColor = Color.red;
    private Coroutine turnTimeoutRoutine;

    [Header("合計経過時間")]
    [SerializeField] private TextMeshProUGUI elapsedTimeText; // HUDに表示する対戦開始からの合計経過時間(未設定なら表示なし)
    private const string KeyBattleStartedAt = "duelv_battle_started_at"; // "{部屋コード}:{対戦開始のUTC unix秒}"(オンライン対戦のみ、リロード対策)
    private long battleStartUnix = -1;
    private int lastDisplayedElapsedSeconds = -1;

    private int nowCharacterIndex = 0;
    private bool isFirstTurnStart = true; // Turn1の最初のTurnStartだけは必ず0番目から

    private enum SelectionMode { None, Attack, Skill, Ultimate, Swap, DeathSwap, Catalyst, CatalystTarget, SkillStep2 }
    private SelectionMode currentSelectionMode = SelectionMode.None;

    private bool[] isTargetAttackable = new bool[] { true, true, false, false, false };
    private List<int> buttonToActualIndex = new List<int>();
    private ActionType pendingActionType;

    // Grace/Lapseの2段階選択(ステップ1で選んだ内容をステップ2まで保持する)
    private CharacterState pendingLapseTarget;
    private List<CatalystInstance> pendingHandpickOptions;

    private List<CatalystInstance> pendingCatalystOptions;
    private List<GameObject> spawnedCatalystButtons = new List<GameObject>();
    private CatalystInstance pendingCatalystInstance;
    



    public enum TargetScope
    {
        EnemyForward,   // 相手FW
        EnemyAll,       // 相手全体
        AllyForward,    // 味方FW
        Self,            // 自分自身
        AllyAny        // 味方全体から1体
    }

    public void UpdateAttackableTargets(TargetScope scope)
    {
        List<CharacterState> candidates = scope switch
        {
            TargetScope.EnemyForward => BattleManager.Instance.EnemyTeam.forwards,
            TargetScope.EnemyAll => new List<CharacterState>(BattleManager.Instance.EnemyTeam.AllCharacters()),
            TargetScope.AllyForward => BattleManager.Instance.PlayerTeam.forwards,
            TargetScope.AllyAny => new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()),
            _ => new List<CharacterState>()
        };

        isTargetAttackable = new bool[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            isTargetAttackable[i] = !candidates[i].IsDefeated;

            // 「獲物」「拘束」など、特定の相手としか戦えない効果があればここでさらに絞る
            // if (attacker has "獲物" effect) isTargetAttackable[i] = candidates[i] == boundTarget;
        }
    }

    // ---------------- 対象選択ラベル(「味方」「敵」「味方/敵カタリスト」) ----------------

    private void SetTargetLabel(bool isAlly)
    {
        if (targetScopeLabelText != null) targetScopeLabelText.text = isAlly ? "味方" : "敵";
    }

    private void SetCatalystTargetLabel(bool isAlly)
    {
        if (targetScopeLabelText != null) targetScopeLabelText.text = isAlly ? "味方カタリスト" : "敵カタリスト";
    }

    private void ClearTargetLabel()
    {
        if (targetScopeLabelText != null) targetScopeLabelText.text = "";
    }

    private void SetTargetLabelFromScope(TargetScope scope)
    {
        switch (scope)
        {
            case TargetScope.EnemyForward:
            case TargetScope.EnemyAll:
                SetTargetLabel(false);
                break;
            case TargetScope.AllyForward:
            case TargetScope.AllyAny:
                SetTargetLabel(true);
                break;
            default:
                ClearTargetLabel(); // Self等、対象選択ボタン自体を出さないスコープ
                break;
        }
    }

    private void ShowTargetButtons(List<CharacterState> candidates, System.Func<CharacterState, bool> isSelectable)
    {
        buttonToActualIndex.Clear();
        int buttonIndex = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (isSelectable(candidates[i]))
            {
                actionTargetButtons[buttonIndex].SetActive(true);
                actionTargetText[buttonIndex].text = candidates[i].data.characterName;
                buttonToActualIndex.Add(i);
                buttonIndex++;
            }
        }
        for (int j = buttonIndex; j < actionTargetButtons.Count; j++)
        {
            actionTargetButtons[j].SetActive(false);
        }
    }

    // Lapseのハンドピーク用: 同じボタン群をキャラではなくカタリストの選択肢として表示する
    private void ShowCatalystTargetButtons(List<CatalystInstance> candidates, bool isAlly)
    {
        SetCatalystTargetLabel(isAlly);

        buttonToActualIndex.Clear();
        int buttonIndex = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            actionTargetButtons[buttonIndex].SetActive(true);
            actionTargetText[buttonIndex].text = candidates[i].data.catalystName;
            buttonToActualIndex.Add(i);
            buttonIndex++;
        }
        for (int j = buttonIndex; j < actionTargetButtons.Count; j++)
        {
            actionTargetButtons[j].SetActive(false);
        }
    }

    private void CloseAllTargetSelection()
    {
        foreach (var obj in actionTargetButtons) obj.SetActive(false);
        currentSelectionMode = SelectionMode.None;
        ClearCatalystOptions();
        pendingCatalystInstance = null;
        ClearTargetLabel();
    }

    private void ShowScopedTargets(TargetScope scope, CharacterState actor)
    {
        SetTargetLabelFromScope(scope);

        List<CharacterState> candidates = scope switch
        {
            TargetScope.EnemyForward => BattleManager.Instance.EnemyTeam.forwards,
            TargetScope.EnemyAll => new List<CharacterState>(BattleManager.Instance.EnemyTeam.AllCharacters()),
            TargetScope.AllyForward => BattleManager.Instance.PlayerTeam.forwards,
            TargetScope.AllyAny => new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()),
            _ => new List<CharacterState>()
        };
        // 他人が拘束している「獲物」(ハンティンググラウンド)は対象にできない
        ShowTargetButtons(candidates, c => !c.IsDefeated && c.IsTargetableBy(actor));
    }

    void Start()
    {
        // isTargetAttackable.Length(初期値5固定)ではなく、実際のボタン総数でループする。
        // ボタン数を増やしても起動直後に余ったボタンが非表示にならない、という取りこぼしを防ぐため
        for (int i = 0; i < actionTargetButtons.Count; i++)
        {
            actionTargetButtons[i].SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateElapsedTimeDisplay();
    }

    public void OnAttackClick()
    {
        if (currentSelectionMode == SelectionMode.Attack)
        {
            CloseAllTargetSelection();
            return;
        }

        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];
        if (actor.IsDefenseOnly(TurnManager.Instance.currentHalfTurn)) return; // 献身中は防御しか選べない
        if (actor.isSuspendedAnimation) return; // 仮死中はウルトか交代しか選べない

        var enemyForwards = BattleManager.Instance.EnemyTeam.forwards;
        SetTargetLabel(false);

        // クロニクルのハンティンググラウンド中は、互いにしか通常攻撃できない
        // (indexの対応を保つため、リストは絞らずisSelectable側で絞り込む)
        if (actor.huntBoundTo != null && !actor.huntBoundTo.IsDefeated)
        {
            ShowTargetButtons(enemyForwards, c => !c.IsDefeated && c == actor.huntBoundTo);
        }
        else
        {
            // 他人が拘束している「獲物」は対象にできない
            ShowTargetButtons(enemyForwards, c => !c.IsDefeated && c.IsTargetableBy(actor));
        }
        currentSelectionMode = SelectionMode.Attack;
    }

    public void OnDefenseClick()
    {
        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];
        if (actor.huntBoundTo != null && !actor.huntBoundTo.IsDefeated) return; // ハンティンググラウンド中は防御行動を選べない
        if (actor.isSuspendedAnimation) return; // 仮死中はウルトか交代しか選べない

        TurnManager.Instance.QueueAction(actor, ActionType.Defense, null);
        actor.MarkAsActed();
        AdvanceToNextCharacterOrEndTurn();
    }

    public void OnSwapClick()
    {
        if (currentSelectionMode == SelectionMode.Swap)
        {
            CloseAllTargetSelection();
            return;
        }

        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];
        if (actor.IsSwapLocked(TurnManager.Instance.currentHalfTurn)) return;
        if (actor.IsDefenseOnly(TurnManager.Instance.currentHalfTurn)) return; // 献身中は防御しか選べない

        var playerBackups = BattleManager.Instance.PlayerTeam.backups;
        SetTargetLabel(true);
        ShowTargetButtons(playerBackups, c => !c.IsDefeated);
        currentSelectionMode = SelectionMode.Swap;
    }

    public void OnSkillClick()
    {
        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];
        if (actor.IsSilenced(TurnManager.Instance.currentHalfTurn))
        {
            // サイレンス中はスキルを使えない
            return;
        }
        if (actor.IsDefenseOnly(TurnManager.Instance.currentHalfTurn)) return; // 献身中は防御しか選べない
        if (actor.isSuspendedAnimation) return; // 仮死中はウルトか交代しか選べない

        if (currentSelectionMode == SelectionMode.SkillStep2) return; // 2段階目に入ったらキャンセル不可

        if (currentSelectionMode == SelectionMode.Skill)
        {
            CloseAllTargetSelection();
            return;
        }

        // グレイスのアンコール: 通常のスコープではなく、まず交代先のBKを選ばせる(2段階選択の1段階目)
        if (actor.data.id == CharacterId.Grace)
        {
            var aliveBackups = BattleManager.Instance.PlayerTeam.backups.FindAll(c => !c.IsDefeated);
            if (aliveBackups.Count == 0) return;

            pendingActionType = ActionType.Skill;
            SetTargetLabel(true);
            ShowTargetButtons(BattleManager.Instance.PlayerTeam.backups, c => !c.IsDefeated);
            currentSelectionMode = SelectionMode.Skill;
            return;
        }

        TargetScope scope = SkillRegistry.GetSkillTargetScope(actor.data.id);

        if (scope == TargetScope.Self)
        {
            TurnManager.Instance.QueueAction(actor, ActionType.Skill, actor);
            actor.MarkAsActed();
            AdvanceToNextCharacterOrEndTurn();
            return;
        }

        pendingActionType = ActionType.Skill;
        ShowScopedTargets(scope, actor);
        currentSelectionMode = SelectionMode.Skill;
    }

    public void OnUltimateClick()
    {
        if (currentSelectionMode == SelectionMode.Ultimate)
        {
            CloseAllTargetSelection();
            return;
        }

        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];

        if (!actor.CanUseUltimate(TurnManager.Instance.currentHalfTurn))
        {
            // ゲージ不足/ULT封じ中/(モルフェの場合)生きている間は発動できない
            return;
        }

        if (actor.IsDefenseOnly(TurnManager.Instance.currentHalfTurn)) return; // 献身中は防御しか選べない

        // グレイスのワンモアタイム: 戦闘不能の味方から復活対象を選ばせる
        if (actor.data.id == CharacterId.Grace)
        {
            var defeatedAllies = new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()).FindAll(c => c.IsDefeated);
            if (defeatedAllies.Count == 0) return; // 戦闘不能の味方がいなければ発動しない

            pendingActionType = ActionType.Ultimate;
            SetTargetLabel(true);
            ShowTargetButtons(defeatedAllies, c => true);
            currentSelectionMode = SelectionMode.Ultimate;
            return;
        }

        TargetScope scope = SkillRegistry.GetUltimateTargetScope(actor.data.id);

        if (scope == TargetScope.Self)
        {
            TurnManager.Instance.QueueAction(actor, ActionType.Ultimate, actor);
            actor.MarkAsActed();
            AdvanceToNextCharacterOrEndTurn();
            return;
        }

        pendingActionType = ActionType.Ultimate;
        ShowScopedTargets(scope, actor);
        currentSelectionMode = SelectionMode.Ultimate;
    }

    public void OnTargetSelected(int targetIndex)
    {
        int actualIndex = buttonToActualIndex[targetIndex];
        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];

        switch (currentSelectionMode)
        {
            case SelectionMode.DeathSwap:
                CloseAllTargetSelection();
                TurnManager.Instance.OnDeathSwapTargetSelected(actualIndex);
                return;

            case SelectionMode.Swap:
                {
                    var target = BattleManager.Instance.PlayerTeam.backups[actualIndex];
                    CloseAllTargetSelection();
                    TurnManager.Instance.QueueAction(actor, ActionType.Swap, target);
                    actor.MarkAsActed();
                    AdvanceToNextCharacterOrEndTurn();
                    return;
                }

            case SelectionMode.Attack:
                {
                    var target = BattleManager.Instance.EnemyTeam.forwards[actualIndex];
                    CloseAllTargetSelection();
                    TurnManager.Instance.QueueAction(actor, ActionType.Attack, target);
                    actor.MarkAsActed();
                    AdvanceToNextCharacterOrEndTurn();
                    return;
                }

            case SelectionMode.Skill:
            case SelectionMode.Ultimate:
                {
                    // グレイスのワンモアタイム: 選んだ戦闘不能の味方を復活対象にする(1段階のみ)
                    if (pendingActionType == ActionType.Ultimate && actor.data.id == CharacterId.Grace)
                    {
                        var defeatedAllies = new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()).FindAll(c => c.IsDefeated);
                        var revivalTarget = defeatedAllies[actualIndex];
                        CloseAllTargetSelection();
                        TurnManager.Instance.QueueAction(actor, ActionType.Ultimate, revivalTarget);
                        actor.MarkAsActed();
                        AdvanceToNextCharacterOrEndTurn();
                        return;
                    }

                    // グレイスのアンコール: 1段階目(交代先BK)を選んだので、続けて2段階目(攻撃対象の敵FW)を出す
                    if (pendingActionType == ActionType.Skill && actor.data.id == CharacterId.Grace)
                    {
                        var incoming = BattleManager.Instance.PlayerTeam.backups[actualIndex];
                        actor.pendingEncoreIncoming = incoming;

                        var enemyForwardsForGrace = BattleManager.Instance.EnemyTeam.forwards;
                        var aliveEnemies = enemyForwardsForGrace.FindAll(c => !c.IsDefeated);
                        if (aliveEnemies.Count == 0)
                        {
                            // 攻撃対象がいなければ交代だけ実行して終える
                            CloseAllTargetSelection();
                            TurnManager.Instance.QueueAction(actor, ActionType.Skill, actor);
                            actor.MarkAsActed();
                            AdvanceToNextCharacterOrEndTurn();
                            return;
                        }

                        SetTargetLabel(false);
                        ShowTargetButtons(enemyForwardsForGrace, c => !c.IsDefeated);
                        currentSelectionMode = SelectionMode.SkillStep2;
                        return;
                    }

                    // ラプスのハンドピーク: 1段階目(相手FW)を選んだので、続けて2段階目(そのFWのカタリスト)を出す
                    if (pendingActionType == ActionType.Skill && actor.data.id == CharacterId.Lapse)
                    {
                        var chosenEnemy = BattleManager.Instance.EnemyTeam.forwards[actualIndex];
                        var availableCatalysts = chosenEnemy.catalysts.FindAll(c => !c.isUsed);
                        if (availableCatalysts.Count == 0)
                        {
                            // 封じられるカタリストが無ければそのまま(空振りの)スキルとして終える
                            CloseAllTargetSelection();
                            TurnManager.Instance.QueueAction(actor, ActionType.Skill, chosenEnemy);
                            actor.MarkAsActed();
                            AdvanceToNextCharacterOrEndTurn();
                            return;
                        }

                        pendingLapseTarget = chosenEnemy;
                        pendingHandpickOptions = availableCatalysts;
                        ShowCatalystTargetButtons(availableCatalysts, isAlly: false);
                        currentSelectionMode = SelectionMode.SkillStep2;
                        return;
                    }

                    var scope = pendingActionType == ActionType.Skill
                        ? SkillRegistry.GetSkillTargetScope(actor.data.id)
                        : SkillRegistry.GetUltimateTargetScope(actor.data.id);

                    var targetList = scope == TargetScope.AllyForward
                        ? BattleManager.Instance.PlayerTeam.forwards
                        : BattleManager.Instance.EnemyTeam.forwards;

                    var target = targetList[actualIndex];
                    var actionType = pendingActionType;
                    CloseAllTargetSelection();
                    TurnManager.Instance.QueueAction(actor, actionType, target);
                    actor.MarkAsActed();
                    AdvanceToNextCharacterOrEndTurn();
                    return;
                }

            case SelectionMode.SkillStep2:
                {
                    // グレイスのアンコール2段階目: 選んだ敵FWを攻撃対象にして実行
                    if (actor.data.id == CharacterId.Grace)
                    {
                        var attackTarget = BattleManager.Instance.EnemyTeam.forwards[actualIndex];
                        CloseAllTargetSelection();
                        TurnManager.Instance.QueueAction(actor, ActionType.Skill, attackTarget);
                        actor.MarkAsActed();
                        AdvanceToNextCharacterOrEndTurn();
                        return;
                    }

                    // ラプスのハンドピーク2段階目: 選んだカタリストを覚えておいて実行
                    if (actor.data.id == CharacterId.Lapse)
                    {
                        var chosenCatalyst = pendingHandpickOptions[actualIndex];
                        var chosenEnemy = pendingLapseTarget;
                        pendingHandpickOptions = null;
                        pendingLapseTarget = null;

                        actor.pendingHandpickChoice = chosenCatalyst;
                        CloseAllTargetSelection();
                        TurnManager.Instance.QueueAction(actor, ActionType.Skill, chosenEnemy);
                        actor.MarkAsActed();
                        AdvanceToNextCharacterOrEndTurn();
                        return;
                    }
                    return;
                }
            case SelectionMode.CatalystTarget:
                {
                    // 転身: 自分のBK一覧から選んだ交代先
                    if (pendingCatalystInstance.Id == CatalystId.Tenshin)
                    {
                        var incoming = BattleManager.Instance.PlayerTeam.backups[actualIndex];
                        var tenshinInstance = pendingCatalystInstance;
                        CloseAllTargetSelection();
                        CatalystRegistry.ExecuteCatalyst(actor, incoming, tenshinInstance);
                        UpdateVisual();
                        return;
                    }

                    // 奇襲: 選んだ相手BKを自分自身が攻撃して実行(1段階のみ)
                    if (pendingCatalystInstance.Id == CatalystId.Kishuu)
                    {
                        var kishuuTarget = BattleManager.Instance.EnemyTeam.backups[actualIndex];
                        var kishuuInstance = pendingCatalystInstance;
                        CloseAllTargetSelection();
                        CatalystRegistry.ExecuteCatalyst(actor, kishuuTarget, kishuuInstance);
                        UpdateVisual();
                        return;
                    }

                    // 模倣: コピー元カタリストのスコープ(Mohou自身のスコープではない)で対象を解決する
                    if (pendingCatalystInstance.Id == CatalystId.Mohou)
                    {
                        var mimicId = CatalystRegistry.GetMohouMimicCandidate(actor);
                        var mimicScope = mimicId != null
                            ? CatalystRegistry.GetCatalystTargetScope(mimicId.Value)
                            : TargetScope.EnemyForward;

                        var mohouTargetList = mimicScope switch
                        {
                            TargetScope.AllyForward => BattleManager.Instance.PlayerTeam.forwards,
                            TargetScope.AllyAny => new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()),
                            TargetScope.EnemyAll => new List<CharacterState>(BattleManager.Instance.EnemyTeam.AllCharacters()),
                            _ => BattleManager.Instance.EnemyTeam.forwards
                        };
                        var mohouTarget = mohouTargetList[actualIndex];
                        var mohouInstance = pendingCatalystInstance;
                        CloseAllTargetSelection();
                        CatalystRegistry.ExecuteCatalyst(actor, mohouTarget, mohouInstance);
                        UpdateVisual();
                        return;
                    }

                    var scope = CatalystRegistry.GetCatalystTargetScope(pendingCatalystInstance.Id);
                    var targetList = scope switch
                    {
                        TargetScope.AllyForward => BattleManager.Instance.PlayerTeam.forwards,
                        TargetScope.AllyAny => new List<CharacterState>(BattleManager.Instance.PlayerTeam.AllCharacters()),
                        TargetScope.EnemyAll => new List<CharacterState>(BattleManager.Instance.EnemyTeam.AllCharacters()),
                        _ => BattleManager.Instance.EnemyTeam.forwards
                    };
                    var target = targetList[actualIndex];
                    var instance = pendingCatalystInstance;
                    CloseAllTargetSelection();
                    CatalystRegistry.ExecuteCatalyst(actor, target, instance);
                    UpdateVisual();
                    return;
                }
        }
    }
    public void OnCatalystClick()
    {
        if (currentSelectionMode == SelectionMode.Catalyst)
        {
            CloseAllTargetSelection();
            return;
        }

        // 先行は最初の1ターンサブカード使用不可
        if (TurnManager.Instance.isPlayerFirst && TurnManager.Instance.currentHalfTurn == 0)
        {
            return;
        }

        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];

        if (actor.hasUsedCatalystThisHalfTurn) return;
        if (actor.isSuspendedAnimation) return; // 仮死中はウルトか交代しか選べない

        int currentHalfTurn = TurnManager.Instance.currentHalfTurn;
        var available = actor.catalysts.FindAll(c => !c.isUsed && !c.IsDisabled(currentHalfTurn));
        if (available.Count == 0) return; // 持ってる未使用カタリストが無ければ何もしない

        ShowCatalystOptions(available);
        currentSelectionMode = SelectionMode.Catalyst;
    }

    private void ShowCatalystOptions(List<CatalystInstance> available)
    {
        ClearCatalystOptions();
        pendingCatalystOptions = available;

        for (int i = 0; i < available.Count; i++)
        {
            var instance = available[i]; // クロージャ用にローカル変数へ
            var buttonObj = Instantiate(catalystButtonPrefab, catalystButtonParent);

            var text = buttonObj.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = instance.data.catalystName;

            var button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => OnCatalystOptionSelected(instance));

            spawnedCatalystButtons.Add(buttonObj);
        }
    }

    private void ClearCatalystOptions()
    {
        foreach (var obj in spawnedCatalystButtons)
        {
            Destroy(obj);
        }
        spawnedCatalystButtons.Clear();
    }
    public void OnCatalystOptionSelected(CatalystInstance instance)
    {
        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];
        ClearCatalystOptions(); // 選択肢ボタンは選んだ時点で不要になるので破棄

        // 転身: 自分のBKから交代先を選ばせる(Selfスコープだが即時実行ではなく対象選択が要る特殊ケース)
        if (instance.Id == CatalystId.Tenshin)
        {
            var aliveBackups = BattleManager.Instance.PlayerTeam.backups.FindAll(c => !c.IsDefeated);
            if (aliveBackups.Count == 0)
            {
                currentSelectionMode = SelectionMode.None;
                return;
            }

            pendingCatalystInstance = instance;
            SetTargetLabel(true);
            ShowTargetButtons(BattleManager.Instance.PlayerTeam.backups, c => !c.IsDefeated);
            currentSelectionMode = SelectionMode.CatalystTarget;
            return;
        }

        // 奇襲: 自分自身が相手BKを攻撃する。狙う相手BKを選ばせる(1段階のみ)
        if (instance.Id == CatalystId.Kishuu)
        {
            var aliveEnemyBackups = BattleManager.Instance.EnemyTeam.backups.FindAll(c => !c.IsDefeated);
            if (aliveEnemyBackups.Count == 0)
            {
                currentSelectionMode = SelectionMode.None;
                return;
            }

            pendingCatalystInstance = instance;
            SetTargetLabel(false);
            ShowTargetButtons(BattleManager.Instance.EnemyTeam.backups, c => !c.IsDefeated);
            currentSelectionMode = SelectionMode.CatalystTarget;
            return;
        }

        // 模倣: コピー元カタリストのスコープに応じて対象を選ばせる(コピー元自体はSelfスコープでない場合)
        if (instance.Id == CatalystId.Mohou)
        {
            var mimicId = CatalystRegistry.GetMohouMimicCandidate(actor);
            if (mimicId == null)
            {
                // コピーできるカタリストが無ければ何もせず終える(消費しない)
                currentSelectionMode = SelectionMode.None;
                return;
            }

            var mimicScope = CatalystRegistry.GetCatalystTargetScope(mimicId.Value);

            if (mimicScope == TargetScope.Self)
            {
                CatalystRegistry.ExecuteCatalyst(actor, actor, instance);
                currentSelectionMode = SelectionMode.None;
                UpdateVisual();
                return;
            }

            pendingCatalystInstance = instance;
            ShowScopedTargets(mimicScope, actor);
            currentSelectionMode = SelectionMode.CatalystTarget;
            return;
        }

        TargetScope scope = CatalystRegistry.GetCatalystTargetScope(instance.Id);

        if (scope == TargetScope.Self)
        {
            CatalystRegistry.ExecuteCatalyst(actor, actor, instance);
            currentSelectionMode = SelectionMode.None;
            UpdateVisual();
            return;
        }

        pendingCatalystInstance = instance;
        ShowScopedTargets(scope, actor);
        currentSelectionMode = SelectionMode.CatalystTarget;
    }

    public void ShowDeathSwapSelection(List<CharacterState> backups)
    {
        SetTargetLabel(true); // 自チームの繰り上げ選択なので常に味方
        ShowTargetButtons(backups, c => !c.IsDefeated);
        currentSelectionMode = SelectionMode.DeathSwap;
    }

    // ---------------- 降参 ----------------

    /// <summary>「降参」ボタンから呼ぶ。即座には降参せず、確認パネルを開くだけ。</summary>
    public void OnSurrenderButtonClicked()
    {
        if (surrenderConfirmPanel != null) surrenderConfirmPanel.SetActive(true);
    }

    /// <summary>確認パネルの「はい」ボタンから呼ぶ。</summary>
    public void OnSurrenderConfirmed()
    {
        if (surrenderConfirmPanel != null) surrenderConfirmPanel.SetActive(false);
        BattleManager.Instance.Surrender();
    }

    /// <summary>確認パネルの「いいえ」ボタンから呼ぶ。</summary>
    public void OnSurrenderCancelled()
    {
        if (surrenderConfirmPanel != null) surrenderConfirmPanel.SetActive(false);
    }

    // ---------------- 行動制限時間(タイムアウト) ----------------

    private const string KeyTurnStartedAt = "duelv_turn_started_at"; // "{部屋コード}:{手番が本当に始まったUTC unix秒}"

    private void StartTurnTimer()
    {
        StopTurnTimer(); // 念のため既存のコルーチンだけ止める(チェックポイントには触らない)

        if (turnTimeLimitSeconds <= 0f) return;

        bool isOnline = BattleManager.Instance != null && BattleManager.Instance.IsOnlineMatch;
        if (!isOnline)
        {
            // CPU戦はリロード/再接続という概念が無いので、素直にフルの制限時間から始める
            turnTimeoutRoutine = StartCoroutine(TurnTimeoutRoutine(turnTimeLimitSeconds));
            return;
        }

        // オンライン戦: ここでフルの制限時間から素直にコルーチンを回すと、期限が来る直前にリロードするだけで
        // 手番タイマーを何度でもリセットできてしまう(=事実上無限に粘れる)。
        // 「この手番が本当に始まった時刻」をUTC実時刻でPlayerPrefsに残し、リロード後もそこからの
        // 経過時間を差し引いた残り時間で再開することで、リロード連打による無限粘りを防ぐ。
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long startedAt = nowUnix;

        string stored = PlayerPrefs.GetString(KeyTurnStartedAt, "");
        string[] parts = stored.Split(':');
        if (parts.Length == 2 && parts[0] == RoomManager.CurrentRoomCode && long.TryParse(parts[1], out var parsedTs))
        {
            startedAt = parsedTs; // リロード後の再開: 元の開始時刻をそのまま使う
        }
        else
        {
            PlayerPrefs.SetString(KeyTurnStartedAt, $"{RoomManager.CurrentRoomCode}:{startedAt}");
            PlayerPrefs.Save();
        }

        // リロードを繰り返す等で既に制限時間を超えていた場合は0にクランプする(負数のままでもコルーチン側で
        // 即タイムアウト処理されるが、TurnStart()の呼び出し元へ一度戻ってから処理するため直接ここでは呼ばない。
        // ここで同期的にHandleTurnTimeout()→TurnEnd()→...→TurnStart()と呼んでしまうと、
        // まだ実行中の元のTurnStart()呼び出しと二重に走ってしまう(再入)ため)
        float remaining = Mathf.Max(0f, turnTimeLimitSeconds - (nowUnix - startedAt));
        turnTimeoutRoutine = StartCoroutine(TurnTimeoutRoutine(remaining));
    }

    private void StopTurnTimer()
    {
        if (turnTimeoutRoutine != null)
        {
            StopCoroutine(turnTimeoutRoutine);
            turnTimeoutRoutine = null;
        }
        if (timeLimitText != null)
        {
            timeLimitText.text = "";
            timeLimitText.color = timeLimitNormalColor;
        }
    }

    /// <summary>手番が正常に(自分の行動 or タイムアウト処理で)終わった時に呼ぶ。リロード対策用のチェックポイントを消す。</summary>
    private void ClearTurnTimerCheckpoint()
    {
        PlayerPrefs.DeleteKey(KeyTurnStartedAt);
        PlayerPrefs.Save();
    }

    private IEnumerator TurnTimeoutRoutine(float duration)
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            if (timeLimitText != null)
            {
                timeLimitText.text = Mathf.CeilToInt(remaining).ToString();
                timeLimitText.color = remaining <= timeLimitWarningThreshold ? timeLimitWarningColor : timeLimitNormalColor;
            }
            yield return null;
            remaining -= Time.deltaTime;
        }
        // durationが既に0(リロード復帰時点で制限時間超過)の場合でも、必ず1フレーム待ってから処理する。
        // ここでyieldを挟まずに即HandleTurnTimeout()を呼ぶと、StartCoroutineを呼んだ側(TurnStart()の
        // 冒頭)がまだ実行中のうちにTurnEnd()経由で再度TurnStart()が呼ばれてしまう(再入)おそれがある
        yield return null;
        turnTimeoutRoutine = null;
        HandleTurnTimeout();
    }

    /// <summary>制限時間切れ。まだ行動していない自チームのFWを自動で行動させてターンを終える。</summary>
    private void HandleTurnTimeout()
    {
        Debug.Log("[ActionUI] 行動制限時間切れ。自動で行動をパスします。");
        CloseAllTargetSelection();

        for (int i = 0; i < 2; i++)
        {
            var actor = BattleManager.Instance.PlayerTeam.forwards[i];
            if (actor.hasActedThisTurn) continue;
            AutoPassAction(actor);
        }
        TurnEnd();
    }

    private void AutoPassAction(CharacterState actor)
    {
        if (actor.isSuspendedAnimation)
        {
            // 仮死中は防御も選べない(ウルト/交代のみ選択可)。強制的に交代させると戦闘不能扱いになってしまうため、何もせず手番を終える
        }
        else if (actor.huntBoundTo != null && !actor.huntBoundTo.IsDefeated)
        {
            // ハンティンググラウンド中は防御を選べないため、拘束対象への通常攻撃で代替する
            TurnManager.Instance.QueueAction(actor, ActionType.Attack, actor.huntBoundTo);
        }
        else
        {
            TurnManager.Instance.QueueAction(actor, ActionType.Defense, null);
        }
        actor.MarkAsActed();
    }

    void TurnEnd()
    {
        StopTurnTimer();
        ClearTurnTimerCheckpoint();

        if (BattleManager.Instance.IsOnlineMatch)
        {
            OnlineBattleSync.Instance.SendBufferedActions();
        }

        rightObj.SetActive(false);
        leftObj.SetActive(false);
        UI_BG_Obj.SetActive(false);
        UI_Catalyst_BG_Obj.SetActive(false);

        foreach (var obj in UI_Action_BG_Obj)
        {
            obj.SetActive(false);
        }

        TurnManager.Instance.OnTurnEndPressed();
    }

    public void TurnStart()
    {
        StartTurnTimer();

        rightObj.SetActive(true);
        leftObj.SetActive(true);
        UI_BG_Obj.SetActive(true);
        UI_Catalyst_BG_Obj.SetActive(true);

        if (isFirstTurnStart)
        {
            isFirstTurnStart = false;
            nowCharacterIndex = 0;
            LeftCharacterButton.SetAsLastSibling();
        }
        else
        {
            int leftIndex = LeftCharacterButton.GetSiblingIndex();
            int rightIndex = RightCharacterButton.GetSiblingIndex();

            if (leftIndex > rightIndex)
            {
                nowCharacterIndex = 1;
                RightCharacterButton.SetAsLastSibling();
            }
            else
            {
                nowCharacterIndex = 0;
                LeftCharacterButton.SetAsLastSibling();
            }
        }
        UpdateVisual();
        foreach (var obj in UI_Action_BG_Obj)
        {
            obj.SetActive(true);
        }

        var leftActor = BattleManager.Instance.PlayerTeam.forwards[0];
        var rightActor = BattleManager.Instance.PlayerTeam.forwards[1];
        int currentHalfTurn = TurnManager.Instance.currentHalfTurn;

        // マスタリーXP付与用: 自分の手番が来た=このキャラは今回FWとして出た、の記録
        leftActor.everActiveAsForward = true;
        rightActor.everActiveAsForward = true;

        // 仮死中(モルフェ)は「行動不能」を持っていてもウルト/交代だけは選べるので、丸ごとの自動スキップ対象からは外す
        if (leftActor.IsStunned(currentHalfTurn) && !leftActor.isSuspendedAnimation)
        {
            nowCharacterIndex = 0;
            leftActor.MarkAsActed();
            AdvanceToNextCharacterOrEndTurn();
        }

        if (rightActor.IsStunned(currentHalfTurn) && !rightActor.hasActedThisTurn && !rightActor.isSuspendedAnimation)
        {
            nowCharacterIndex = 1;
            rightActor.MarkAsActed();
            AdvanceToNextCharacterOrEndTurn();
        }

        leftImage.sprite = left[0];
        leftButton.interactable = true;
        rightImage.sprite = right[0];
        rightButton.interactable = true;

        rightText.text = BattleManager.Instance.PlayerTeam.forwards[1].data.characterName;
        leftText.text = BattleManager.Instance.PlayerTeam.forwards[0].data.characterName;
    }
    public void InitializeVisual()
    {
        InitializeBattleStartTime();
        UpdateVisual();
    }

    // ---------------- 合計経過時間 ----------------

    private void InitializeBattleStartTime()
    {
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (BattleManager.Instance.IsOnlineMatch)
        {
            // リロードで再接続してもストップウォッチが0に戻らないよう、部屋コードと紐付けて開始時刻を覚えておく
            string stored = PlayerPrefs.GetString(KeyBattleStartedAt, "");
            string[] parts = stored.Split(':');
            if (parts.Length == 2 && parts[0] == RoomManager.CurrentRoomCode && long.TryParse(parts[1], out var parsedTs))
            {
                battleStartUnix = parsedTs;
            }
            else
            {
                battleStartUnix = nowUnix;
                PlayerPrefs.SetString(KeyBattleStartedAt, $"{RoomManager.CurrentRoomCode}:{battleStartUnix}");
                PlayerPrefs.Save();
            }
        }
        else
        {
            battleStartUnix = nowUnix; // CPU戦は再接続がないので毎回フレッシュに計測する
        }

        lastDisplayedElapsedSeconds = -1; // 強制的に表示を更新させる
    }

    /// <summary>対戦が終わった(勝敗確定・降参・切断判定)時にBattleManagerから呼ぶ。</summary>
    public void ClearBattleStartCheckpoint()
    {
        PlayerPrefs.DeleteKey(KeyBattleStartedAt);
        PlayerPrefs.Save();
    }

    private void UpdateElapsedTimeDisplay()
    {
        if (elapsedTimeText == null || battleStartUnix < 0) return;

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int elapsedSeconds = (int)Math.Max(0, nowUnix - battleStartUnix);

        if (elapsedSeconds == lastDisplayedElapsedSeconds) return; // 1秒単位でしか変わらないので毎フレーム文字列を作り直さない
        lastDisplayedElapsedSeconds = elapsedSeconds;

        int minutes = elapsedSeconds / 60;
        int seconds = elapsedSeconds % 60;
        elapsedTimeText.text = $"{minutes:00}:{seconds:00}";
    }
    private void UpdateVisual()
    {
        rightText.text = BattleManager.Instance.PlayerTeam.forwards[1].data.characterName;
        leftText.text = BattleManager.Instance.PlayerTeam.forwards[0].data.characterName;
        var actor = BattleManager.Instance.PlayerTeam.forwards[nowCharacterIndex];
        bool isReady = actor.CanUseUltimate(TurnManager.Instance.currentHalfTurn);
        UI_Action_BG_Image[2].sprite = isReady ? UI_Action_BG[0] : UI_Action_BG[1];
        Debug.Log($"Ultimate currentGauge {actor.data.characterName}{actor.currentUltGauge}");

        bool isSilenced = actor.IsSilenced(TurnManager.Instance.currentHalfTurn);
        UI_Action_BG_Image[1].sprite = isSilenced ? UI_Action_BG[1] : UI_Action_BG[0];

        bool catalystLocked = actor.hasUsedCatalystThisHalfTurn
            || (TurnManager.Instance.isPlayerFirst && TurnManager.Instance.currentHalfTurn == 0);
        UI_Catalyst_BG_Image.sprite = catalystLocked ? UI_Catalyst_BG[1] : UI_Catalyst_BG[0];
    }

    public void OnNowCharacterSelected(int characterIndex)
    {
        
        nowCharacterIndex = characterIndex;
        UpdateVisual();
        if (characterIndex == 0)
        {
            LeftCharacterButton.SetAsLastSibling();
        }
        else
        {
            RightCharacterButton.SetAsLastSibling();
        }
    }

    private void AdvanceToNextCharacterOrEndTurn()
    {
        CloseAllTargetSelection();

        if (nowCharacterIndex == 0)
        {
            leftImage.sprite = left[1];
            leftButton.interactable = false;

            foreach (var obj in actionTargetButtons)
            {
                obj.SetActive(false);
            }

            bool bothActed = BattleManager.Instance.PlayerTeam.forwards[0].hasActedThisTurn
               && BattleManager.Instance.PlayerTeam.forwards[1].hasActedThisTurn;

            if (bothActed)
            {
                TurnEnd();
                return;
            }
            nowCharacterIndex = 1;
            RightCharacterButton.SetAsLastSibling();
        }
        else
        {
            rightImage.sprite = right[1];
            rightButton.interactable = false;

            foreach (var obj in actionTargetButtons)
            {
                obj.SetActive(false);
            }

            bool bothActed = BattleManager.Instance.PlayerTeam.forwards[0].hasActedThisTurn
               && BattleManager.Instance.PlayerTeam.forwards[1].hasActedThisTurn;

            if (bothActed)
            {
                TurnEnd();
                return;
            }
            nowCharacterIndex = 0;
            LeftCharacterButton.SetAsLastSibling();
        }
    }

    
}
