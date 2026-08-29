using UnityEngine;

/// <summary>
/// ミッション一覧の表示。ScrollView+GridLayoutGroup前提(Hensei/Zukanと同パターン)。
/// MenuScene等、好きな場所に配置する。有効になるたびに最新の達成状況を取り直して並べ直す。
/// </summary>
public class MissionListUI : MonoBehaviour
{
    [SerializeField] private Transform gridParent;
    [SerializeField] private MissionEntryUI entryPrefab;

    private void OnEnable()
    {
        MissionProgress.Load(Refresh);
    }

    public void Refresh()
    {
        if (gridParent == null || entryPrefab == null) return;

        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        if (MissionRegistry.Instance == null) return;

        foreach (var data in MissionRegistry.Instance.AllMissions)
        {
            var entry = Instantiate(entryPrefab, gridParent);
            entry.Setup(data);
        }
    }
}
