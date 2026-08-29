using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラ図鑑の詳細画面。常時表示(ポートレート・名前・出身国・ステータス)+
/// 「キャラ/スキル/ウルト」3タブで名前・説明文だけ入れ替わる方式(編成シーンと同じ)。
/// 未所持キャラも全部表示(全公開方針)。未所持の場合のみ購入ボタンを出し、共通購入モーダルを開く。
/// </summary>
public class CharacterZukanDetailUI : MonoBehaviour
{
    [SerializeField] private ZukanUIController controller;
    [SerializeField] private PurchaseConfirmModalUI purchaseModal;

    [Header("常時表示")]
    [SerializeField] private Image characterImage;
    [SerializeField] private float characterImageHeight = 300f; // characterImageを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text originText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text defenseText;
    [SerializeField] private TMP_Text ultGaugeText;
    [SerializeField] private GameObject purchaseButtonObj;
    [SerializeField] private TMP_Text priceText; // 購入ボタンと連動して表示/非表示(未所持の間だけ常時表示)
    [SerializeField] private GameObject lockOverlay; // ポートレートに重ねる未所持表示。購入ボタンと連動(未接続でもよい)

    [Header("タブで中身が入れ替わる名前/説明(キャラ/スキル/ウルト)")]
    [SerializeField] private TMP_Text subNameText;    // キャラ:空 / スキル:スキル名 / ウルト:ウルト名
    [SerializeField] private TMP_Text descriptionText; // キャラ:説明文 / スキル:スキル説明 / ウルト:ウルト説明

    [Header("マスタリー進捗(未所持キャラは0XP扱いでそのまま表示)")]
    [SerializeField] private TMP_Text masteryLevelText;  // 例:「Lv.3」
    [SerializeField] private TMP_Text masteryXpText;     // 例:「150 / 200」、MAXなら「1000 (MAX)」
    [SerializeField] private Image masteryProgressFill;  // Image Type: Filled推奨。fillAmountで進捗を表現
    [SerializeField] private TMP_Text nextRewardText;    // 次に貰える報酬だけを「Next → ○○」で表示。MAXなら別文言
    [SerializeField] private string nextRewardFormat = "Next → {0}";
    [SerializeField] private string nextRewardMaxText = "MAX";

    // Lv1〜5で貰える報酬の"種類"(中身はまだ無いので種類名だけ)。DuelVのマスタリー仕様書と対応させること
    private static readonly string[] MasteryRewardTypeLabels = { "称号", "アイコン", "称号", "アイコン", "スプライットバリアント" };

    private CharacterData current;

    public void Show(CharacterData data)
    {
        current = data;

        characterImage.sprite = data.characterSprite;
        FitImageToHeightPreservingAspect(characterImage, characterImageHeight); // 横長スプライトを縮めず、縦基準で合わせて左右をクリップする
        nameText.text = data.characterName;
        if (originText != null) originText.text = data.origin; // 出身国欄は未接続でもよい
        hpText.text = $"{data.maxHP}";
        attackText.text = $"{data.attack}";
        defenseText.text = $"{data.defense}";
        ultGaugeText.text = $"{data.maxUltGauge}";

        bool notOwned = !PlayerCollection.IsCharacterOwned(data.id);
        purchaseButtonObj.SetActive(notOwned);
        if (priceText != null)
        {
            priceText.gameObject.SetActive(notOwned);
            priceText.text = $"{data.price} エコー";
        }
        if (lockOverlay != null) lockOverlay.SetActive(notOwned);

        RefreshMastery(data.id);

        OnClickCharacterInfoTab(); // 開いたら/選び直したら「キャラ」タブへ戻す
    }

    private void RefreshMastery(CharacterId id)
    {
        int xp = CharacterMastery.GetXp(id);
        int level = MasteryLevels.GetLevel(xp);
        bool isMax = level >= MasteryLevels.MaxLevel;

        // 1本のバーの中に、Lv1〜5を「実際のXP間隔に関わらず均等な5等分」で並べる。
        // (10/40/150/200/600という間隔の差はバーの見た目には反映しない。各Lvは常にバーの1/5を占める)
        MasteryLevels.GetProgressRange(xp, out int rangeStart, out int rangeEnd);
        int rangeSize = rangeEnd - rangeStart;

        if (masteryLevelText != null) masteryLevelText.text = $"Lv.{level}";

        if (masteryXpText != null)
        {
            masteryXpText.text = isMax ? $"{xp} (MAX)" : $"{xp - rangeStart} / {rangeSize}";
        }

        if (masteryProgressFill != null)
        {
            masteryProgressFill.fillAmount = MasteryLevels.GetEqualSegmentFillAmount(xp);
        }

        if (nextRewardText != null)
        {
            // levelは「達成済みのLv数」なので、次に貰える報酬はそのままMasteryRewardTypeLabels[level](0始まり)
            if (isMax || level >= MasteryRewardTypeLabels.Length)
            {
                nextRewardText.text = nextRewardMaxText;
            }
            else
            {
                nextRewardText.text = string.Format(nextRewardFormat, MasteryRewardTypeLabels[level]);
            }
        }
    }

    // 「キャラ」タブボタンのOnClickから呼ぶ: 名前は空、説明はキャラの説明文
    public void OnClickCharacterInfoTab()
    {
        if (current == null) return;

        subNameText.text = "";
        descriptionText.text = current.description;
    }

    // 「スキル」タブボタンのOnClickから呼ぶ: 名前はスキル名、説明はスキル説明
    public void OnClickSkillTab()
    {
        if (current == null) return;

        subNameText.text = current.skillName;
        descriptionText.text = current.skillDescription;
    }

    // 「ウルト」タブボタンのOnClickから呼ぶ: 名前はウルト名、説明はウルト説明
    public void OnClickUltTab()
    {
        if (current == null) return;

        subNameText.text = current.ultName;
        descriptionText.text = current.ultDescription;
    }

    // 購入ボタンのOnClickから呼ぶ
    public void OnPurchaseClicked()
    {
        if (current == null) return;

        purchaseModal.Open(current.characterName, current.characterIconSprite, current.price, () =>
        {
            PlayerCollection.GrantCharacter(current.id, ok =>
            {
                if (!ok) return;
                purchaseButtonObj.SetActive(false);
                if (priceText != null) priceText.gameObject.SetActive(false);
                if (lockOverlay != null) lockOverlay.SetActive(false);
                controller.RefreshCharacterList(); // 裏に隠れている一覧のロック表示もその場で更新する
            });
        });
    }

    // 「戻る」ボタン(共通)のOnClickから呼ぶ
    public void OnBackClicked()
    {
        controller.OnBackButtonClicked();
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
