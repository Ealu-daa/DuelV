using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ルームコード方式のオンラインマッチング。
///
/// 想定フロー:
///   Menu → OnlineMatchScene(部屋作成/入室) → HenseiScene(編成) → DuelScene(対戦)
///
/// status遷移:
///   "waiting"  : 部屋作成直後、相手を待っている
///   "matched"  : 2人揃った。両者HenseiSceneへ移動して編成する
///   "battling" : 両者が編成(プリセット選択)を終えた。DuelSceneへ移動する
///
/// このコンポーネントはDontDestroyOnLoadで永続化し、
/// OnlineMatchScene → HenseiScene → DuelScene を跨いで状態を監視し続ける。
/// </summary>
public class RoomManager : MonoBehaviour
{
    private static RoomManager _instance;
    public static RoomManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RoomManager>();
                if (_instance == null)
                {
                    // MenuScene起動直後などOnlineMatchSceneをまだ一度も訪れていない場合、
                    // シーンに置かれた本来のOnlineMatchingManagerがまだ存在しないため自動生成する
                    // (再接続チェックをMenuManager.Start()から呼ぶために、ここで必ず取得できる必要がある)
                    var go = new GameObject("RoomManager (Auto)");
                    _instance = go.AddComponent<RoomManager>();
                }
            }
            return _instance;
        }
    }

    // シーンを跨いで参照される情報
    public static string CurrentRoomCode;
    public static bool IsHost;

    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 紛らわしい0/O,1/Iは除外
    private const int CodeLength = 6;

    private bool hasTransitionedToHensei = false;
    private bool hasTransitionedToDuel = false;
    private bool hasHandledSurrender = false;
    private bool hasHandledHenseiTimeout = false;

    // Hensei側で「相手が準備完了か」を表示するための通知。引数は相手の準備完了状態
    public event Action<bool> OnOpponentReadyChanged;
    private bool lastKnownOpponentReady = false;

    [Header("お披露目演出(編成確定後、DuelScene遷移前に数秒表示)")]
    [SerializeField] private float revealDisplaySeconds = 8f;

    // 両者の編成が確定した瞬間に発火。引数は(相手uid, 相手のpresetIndex)。
    // HenseiScene側(Hensei.cs)がこれを購読してお披露目パネルを表示する。
    // DuelSceneへの遷移自体はこのイベントの成否に関わらずRoomManagerがrevealDisplaySeconds後に必ず行う
    public event Action<string, int> OnRevealStart;

    [Header("切断検知(オンライン対戦中のみ、BattleManagerが開始/停止する)")]
    [SerializeField] private float heartbeatInterval = 5f;    // 自分の生存報告を送る間隔(秒)
    [SerializeField] private float disconnectThreshold = 120f; // 相手の生存報告がこれだけ途絶えたら切断とみなす(秒)。この時間内に生存報告が再開すれば通常通り試合続行できる

    private Coroutine heartbeatRoutine;
    private long opponentHeartbeatUnix = -1; // 相手から最後に届いた生存報告の時刻(UTC unix秒)。未受信なら-1
    private bool hasHandledDisconnect = false;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        // DontDestroyOnLoadはルート(親を持たない)GameObjectにしか効かない。
        // OnlineMatchingManagerの子として配置されていても正しく永続化されるよう、ルート側に対して呼ぶ
        DontDestroyOnLoad(transform.root.gameObject);
    }

    void OnEnable()
    {
        FirestoreBridge.Instance.OnGameUpdated += HandleGameUpdated;
        FirestoreBridge.Instance.OnGameError += HandleGameError;
    }

    void OnDisable()
    {
        if (FirestoreBridge.Instance == null) return;
        FirestoreBridge.Instance.OnGameUpdated -= HandleGameUpdated;
        FirestoreBridge.Instance.OnGameError -= HandleGameError;
    }

    // ---------------- ホスト: 部屋作成 ----------------

    public void CreateRoom(Action<string> onCreated = null, Action<string> onError = null)
    {
        string code = GenerateRoomCode();
        string uid = LocalUser.GetOrCreateUid();

        var fields = new Dictionary<string, object>
        {
            { "status", "waiting" },
            { "hostUid", uid },
            { "hostPresetIndex", -1 },
            { "guestUid", "" },
            { "guestPresetIndex", -1 }
        };

        Action<string> handler = null;
        handler = createdId =>
        {
            FirestoreBridge.Instance.OnGameCreated -= handler;

            CurrentRoomCode = code;
            IsHost = true;
            hasTransitionedToHensei = false;
            hasTransitionedToDuel = false;
            hasHandledSurrender = false;
            hasHandledHenseiTimeout = false;
            lastKnownOpponentReady = false;

            FirestoreBridge.Instance.JoinGame(code); // 以後この部屋を監視

            onCreated?.Invoke(code);
        };
        FirestoreBridge.Instance.OnGameCreated += handler;

        FirestoreBridge.Instance.CreateNewGame(code, fields);
    }

    // ---------------- ゲスト: 入室 ----------------

    public void JoinRoom(string code, Action onJoined = null, Action<string> onError = null)
    {
        code = code.Trim().ToUpper();

        FirestoreBridge.Instance.GetOnce(code, doc =>
        {
            if (doc == null)
            {
                onError?.Invoke("部屋が見つかりません");
                return;
            }

            string status = doc.TryGetValue("status", out var s) ? s as string : null;
            if (status != "waiting")
            {
                onError?.Invoke("この部屋にはもう入室できません");
                return;
            }

            string uid = LocalUser.GetOrCreateUid();

            CurrentRoomCode = code;
            IsHost = false;
            hasTransitionedToHensei = false;
            hasTransitionedToDuel = false;
            hasHandledSurrender = false;
            hasHandledHenseiTimeout = false;
            lastKnownOpponentReady = false;

            FirestoreBridge.Instance.JoinGame(code); // 監視開始してから更新を送る

            var updateFields = new Dictionary<string, object>
            {
                { "guestUid", uid },
                { "status", "matched" } // 2人揃った。編成フェーズへ
            };
            FirestoreBridge.Instance.SendUpdate(updateFields);

            onJoined?.Invoke();
        });
    }

    // ---------------- 編成完了の通知(HenseiSceneから呼ぶ) ----------------

    /// <summary>編成(プリセット選択)が終わったら呼ぶ。両者揃うと自動でDuelSceneへ遷移する。</summary>
    public void MarkPresetReady(int myPresetIndex)
    {
        string field = IsHost ? "hostPresetIndex" : "guestPresetIndex";
        var update = new Dictionary<string, object> { { field, myPresetIndex } };
        FirestoreBridge.Instance.SendUpdate(update);
    }

    // ---------------- 部屋の状態監視 ----------------

    private void HandleGameUpdated(Dictionary<string, object> data)
    {
        if (data == null) return;

        // 相手の生存報告(ハートビート)を記録しておく。statusフィールドの有無に関わらず毎回チェックする
        string opponentHeartbeatField = IsHost ? "guestHeartbeatAt" : "hostHeartbeatAt";
        if (data.TryGetValue(opponentHeartbeatField, out var hbObj))
        {
            opponentHeartbeatUnix = Convert.ToInt64(hbObj);
        }

        // 相手が降参したかどうかは、statusフィールドの有無に関わらず毎回チェックする
        if (!hasHandledSurrender
            && data.TryGetValue("surrenderedBy", out var surrenderedByObj)
            && surrenderedByObj is string surrenderedBy)
        {
            string myRole = IsHost ? "host" : "guest";
            if (surrenderedBy != myRole && surrenderedBy != "")
            {
                hasHandledSurrender = true;
                if (BattleManager.Instance != null)
                {
                    BattleManager.Instance.ShowResult(true); // 相手が降参 = 自分の勝ち
                }
            }
        }

        // 相手の準備完了(プリセット確定)状態を毎回チェックし、変化があればHenseiSceneへ通知する
        if (data.TryGetValue("hostPresetIndex", out var hpObj) && data.TryGetValue("guestPresetIndex", out var gpObj))
        {
            long hostPresetForReady = Convert.ToInt64(hpObj);
            long guestPresetForReady = Convert.ToInt64(gpObj);
            bool opponentReady = IsHost ? guestPresetForReady >= 0 : hostPresetForReady >= 0;
            if (opponentReady != lastKnownOpponentReady)
            {
                lastKnownOpponentReady = opponentReady;
                OnOpponentReadyChanged?.Invoke(opponentReady);
            }
        }

        // Hensei側で90秒経っても両者揃わなかった時のドッジ通知。statusフィールドの有無に関わらず毎回チェックする
        if (!hasHandledHenseiTimeout
            && data.TryGetValue("henseiTimedOut", out var timedOutObj)
            && timedOutObj is bool timedOut && timedOut)
        {
            hasHandledHenseiTimeout = true;
            LeaveRoom();
            SceneManager.LoadScene("OnlineMatchScene");
            return;
        }

        if (!data.TryGetValue("status", out var statusObj)) return;
        string status = statusObj as string;

        if (status == "matched" && !hasTransitionedToHensei)
        {
            hasTransitionedToHensei = true;
            SceneManager.LoadScene("HenseiScene");
        }
        else if (status == "matched")
        {
            // 編成フェーズ中: 両者のpresetIndexが揃ったか確認し、揃ったらbattlingへ
            long hostPreset = data.TryGetValue("hostPresetIndex", out var hp) ? (long)hp : -1;
            long guestPreset = data.TryGetValue("guestPresetIndex", out var gp) ? (long)gp : -1;

            if (hostPreset >= 0 && guestPreset >= 0 && !hasTransitionedToDuel)
            {
                FirestoreBridge.Instance.SendUpdate(new Dictionary<string, object> { { "status", "battling" } });
            }
        }
        else if (status == "battling" && !hasTransitionedToDuel)
        {
            hasTransitionedToDuel = true;

            string hostUidForReveal = data.TryGetValue("hostUid", out var huObj) ? huObj as string : null;
            string guestUidForReveal = data.TryGetValue("guestUid", out var guObj) ? guObj as string : null;
            long hostPresetForReveal = data.TryGetValue("hostPresetIndex", out var hprObj) ? Convert.ToInt64(hprObj) : -1;
            long guestPresetForReveal = data.TryGetValue("guestPresetIndex", out var gprObj) ? Convert.ToInt64(gprObj) : -1;

            string opponentUidForReveal = IsHost ? guestUidForReveal : hostUidForReveal;
            int opponentPresetForReveal = (int)(IsHost ? guestPresetForReveal : hostPresetForReveal);

            OnRevealStart?.Invoke(opponentUidForReveal, opponentPresetForReveal);
            StartCoroutine(TransitionToDuelAfterReveal());
        }
    }

    private IEnumerator TransitionToDuelAfterReveal()
    {
        yield return new WaitForSeconds(revealDisplaySeconds);
        SceneManager.LoadScene("DuelScene");
    }

    private void HandleGameError(string message)
    {
        Debug.LogError("[RoomManager] Firestoreエラー: " + message);
    }

    /// <summary>対戦終了後などに部屋の監視を止めて状態をリセットする</summary>
    public void LeaveRoom()
    {
        FirestoreBridge.Instance.LeaveGame();
        StopHeartbeat();
        CurrentRoomCode = null;
        hasTransitionedToHensei = false;
        hasTransitionedToDuel = false;
        hasHandledSurrender = false;
        hasHandledHenseiTimeout = false;
        lastKnownOpponentReady = false;
        opponentHeartbeatUnix = -1;
        hasHandledDisconnect = false;
        ClearActiveMatch();
    }

    // ---------------- Hensei準備完了タイムアウト(HenseiSceneから呼ぶ) ----------------

    /// <summary>
    /// HenseiScene側で90秒経っても両者の準備が揃わなかった時に呼ぶ。Firestoreにフラグを送るだけで、
    /// 自分自身のロビーへの帰還は呼び出し側(Hensei.cs)が行うこと(相手側はHandleGameUpdated経由で検知する)。
    /// </summary>
    public void ReportHenseiTimeout()
    {
        FirestoreBridge.Instance.SendUpdate(new Dictionary<string, object> { { "henseiTimedOut", true } });
    }

    // ---------------- リロード後の再接続(進行中の対戦をPlayerPrefsに覚えておく) ----------------

    private const string KeyActiveRoomCode = "duelv_active_room_code";
    private const string KeyActiveRoomIsHost = "duelv_active_room_ishost";

    /// <summary>DuelSceneでオンライン対戦が始まったら呼ぶ。リロードされても再接続できるよう端末に保存する。</summary>
    public void PersistActiveMatch()
    {
        PlayerPrefs.SetString(KeyActiveRoomCode, CurrentRoomCode);
        PlayerPrefs.SetInt(KeyActiveRoomIsHost, IsHost ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>対戦が終わった(勝敗確定・降参・切断判定・部屋を出る)時に呼ぶ。</summary>
    public void ClearActiveMatch()
    {
        PlayerPrefs.DeleteKey(KeyActiveRoomCode);
        PlayerPrefs.DeleteKey(KeyActiveRoomIsHost);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// アプリ起動時(MenuManager.Start())に呼ぶ。リロード等で中断された進行中の対戦があれば、
    /// Firestore側がまだ"battling"であることを確認した上でtrueを返す。
    /// trueの場合、呼び出し側はDuelSceneへ直行してよい(再接続に必要な状態は全てここで復元済み)。
    /// </summary>
    public void CheckForActiveMatch(Action<bool> onResult)
    {
        string code = PlayerPrefs.GetString(KeyActiveRoomCode, "");
        if (string.IsNullOrEmpty(code))
        {
            onResult?.Invoke(false);
            return;
        }

        FirestoreBridge.Instance.GetOnce(code, doc =>
        {
            string status = doc != null && doc.TryGetValue("status", out var s) ? s as string : null;
            if (status != "battling")
            {
                // 対戦は既に終わっている/部屋が消えている等 → 復帰しない
                ClearActiveMatch();
                onResult?.Invoke(false);
                return;
            }

            CurrentRoomCode = code;
            IsHost = PlayerPrefs.GetInt(KeyActiveRoomIsHost, 0) == 1;
            hasTransitionedToHensei = true; // 既に対戦フェーズなので編成フェーズは通過済み扱い
            hasTransitionedToDuel = true;
            hasHandledSurrender = false;

            FirestoreBridge.Instance.JoinGame(code); // 監視再開

            BattleManager.IsReconnecting = true;
            onResult?.Invoke(true);
        });
    }

    // ---------------- 切断検知(DuelScene中のみ、BattleManagerから呼ぶ) ----------------

    /// <summary>対戦開始時に呼ぶ。自分の生存報告を定期送信し、相手の生存報告が途絶えたら切断扱いにする。</summary>
    public void StartHeartbeat()
    {
        StopHeartbeat();
        opponentHeartbeatUnix = -1;
        hasHandledDisconnect = false;
        heartbeatRoutine = StartCoroutine(HeartbeatLoop());
    }

    /// <summary>対戦終了時(勝敗確定・降参・部屋を出る時)に呼ぶ。</summary>
    public void StopHeartbeat()
    {
        if (heartbeatRoutine != null)
        {
            StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = null;
        }
    }

    private IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSeconds(heartbeatInterval);
        while (true)
        {
            string myField = IsHost ? "hostHeartbeatAt" : "guestHeartbeatAt";
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            FirestoreBridge.Instance.SendUpdate(new Dictionary<string, object> { { myField, nowUnix } });

            if (opponentHeartbeatUnix > 0 && !hasHandledDisconnect)
            {
                long elapsed = nowUnix - opponentHeartbeatUnix;
                if (elapsed > (long)disconnectThreshold)
                {
                    hasHandledDisconnect = true;
                    Debug.LogWarning($"[RoomManager] 相手の生存報告が{elapsed}秒途絶えました。切断とみなして勝利扱いにします。");
                    if (BattleManager.Instance != null)
                    {
                        BattleManager.Instance.ShowResult(true); // 相手切断 = 自分の勝ち
                    }
                    yield break;
                }
            }

            yield return wait;
        }
    }

    // ---------------- コード生成 ----------------

    private string GenerateRoomCode()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < CodeLength; i++)
        {
            sb.Append(CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)]);
        }
        return sb.ToString();
    }
}