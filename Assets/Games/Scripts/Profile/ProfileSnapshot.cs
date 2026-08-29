using System;

/// <summary>
/// 任意uidのプロフィール(表示名+アイコン/フレーム+メイン称号)を1回だけ読み取るための軽量スナップショット。
/// PlayerProfile(静的・自分専用のキャッシュ)とは別物で、Hensei準備完了後の「相手プロフィール一斉公開」など、
/// 自分以外のuidのプロフィールを一時的に覗きたい場面で使う(自分のPlayerProfileキャッシュを上書きしないため)。
/// </summary>
public class ProfileSnapshot
{
    public string DisplayName = "";
    public ProfileIconId IconId = ProfileIconId.None;
    public ProfileFrameId FrameId = ProfileFrameId.None;
    public TitleId MainTitleId = TitleId.None;

    public static void LoadForUid(string uid, Action<ProfileSnapshot> onLoaded)
    {
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            var snapshot = new ProfileSnapshot();

            if (profile != null)
            {
                if (profile.TryGetValue("displayName", out var n) && n is string nameStr)
                    snapshot.DisplayName = nameStr;

                if (profile.TryGetValue("equippedIconId", out var iconObj))
                    snapshot.IconId = (ProfileIconId)Convert.ToInt32(iconObj);

                if (profile.TryGetValue("equippedFrameId", out var frameObj))
                    snapshot.FrameId = (ProfileFrameId)Convert.ToInt32(frameObj);

                if (profile.TryGetValue("equippedMainTitleId", out var mainTitleObj))
                    snapshot.MainTitleId = (TitleId)Convert.ToInt32(mainTitleObj);
            }

            onLoaded?.Invoke(snapshot);
        });
    }
}
