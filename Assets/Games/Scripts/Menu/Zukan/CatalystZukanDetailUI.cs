using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カタリスト図鑑の詳細画面。アイコン・名前・対象ロール・効果説明・価格を表示する。
/// 未所持カタリストも全部表示(全公開方針)。未所持の場合のみ購入ボタンを出し、共通購入モーダルを開く。
/// </summary>
public class CatalystZukanDetailUI : MonoBehaviour
{
    [SerializeField] private ZukanUIController controller;
    [SerializeField] private PurchaseConfirmModalUI purchaseModal;

    [Header("常時表示")]
    [SerializeField] private Image catalystImage;
    [SerializeField] private float catalystImageHeight = 150f; // catalystImageを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text restrictedRoleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject purchaseButtonObj;
    [SerializeField] private TMP_Text priceText; // 購入ボタンと連動して表示/非表示
    [SerializeField] private GameObject lockOverlay; // アイコンに重ねる未所持表示。購入ボタンと連動(未接続でもよい)

    private CatalystData current;

    public void Show(CatalystData data)
    {
        current = data;

        catalystImage.sprite = data.icon;
        FitImageToHeightPreservingAspect(catalystImage, catalystImageHeight); // 横長スプライトを縮めず、縦基準で合わせて左右をクリップする
        nameText.text = data.catalystName;
        restrictedRoleText.text = GetRoleDisplayName(data.restrictedRole);
        descriptionText.text = data.description;

        bool notOwned = !PlayerCollection.IsCatalystOwned(data.id);
        purchaseButtonObj.SetActive(notOwned);
        if (priceText != null)
        {
            priceText.gameObject.SetActive(notOwned);
            priceText.text = $"{data.price} エコー";
        }
        if (lockOverlay != null) lockOverlay.SetActive(notOwned);
    }

    // 購入ボタンのOnClickから呼ぶ
    public void OnPurchaseClicked()
    {
        if (current == null) return;

        purchaseModal.Open(current.catalystName, current.icon, current.price, () =>
        {
            PlayerCollection.GrantCatalyst(current.id, ok =>
            {
                if (!ok) return;
                purchaseButtonObj.SetActive(false);
                if (priceText != null) priceText.gameObject.SetActive(false);
                if (lockOverlay != null) lockOverlay.SetActive(false);
                controller.RefreshCatalystList(); // 裏に隠れている一覧のロック表示もその場で更新する
            });
        });
    }

    // 「戻る」ボタン(共通)のOnClickから呼ぶ
    public void OnBackClicked()
    {
        controller.OnBackButtonClicked();
    }

    private string GetRoleDisplayName(Role role)
    {
        switch (role)
        {
            case Role.None: return "オール";
            case Role.Duelist: return "デュエリスト";
            case Role.Guardian: return "ガーディアン";
            case Role.Controller: return "コントローラー";
            case Role.Support: return "サポート";
            default: return role.ToString();
        }
    }

    // 横長スプライトを縮めるのではなく、枠の高さいっぱいに合わせて表示する
    // (幅は枠より大きくなるが、親のRectMask2Dで左右がクリップされる想定)
    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;

        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
