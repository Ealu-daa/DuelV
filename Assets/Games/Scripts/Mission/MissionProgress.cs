using System;
using System.Collections.Generic;

/// <summary>
/// ミッションの進捗(達成期間の記録)。PlayerProfile等と同じパターン。
/// 実体はFirestoreの users/{uid}.missionProgress(キー=MissionIdの文字列、値=最後に達成した期間キー)。
///
/// 期間キーの意味はカテゴリごとに変わる: Permanent="permanent"固定 / Daily=UTC日付 / Weekly=その週(月曜始まり)の日付。
/// 「今の期間キー」と保存されている値が一致していれば、今の期間はもう達成・付与済みという判定になる。
///
/// 報酬は自動付与(手動の受け取りボタンは無し)。CompleteAndGrant()を条件成立の瞬間に呼ぶ想定。
/// </summary>
public static class MissionProgress
{
    public static bool IsLoaded { get; private set; }

    private static readonly Dictionary<int, string> lastCompletedPeriodByMissionId = new Dictionary<int, string>();

    /// <summary>Firestoreから進捗を読み込む(画面表示・判定前に呼ぶ)。取得できなければ全部未達成扱いになる</summary>
    public static void Load(Action onLoaded = null)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            lastCompletedPeriodByMissionId.Clear();

            if (profile != null && profile.TryGetValue("missionProgress", out var m) && m is Dictionary<string, object> map)
            {
                foreach (var kv in map)
                {
                    if (int.TryParse(kv.Key, out int missionId) && kv.Value is string periodKey)
                        lastCompletedPeriodByMissionId[missionId] = periodKey;
                }
            }

            IsLoaded = true;
            onLoaded?.Invoke();
        });
    }

    public static string GetCurrentPeriodKey(MissionCategory category)
    {
        switch (category)
        {
            case MissionCategory.Permanent: return "permanent";
            case MissionCategory.Daily: return DateTime.UtcNow.ToString("yyyy-MM-dd");
            case MissionCategory.Weekly: return GetWeekStartKey(DateTime.UtcNow);
            default: return "";
        }
    }

    // 週の始まりを月曜日とする(UTC基準)。年またぎ等でもDateTime.AddDaysが正しく処理してくれる
    private static string GetWeekStartKey(DateTime utcNow)
    {
        int diff = (7 + (int)utcNow.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        DateTime monday = utcNow.Date.AddDays(-diff);
        return monday.ToString("yyyy-MM-dd");
    }

    /// <summary>このミッションが「今の期間、既に達成・付与済み」かどうか</summary>
    public static bool IsCompletedForCurrentPeriod(MissionData mission)
    {
        if (mission == null) return false;
        string currentKey = GetCurrentPeriodKey(mission.category);
        return lastCompletedPeriodByMissionId.TryGetValue((int)mission.id, out var stored) && stored == currentKey;
    }

    /// <summary>
    /// 条件が成立した瞬間に呼ぶ。今の期間でまだ達成していなければ、Echo+称号を自動付与して記録する。
    /// 既に達成済みなら何もしない(二重付与防止)。呼ぶ前にMissionProgress.Load()とPlayerProfile.Load()を
    /// 済ませておくこと(称号報酬がある場合、PlayerProfileが未ロードだと付与に失敗する)。
    /// </summary>
    public static void CompleteAndGrant(MissionData mission, Action<bool> onDone = null)
    {
        if (mission == null) { onDone?.Invoke(false); return; }

        if (!IsLoaded)
        {
            UnityEngine.Debug.LogWarning("[MissionProgress] 未ロードのため付与をスキップしました。呼び出し元でLoad()を先に済ませること。");
            onDone?.Invoke(false);
            return;
        }

        if (IsCompletedForCurrentPeriod(mission)) { onDone?.Invoke(false); return; }

        string uid = LocalUser.GetOrCreateUid();
        lastCompletedPeriodByMissionId[(int)mission.id] = GetCurrentPeriodKey(mission.category);

        if (mission.echoReward > 0)
        {
            FirestoreBridge.Instance.GetUserProfile(uid, profile =>
            {
                long currentEcho = 0;
                if (profile != null && profile.TryGetValue("echo", out var e)) currentEcho = Convert.ToInt64(e);

                long newTotal = currentEcho + mission.echoReward;
                FirestoreBridge.Instance.SaveEchoResult(uid, (int)newTotal, null, ok =>
                {
                    if (ok) EchoWallet.SetBalance((int)newTotal);
                    GrantTitleIfAny(mission);
                    SaveProgress(uid, onDone);
                });
            });
        }
        else
        {
            GrantTitleIfAny(mission);
            SaveProgress(uid, onDone);
        }
    }

    private static void GrantTitleIfAny(MissionData mission)
    {
        if (mission.titleReward != TitleId.None)
            PlayerProfile.GrantTitle(mission.titleReward);
    }

    private static void SaveProgress(string uid, Action<bool> onDone)
    {
        var mapPayload = new Dictionary<string, object>();
        foreach (var kv in lastCompletedPeriodByMissionId)
            mapPayload[kv.Key.ToString()] = kv.Value;

        FirestoreBridge.Instance.SaveProfileFields(
            uid,
            new Dictionary<string, object> { { "missionProgress", mapPayload } },
            onDone
        );
    }
}
