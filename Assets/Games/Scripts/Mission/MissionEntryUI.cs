using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミッション一覧の1行分。Prefab化してInstantiateする(Hensei/Zukanと同パターン)。
/// 報酬は自動付与なので、ここは状況を見せるだけ(受け取るボタンは無い)。
///
/// 現状は達成/未達成の2値判定しか無いので、進捗は「0/targetCount」または「達成済み」の2状態のみ表示する
/// (「3回勝利する」のような回数ミッションで実際にカウントアップするようになったら、currentCountの
/// 取得元をMissionProgress側に追加してここを差し替えること)。
/// </summary>
public class MissionEntryUI : MonoBehaviour
{
    [Header("進捗")]
    [SerializeField] private Image progressFill; // Image Type: Filled推奨
    [SerializeField] private TMP_Text progressText; // 例:「0/1」、達成済みなら「達成済み」に切り替わる
    [SerializeField] private string completedProgressLabel = "達成済み";

    [Header("ミッション内容")]
    [SerializeField] private TMP_Text missionText; // ミッションの内容(MissionData.title)

    [Header("報酬")]
    [SerializeField] private TMP_Text rewardText; // 例:「報酬: 600 エコー 称号「ああああ」」
    [SerializeField] private string rewardPrefix = "報酬: ";
    [SerializeField] private string rewardEchoFormat = "{0} エコー";
    [SerializeField] private string rewardTitleFormat = "称号「{0}」";

    public void Setup(MissionData data)
    {
        bool completed = MissionProgress.IsCompletedForCurrentPeriod(data);
        int current = completed ? data.targetCount : 0;

        if (progressFill != null)
            progressFill.fillAmount = data.targetCount > 0 ? (float)current / data.targetCount : (completed ? 1f : 0f);

        if (progressText != null)
            progressText.text = completed ? completedProgressLabel : $"{current}/{data.targetCount}";

        if (missionText != null)
            missionText.text = data.title;

        if (rewardText != null)
            rewardText.text = rewardPrefix + BuildRewardSummary(data);
    }

    private string BuildRewardSummary(MissionData data)
    {
        var parts = new List<string>();

        if (data.echoReward > 0)
            parts.Add(string.Format(rewardEchoFormat, data.echoReward));

        if (data.titleReward != TitleId.None)
        {
            var titleData = TitleRegistry.Instance != null ? TitleRegistry.Instance.GetData(data.titleReward) : null;
            string titleName = titleData != null ? titleData.titleName : data.titleReward.ToString();
            parts.Add(string.Format(rewardTitleFormat, titleName));
        }

        return string.Join(" ", parts);
    }
}
