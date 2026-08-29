using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面(MenuScene)隅のTips表示。起動時にランダムで1個表示し、タップするたびに次の1個へ切り替える。
/// 4カテゴリ(ルール豆知識/カタリスト・編成/キャラ・世界観/その他小ネタ)を完全均等ランダムで混在させる
/// (実際の抽選ロジックはTipRegistry.GetRandomTip側)。
/// </summary>
[RequireComponent(typeof(Button))]
public class HomeTipDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private string prefix = "Tip: ";

    private TipData currentTip;

    private void Start()
    {
        ShowNextTip();
        GetComponent<Button>().onClick.AddListener(ShowNextTip);
    }

    private void ShowNextTip()
    {
        currentTip = TipRegistry.Instance.GetRandomTip(currentTip);
        if (tipText != null) tipText.text = currentTip != null ? prefix + currentTip.text : "";
    }
}
