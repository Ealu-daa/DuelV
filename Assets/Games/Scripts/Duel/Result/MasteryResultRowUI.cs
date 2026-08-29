using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ResultSceneでキャラ1体分のマスタリーXP獲得結果を表示する行。Instantiateして使う(Prefab運用)。
///
/// 使い方: Initialize()で「試合前」の状態(名前・アイコン・Lv/バー/XP表記は試合前の値)を確定させ、
/// 後からPlayAnimation()を呼ぶとバー・現在Lv・XP表記が試合前→試合後の値まで一斉にアニメーションする。
///
/// XP表記は「100/300 +30」のような形式: 現在Lv区間内の進捗(100/300)はカウントアップ、
/// 末尾の残りXP(+30)は0に向かってカウントダウンする(全部吸収し終わると消える)。
/// </summary>
public class MasteryResultRowUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconHeight = 60f;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;      // アニメーション中、Lvの閾値を跨いだ瞬間に切り替わる。例:「Lv.3」
    [SerializeField] private TMP_Text xpProgressText; // 例:「100/300 +30」。前半カウントアップ、後半カウントダウン
    [SerializeField] private Image progressFill;      // Image Type: Filled推奨。Zukanの詳細画面と同じ均等5等分の進捗バー

    /// <summary>キャラ情報・試合前時点のLv/バー/XP表記を即座にセットする(アニメーション開始前の見た目確定用)</summary>
    public void Initialize(CharacterMasteryResult result)
    {
        var data = CharacterRegistry.Instance != null ? CharacterRegistry.Instance.GetData(result.characterId) : null;

        if (data != null)
        {
            if (nameText != null) nameText.text = data.characterName;
            if (iconImage != null && data.characterIconSprite != null)
            {
                iconImage.sprite = data.characterIconSprite;
                FitImageToHeightPreservingAspect(iconImage, iconHeight);
            }
        }

        // アニメーションが来るまでは「試合前」の状態で止めておく(残りXPは満額表示)
        ApplyXpState(result.oldXp, result.newXp);
    }

    /// <summary>バー・現在Lv・XP表記を、試合前→試合後の値まで同時にアニメーションさせる</summary>
    public IEnumerator PlayAnimation(CharacterMasteryResult result, float duration)
    {
        int fromXp = result.oldXp;
        int toXp = result.newXp;

        if (toXp <= fromXp) { ApplyXpState(toXp, toXp); yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int currentXp = Mathf.RoundToInt(Mathf.Lerp(fromXp, toXp, t));
            ApplyXpState(currentXp, toXp);
            yield return null;
        }

        ApplyXpState(toXp, toXp);
    }

    /// <summary>アニメーションを飛ばして、試合後の最終状態を即座に反映する(スキップ用)</summary>
    public void ApplyFinalState(CharacterMasteryResult result)
    {
        ApplyXpState(result.newXp, result.newXp);
    }

    // currentXp: 今アニメーションで指している累計XP(カウントアップ側の基準)
    // finalXp: 最終的に到達する累計XP(残りXP = finalXp - currentXp、カウントダウン側の基準)
    private void ApplyXpState(int currentXp, int finalXp)
    {
        int level = MasteryLevels.GetLevel(currentXp);
        bool isMax = level >= MasteryLevels.MaxLevel;
        int remaining = Mathf.Max(0, finalXp - currentXp);
        string remainingPart = remaining > 0 ? $" +{remaining}" : "";

        if (levelText != null) levelText.text = $"Lv.{level}";
        if (progressFill != null) progressFill.fillAmount = MasteryLevels.GetEqualSegmentFillAmount(currentXp);

        if (xpProgressText != null)
        {
            if (isMax)
            {
                xpProgressText.text = $"{currentXp} (MAX){remainingPart}";
            }
            else
            {
                MasteryLevels.GetProgressRange(currentXp, out int rangeStart, out int rangeEnd);
                xpProgressText.text = $"{currentXp - rangeStart}/{rangeEnd - rangeStart}{remainingPart}";
            }
        }
    }

    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
