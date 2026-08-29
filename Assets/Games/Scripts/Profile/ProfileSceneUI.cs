using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// プロフィール編集画面。MenuSceneから遷移してくる。
/// 名前編集はその場で完結、アイコン/フレーム/称号はメイン画面から専用ピッカー画面を開いて選ぶ方式
/// (Hensei/Zukanのピッカーと同じPrefab+Instantiate運用)。
///
/// アイコン/フレームには現時点で所持制限が無い(登録されている全件を表示)。称号は所持しているものだけ選べる。
/// サブ称号枠はEchoで順番にしか解放できないため、「次に解放できる枠」のボタンだけが解放操作として機能する。
/// </summary>
public class ProfileSceneUI : MonoBehaviour
{
    private enum ProfileScreen { Main, IconPicker, FramePicker, TitlePicker }
    private ProfileScreen currentScreen = ProfileScreen.Main;

    [Header("サブ称号枠の購入(Zukanと同じPurchaseConfirmModalUIを使い回す)")]
    [SerializeField] private PurchaseConfirmModalUI purchaseModal;
    [SerializeField] private Sprite subTitleSlotPurchaseIcon; // モーダルに出すアイコン。無くてもよい
    [SerializeField] private string subTitleSlotItemNameFormat = "サブ称号枠{0}";

    [Header("メイン画面")]
    [SerializeField] private GameObject mainPanel;

    [Header("名前")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text nameSavedFeedbackText; // 任意。保存結果のフィードバック表示

    [Header("アイコン")]
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject iconImageObj; // 未設定(None)の間は非表示にしたい場合にセット
    [SerializeField] private float iconHeight = 100f;
    [SerializeField] private TMP_Text iconNameText; // 任意。ProfileIconData.displayNameを表示
    [SerializeField] private string emptyIconNameLabel = "未設定";

    [Header("フレーム")]
    [SerializeField] private Image frameImage;
    [SerializeField] private GameObject frameImageObj; // 未設定(None)の間は非表示にしたい場合にセット
    [SerializeField] private TMP_Text frameNameText; // 任意。ProfileFrameData.displayNameを表示
    [SerializeField] private string emptyFrameNameLabel = "未設定";

    [Header("アカウントレベル")]
    [SerializeField] private TMP_Text accountLevelText;         // 例:「Lv.5」
    [SerializeField] private TMP_Text accountXpProgressText;    // 例:「40/100」
    [SerializeField] private Image accountProgressFill;         // Image Type: Filled推奨

    [Header("称号: 共通見た目(購入済み/未購入でボタンの画像・文字色・文言を切り替える)")]
    [SerializeField] private Sprite unlockedTitleButtonSprite; // 購入済み(称号用)のボタン画像
    [SerializeField] private Sprite lockedTitleButtonSprite;   // 未購入(購入用)のボタン画像
    [SerializeField] private Color unlockedButtonTextColor = Color.black;
    [SerializeField] private Color lockedButtonTextColor = Color.red;
    [SerializeField] private string unlockedButtonLabel = "変更";
    [SerializeField] private string lockedButtonLabel = "購入";
    [SerializeField] private string emptySubTitleLabel = "未設定";
    [SerializeField] private string emptyMainTitleLabel = "未設定";

    [Header("称号: メイン枠(常に購入済み扱い、lockOverlayは無い)")]
    [SerializeField] private Image mainTitleButtonImage;
    [SerializeField] private TMP_Text mainTitleButtonLabelText; // ボタン内の「変更」テキスト
    [SerializeField] private TMP_Text mainTitleText;            // 称号名を表示

    [Header("称号: サブ枠(4個、0〜3)")]
    [SerializeField] private Image[] subTitleButtonImages;
    [SerializeField] private TMP_Text[] subTitleButtonLabelTexts; // ボタン内の「変更」/「購入」テキスト
    [SerializeField] private TMP_Text[] subTitleTexts;             // 称号名 or 価格を表示
    [SerializeField] private GameObject[] subTitleLockOverlays;    // 未購入の枠にだけ表示
    [SerializeField] private Button[] subTitleButtons;             // 解放済み=変更、次に解放できる枠=購入、それ以外=非activeにする

    [Header("アイコン選択画面(選択→決定の2段階。クリックでは確定しない)")]
    [SerializeField] private GameObject iconPickerPanel;
    [SerializeField] private Transform iconPickerGridParent;
    [SerializeField] private ProfileIconPickerEntryUI iconPickerEntryPrefab;
    [SerializeField] private TMP_Text selectedIconNameText;

    [Header("フレーム選択画面(選択→決定の2段階。クリックでは確定しない)")]
    [SerializeField] private GameObject framePickerPanel;
    [SerializeField] private Transform framePickerGridParent;
    [SerializeField] private ProfileFramePickerEntryUI framePickerEntryPrefab;
    [SerializeField] private TMP_Text selectedFrameNameText;

    [Header("称号選択画面(選択→決定の2段階。クリックでは確定しない)")]
    [SerializeField] private GameObject titlePickerPanel;
    [SerializeField] private Transform titlePickerGridParent;
    [SerializeField] private TitlePickerEntryUI titlePickerEntryPrefab;
    [SerializeField] private TMP_Text selectedTitleDescriptionText; // 任意。選択中の称号のTitleData.descriptionを表示

    private int pendingSubTitleSlotIndex = -1; // 称号ピッカーがどの枠向けに開かれたか(-1ならメイン枠)
    private ProfileIconData pendingSelectedIcon;
    private ProfileFrameData pendingSelectedFrame;
    private TitleData pendingSelectedTitle;

    private void Start()
    {
        PlayerProfile.Load(RefreshMain);
        AccountLevel.Load(RefreshMain); // どちらが先に終わってもRefreshMainは現在値を読み直すだけなので問題ない
        ShowMain();
    }

    // ---------------- 画面切り替え ----------------

    public void ShowMain()
    {
        currentScreen = ProfileScreen.Main;
        SetPanelsActive(main: true);
        RefreshMain();
    }

    public void OnChangeIconClicked()
    {
        currentScreen = ProfileScreen.IconPicker;
        SetPanelsActive(iconPicker: true);

        // 開いた時点では「今装備中のアイコン」を選択中として扱う
        pendingSelectedIcon = ProfileIconRegistry.Instance != null ? ProfileIconRegistry.Instance.GetData(PlayerProfile.EquippedIconId) : null;
        RefreshSelectedIconName();
        PopulateIconPicker();
    }

    public void OnChangeFrameClicked()
    {
        currentScreen = ProfileScreen.FramePicker;
        SetPanelsActive(framePicker: true);

        // 開いた時点では「今装備中のフレーム」を選択中として扱う
        pendingSelectedFrame = ProfileFrameRegistry.Instance != null ? ProfileFrameRegistry.Instance.GetData(PlayerProfile.EquippedFrameId) : null;
        RefreshSelectedFrameName();
        PopulateFramePicker();
    }

    public void OnChangeMainTitleClicked()
    {
        pendingSubTitleSlotIndex = -1;
        OpenTitlePicker();
    }

    // サブ称号枠ボタン(0〜3)のOnClickから呼ぶ。それぞれ引数だけ変えて接続する
    public void OnChangeSubTitleClicked(int slotIndex)
    {
        if (PlayerProfile.IsSubTitleSlotUnlocked(slotIndex))
        {
            pendingSubTitleSlotIndex = slotIndex;
            OpenTitlePicker();
        }
        else if (slotIndex == PlayerProfile.UnlockedSubTitleSlotCount)
        {
            // ちょうど次に解放できる枠なら、選択ではなくZukanと同じ購入確認モーダルを開く
            if (purchaseModal == null) return;

            int price = PlayerProfile.SubTitleSlotPrices[slotIndex];
            string itemName = string.Format(subTitleSlotItemNameFormat, slotIndex + 1);

            purchaseModal.Open(itemName, subTitleSlotPurchaseIcon, price, () =>
            {
                // 支払いはモーダル側で成立済みなので、ここでは枠の解放だけ行う
                PlayerProfile.GrantNextSubTitleSlot(_ => RefreshMain());
            });
        }
        // それ以外(まだ順番が来ていない枠)はボタン自体がinteractable=falseになっているはずなので何もしない
    }

    private void OpenTitlePicker()
    {
        currentScreen = ProfileScreen.TitlePicker;
        SetPanelsActive(titlePicker: true);

        // 開いた時点では「今その枠に装備中の称号」を選択中として扱う
        TitleId currentId = pendingSubTitleSlotIndex < 0
            ? PlayerProfile.EquippedMainTitleId
            : PlayerProfile.EquippedSubTitleIds[pendingSubTitleSlotIndex];
        pendingSelectedTitle = TitleRegistry.Instance != null ? TitleRegistry.Instance.GetData(currentId) : null;
        RefreshSelectedTitleDescription();

        PopulateTitlePicker();
    }

    // 各ピッカー画面の「戻る」ボタン(共通)のOnClickから呼ぶ
    public void OnBackFromPickerClicked()
    {
        ShowMain();
    }

    // ProfileScene自体の「戻る」ボタンのOnClickから呼ぶ
    public void OnBackToMenuClicked()
    {
        SceneManager.LoadScene("MenuScene");
    }

    private void SetPanelsActive(bool main = false, bool iconPicker = false, bool framePicker = false, bool titlePicker = false)
    {
        if (mainPanel != null) mainPanel.SetActive(main);
        if (iconPickerPanel != null) iconPickerPanel.SetActive(iconPicker);
        if (framePickerPanel != null) framePickerPanel.SetActive(framePicker);
        if (titlePickerPanel != null) titlePickerPanel.SetActive(titlePicker);
    }

    // ---------------- メイン画面の表示更新 ----------------

    private void RefreshMain()
    {
        if (nameInputField != null) nameInputField.text = PlayerProfile.GetDisplayNameOrDefault();
        if (nameSavedFeedbackText != null) nameSavedFeedbackText.text = "";

        RefreshIcon();
        RefreshFrame();
        RefreshTitles();
        RefreshAccountLevel();
    }

    private void RefreshAccountLevel()
    {
        int xpIntoLevel = AccountLevel.XpIntoCurrentLevel;

        if (accountLevelText != null) accountLevelText.text = $"Lv.{AccountLevel.Level}";
        if (accountXpProgressText != null) accountXpProgressText.text = $"{xpIntoLevel}/{AccountLevel.XpPerLevel}";
        if (accountProgressFill != null) accountProgressFill.fillAmount = (float)xpIntoLevel / AccountLevel.XpPerLevel;
    }

    private void RefreshIcon()
    {
        var data = ProfileIconRegistry.Instance != null ? ProfileIconRegistry.Instance.GetData(PlayerProfile.EquippedIconId) : null;

        if (iconNameText != null) iconNameText.text = data != null ? data.displayName : emptyIconNameLabel;

        if (iconImageObj != null) iconImageObj.SetActive(data != null);
        if (data == null || iconImage == null) return;

        iconImage.sprite = data.icon;
        FitImageToHeightPreservingAspect(iconImage, iconHeight);
    }

    private void RefreshFrame()
    {
        var data = ProfileFrameRegistry.Instance != null ? ProfileFrameRegistry.Instance.GetData(PlayerProfile.EquippedFrameId) : null;

        if (frameNameText != null) frameNameText.text = data != null ? data.displayName : emptyFrameNameLabel;

        if (frameImageObj != null) frameImageObj.SetActive(data != null);
        if (data == null || frameImage == null) return;

        frameImage.sprite = data.frameSprite;
    }

    private void RefreshTitles()
    {
        // メイン枠: 購入という概念が無いので常に「購入済み」の見た目、lockOverlayも無い
        var mainData = TitleRegistry.Instance != null ? TitleRegistry.Instance.GetData(PlayerProfile.EquippedMainTitleId) : null;
        string mainTitleName = mainData != null ? mainData.titleName : emptyMainTitleLabel;
        ApplyTitleSlotVisual(isLocked: false, mainTitleButtonImage, mainTitleButtonLabelText,
            mainTitleText, lockOverlay: null, mainTitleName, price: 0);

        int nextUnlockableSlot = PlayerProfile.UnlockedSubTitleSlotCount;

        for (int i = 0; i < 4; i++)
        {
            bool unlocked = PlayerProfile.IsSubTitleSlotUnlocked(i);

            string subTitleName = emptySubTitleLabel;
            if (unlocked)
            {
                var data = (i < PlayerProfile.EquippedSubTitleIds.Length && TitleRegistry.Instance != null)
                    ? TitleRegistry.Instance.GetData(PlayerProfile.EquippedSubTitleIds[i])
                    : null;
                subTitleName = data != null ? data.titleName : emptySubTitleLabel;
            }

            ApplyTitleSlotVisual(
                isLocked: !unlocked,
                GetOrNull(subTitleButtonImages, i),
                GetOrNull(subTitleButtonLabelTexts, i),
                GetOrNull(subTitleTexts, i),
                GetOrNull(subTitleLockOverlays, i),
                subTitleName,
                PlayerProfile.SubTitleSlotPrices[i]);

            var button = GetOrNull(subTitleButtons, i);
            if (button != null)
            {
                // 解放済み(選択操作) or ちょうど次に解放できる枠(購入操作)だけ押せる。
                // それより先の未解放枠は、見た目は「未購入」のままだがボタンは押せない
                button.interactable = unlocked || i == nextUnlockableSlot;
            }
        }
    }

    // 称号1枠分の見た目(ボタン画像・ボタン内テキストの文言と色・表示テキスト・lockOverlay)をまとめて適用する。
    // メイン枠(常にisLocked=false、lockOverlay=null)とサブ枠4個で共通して使う
    private void ApplyTitleSlotVisual(bool isLocked, Image buttonImage, TMP_Text buttonLabelText,
        TMP_Text displayText, GameObject lockOverlay, string equippedTitleName, int price)
    {
        if (buttonImage != null)
            buttonImage.sprite = isLocked ? lockedTitleButtonSprite : unlockedTitleButtonSprite;

        if (buttonLabelText != null)
        {
            buttonLabelText.text = isLocked ? lockedButtonLabel : unlockedButtonLabel;
            buttonLabelText.color = isLocked ? lockedButtonTextColor : unlockedButtonTextColor;
        }

        if (displayText != null)
            displayText.text = isLocked ? $"{price} エコー" : equippedTitleName;

        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);
    }

    private static T GetOrNull<T>(T[] array, int index) where T : class
    {
        return (array != null && index >= 0 && index < array.Length) ? array[index] : null;
    }

    // 名前保存ボタンのOnClickから呼ぶ
    public void OnSaveNameClicked()
    {
        if (nameInputField == null) return;

        string newName = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(newName)) return;

        PlayerProfile.SetDisplayName(newName, ok =>
        {
            if (nameSavedFeedbackText != null)
                nameSavedFeedbackText.text = ok ? "保存しました" : "保存に失敗しました";
        });
    }

    // ---------------- アイコン選択画面 ----------------

    private void PopulateIconPicker()
    {
        if (iconPickerGridParent == null || iconPickerEntryPrefab == null) return;

        foreach (Transform child in iconPickerGridParent)
            Destroy(child.gameObject);

        if (ProfileIconRegistry.Instance == null) return;

        // グリッド全体で共通のプレビュー用フレーム(初期固定、クリックしても変わらない)
        var frameData = ProfileFrameRegistry.Instance != null ? ProfileFrameRegistry.Instance.GetData(PlayerProfile.EquippedFrameId) : null;
        Sprite previewFrameSprite = frameData != null ? frameData.frameSprite : null;

        foreach (var data in ProfileIconRegistry.Instance.AllIcons)
        {
            var entry = Instantiate(iconPickerEntryPrefab, iconPickerGridParent);
            entry.Setup(data, previewFrameSprite, OnIconEntryClicked);
        }
    }

    // グリッドのマスをクリックした時。ここでは選択状態にするだけで、装備はConfirmまで確定しない
    private void OnIconEntryClicked(ProfileIconData data)
    {
        pendingSelectedIcon = data;
        RefreshSelectedIconName();
    }

    private void RefreshSelectedIconName()
    {
        if (selectedIconNameText != null)
            selectedIconNameText.text = pendingSelectedIcon != null ? pendingSelectedIcon.displayName : emptyIconNameLabel;
    }

    // 決定ボタンのOnClickから呼ぶ
    public void OnConfirmIconClicked()
    {
        ProfileIconId iconId = pendingSelectedIcon != null ? pendingSelectedIcon.id : ProfileIconId.None;
        PlayerProfile.EquipCosmetics(iconId, PlayerProfile.EquippedFrameId);
        ShowMain();
    }

    // ---------------- フレーム選択画面 ----------------

    private void PopulateFramePicker()
    {
        if (framePickerGridParent == null || framePickerEntryPrefab == null) return;

        foreach (Transform child in framePickerGridParent)
            Destroy(child.gameObject);

        if (ProfileFrameRegistry.Instance == null) return;

        foreach (var data in ProfileFrameRegistry.Instance.AllFrames)
        {
            var entry = Instantiate(framePickerEntryPrefab, framePickerGridParent);
            entry.Setup(data, OnFrameEntryClicked);
        }
    }

    // グリッドのマスをクリックした時。ここでは選択状態にするだけで、装備はConfirmまで確定しない
    private void OnFrameEntryClicked(ProfileFrameData data)
    {
        pendingSelectedFrame = data;
        RefreshSelectedFrameName();
    }

    private void RefreshSelectedFrameName()
    {
        if (selectedFrameNameText != null)
            selectedFrameNameText.text = pendingSelectedFrame != null ? pendingSelectedFrame.displayName : emptyFrameNameLabel;
    }

    // 決定ボタンのOnClickから呼ぶ
    public void OnConfirmFrameClicked()
    {
        ProfileFrameId frameId = pendingSelectedFrame != null ? pendingSelectedFrame.id : ProfileFrameId.None;
        PlayerProfile.EquipCosmetics(PlayerProfile.EquippedIconId, frameId);
        ShowMain();
    }

    // ---------------- 称号選択画面(メイン/サブ共通) ----------------

    private void PopulateTitlePicker()
    {
        if (titlePickerGridParent == null || titlePickerEntryPrefab == null) return;

        foreach (Transform child in titlePickerGridParent)
            Destroy(child.gameObject);

        if (TitleRegistry.Instance == null) return;

        foreach (var data in TitleRegistry.Instance.AllTitles)
        {
            if (!PlayerProfile.IsTitleOwned(data.id)) continue; // 未所持の称号は選べない

            var entry = Instantiate(titlePickerEntryPrefab, titlePickerGridParent);
            entry.Setup(data, OnTitleEntryClicked);
        }
    }

    // グリッドのマスをクリックした時。ここでは選択状態にするだけで、装備はConfirmまで確定しない
    private void OnTitleEntryClicked(TitleData data)
    {
        pendingSelectedTitle = data;
        RefreshSelectedTitleDescription();
    }

    private void RefreshSelectedTitleDescription()
    {
        if (selectedTitleDescriptionText != null)
            selectedTitleDescriptionText.text = pendingSelectedTitle != null ? pendingSelectedTitle.description : "";
    }

    // 決定ボタンのOnClickから呼ぶ
    public void OnConfirmTitleClicked()
    {
        TitleId titleId = pendingSelectedTitle != null ? pendingSelectedTitle.id : TitleId.None;

        if (pendingSubTitleSlotIndex < 0)
            PlayerProfile.EquipMainTitle(titleId);
        else
            PlayerProfile.EquipSubTitle(pendingSubTitleSlotIndex, titleId);

        ShowMain();
    }

    // アスペクト比を保ったまま高さだけ固定する(FitImageToHeightPreservingAspectパターン)。
    // 親にRectMask2D、Anchorは中央固定(Stretch不可)が前提
    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
