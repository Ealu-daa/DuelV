using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Firestore REST APIを使った対戦データの同期。
/// UnityWebRequestベースなので Editor / WebGLビルド どちらでも同じコードで動作する。
///
/// 使い方:
///   FirestoreBridge.Instance.CreateNewGame(gameId, fields);  // 部屋作成
///   FirestoreBridge.Instance.JoinGame(gameId);                // 監視(ポーリング)開始
///   FirestoreBridge.Instance.SendUpdate(fields);               // 手を打つ・状態更新
///   FirestoreBridge.Instance.LeaveGame();                      // 監視停止
///
/// 現在はFirestoreのテストモード(認証不要)前提。
/// 本番でセキュリティルールを絞る際はAuthorizationヘッダーにIDトークンを付与する必要あり。
/// </summary>
public class FirestoreBridge : MonoBehaviour
{

    private static FirestoreBridge _instance;

    [Header("Firebase設定")]
    [SerializeField] private string projectId = "duelv-3d3ac";

    [Header("ポーリング間隔(秒) - ターン制なので2〜3秒で十分")]
    [SerializeField] private float pollInterval = 2f;

    private string BaseUrl => $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents";

    [HideInInspector] public string currentGameId;

    private Coroutine pollRoutine;
    private string lastUpdateTime; // 変化検知用(同じデータでのコールバック連発を防ぐ)

    // ---- イベント(ゲーム側はこれを購読する) ----
    public event Action<Dictionary<string, object>> OnGameUpdated;
    public event Action<string> OnGameCreated;
    public event Action<string> OnGameError;

    public static FirestoreBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<FirestoreBridge>();
                if (_instance == null)
                {
                    var go = new GameObject("FirestoreBridge (Auto)");
                    _instance = go.AddComponent<FirestoreBridge>();
                    DontDestroyOnLoad(go);
                    Debug.LogWarning("[FirestoreBridge] シーンに存在しなかったため自動生成しました。本来は起動シーンから通しで再生してください。");
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        // DontDestroyOnLoadはルート(親を持たない)GameObjectにしか効かない。
        // 他の永続マネージャーの子として配置されていても正しく永続化されるよう、ルート側に対して呼ぶ
        DontDestroyOnLoad(transform.root.gameObject);
    }

    // ---------------- 公開API ----------------

    /// <summary>新規ゲームドキュメントを作成(部屋を作る)</summary>
    public void CreateNewGame(string gameId, Dictionary<string, object> fields)
    {
        StartCoroutine(CreateGameRoutine(gameId, fields));
    }

    /// <summary>既存ゲームに参加し、監視(ポーリング)を開始</summary>
    public void JoinGame(string gameId)
    {
        currentGameId = gameId;
        if (pollRoutine != null) StopCoroutine(pollRoutine);
        lastUpdateTime = null;
        pollRoutine = StartCoroutine(PollLoop());
    }

    /// <summary>監視を止める(退室・シーン離脱時)</summary>
    public void LeaveGame()
    {
        if (pollRoutine != null) StopCoroutine(pollRoutine);
        pollRoutine = null;
        currentGameId = null;
    }

    /// <summary>指定フィールドだけを部分更新(手を打つ・ターン切り替えなど)</summary>
    public void SendUpdate(Dictionary<string, object> fields)
    {
        if (string.IsNullOrEmpty(currentGameId))
        {
            Debug.LogWarning("[FirestoreBridge] currentGameIdが未設定です");
            return;
        }
        StartCoroutine(UpdateGameRoutine(currentGameId, fields));
    }

    /// <summary>即座に1回だけ取得したい場合(ポーリングを待たず確認したい時など)</summary>
    public void GetOnce(string gameId, Action<Dictionary<string, object>> onResult)
    {
        StartCoroutine(GetGameRoutine(gameId, onResult));
    }

    // ---------------- プリセット保存/読込 (users/{uid}/presets/{slot}) ----------------

    /// <summary>プリセットを保存(全上書き)。presetSlotは1,2,3など。onDoneで完了(成否)を受け取れる。</summary>
    public void SavePreset(string uid, int presetSlot, string presetName, List<Dictionary<string, object>> characters, Action<bool> onDone = null)
    {
        var fields = new Dictionary<string, object> {
            { "name", presetName },
            { "characters", characters.ConvertAll(c => (object)c) }
        };
        string path = $"users/{uid}/presets/preset{presetSlot}";
        StartCoroutine(SetDocumentRoutine(path, fields, ok =>
        {
            if (ok) Debug.Log($"[FirestoreBridge] プリセット{presetSlot}保存完了");
            onDone?.Invoke(ok);
        }));
    }

    /// <summary>プリセット1件を読み込み</summary>
    public void LoadPreset(string uid, int presetSlot, Action<Dictionary<string, object>> onResult)
    {
        string path = $"users/{uid}/presets/preset{presetSlot}";
        StartCoroutine(GetDocumentRoutine(path, onResult));
    }

    /// <summary>そのユーザーの全プリセットを読み込み</summary>
    public void LoadAllPresets(string uid, Action<List<Dictionary<string, object>>> onResult)
    {
        string path = $"users/{uid}/presets";
        StartCoroutine(ListDocumentsRoutine(path, onResult));
    }

    // ---------------- 汎用コルーチン(任意パス) ----------------

    IEnumerator SetDocumentRoutine(string path, Dictionary<string, object> fields, Action<bool> onDone)
    {
        // documentIdやupdateMaskを付けない素のPATCH = ドキュメント全体を作成/上書き
        string url = $"{BaseUrl}/{path}";
        string body = MiniJson.Serialize(new Dictionary<string, object> {
            { "fields", ToFirestoreFields(fields) }
        });

        using (var req = BuildRequest(url, "PATCH", body))
        {
            yield return req.SendWebRequest();

            bool success = req.result == UnityWebRequest.Result.Success;
            if (!success)
            {
                Debug.LogError("[FirestoreBridge] SetDocument失敗: " + req.error + " / " + req.downloadHandler.text);
                OnGameError?.Invoke(req.error);
            }
            onDone?.Invoke(success);
        }
    }

    IEnumerator GetDocumentRoutine(string path, Action<Dictionary<string, object>> onResult)
    {
        string url = $"{BaseUrl}/{path}";
        using (var req = BuildRequest(url, "GET", null))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FirestoreBridge] GetDocument失敗(未作成の可能性): " + req.error);
                onResult?.Invoke(null);
                yield break;
            }

            var raw = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            onResult?.Invoke(ParseFirestoreDocument(raw));
        }
    }

    IEnumerator ListDocumentsRoutine(string collectionPath, Action<List<Dictionary<string, object>>> onResult)
    {
        string url = $"{BaseUrl}/{collectionPath}";
        using (var req = BuildRequest(url, "GET", null))
        {
            yield return req.SendWebRequest();

            var result = new List<Dictionary<string, object>>();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FirestoreBridge] ListDocuments失敗: " + req.error);
                onResult?.Invoke(result);
                yield break;
            }

            var raw = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            if (raw != null && raw.TryGetValue("documents", out var docsObj) && docsObj is List<object> docs)
            {
                foreach (var d in docs)
                {
                    var doc = d as Dictionary<string, object>;
                    var parsed = ParseFirestoreDocument(doc);
                    // ドキュメントIDをnameパスの末尾から取り出して付与しておく(例: "preset1")
                    if (doc != null && doc.TryGetValue("name", out var nameObj))
                    {
                        string fullName = nameObj as string;
                        string docId = fullName?.Substring(fullName.LastIndexOf('/') + 1);
                        parsed["_docId"] = docId;
                    }
                    result.Add(parsed);
                }
            }
            onResult?.Invoke(result);
        }
    }

    // ---------------- 内部コルーチン(games専用) ----------------

    IEnumerator CreateGameRoutine(string gameId, Dictionary<string, object> fields)
    {
        string url = $"{BaseUrl}/games?documentId={UnityWebRequest.EscapeURL(gameId)}";
        string body = MiniJson.Serialize(new Dictionary<string, object> {
            { "fields", ToFirestoreFields(fields) }
        });

        using (var req = BuildRequest(url, "POST", body))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirestoreBridge] Create失敗: " + req.error + " / " + req.downloadHandler.text);
                OnGameError?.Invoke(req.error);
                yield break;
            }

            Debug.Log("[FirestoreBridge] ゲーム作成完了: " + gameId);
            OnGameCreated?.Invoke(gameId);
        }
    }

    IEnumerator UpdateGameRoutine(string gameId, Dictionary<string, object> fields)
    {
        var sb = new StringBuilder($"{BaseUrl}/games/{gameId}?");
        foreach (var key in fields.Keys)
        {
            sb.Append("updateMask.fieldPaths=").Append(UnityWebRequest.EscapeURL(key)).Append("&");
        }
        string url = sb.ToString().TrimEnd('&');

        string body = MiniJson.Serialize(new Dictionary<string, object> {
            { "fields", ToFirestoreFields(fields) }
        });

        using (var req = BuildRequest(url, "PATCH", body))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirestoreBridge] Update失敗: " + req.error + " / " + req.downloadHandler.text);
                OnGameError?.Invoke(req.error);
            }
        }
    }

    IEnumerator GetGameRoutine(string gameId, Action<Dictionary<string, object>> onResult)
    {
        string url = $"{BaseUrl}/games/{gameId}";
        using (var req = BuildRequest(url, "GET", null))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirestoreBridge] Get失敗: " + req.error);
                OnGameError?.Invoke(req.error);
                onResult?.Invoke(null);
                yield break;
            }

            var raw = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            var parsed = ParseFirestoreDocument(raw);
            onResult?.Invoke(parsed);
        }
    }

    IEnumerator PollLoop()
    {
        var wait = new WaitForSeconds(pollInterval);
        while (!string.IsNullOrEmpty(currentGameId))
        {
            string url = $"{BaseUrl}/games/{currentGameId}";
            using (var req = BuildRequest(url, "GET", null))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var raw = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
                    if (raw != null && raw.TryGetValue("updateTime", out var ut))
                    {
                        string updateTime = ut as string;
                        // 前回取得時と更新時刻が同じならコールバックを呼ばない(無駄な処理を防ぐ)
                        if (updateTime != lastUpdateTime)
                        {
                            lastUpdateTime = updateTime;
                            var parsed = ParseFirestoreDocument(raw);
                            OnGameUpdated?.Invoke(parsed);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[FirestoreBridge] Poll失敗: " + req.error);
                }
            }
            yield return wait;
        }
    }

    UnityWebRequest BuildRequest(string url, string method, string body)
    {
        var req = new UnityWebRequest(url, method);
        if (!string.IsNullOrEmpty(body))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.SetRequestHeader("Content-Type", "application/json");
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        return req;
    }

    // ---------------- Firestore型変換 ----------------

    /// <summary>プレーンなDictionaryをFirestore REST用の型付きfields形式に変換</summary>
    static Dictionary<string, object> ToFirestoreFields(Dictionary<string, object> plain)
    {
        var result = new Dictionary<string, object>();
        foreach (var kv in plain)
            result[kv.Key] = ToFirestoreValue(kv.Value);
        return result;
    }

    static Dictionary<string, object> ToFirestoreValue(object v)
    {
        if (v == null) return new Dictionary<string, object> { { "nullValue", null } };

        switch (v)
        {
            case string s:
                return new Dictionary<string, object> { { "stringValue", s } };
            case bool b:
                return new Dictionary<string, object> { { "booleanValue", b } };
            case int i:
                return new Dictionary<string, object> { { "integerValue", i.ToString() } };
            case long l:
                return new Dictionary<string, object> { { "integerValue", l.ToString() } };
            case float f:
                return new Dictionary<string, object> { { "doubleValue", (double)f } };
            case double d:
                return new Dictionary<string, object> { { "doubleValue", d } };
            case Dictionary<string, object> map:
                return new Dictionary<string, object> {
                    { "mapValue", new Dictionary<string, object> { { "fields", ToFirestoreFields(map) } } }
                };
            case List<object> list:
                var values = new List<object>();
                foreach (var item in list) values.Add(ToFirestoreValue(item));
                return new Dictionary<string, object> {
                    { "arrayValue", new Dictionary<string, object> { { "values", values } } }
                };
            default:
                return new Dictionary<string, object> { { "stringValue", v.ToString() } };
        }
    }

    /// <summary>Firestoreから返ってきたドキュメント全体(name/fields/updateTime等)からプレーンなDictionaryを取り出す</summary>
    static Dictionary<string, object> ParseFirestoreDocument(Dictionary<string, object> doc)
    {
        var result = new Dictionary<string, object>();
        if (doc == null || !doc.ContainsKey("fields")) return result;

        var fields = doc["fields"] as Dictionary<string, object>;
        foreach (var kv in fields)
            result[kv.Key] = FromFirestoreValue(kv.Value as Dictionary<string, object>);
        return result;
    }

    static object FromFirestoreValue(Dictionary<string, object> wrapper)
    {
        if (wrapper == null) return null;

        if (wrapper.ContainsKey("stringValue")) return wrapper["stringValue"];
        if (wrapper.ContainsKey("booleanValue")) return wrapper["booleanValue"];
        if (wrapper.ContainsKey("integerValue")) return long.Parse((string)wrapper["integerValue"]);
        if (wrapper.ContainsKey("doubleValue")) return Convert.ToDouble(wrapper["doubleValue"]);
        if (wrapper.ContainsKey("nullValue")) return null;
        if (wrapper.ContainsKey("mapValue"))
        {
            var mapObj = wrapper["mapValue"] as Dictionary<string, object>;
            var inner = mapObj != null && mapObj.ContainsKey("fields")
                ? mapObj["fields"] as Dictionary<string, object>
                : new Dictionary<string, object>();
            var result = new Dictionary<string, object>();
            foreach (var kv in inner)
                result[kv.Key] = FromFirestoreValue(kv.Value as Dictionary<string, object>);
            return result;
        }
        if (wrapper.ContainsKey("arrayValue"))
        {
            var arrObj = wrapper["arrayValue"] as Dictionary<string, object>;
            var values = arrObj != null && arrObj.ContainsKey("values")
                ? arrObj["values"] as List<object>
                : new List<object>();
            var list = new List<object>();
            foreach (var item in values)
                list.Add(FromFirestoreValue(item as Dictionary<string, object>));
            return list;
        }
        return null;
    }
    // ---------------- ユーザープロフィール(users/{uid}) ----------------

    /// <summary>ユーザープロフィールドキュメント(echo・lastWinDate等)を取得</summary>
    public void GetUserProfile(string uid, Action<Dictionary<string, object>> onResult)
    {
        string path = $"users/{uid}";
        StartCoroutine(GetDocumentRoutine(path, onResult));
    }

    /// <summary>エコー加算結果と勝利日を部分更新で保存(他フィールドは維持される)</summary>
    public void SaveEchoResult(string uid, int newEchoTotal, string lastWinDate, Action<bool> onDone = null)
    {
        string path = $"users/{uid}";
        var fields = new Dictionary<string, object> { { "echo", newEchoTotal } };
        if (lastWinDate != null)
            fields["lastWinDate"] = lastWinDate;

        StartCoroutine(UpdateDocumentRoutine(path, fields, onDone));
    }

    /// <summary>所持キャラ/カタリストのIDリストを部分更新で保存(他フィールドは維持される)。図鑑・ショップ共通で使う</summary>
    public void SaveOwnedCollections(string uid, List<int> ownedCharacterIds, List<int> ownedCatalystIds, Action<bool> onDone = null)
    {
        string path = $"users/{uid}";
        var fields = new Dictionary<string, object>
        {
            { "ownedCharacterIds", ownedCharacterIds.ConvertAll(id => (object)id) },
            { "ownedCatalystIds", ownedCatalystIds.ConvertAll(id => (object)id) }
        };

        StartCoroutine(UpdateDocumentRoutine(path, fields, onDone));
    }

    /// <summary>プロフィール(表示名・アイコン/フレーム・称号など)を任意フィールドの部分更新で保存する。PlayerProfileから使う</summary>
    public void SaveProfileFields(string uid, Dictionary<string, object> fields, Action<bool> onDone = null)
    {
        string path = $"users/{uid}";
        StartCoroutine(UpdateDocumentRoutine(path, fields, onDone));
    }

    // ---------------- 汎用: 部分更新(updateMask付き、任意パス) ----------------

    IEnumerator UpdateDocumentRoutine(string path, Dictionary<string, object> fields, Action<bool> onDone)
    {
        var sb = new StringBuilder($"{BaseUrl}/{path}?");
        foreach (var key in fields.Keys)
            sb.Append("updateMask.fieldPaths=").Append(UnityWebRequest.EscapeURL(key)).Append("&");
        string url = sb.ToString().TrimEnd('&');

        string body = MiniJson.Serialize(new Dictionary<string, object> {
        { "fields", ToFirestoreFields(fields) }
    });

        using (var req = BuildRequest(url, "PATCH", body))
        {
            yield return req.SendWebRequest();

            bool success = req.result == UnityWebRequest.Result.Success;
            if (!success)
            {
                Debug.LogError("[FirestoreBridge] UpdateDocument失敗: " + req.error + " / " + req.downloadHandler.text);
            }
            onDone?.Invoke(success);
        }
    }
}