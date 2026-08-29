using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// お披露目演出のパーティ表示、1キャラ分のスロット。キャラアイコン+ロールアイコン+名前の3点セット。
/// Henseiの自分側/相手側/CPU側、どのパーティ表示からも同じコンポーネントを使い回す。
/// </summary>
public class PartyMemberDisplayUI : MonoBehaviour
{
    [Header("キャラアイコン")]
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconHeight = 100f;

    [Header("ロールアイコン")]
    [SerializeField] private Image roleImage;
    [SerializeField] private Sprite[] roleSprites; // Duelist/Guardian/Controller/Support の順(Role enum 1〜4に対応)

    [Header("名前")]
    [SerializeField] private TMP_Text nameText;

    [Header("空枠")]
    [SerializeField] private Sprite emptyIconSprite; // 未設定時のキャラアイコン。任意

    public void SetData(CharacterData data, Role role)
    {
        if (data == null)
        {
            Clear();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = data.characterSprite;
            FitImageToHeightPreservingAspect(iconImage, iconHeight);
        }

        if (roleImage != null)
        {
            Sprite sprite = GetRoleSprite(role);
            roleImage.gameObject.SetActive(sprite != null);
            if (sprite != null) roleImage.sprite = sprite;
        }

        if (nameText != null) nameText.text = data.characterName;
    }

    public void Clear()
    {
        if (iconImage != null) iconImage.sprite = emptyIconSprite;
        if (roleImage != null) roleImage.gameObject.SetActive(false);
        if (nameText != null) nameText.text = "";
    }

    private Sprite GetRoleSprite(Role role)
    {
        int index = (int)role - 1; // Duelist=1→0, Guardian=2→1, ...
        return (roleSprites != null && index >= 0 && index < roleSprites.Length) ? roleSprites[index] : null;
    }

    // Hensei.cs等と同じお馴染みの処理: アスペクト比を保ったまま高さだけ固定する
    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
