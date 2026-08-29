using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// オンライン対戦時、TurnManagerの行動選択(QueueAction)と死亡繰り上げ選択をFirestore経由で相手と同期する。
///
/// 送るのは「誰が・何を・誰にしたか」という選択(入力)のみ。ダメージ計算等の結果は
/// 両クライアントが同じロジック(TurnManager/ExecuteAction)で独立に実行するので送らない。
///
/// 送信タイミングの重要な注意点:
///   自分の半ターン2行動が確定した直後(処理を始める前)にすぐ送信する。
///   処理完了を待ってから送ると、死亡繰り上げのように「相手の入力待ち」が発生した時に
///   相手がまだ何も受け取っていない状態でデッドロックするため。
/// </summary>
public class OnlineBattleSync : MonoBehaviour
{
    public static OnlineBattleSync Instance { get; private set; }

    [System.Serializable]
    private class EncodedAction
    {
        public int actorSlot;
        public string actionType;
        public string targetSide;   // "own" | "opponent" | "none"
        public bool targetIsBackup;
        public int targetSlot;
    }

    private List<EncodedAction> outgoingBuffer = new List<EncodedAction>();
    private int lastAppliedActionSeq = 0;
    private int lastAppliedSwapSeq = 0;

    /// <summary>相手からの行動を再生中はtrue。この間はQueueActionをバッファに積まない。</summary>
    public bool IsReplaying { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        FirestoreBridge.Instance.OnGameUpdated += HandleGameUpdated;
    }

    void OnDisable()
    {
        if (FirestoreBridge.Instance == null) return;
        FirestoreBridge.Instance.OnGameUpdated -= HandleGameUpdated;
    }

    // ---------------- 送信側: 通常の行動 ----------------

    /// <summary>TurnManager.QueueActionから呼ばれる。自分のFWの行動だけをバッファに積む。</summary>
    public void BufferOutgoingAction(CharacterState actor, ActionType type, CharacterState target)
    {
        if (IsReplaying) return;

        var myTeam = BattleManager.Instance.PlayerTeam;
        var enemyTeam = BattleManager.Instance.EnemyTeam;

        int actorSlot = myTeam.forwards.IndexOf(actor);
        if (actorSlot == -1) return;

        string targetSide = "none";
        bool targetIsBackup = false;
        int targetSlot = -1;

        if (target != null)
        {
            int slotInMyFw = myTeam.forwards.IndexOf(target);
            int slotInMyBk = myTeam.backups.IndexOf(target);
            int slotInEnemyFw = enemyTeam.forwards.IndexOf(target);

            if (slotInMyFw != -1) { targetSide = "own"; targetSlot = slotInMyFw; }
            else if (slotInMyBk != -1) { targetSide = "own"; targetIsBackup = true; targetSlot = slotInMyBk; }
            else if (slotInEnemyFw != -1) { targetSide = "opponent"; targetSlot = slotInEnemyFw; }
        }

        outgoingBuffer.Add(new EncodedAction
        {
            actorSlot = actorSlot,
            actionType = type.ToString(),
            targetSide = targetSide,
            targetIsBackup = targetIsBackup,
            targetSlot = targetSlot
        });
    }

    /// <summary>
    /// 自分の半ターン2行動が確定した直後、処理を始める前に呼ぶ(ActionUIから)。
    /// これにより、自分が処理中に相手の入力待ちが発生しても相手は既に受信済みの状態になる。
    /// </summary>
    public void SendBufferedActions()
    {
        var actionsPayload = outgoingBuffer.Select(a => (object)new Dictionary<string, object>
        {
            { "actorSlot", a.actorSlot },
            { "actionType", a.actionType },
            { "targetSide", a.targetSide },
            { "targetIsBackup", a.targetIsBackup },
            { "targetSlot", a.targetSlot }
        }).ToList();

        lastAppliedActionSeq++;

        var fields = new Dictionary<string, object>
        {
            { "battleActionSeq", lastAppliedActionSeq },
            { "battleActionBy", RoomManager.IsHost ? "host" : "guest" },
            { "battleActions", actionsPayload }
        };

        FirestoreBridge.Instance.SendUpdate(fields);
        outgoingBuffer.Clear();
    }

    // ---------------- 送信側: 死亡繰り上げの選択 ----------------

    /// <summary>自分のFWが戦闘不能になり、繰り上げるBKを選んだら呼ぶ(TurnManagerから)。</summary>
    public void SendDeathSwapChoice(int backupIndex)
    {
        lastAppliedSwapSeq++;

        var fields = new Dictionary<string, object>
        {
            { "deathSwapSeq", lastAppliedSwapSeq },
            { "deathSwapBy", RoomManager.IsHost ? "host" : "guest" },
            { "deathSwapBackupIndex", backupIndex }
        };

        FirestoreBridge.Instance.SendUpdate(fields);
    }

    // ---------------- 受信側 ----------------

    private void HandleGameUpdated(Dictionary<string, object> data)
    {
        if (data == null) return;
        string myRole = RoomManager.IsHost ? "host" : "guest";

        // --- 通常の行動 ---
        if (data.TryGetValue("battleActionSeq", out var seqObj))
        {
            int seq = (int)(long)seqObj;
            string by = data.TryGetValue("battleActionBy", out var byObj) ? byObj as string : null;

            if (seq > lastAppliedActionSeq && by != myRole)
            {
                if (data.TryGetValue("battleActions", out var actionsObj) && actionsObj is List<object> actionsList)
                {
                    lastAppliedActionSeq = seq;
                    ApplyRemoteActions(actionsList);
                }
            }
        }

        // --- 死亡繰り上げの選択 ---
        if (data.TryGetValue("deathSwapSeq", out var swapSeqObj))
        {
            int swapSeq = (int)(long)swapSeqObj;
            string by = data.TryGetValue("deathSwapBy", out var byObj) ? byObj as string : null;

            if (swapSeq > lastAppliedSwapSeq && by != myRole)
            {
                if (data.TryGetValue("deathSwapBackupIndex", out var idxObj))
                {
                    lastAppliedSwapSeq = swapSeq;
                    int backupIndex = (int)(long)idxObj;
                    ApplyRemoteDeathSwap(backupIndex);
                }
            }
        }
    }

    private void ApplyRemoteActions(List<object> actionsList)
    {
        var myTeam = BattleManager.Instance.PlayerTeam;
        var enemyTeam = BattleManager.Instance.EnemyTeam;

        IsReplaying = true;

        foreach (var raw in actionsList)
        {
            var dict = raw as Dictionary<string, object>;
            if (dict == null) continue;

            int actorSlot = (int)(long)dict["actorSlot"];
            string actionTypeStr = dict["actionType"] as string;
            string targetSide = dict["targetSide"] as string;
            bool targetIsBackup = dict.TryGetValue("targetIsBackup", out var tib) && (bool)tib;
            int targetSlot = (int)(long)dict["targetSlot"];

            var actor = enemyTeam.forwards[actorSlot];

            CharacterState target = null;
            if (targetSide == "own")
            {
                target = targetIsBackup ? enemyTeam.backups[targetSlot] : enemyTeam.forwards[targetSlot];
            }
            else if (targetSide == "opponent")
            {
                target = myTeam.forwards[targetSlot];
            }

            ActionType actionType = (ActionType)System.Enum.Parse(typeof(ActionType), actionTypeStr);
            TurnManager.Instance.QueueAction(actor, actionType, target);
            actor.MarkAsActed();
        }

        IsReplaying = false;

        TurnManager.Instance.OnTurnEndPressed();
    }

    private void ApplyRemoteDeathSwap(int backupIndex)
    {
        // 相手が「自分のFW」の繰り上げを選んだ通知 = 受信側から見るとEnemyTeamの繰り上げ
        var enemyTeam = BattleManager.Instance.EnemyTeam;
        int fwSlot = TurnManager.Instance.PendingEnemyDeathSwapFwSlot;

        if (fwSlot < 0 || fwSlot >= enemyTeam.forwards.Count)
        {
            Debug.LogError("[OnlineBattleSync] 繰り上げ対象のスロットが不正です。ローカル側で死亡が未検知の可能性があります。");
            return;
        }

        var incoming = enemyTeam.backups[backupIndex];
        var outgoing = enemyTeam.forwards[fwSlot];

        enemyTeam.forwards[fwSlot] = incoming;
        enemyTeam.backups.RemoveAt(backupIndex);
        enemyTeam.backups.Add(outgoing);

        incoming.hasActedThisTurn = false;

        TurnManager.Instance.RebindAfterRemoteDeathSwap(enemyTeam, fwSlot);
        TurnManager.Instance.ResumeAfterEnemyDeathSwap();
    }
}