using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Team;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public ActionUI actionUI;

    [Header("敵編成データ(CPU戦用、Inspectorでセット)")]
    [SerializeField] private List<CharacterData> enemyFWData;
    [SerializeField] private List<CharacterData> enemyBKData;
    [SerializeField] private List<CharacterCatalystLoadout> enemyCatalystLoadouts;

    public Team PlayerTeam { get; private set; }
    public Team EnemyTeam { get; private set; } // CPU戦: CPU / オンライン戦: 対戦相手

    /// <summary>オンライン対戦かどうか(RoomManagerのコードが残っているかで判定)</summary>
    public bool IsOnlineMatch => !string.IsNullOrEmpty(RoomManager.CurrentRoomCode);

    /// <summary>
    /// リロード等で中断された進行中のオンライン対戦へ復帰する場合、DuelSceneを読み込む前に
    /// (RoomManager.CheckForActiveMatchから)trueにセットする。SetupOnlineMatch側で1回だけ消費する。
    /// </summary>
    public static bool IsReconnecting;

    public event Action OnTeamsReady;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // マスタリー/アカウントレベルXP付与(ShowResultで使う)の前に、既存XPのキャッシュを読み込んでおく。
        // 未ロードのまま加算すると既存の蓄積を消してしまう事故になるため
        CharacterMastery.Load();
        AccountLevel.Load();

        if (IsOnlineMatch)
        {
            SetupOnlineMatch();
        }
        else
        {
            SetupCpuMatch();
        }
    }

    // ---------------- CPU戦 ----------------

    private void SetupCpuMatch()
    {
        EnemyTeam = Team.CreateFrom(enemyFWData, enemyBKData, enemyCatalystLoadouts);

        string uid = LocalUser.GetOrCreateUid();
        LoadTeamFromPreset(uid, Hensei.SelectedPresetIndex, team =>
        {
            PlayerTeam = team;
            FinishSetup();
        });
    }

    // ---------------- オンライン対戦 ----------------

    private void SetupOnlineMatch()
    {
        if (IsReconnecting)
        {
            IsReconnecting = false;
            SetupOnlineMatchFromReconnect();
            return;
        }

        string roomCode = RoomManager.CurrentRoomCode;
        Debug.Log($"[BattleManager] SetupOnlineMatch開始 roomCode={roomCode}");

        FirestoreBridge.Instance.GetOnce(roomCode, doc =>
        {
            if (doc == null)
            {
                Debug.LogError("[BattleManager] 部屋データの取得に失敗しました");
                return;
            }

            string hostUid = doc.TryGetValue("hostUid", out var h) ? h as string : null;
            int hostPresetIndex = doc.TryGetValue("hostPresetIndex", out var hp) ? (int)(long)hp : -1;
            string guestUid = doc.TryGetValue("guestUid", out var g) ? g as string : null;
            int guestPresetIndex = doc.TryGetValue("guestPresetIndex", out var gp) ? (int)(long)gp : -1;

            string myUid = LocalUser.GetOrCreateUid();

            string opponentUid = RoomManager.IsHost ? guestUid : hostUid;
            int opponentPresetIndex = RoomManager.IsHost ? guestPresetIndex : hostPresetIndex;
            int myPresetIndex = RoomManager.IsHost ? hostPresetIndex : guestPresetIndex;

            Debug.Log($"[BattleManager] IsHost={RoomManager.IsHost} myUid={myUid} myPresetIndex={myPresetIndex} opponentUid={opponentUid} opponentPresetIndex={opponentPresetIndex}");

            LoadTeamFromPreset(myUid, myPresetIndex, myTeam =>
            {
                Debug.Log($"[BattleManager] PlayerTeam読み込み完了 null?={myTeam == null}");
                PlayerTeam = myTeam;

                LoadTeamFromPreset(opponentUid, opponentPresetIndex, opponentTeam =>
                {
                    Debug.Log($"[BattleManager] EnemyTeam読み込み完了 null?={opponentTeam == null}");
                    EnemyTeam = opponentTeam;
                    FinishSetup();
                });
            });
        });
    }

    // リロード等で中断された対戦への復帰。プリセットから作り直すのではなく、
    // 直前にBattleSnapshotへ保存しておいた「進行中の状態」をそのまま読み込む
    private void SetupOnlineMatchFromReconnect()
    {
        string roomCode = RoomManager.CurrentRoomCode;
        Debug.Log($"[BattleManager] SetupOnlineMatchFromReconnect開始 roomCode={roomCode}");

        FirestoreBridge.Instance.GetOnce(roomCode, doc =>
        {
            if (BattleSnapshot.TryRestore(doc, out var myTeam, out var enemyTeam,
                    out int halfTurn, out int turnNumber, out bool isPlayerTurnNow, out bool isPlayerFirst))
            {
                PlayerTeam = myTeam;
                EnemyTeam = enemyTeam;

                TurnManager.Instance.currentHalfTurn = halfTurn;
                TurnManager.Instance.currentTurnNumber = turnNumber;
                TurnManager.Instance.isPlayerTurnNow = isPlayerTurnNow;
                TurnManager.Instance.isPlayerFirst = isPlayerFirst;

                FinishSetup();
            }
            else
            {
                Debug.LogError("[BattleManager] 再接続用スナップショットが見つかりませんでした。通常の対戦開始にフォールバックします。");
                SetupOnlineMatch();
            }
        });
    }

    // ---------------- 共通処理 ----------------

    private void LoadTeamFromPreset(string uid, int presetIndex, Action<Team> onLoaded)
    {
        Debug.Log($"[BattleManager] LoadTeamFromPreset uid={uid} presetIndex={presetIndex}");
        FirestoreBridge.Instance.LoadPreset(uid, presetIndex, doc =>
        {
            Debug.Log($"[BattleManager] Firestore doc取得 keys={(doc != null ? string.Join(",", doc.Keys) : "null")}");
            TeamPreset preset = TeamPreset.FromFirestoreDocument(doc);
            Team team = TeamPresetConverter.ToTeam(preset);

            if (team == null)
            {
                Debug.LogError($"[BattleManager] チーム生成失敗 uid={uid} preset={presetIndex}");
            }
            onLoaded?.Invoke(team);
        });
    }

    private void FinishSetup()
    {
        if (PlayerTeam == null || EnemyTeam == null)
        {
            Debug.LogError("[BattleManager] チームが揃わなかったためバトルを開始できません");
            return;
        }

        actionUI.InitializeVisual();
        OnTeamsReady?.Invoke();

        if (IsOnlineMatch)
        {
            RoomManager.Instance.StartHeartbeat();
            RoomManager.Instance.PersistActiveMatch(); // リロードされても復帰できるよう端末に保存
        }
    }

    public bool CheckVictoryCondition()
    {
        bool playerDefeated = PlayerTeam.IsDefeated();
        bool enemyDefeated = EnemyTeam.IsDefeated();

        if (!playerDefeated && !enemyDefeated)
            return false;

        bool playerWon = enemyDefeated; // 両者全滅時はplayerWon=false扱い
        ShowResult(playerWon);
        return true;
    }

    /// <summary>
    /// 自分から降参する。オンライン対戦なら相手にも通知してから、自分は即座に負け扱いで終了する。
    /// 相手側の終了はRoomManagerがsurrenderedByの更新を検知して行う。
    /// </summary>
    public void Surrender()
    {
        if (IsOnlineMatch)
        {
            FirestoreBridge.Instance.SendUpdate(new Dictionary<string, object>
            {
                { "surrenderedBy", RoomManager.IsHost ? "host" : "guest" }
            });
        }

        ShowResult(false);
    }

    public void ShowResult(bool playerWon)
    {
        if (IsOnlineMatch)
        {
            RoomManager.Instance.StopHeartbeat();
            RoomManager.Instance.ClearActiveMatch(); // 対戦が終わったので再接続対象から外す
            actionUI.ClearBattleStartCheckpoint();
        }

        // マスタリーXP付与: 自チームで1回でもFWとして出たキャラ全員へ「試合のターン数(+勝利なら+5)」を加算
        var masteryResults = CharacterMastery.GrantMatchXp(PlayerTeam.AllCharacters(), TurnManager.Instance.currentTurnNumber, playerWon);

        // アカウントXP付与: マスタリーとは独立。試合ごとに1回、「試合のターン数(+勝利なら+5/敗北なら+2)」を加算
        var accountLevelResult = AccountLevel.GrantMatchXp(TurnManager.Instance.currentTurnNumber, playerWon);

        BattleResultData.Pending = new BattleResultData
        {
            isVictory = playerWon,
            masteryResults = masteryResults,
            accountLevelResult = accountLevelResult,
            endHalfTurn = TurnManager.Instance.currentHalfTurn + 1
        };

        SceneManager.LoadScene("ResultScene");
    }
}