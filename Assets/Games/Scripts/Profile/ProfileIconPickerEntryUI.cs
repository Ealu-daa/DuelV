using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ProfileSceneの「アイコンを選ぶ」グリッドの1マス分。Prefab化してInstantiateする(Hensei/Zukanと同パターン)。
/// frameImageは「今装備中のフレーム」をプレビュー用に表示するだけの初期固定値(グリッド全体で共通、
/// クリックしても変わらない)。
/// </summary>
public class ProfileIconPickerEntryUI : MonoBehaviour
{
    [SerializeField] private Image frameImage; // 初期固定(プレビュー用、現在装備中のフレーム)
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconHeight = 64f;

    private ProfileIconData data;
    private Action<ProfileIconData> onClicked;

    public void Setup(ProfileIconData data, Sprite previewFrameSprite, Action<ProfileIconData> onClicked)
    {
        this.data = data;
        this.onClicked = onClicked;

        if (frameImage != null) frameImage.sprite = previewFrameSprite;

        if (iconImage != null && data.icon != null)
        {
            iconImage.sprite = data.icon;
            FitImageToHeightPreservingAspect(iconImage, iconHeight);
        }
    }

    // このマスのButtonのOnClickから呼ぶ。ここでは選択状態にするだけで、確定(装備)はしない
    public void OnClickEntry()
    {
        onClicked?.Invoke(data);
    }

    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
