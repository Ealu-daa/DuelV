using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ProfileSceneの「フレームを選ぶ」グリッドの1マス分。Prefab化してInstantiateする(Hensei/Zukanと同パターン)。
/// </summary>
public class ProfileFramePickerEntryUI : MonoBehaviour
{
    [SerializeField] private Image frameImage;
    [SerializeField] private float frameHeight = 64f;

    private ProfileFrameData data;
    private Action<ProfileFrameData> onClicked;

    public void Setup(ProfileFrameData data, Action<ProfileFrameData> onClicked)
    {
        this.data = data;
        this.onClicked = onClicked;

        if (frameImage != null && data.frameSprite != null)
        {
            frameImage.sprite = data.frameSprite;
            FitImageToHeightPreservingAspect(frameImage, frameHeight);
        }
    }

    // このマスのButtonのOnClickから呼ぶ
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
