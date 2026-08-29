using System;
using System.Collections.Generic;

/// <summary>
/// プレイヤーのプロフィール(表示名・アイコン/フレーム・称号)を管理する。PlayerCollectionと同じパターン。
/// 実体はFirestoreの users/{uid} ドキュメントの各フィールド。
///
/// 称号はメイン1枠(常時使用可)+サブ4枠(Echoで順番に解放: 100/300/900/2700)。
/// サブ枠は「特定の称号を買う」のではなく「空き枠そのもの」を解放する仕組みで、
/// 解放済みの枠には所持している称号(ownedTitleIds)を自由に付け替えられる。
///
/// アイコン/フレーム/称号の中身(実際の見た目・入手経路)はまだ無いので、
/// ここでは箱(データモデル+保存/読込)だけ用意してある。表示側はProfileIconRegistry等が
/// 空でも問題ないように、GetEquippedXxx()系はnull/空文字を返すだけで済むようにしてある。
/// </summary>
public static class PlayerProfile
{
    public static readonly int[] SubTitleSlotPrices = { 100, 300, 900, 2700 };
    private const int SubTitleSlotCount = 4;

    // 新規プレイヤー(Firestoreにまだ何も保存されていない)の初期装備。Firestore側に既に値がある場合はそちらが優先される
    private const ProfileIconId DefaultIconId = ProfileIconId.DuelV;
    private const ProfileFrameId DefaultFrameId = ProfileFrameId.NormalFrame;
    private const TitleId DefaultMainTitleId = TitleId.Hajimemashite;

    public static bool IsLoaded { get; private set; }

    public static string DisplayName { get; private set; } = "";
    public static ProfileIconId EquippedIconId { get; private set; } = ProfileIconId.None;
    public static ProfileFrameId EquippedFrameId { get; private set; } = ProfileFrameId.None;
    public static TitleId EquippedMainTitleId { get; private set; } = TitleId.None;
    public static TitleId[] EquippedSubTitleIds { get; private set; } = new TitleId[SubTitleSlotCount];
    public static int UnlockedSubTitleSlotCount { get; private set; } = 0;

    private static readonly HashSet<int> ownedTitleIds = new HashSet<int>();

    /// <summary>Firestoreからプロフィールを読み込む(画面表示前に呼ぶ)。取得できなければ全部デフォルト扱いになる</summary>
    public static void Load(Action onLoaded = null)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            DisplayName = "";
            EquippedIconId = DefaultIconId;
            EquippedFrameId = DefaultFrameId;
            EquippedMainTitleId = DefaultMainTitleId;
            EquippedSubTitleIds = new TitleId[SubTitleSlotCount];
            UnlockedSubTitleSlotCount = 0;
            ownedTitleIds.Clear();
            ownedTitleIds.Add((int)DefaultMainTitleId); // 初期称号は誰でも最初から所持している扱い

            if (profile != null)
            {
                if (profile.TryGetValue("displayName", out var n) && n is string nameStr)
                    DisplayName = nameStr;

                if (profile.TryGetValue("equippedIconId", out var iconObj))
                    EquippedIconId = (ProfileIconId)Convert.ToInt32(iconObj);

                if (profile.TryGetValue("equippedFrameId", out var frameObj))
                    EquippedFrameId = (ProfileFrameId)Convert.ToInt32(frameObj);

                if (profile.TryGetValue("equippedMainTitleId", out var mainTitleObj))
                    EquippedMainTitleId = (TitleId)Convert.ToInt32(mainTitleObj);

                if (profile.TryGetValue("equippedSubTitleIds", out var subTitlesObj) && subTitlesObj is List<object> subTitleList)
                {
                    for (int i = 0; i < SubTitleSlotCount && i < subTitleList.Count; i++)
                        EquippedSubTitleIds[i] = (TitleId)Convert.ToInt32(subTitleList[i]);
                }

                if (profile.TryGetValue("unlockedSubTitleSlotCount", out var slotCountObj))
                    UnlockedSubTitleSlotCount = Convert.ToInt32(slotCountObj);

                if (profile.TryGetValue("ownedTitleIds", out var ownedObj) && ownedObj is List<object> ownedList)
                    foreach (var t in ownedList) ownedTitleIds.Add(Convert.ToInt32(t));
            }

            IsLoaded = true;
            onLoaded?.Invoke();
        });
    }

    /// <summary>表示用の名前。未設定なら仮の名前を返す(ProfileSceneで設定するまでの暫定表示)</summary>
    public static string GetDisplayNameOrDefault()
    {
        return string.IsNullOrEmpty(DisplayName) ? "プレイヤー" : DisplayName;
    }

    public static bool IsTitleOwned(TitleId id) => ownedTitleIds.Contains((int)id);

    public static bool IsSubTitleSlotUnlocked(int slotIndex) => slotIndex >= 0 && slotIndex < UnlockedSubTitleSlotCount;

    /// <summary>次に解放できるサブ称号枠の価格(Echo)。もう全部解放済みなら-1</summary>
    public static int GetNextSubTitleSlotPrice()
    {
        return UnlockedSubTitleSlotCount < SubTitleSlotPrices.Length
            ? SubTitleSlotPrices[UnlockedSubTitleSlotCount]
            : -1;
    }

    /// <summary>表示名を変更してFirestoreへ保存する(ProfileSceneから呼ぶ想定)</summary>
    public static void SetDisplayName(string newName, Action<bool> onDone = null)
    {
        DisplayName = newName ?? "";
        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object> { { "displayName", DisplayName } },
            onDone
        );
    }

    /// <summary>アイコン/フレームを変更してFirestoreへ保存する(ProfileSceneから呼ぶ想定)</summary>
    public static void EquipCosmetics(ProfileIconId iconId, ProfileFrameId frameId, Action<bool> onDone = null)
    {
        EquippedIconId = iconId;
        EquippedFrameId = frameId;
        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object>
            {
                { "equippedIconId", (int)iconId },
                { "equippedFrameId", (int)frameId }
            },
            onDone
        );
    }

    /// <summary>メイン称号を変更する。所持していない称号は無視する</summary>
    public static void EquipMainTitle(TitleId id, Action<bool> onDone = null)
    {
        if (id != TitleId.None && !IsTitleOwned(id)) { onDone?.Invoke(false); return; }

        EquippedMainTitleId = id;
        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object> { { "equippedMainTitleId", (int)id } },
            onDone
        );
    }

    /// <summary>サブ称号枠(0〜3)に称号を設定する。枠が未解放/称号を未所持なら失敗する</summary>
    public static void EquipSubTitle(int slotIndex, TitleId id, Action<bool> onDone = null)
    {
        if (!IsSubTitleSlotUnlocked(slotIndex)) { onDone?.Invoke(false); return; }
        if (id != TitleId.None && !IsTitleOwned(id)) { onDone?.Invoke(false); return; }

        EquippedSubTitleIds[slotIndex] = id;
        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object>
            {
                { "equippedSubTitleIds", Array.ConvertAll(EquippedSubTitleIds, t => (object)(int)t) }
            },
            onDone
        );
    }

    /// <summary>
    /// 次のサブ称号枠を「解放済み」にする。Echoの残高チェック・減算は行わない
    /// (Zukanの購入確認モーダルと同じ役割分担: 支払いはPurchaseConfirmModalUI側の責任で、
    /// これはその支払いが成立した後onConfirmedから呼ぶ「付与」だけを担当する)。
    /// </summary>
    public static void GrantNextSubTitleSlot(Action<bool> onDone = null)
    {
        int newSlotCount = UnlockedSubTitleSlotCount + 1;
        if (newSlotCount > SubTitleSlotPrices.Length) { onDone?.Invoke(false); return; }

        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object> { { "unlockedSubTitleSlotCount", newSlotCount } },
            ok =>
            {
                if (ok) UnlockedSubTitleSlotCount = newSlotCount;
                onDone?.Invoke(ok);
            }
        );
    }

    /// <summary>称号を入手した時に呼ぶ(マスタリー報酬・ミッション報酬・ショップ購入等から)。ローカルキャッシュに反映してからFirestoreへ保存する</summary>
    public static void GrantTitle(TitleId id, Action<bool> onDone = null)
    {
        if (!IsLoaded)
        {
            // 未ロードのまま保存すると、ローカルキャッシュが空/不完全な状態でownedTitleIds全体を
            // 上書きしてしまい、他に所持していた称号を消してしまう。安全のため何もせず終える
            UnityEngine.Debug.LogWarning("[PlayerProfile] 未ロードのため称号付与をスキップしました。呼び出し元でPlayerProfile.Load()を先に済ませること。");
            onDone?.Invoke(false);
            return;
        }

        ownedTitleIds.Add((int)id);
        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object> { { "ownedTitleIds", new List<int>(ownedTitleIds).ConvertAll(x => (object)x) } },
            onDone
        );
    }
}
