using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プロフィール(名前・アイコン/フレーム・称号・アカウントレベル)を表示するだけのコンポーネント。
/// MenuScene等、プロフィールを見せたい場所に配置する。編集はProfileScene(別途実装予定)側で行う。
///
/// ランク/ランクバッジは現時点でランクマッチ自体が未実装のため、ここでは表示しない。
/// アカウントレベルはAccountLevel(マスタリーとは独立したXP)を表示する。
///
/// autoLoadOwnProfile=falseにすると起動時の自動読込(PlayerProfile/AccountLevelの自分専用キャッシュ)を止め、
/// ApplyManual()/ApplyManualRaw()で外部から渡された値だけを表示する「相手用」インスタンスとして使える
/// (Hensei準備完了後のお披露目演出で、自分用/相手用/CPU用のプロフィール表示に同じコンポーネントを使い回すため)。
/// </summary>
public class ProfileDisplayUI : MonoBehaviour
{
    [Header("表示モード")]
    [SerializeField] private bool autoLoadOwnProfile = true; // false: ApplyManual系を外部から呼ぶまで何もしない

    [Header("名前")]
    [SerializeField] private TextMeshProUGUI displayNameText;

    [Header("アイコン")]
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject iconImageObj; // 未設定(None)の間は非表示にしたい場合にセット
    [SerializeField] private float iconHeight = 80f;

    [Header("フレーム")]
    [SerializeField] private Image frameImage;
    [SerializeField] private GameObject frameImageObj; // 未設定(None)の間は非表示にしたい場合にセット

    [Header("称号")]
    [SerializeField] private TextMeshProUGUI mainTitleText;
    [SerializeField] private GameObject subTitlesContainer; // サブ称号テキスト群の入れ物。プロフィールをクリックすると開閉する(初期非表示)
    [SerializeField] private TextMeshProUGUI[] subTitleTexts; // 4個(サブ枠0〜3に対応)
    [SerializeField] private string lockedSubTitleLabel = "未解放";
    [SerializeField] private string emptySubTitleLabel = "未設定";

    [Header("アカウントレベル")]
    [SerializeField] private TextMeshProUGUI accountLevelText;

    private void Start()
    {
        if (subTitlesContainer != null) subTitlesContainer.SetActive(false);

        if (!autoLoadOwnProfile) return; // 相手用インスタンス: ApplyManual系が呼ばれるまで何もしない

        PlayerProfile.Load(RefreshDisplay);
        AccountLevel.Load(RefreshDisplay); // どちらが先に終わってもRefreshDisplayは現在値を読み直すだけなので問題ない
    }

    // プロフィール表示(アイコン・名前など)のButtonのOnClickから呼ぶ。サブ称号の表示/非表示を切り替える
    public void OnProfileClicked()
    {
        if (subTitlesContainer == null) return;
        subTitlesContainer.SetActive(!subTitlesContainer.activeSelf);
    }

    public void RefreshDisplay()
    {
        if (displayNameText != null)
            displayNameText.text = PlayerProfile.GetDisplayNameOrDefault();

        ApplyIcon(PlayerProfile.EquippedIconId);
        ApplyFrame(PlayerProfile.EquippedFrameId);
        RefreshTitles();

        if (accountLevelText != null)
            accountLevelText.text = $"Lv.{AccountLevel.Level}";
    }

    /// <summary>
    /// PlayerProfile(自分専用の静的キャッシュ)を使わず、外部から渡された値だけで表示を更新する。
    /// アイコン/フレーム/称号はProfileIconId/ProfileFrameId/TitleIdレジストリ経由(オンライン対戦の相手など、
    /// 実際にそのIDを装備している相手用)。サブ称号/アカウントレベルは対象外(相手の分は取得していないため)。
    /// </summary>
    public void ApplyManual(string displayName, ProfileIconId iconId, ProfileFrameId frameId, TitleId mainTitleId)
    {
        WarnIfAutoLoadStillOn();

        if (displayNameText != null)
            displayNameText.text = string.IsNullOrEmpty(displayName) ? "プレイヤー" : displayName;

        ApplyIcon(iconId);
        ApplyFrame(frameId);
        ApplyMainTitleText(mainTitleId);
    }

    /// <summary>
    /// レジストリを介さず、生のSprite/文字列で直接表示を上書きする。CPU戦の「相手」プレースホルダーなど、
    /// ProfileIconId/ProfileFrameId/TitleIdの実体を持たない相手を表示したい場合に使う。
    /// </summary>
    public void ApplyManualRaw(string displayName, Sprite iconSprite, Sprite frameSprite, string mainTitleLabel = "")
    {
        WarnIfAutoLoadStillOn();

        if (displayNameText != null)
            displayNameText.text = string.IsNullOrEmpty(displayName) ? "プレイヤー" : displayName;

        if (iconImageObj != null) iconImageObj.SetActive(iconSprite != null);
        if (iconImage != null && iconSprite != null)
        {
            iconImage.sprite = iconSprite;
            FitImageToHeightPreservingAspect(iconImage, iconHeight);
        }

        if (frameImageObj != null) frameImageObj.SetActive(frameSprite != null);
        if (frameImage != null && frameSprite != null)
            frameImage.sprite = frameSprite;

        if (mainTitleText != null) mainTitleText.text = mainTitleLabel ?? "";
    }

    // autoLoadOwnProfile=trueのままApplyManual系を呼ぶと、直後に走るStart()の自動読込(PlayerProfile.Load→RefreshDisplay)が
    // ここでセットした値を後から上書きしてしまう。相手用/CPU用として使うインスタンスはInspectorでOFFにしておくこと
    private void WarnIfAutoLoadStillOn()
    {
        if (autoLoadOwnProfile)
            Debug.LogWarning($"[ProfileDisplayUI] \"{name}\": autoLoadOwnProfile=trueのままApplyManual系が呼ばれました。" +
                "自動読込(PlayerProfile)が後からこの表示を上書きします。相手用として使うならInspectorでAuto Load Own ProfileをOFFにしてください。");
    }

    private void ApplyIcon(ProfileIconId iconId)
    {
        var iconData = ProfileIconRegistry.Instance != null
            ? ProfileIconRegistry.Instance.GetData(iconId)
            : null;

        if (iconImageObj != null) iconImageObj.SetActive(iconData != null);
        if (iconData == null || iconImage == null) return;

        iconImage.sprite = iconData.icon;
        FitImageToHeightPreservingAspect(iconImage, iconHeight);
    }

    private void ApplyFrame(ProfileFrameId frameId)
    {
        var frameData = ProfileFrameRegistry.Instance != null
            ? ProfileFrameRegistry.Instance.GetData(frameId)
            : null;

        if (frameImageObj != null) frameImageObj.SetActive(frameData != null);
        if (frameData == null || frameImage == null) return;

        frameImage.sprite = frameData.frameSprite;
    }

    private void ApplyMainTitleText(TitleId titleId)
    {
        if (mainTitleText == null) return;
        var mainTitleData = TitleRegistry.Instance != null ? TitleRegistry.Instance.GetData(titleId) : null;
        mainTitleText.text = mainTitleData != null ? mainTitleData.titleName : "";
    }

    private void RefreshTitles()
    {
        ApplyMainTitleText(PlayerProfile.EquippedMainTitleId);

        if (subTitleTexts == null) return;

        for (int i = 0; i < subTitleTexts.Length; i++)
        {
            if (subTitleTexts[i] == null) continue;

            if (!PlayerProfile.IsSubTitleSlotUnlocked(i))
            {
                subTitleTexts[i].text = lockedSubTitleLabel;
                continue;
            }

            var subTitleData = (i < PlayerProfile.EquippedSubTitleIds.Length && TitleRegistry.Instance != null)
                ? TitleRegistry.Instance.GetData(PlayerProfile.EquippedSubTitleIds[i])
                : null;
            subTitleTexts[i].text = subTitleData != null ? subTitleData.titleName : emptySubTitleLabel;
        }
    }

    // アスペクト比を保ったまま高さだけ固定するお馴染みの処理(FitImageToHeightPreservingAspectパターン)。
    // 親にRectMask2D、Anchorは中央固定(Stretch不可)が前提
    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }
}
