using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 編成シーンの「キャラを選ぶ」グリッドの1マス分。Prefab化して実際のキャラ数ぶんだけInstantiateする
/// (新キャラが増えてもEditor側で枠を手で足す必要がない。ScrollView+GridLayoutGroup前提)。
/// </summary>
public class CharacterPickerEntryUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconHeight = 64f; // アイコンを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)

    private CharacterData data;
    private Action<CharacterData> onClicked;

    public void Setup(CharacterData data, Action<CharacterData> onClicked)
    {
        this.data = data;
        this.onClicked = onClicked;
        iconImage.sprite = data.characterIconSprite;
        FitImageToHeightPreservingAspect(iconImage, iconHeight);
    }

    // このマスのButtonのOnClickから呼ぶ
    public void OnClickEntry()
    {
        onClicked?.Invoke(data);
    }

    // 横長スプライトを縮めるのではなく、枠の高さいっぱいに合わせて表示する
    // (幅は枠より大きくなるが、親のRectMask2Dで左右がクリップされる想定。sizeDeltaは固定値からのみ計算し、
    // 読み書きの循環による膨張を避ける)
    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;

        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
