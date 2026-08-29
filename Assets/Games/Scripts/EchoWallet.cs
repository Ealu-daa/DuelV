using System;

/// <summary>
/// プレイヤーのEcho残高。PlayerCollection/PlayerProfile/CharacterMastery/AccountLevelと同じパターン。
/// 実体はFirestoreの users/{uid}.echo。DuelScene/HenseiScene以外の各シーンでEchoを表示するための共通口。
///
/// C#のstaticフィールドはシーン読み込みを跨いでも値を保持するので、Echoが変わる操作(購入・対戦報酬など)を
/// した直後はSetBalance()でこのキャッシュも同期しておくこと(そうしないと他画面が古い値を見せてしまう)。
/// </summary>
public static class EchoWallet
{
    public static bool IsLoaded { get; private set; }
    public static int Balance { get; private set; }

    /// <summary>残高が変わるたびに呼ばれる(Load完了時・SetBalance呼び出し時)</summary>
    public static event Action OnBalanceChanged;

    /// <summary>Firestoreから最新のEcho残高を取得する</summary>
    public static void Load(Action onLoaded = null)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            int balance = 0;
            if (profile != null && profile.TryGetValue("echo", out var e))
                balance = Convert.ToInt32(Convert.ToInt64(e));

            Balance = balance;
            IsLoaded = true;
            OnBalanceChanged?.Invoke();
            onLoaded?.Invoke();
        });
    }

    /// <summary>Echoが変わる操作(購入・対戦報酬など)を行った直後に呼ぶ。ローカルキャッシュを最新値に同期する</summary>
    public static void SetBalance(int newBalance)
    {
        Balance = newBalance;
        IsLoaded = true;
        OnBalanceChanged?.Invoke();
    }
}
