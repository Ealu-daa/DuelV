using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Firebase Authentication REST APIを使った認証。FirestoreBridgeと同じくUnityWebRequestベースなので
/// Editor / WebGLビルドどちらでも同じコードで動作する(Firebase Unity SDKは使わない)。
///
/// 今は匿名認証のみ。将来Googleアカウント等をリンクして、機種変更してもデータを引き継げるようにする予定。
///
/// 使い方: アプリ起動時(MenuManagerのStart等)に一度 EnsureSignedIn(onDone) を呼び、
/// 完了を待ってから LocalUser.GetOrCreateUid() や Firestore関連の呼び出しを行うこと。
///
/// 注意: これを導入すると、UIDの発行元が「端末生成の適当なGUID」から「Firebaseが発行する匿名UID」に
/// 切り替わる。既存のテストデータ(echoやプリセットなど)は旧UIDに紐づいたままになるので、
/// 導入直後は一見「データが消えた」ように見える(実際には新しいUIDの下に作り直される)。
/// </summary>
public class FirebaseAuthBridge : MonoBehaviour
{
    private static FirebaseAuthBridge _instance;
    public static FirebaseAuthBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<FirebaseAuthBridge>();
                if (_instance == null)
                {
                    var go = new GameObject("FirebaseAuthBridge (Auto)");
                    _instance = go.AddComponent<FirebaseAuthBridge>();
                    DontDestroyOnLoad(go);
                    Debug.LogWarning("[FirebaseAuthBridge] シーンに存在しなかったため自動生成しました。Web API Keyを設定してください。");
                }
            }
            return _instance;
        }
    }

    [Header("Firebase設定")]
    [SerializeField] private string webApiKey = ""; // Firebase Console > プロジェクトの設定 > 全般 > ウェブAPIキー

    [Header("Google連携設定(WebGLビルドのみ対応)")]
    [SerializeField] private string googleClientId = ""; // Google Cloud Console > 認証情報 > OAuth 2.0 クライアントID(ウェブ アプリケーション)

    private const string KeyUid = "duelv_auth_uid";
    private const string KeyIdToken = "duelv_auth_idtoken";
    private const string KeyRefreshToken = "duelv_auth_refreshtoken";
    private const string KeyExpiry = "duelv_auth_expiry"; // Unixタイムスタンプ(秒)
    private const string KeyLinkedGoogle = "duelv_auth_linked_google";

    public bool IsSignedIn { get; private set; }
    public string Uid { get; private set; }
    public string IdToken { get; private set; }
    public bool IsLinkedWithGoogle { get; private set; }

    /// <summary>Googleリンク成功時に呼ばれる(引数はメールアドレス。取得できなければ空文字)</summary>
    public event Action<string> OnGoogleLinked;
    /// <summary>Googleリンク失敗時に呼ばれる(引数はエラー内容)</summary>
    public event Action<string> OnGoogleLinkFailed;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void InitGoogleSignIn(string clientId);
    [DllImport("__Internal")] private static extern void PromptGoogleSignIn();
#endif

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        // DontDestroyOnLoadはルート(親を持たない)GameObjectにしか効かない。
        // 他の永続マネージャーの子として配置されていても正しく永続化されるよう、ルート側に対して呼ぶ
        DontDestroyOnLoad(transform.root.gameObject);

        IsLinkedWithGoogle = PlayerPrefs.GetInt(KeyLinkedGoogle, 0) == 1;
    }

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(googleClientId))
        {
            InitGoogleSignIn(googleClientId);
        }
#endif
    }

    /// <summary>
    /// Googleサインインのプロンプトを表示する(「Googleでリンク」ボタンから呼ぶ)。
    /// WebGLビルドでのみ動作する(Editor実行では何もしない)。
    /// </summary>
    public void SignInWithGoogle()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(googleClientId))
        {
            Debug.LogError("[FirebaseAuthBridge] Google Client IDが未設定です。Inspectorで設定してください。");
            return;
        }
        PromptGoogleSignIn();
#else
        Debug.LogWarning("[FirebaseAuthBridge] Googleサインインは現在WebGLビルドでのみ対応しています(Editor実行では動作しません)。");
#endif
    }

    /// <summary>
    /// Google Identity Services側のJSコールバックから呼ばれる(SendMessage経由)。
    /// このため、このコンポーネントが乗っているGameObjectの名前は必ず"FirebaseAuthBridge"にしておくこと
    /// (Instanceの自動生成に頼ると名前が"FirebaseAuthBridge (Auto)"になり、コールバックが届かない)。
    /// </summary>
    public void OnGoogleCredentialReceived(string googleIdToken)
    {
        StartCoroutine(LinkWithGoogleRoutine(googleIdToken));
    }

    private IEnumerator LinkWithGoogleRoutine(string googleIdToken)
    {
        if (!IsSignedIn)
        {
            Debug.LogError("[FirebaseAuthBridge] サインインが完了していない状態でGoogleリンクは呼べません。");
            OnGoogleLinkFailed?.Invoke("サインイン未完了");
            yield break;
        }

        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={webApiKey}";
        string postBody = $"id_token={UnityWebRequest.EscapeURL(googleIdToken)}&providerId=google.com";

        var payload = new Dictionary<string, object>
        {
            { "requestUri", string.IsNullOrEmpty(Application.absoluteURL) ? "https://localhost" : Application.absoluteURL },
            { "postBody", postBody },
            { "returnSecureToken", true },
            { "returnIdpCredential", true },
            { "idToken", IdToken } // 既存の匿名セッションに紐付けてリンクする(新規サインインにしない)
        };
        string body = MiniJson.Serialize(payload);

        using (var req = BuildJsonRequest(url, body))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirebaseAuthBridge] Googleリンク失敗: " + req.error + " / " + req.downloadHandler.text);
                OnGoogleLinkFailed?.Invoke(req.downloadHandler.text);
                yield break;
            }

            var res = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            ApplySession(
                uid: res["localId"] as string,
                idToken: res["idToken"] as string,
                refreshToken: res["refreshToken"] as string,
                expiresInSeconds: res.TryGetValue("expiresIn", out var e) ? Convert.ToInt32(e) : 3600
            );

            IsLinkedWithGoogle = true;
            PlayerPrefs.SetInt(KeyLinkedGoogle, 1);
            PlayerPrefs.Save();

            string email = res.TryGetValue("email", out var em) ? em as string : "";
            Debug.Log($"[FirebaseAuthBridge] Googleアカウントとリンクしました: {email}");
            OnGoogleLinked?.Invoke(email);
        }
    }

    /// <summary>
    /// サインイン済み状態を保証する。キャッシュ済みセッションがあればトークンを更新、無ければ匿名で新規作成する。
    /// 何度呼んでもよい(サインイン済みなら即座にonDoneを呼ぶ)。
    /// </summary>
    public void EnsureSignedIn(Action<bool> onDone)
    {
        if (IsSignedIn)
        {
            onDone?.Invoke(true);
            return;
        }

        if (string.IsNullOrEmpty(webApiKey))
        {
            Debug.LogError("[FirebaseAuthBridge] Web API Keyが未設定です。Inspectorで設定してください。");
            onDone?.Invoke(false);
            return;
        }

        string cachedRefreshToken = PlayerPrefs.GetString(KeyRefreshToken, "");
        if (!string.IsNullOrEmpty(cachedRefreshToken))
        {
            StartCoroutine(RefreshTokenRoutine(cachedRefreshToken, success =>
            {
                if (success) { onDone?.Invoke(true); return; }
                // リフレッシュ失敗(トークン失効など) → 匿名で作り直す
                StartCoroutine(SignInAnonymouslyRoutine(onDone));
            }));
        }
        else
        {
            StartCoroutine(SignInAnonymouslyRoutine(onDone));
        }
    }

    private IEnumerator SignInAnonymouslyRoutine(Action<bool> onDone)
    {
        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={webApiKey}";
        string body = MiniJson.Serialize(new Dictionary<string, object> { { "returnSecureToken", true } });

        using (var req = BuildJsonRequest(url, body))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[FirebaseAuthBridge] 匿名サインイン失敗: " + req.error + " / " + req.downloadHandler.text);
                onDone?.Invoke(false);
                yield break;
            }

            var res = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            ApplySession(
                uid: res["localId"] as string,
                idToken: res["idToken"] as string,
                refreshToken: res["refreshToken"] as string,
                expiresInSeconds: res.TryGetValue("expiresIn", out var e) ? Convert.ToInt32(e) : 3600
            );
            onDone?.Invoke(true);
        }
    }

    private IEnumerator RefreshTokenRoutine(string refreshToken, Action<bool> onDone)
    {
        string url = $"https://securetoken.googleapis.com/v1/token?key={webApiKey}";
        string form = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshToken)}";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(form);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[FirebaseAuthBridge] トークン更新失敗: " + req.error);
                onDone?.Invoke(false);
                yield break;
            }

            var res = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            ApplySession(
                uid: res["user_id"] as string,
                idToken: res["id_token"] as string,
                refreshToken: res["refresh_token"] as string,
                expiresInSeconds: res.TryGetValue("expires_in", out var e) ? Convert.ToInt32(e) : 3600
            );
            onDone?.Invoke(true);
        }
    }

    /// <summary>
    /// ログアウトする。ローカルに保存されている認証情報を全て消し、次にEnsureSignedIn()が呼ばれた時は
    /// 新しい匿名セッションが作られる(呼び出し側でMenuSceneを再読み込みするなどして再度EnsureSignedInを
    /// 通すこと。SettingsSceneUI.OnLogoutConfirmed()等から使う想定)。
    /// </summary>
    public void SignOut()
    {
        IsSignedIn = false;
        Uid = null;
        IdToken = null;
        IsLinkedWithGoogle = false;

        PlayerPrefs.DeleteKey(KeyUid);
        PlayerPrefs.DeleteKey(KeyIdToken);
        PlayerPrefs.DeleteKey(KeyRefreshToken);
        PlayerPrefs.DeleteKey(KeyExpiry);
        PlayerPrefs.DeleteKey(KeyLinkedGoogle);
        PlayerPrefs.Save();
    }

    private void ApplySession(string uid, string idToken, string refreshToken, int expiresInSeconds)
    {
        Uid = uid;
        IdToken = idToken;
        IsSignedIn = true;

        PlayerPrefs.SetString(KeyUid, uid);
        PlayerPrefs.SetString(KeyIdToken, idToken);
        PlayerPrefs.SetString(KeyRefreshToken, refreshToken);
        long expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresInSeconds;
        PlayerPrefs.SetString(KeyExpiry, expiry.ToString());
        PlayerPrefs.Save();
    }

    private UnityWebRequest BuildJsonRequest(string url, string jsonBody)
    {
        var req = new UnityWebRequest(url, "POST");
        byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }
}
