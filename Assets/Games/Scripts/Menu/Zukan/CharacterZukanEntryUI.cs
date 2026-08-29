using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラ図鑑の一覧グリッドに並ぶ1マス分。アイコン・名前・未所持ロック表示・クリックで詳細を開く。
/// Prefabとして用意し、CharacterZukanListUIが実数分だけInstantiateする(新キャラが増えても枠を手で足す必要がない)。
/// 未所持キャラも全公開方針なので、ロック表示は「持ってない目印」であって非表示・グレーアウトはしない。
/// </summary>
public class CharacterZukanEntryUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconHeight = 64f; // iconImageを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject lockOverlay; // 未所持時に表示する鍵アイコン等(未所持の目印)

    private CharacterData data;
    private Action<CharacterData> onClicked;

    public void Setup(CharacterData data, Action<CharacterData> onClicked)
    {
        this.data = data;
        this.onClicked = onClicked;

        iconImage.sprite = data.characterIconSprite;
        FitImageToHeightPreservingAspect(iconImage, iconHeight); // 横長スプライトを縮めず、縦基準で合わせて左右をクリップする
        if (nameText != null) nameText.text = data.characterName; // Henseiのアイコンを流用する場合など、名前欄が無くてもよい

        if (lockOverlay != null)
            lockOverlay.SetActive(!PlayerCollection.IsCharacterOwned(data.id));
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
