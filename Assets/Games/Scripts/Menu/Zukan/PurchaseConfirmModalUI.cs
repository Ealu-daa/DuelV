using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 図鑑・ショップ共通の購入確認モーダル。名前・アイコン・価格を表示し、
/// 確認を押すとエコー残高をチェックしてから減算し、成功時だけonConfirmedを呼ぶ(所持付与などは呼び出し側の責任)。
/// 残高不足時は購入を弾いてメッセージを出す。
/// </summary>
public class PurchaseConfirmModalUI : MonoBehaviour
{
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private float itemIconHeight = 100f; // itemIconImageを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text errorText; // 残高不足・保存失敗時のメッセージ。未接続でもよい
    [SerializeField] private Button confirmButton;

    [Header("モーダル表示中、操作不可にする背後のUI(一覧/詳細/戻るボタンなど。表示はそのまま、操作だけ止める)")]
    [SerializeField] private CanvasGroup[] backgroundBlockers;

    private int pendingPrice;
    private Action onConfirmed;

    private void Awake()
    {
        modalPanel.SetActive(false);
    }

    /// <summary>モーダルを開く。onConfirmedは支払い成立後に1回だけ呼ばれる(所持付与はここで行う)</summary>
    public void Open(string itemName, Sprite icon, int price, Action onConfirmed)
    {
        pendingPrice = price;
        this.onConfirmed = onConfirmed;

        if (itemIconImage != null)
        {
            itemIconImage.sprite = icon;
            FitImageToHeightPreservingAspect(itemIconImage, itemIconHeight); // 横長スプライトを縮めず、縦基準で合わせて左右をクリップする
        }
        itemNameText.text = itemName;
        priceText.text = $"{price} エコー";
        if (errorText != null) errorText.text = "";

        modalPanel.SetActive(true);
        SetBackgroundInteractable(false);
    }

    // 確認ボタンのOnClickから呼ぶ
    public void OnConfirmClicked()
    {
        string uid = LocalUser.GetOrCreateUid();
        confirmButton.interactable = false;

        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            confirmButton.interactable = true;

            long currentEcho = 0;
            if (profile != null && profile.TryGetValue("echo", out var e)) currentEcho = Convert.ToInt64(e);

            if (currentEcho < pendingPrice)
            {
                if (errorText != null) errorText.text = "エコーが足りません";
                return;
            }

            long newTotal = currentEcho - pendingPrice;
            FirestoreBridge.Instance.SaveEchoResult(uid, (int)newTotal, null, ok =>
            {
                if (ok)
                {
                    SeManager.PlayPurchase();
                    EchoWallet.SetBalance((int)newTotal); // 他画面のEcho表示にも即座に反映されるようキャッシュを同期
                    onConfirmed?.Invoke();
                    Close();
                }
                else if (errorText != null)
                {
                    errorText.text = "購入に失敗しました。もう一度お試しください";
                }
            });
        });
    }

    // キャンセルボタンのOnClickから呼ぶ
    public void OnCancelClicked()
    {
        Close();
    }

    public void Close()
    {
        modalPanel.SetActive(false);
        SetBackgroundInteractable(true);
    }

    private void SetBackgroundInteractable(bool interactable)
    {
        foreach (var group in backgroundBlockers)
        {
            if (group == null) continue;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }
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
