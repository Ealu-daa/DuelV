using System;
using System.Collections.Generic;

/// <summary>
/// プレイヤーが所持しているキャラクター/カタリストのID一覧を管理する。
/// 実体はFirestoreの users/{uid} ドキュメントの ownedCharacterIds / ownedCatalystIds 配列。
/// 図鑑・ショップなど、所持状態を必要とする画面はここを共通で参照する。
/// </summary>
public static class PlayerCollection
{
    /// <summary>Load()が一度でも完了していればtrue</summary>
    public static bool IsLoaded { get; private set; }

    private static readonly HashSet<int> ownedCharacterIds = new HashSet<int>();
    private static readonly HashSet<int> ownedCatalystIds = new HashSet<int>();

    /// <summary>Firestoreから所持状況を読み込む(画面表示前に呼ぶ)。取得できなければ「何も持っていない」扱いになる</summary>
    public static void Load(Action onLoaded = null)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            ownedCharacterIds.Clear();
            ownedCatalystIds.Clear();

            if (profile != null)
            {
                if (profile.TryGetValue("ownedCharacterIds", out var chars) && chars is List<object> charList)
                    foreach (var c in charList) ownedCharacterIds.Add(Convert.ToInt32(c));

                if (profile.TryGetValue("ownedCatalystIds", out var cats) && cats is List<object> catList)
                    foreach (var c in catList) ownedCatalystIds.Add(Convert.ToInt32(c));
            }

            IsLoaded = true;
            onLoaded?.Invoke();
        });
    }

    public static bool IsCharacterOwned(CharacterId id) => ownedCharacterIds.Contains((int)id);
    public static bool IsCatalystOwned(CatalystId id) => ownedCatalystIds.Contains((int)id);

    /// <summary>購入成立後に呼ぶ。ローカルキャッシュに反映してからFirestoreへ保存する</summary>
    public static void GrantCharacter(CharacterId id, Action<bool> onDone = null)
    {
        ownedCharacterIds.Add((int)id);
        SaveToFirestore(onDone);
    }

    /// <summary>購入成立後に呼ぶ。ローカルキャッシュに反映してからFirestoreへ保存する</summary>
    public static void GrantCatalyst(CatalystId id, Action<bool> onDone = null)
    {
        ownedCatalystIds.Add((int)id);
        SaveToFirestore(onDone);
    }

    private static void SaveToFirestore(Action<bool> onDone)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.SaveOwnedCollections(
            uid,
            new List<int>(ownedCharacterIds),
            new List<int>(ownedCatalystIds),
            onDone
        );
    }
}
