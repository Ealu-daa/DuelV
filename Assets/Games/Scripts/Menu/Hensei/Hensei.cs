using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Hensei : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private Transform characterIconGridParent; // ScrollView内、GridLayoutGroupを付けたContent
    [SerializeField] private CharacterPickerEntryUI characterEntryPrefab;
    [SerializeField] private GameObject characterInformation;
    [SerializeField] private Image[] characterBoxImages;
    [SerializeField] private float characterBoxHeight = 100f; // characterBoxImagesを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)
    [SerializeField] private CharacterData[] characterDataArray;
    [SerializeField] private Sprite defaultCharacterBoxSprite; // デフォルトのキャラクターボックスのスプライト

    [Header("CharacterDetails")]
    [SerializeField] private GameObject characterDetailsPanel;
    [SerializeField] private Image characterPanel;
    [SerializeField] private float characterPanelHeight = 300f; // characterPanelを表示する枠の高さ(固定値。Inspectorで枠に合わせて調整)
    [SerializeField] private TextMeshProUGUI characterNameText; // 常時表示。キャラ自身の名前(タブ切り替えの対象外)

    [Header("CharacterDetails - ステータス(常時表示、タブ切り替えの対象外)")]
    [SerializeField] private TextMeshProUGUI characterHPText;
    [SerializeField] private TextMeshProUGUI characterAtkText;
    [SerializeField] private TextMeshProUGUI characterDefText;
    [SerializeField] private TextMeshProUGUI characterUltGaugeText;

    [Header("CharacterDetails - タブで中身が入れ替わる名前/説明")]
    [SerializeField] private TextMeshProUGUI subNameText;    // キャラ:空 / スキル:スキル名 / ウルト:ウルト名
    [SerializeField] private TextMeshProUGUI descriptionText; // キャラ:説明文 / スキル:スキル説明 / ウルト:ウルト説明

    [SerializeField] private Image SelectButtonImage;
    [SerializeField] private Sprite[] SelectButtonSprite;
    private bool isCharacterIconsActive = false;

    private int nowCharacterBoxIndex = -1; // 現在選択されているキャラクターボックスのインデックス
    private CharacterData nowSelectedCharacterData; // 現在ピッカーで選択中のキャラ

    [Header("Catalyst")]
    [SerializeField] private Transform catalystIconGridParent; // ScrollView内、GridLayoutGroupを付けたContent
    [SerializeField] private CatalystPickerEntryUI catalystEntryPrefab;
    [SerializeField] private GameObject catalystInformation;
    [SerializeField] private Image[] catalystBoxImages;
    private List<CatalystData>[] catalystDataByCharacter = new List<CatalystData>[5];
    [SerializeField] private Sprite defaultCatalystBoxSprite;
    [SerializeField] private Transform[] addCatalystBoxes; // 5キャラ分、それぞれの「追加ボタン(Box)」

    [Header("CatalystDetails")]
    [SerializeField] private GameObject catalystDetailsPanel;
    [SerializeField] private Image catalystPanel;
    [SerializeField] private TextMeshProUGUI catalystNameText;
    [SerializeField] private TextMeshProUGUI catalystDescriptionText;
    [SerializeField] private TextMeshProUGUI catalystRestrictedRoleText;
    [SerializeField] private Image catalystSelectButtonImage;

    [SerializeField] private CatalystBoxUI catalystBoxPrefab;
    [SerializeField] private Transform[] catalystGridContents; // 5キャラ分、それぞれのGridLayoutGroupのContent


    private bool isCatalystIconsActive = false;
    private int nowCatalystBoxIndex = -1;
    private CatalystData nowSelectedCatalystData; // 現在ピッカーで選択中のカタリスト

    private int nowPresetIndex = 1; // 現在選択されているプリセットのインデックス

    public static int SelectedPresetIndex = 1;

    // MenuSceneから直接HenseiSceneに来た場合はtrue(MenuManager.OnHenseiButtonClickedがセットする)。
    // OnlineMatchScene経由(オンライン対戦/CPU対戦)の場合はfalseのまま。
    public static bool EnteredFromMenu = false;

    [Header("戻る/対戦ボタン(共通1個。文言とOnClickの中身をEnteredFromMenuで切り替える)")]
    [SerializeField] private TMP_Text actionButtonText;

    private bool enteredFromMenu; // Start()時にEnteredFromMenu(静的フラグ)を読み込んでキャッシュしたもの

    [Header("オンライン対戦: 相手の準備完了表示+90秒タイムアウト(ドッジ)")]
    [SerializeField] private TMP_Text opponentReadyText;   // 例:「相手: 準備完了」/「相手: 未準備」。任意
    [SerializeField] private TMP_Text henseiTimeoutText;   // 残り秒数の表示。任意
    [SerializeField] private float henseiTimeoutSeconds = 90f;
    [SerializeField] private float henseiTimeoutWarningThreshold = 10f; // これ以下になったら警告色にする
    [SerializeField] private Color henseiTimeoutNormalColor = Color.white;
    [SerializeField] private Color henseiTimeoutWarningColor = Color.red;

    private bool isOnlineHensei; // オンライン対戦経由でこのHenseiSceneに来ているか
    private float henseiTimeoutElapsed = 0f;
    private bool henseiTimeoutHandled = false;

    [Header("オンライン対戦: お披露目演出(編成確定後、DuelScene遷移前に数秒表示)")]
    [SerializeField] private GameObject revealPanel; // 任意。両者確定と同時にRoomManagerのイベント経由で表示

    [Header("お披露目 - 自分側")]
    [SerializeField] private ProfileDisplayUI myRevealProfileUI; // autoLoadOwnProfile=falseにしておくこと(Hensei.csがApplyManualで表示するので自動読込は不要)
    [SerializeField] private PartyMemberDisplayUI[] myRevealParty; // 5枠(FW2+BK3)、characterDataArrayと同じ並び

    [Header("お披露目 - 相手側")]
    [SerializeField] private ProfileDisplayUI opponentRevealProfileUI; // autoLoadOwnProfile=falseにしておくこと(ApplyManual/ApplyManualRawで表示する)
    [SerializeField] private PartyMemberDisplayUI[] opponentRevealParty; // 5枠(FW2+BK3)

    [Header("CPU戦: お披露目相手情報(プレースホルダー、オンラインの相手プロフィールの代わり)")]
    [SerializeField] private string cpuDisplayName = "CPU";
    [SerializeField] private Sprite cpuIconSprite;
    [SerializeField] private Sprite cpuFrameSprite;
    [SerializeField] private CharacterData[] cpuRevealCharacterData; // 5枠(FW2+BK3)。BattleManagerの敵編成(enemyFWData/enemyBKData)と同じデータを設定しておく
    [SerializeField] private float cpuRevealSeconds = 8f; // オンラインはRoomManager.revealDisplaySecondsを使うため、CPU戦用は別に持つ

    // Start is called before the first frame update
    void Start()
    {
        LoadPresetIntoEditor(1);

        enteredFromMenu = EnteredFromMenu;
        EnteredFromMenu = false; // 読んだらリセット(次にオンライン対戦経由で入った時に誤って残らないように)

        if (actionButtonText != null) actionButtonText.text = enteredFromMenu ? "戻る" : "対戦";

        // MenuSceneから直接来た場合はオンライン対戦の編成フェーズではないので、準備完了/タイムアウトの対象外
        isOnlineHensei = !enteredFromMenu && !string.IsNullOrEmpty(RoomManager.CurrentRoomCode);
        if (isOnlineHensei)
        {
            RoomManager.Instance.OnOpponentReadyChanged += HandleOpponentReadyChanged;
            RoomManager.Instance.OnRevealStart += HandleRevealStart;
            RefreshOpponentReadyDisplay(false);
        }
        else
        {
            // CPU戦/MenuScene経由: 準備完了表示・タイムアウト表示はオンライン対戦専用なので出さない
            // (Update()側はisOnlineHenseiがfalseの間そもそも更新しないため、放っておくと初期値のまま画面に残ってしまう)
            if (opponentReadyText != null) opponentReadyText.gameObject.SetActive(false);
            if (henseiTimeoutText != null) henseiTimeoutText.gameObject.SetActive(false);
        }

        if (revealPanel != null) revealPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (isOnlineHensei && RoomManager.Instance != null)
        {
            RoomManager.Instance.OnOpponentReadyChanged -= HandleOpponentReadyChanged;
            RoomManager.Instance.OnRevealStart -= HandleRevealStart;
        }
    }

    private void HandleOpponentReadyChanged(bool ready)
    {
        RefreshOpponentReadyDisplay(ready);
    }

    private void RefreshOpponentReadyDisplay(bool ready)
    {
        if (opponentReadyText != null) opponentReadyText.text = ready ? "相手: 準備完了" : "相手: 未準備";
    }

    // 「戻る/対戦」共通ボタンのOnClickから呼ぶ。
    // MenuSceneから来た場合: プリセットだけ保存してMenuSceneへ戻る
    // OnlineMatchScene経由の場合: 従来通り保存してデュエルへ(CPU戦・オンライン共にお披露目演出を挟んでから遷移)
    public void OnActionButtonClicked()
    {
        if (enteredFromMenu)
        {
            SaveCurrentPreset(nowPresetIndex, success =>
            {
                if (!success)
                {
                    Debug.LogError("保存に失敗しました。Menuには戻りません。");
                    return;
                }

                SceneManager.LoadScene("MenuScene");
            });
            return;
        }

        SaveCurrentPreset(nowPresetIndex, success =>
        {
            if (!success)
            {
                Debug.LogError("保存に失敗しました。デュエル画面には移動しません。");
                return;
            }

            Hensei.SelectedPresetIndex = nowPresetIndex;

            if (!string.IsNullOrEmpty(RoomManager.CurrentRoomCode))
            {
                // オンライン対戦: 自分の準備完了をFirestoreに通知するだけ。
                // シーン遷移は両者揃った時にRoomManagerが自動で行う。
                RoomManager.Instance.MarkPresetReady(nowPresetIndex);
                Debug.Log("編成完了。相手の編成を待っています...");
                // 必要なら「相手を待っています」的なUI表示をここに追加
            }
            else
            {
                // CPU戦: 自分側+CPU側(プレースホルダー)のお披露目を数秒挟んでから遷移
                StartCoroutine(ShowCpuRevealThenLoadDuelScene());
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
        if (!isOnlineHensei || henseiTimeoutHandled) return;

        henseiTimeoutElapsed += Time.deltaTime;
        float remaining = henseiTimeoutSeconds - henseiTimeoutElapsed;

        if (henseiTimeoutText != null)
        {
            henseiTimeoutText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
            henseiTimeoutText.color = remaining <= henseiTimeoutWarningThreshold ? henseiTimeoutWarningColor : henseiTimeoutNormalColor;
        }

        if (remaining <= 0f)
        {
            henseiTimeoutHandled = true;
            // 相手にも通知(先に気づいた側が伝える形。相手はRoomManager.HandleGameUpdated経由で検知する)
            RoomManager.Instance.ReportHenseiTimeout();
            // 自分側は通知を待たず、その場でロビー(OnlineMatchScene)へ戻る
            RoomManager.Instance.LeaveRoom();
            SceneManager.LoadScene("OnlineMatchScene");
        }
    }
    // 両者の編成が確定した瞬間にRoomManagerから通知される。以後DuelSceneへ遷移するまでの数秒間、
    // 自分/相手のプロフィール+FW/BKを一斉公開する(お披露目演出)。
    private void HandleRevealStart(string opponentUid, int opponentPresetIndex)
    {
        henseiTimeoutHandled = true; // 既に両者確定済みなのでタイムアウト監視は不要

        SeManager.PlayReveal();
        if (revealPanel != null) revealPanel.SetActive(true);

        PopulateMyRevealSide();
        PopulateOpponentRevealSide(opponentUid, opponentPresetIndex);
    }

    private void PopulateMyRevealSide()
    {
        PlayerProfile.Load(() =>
        {
            if (myRevealProfileUI != null)
                myRevealProfileUI.ApplyManual(PlayerProfile.GetDisplayNameOrDefault(), PlayerProfile.EquippedIconId, PlayerProfile.EquippedFrameId, PlayerProfile.EquippedMainTitleId);
        });

        // 自チームは編成画面で選択済みのデータをそのまま使う(再読込不要)
        PopulateParty(myRevealParty, characterDataArray);
    }

    private void PopulateOpponentRevealSide(string opponentUid, int opponentPresetIndex)
    {
        if (string.IsNullOrEmpty(opponentUid)) return;

        ProfileSnapshot.LoadForUid(opponentUid, snapshot =>
        {
            if (opponentRevealProfileUI != null)
                opponentRevealProfileUI.ApplyManual(snapshot.DisplayName, snapshot.IconId, snapshot.FrameId, snapshot.MainTitleId);
        });

        FirestoreBridge.Instance.LoadPreset(opponentUid, opponentPresetIndex, doc =>
        {
            TeamPreset preset = TeamPreset.FromFirestoreDocument(doc);
            int slotCount = opponentRevealParty != null ? opponentRevealParty.Length : 0;
            var opponentCharacters = new CharacterData[slotCount];

            if (preset != null)
            {
                for (int i = 0; i < slotCount && i < preset.characters.Count; i++)
                    opponentCharacters[i] = CharacterRegistry.Instance.GetData(preset.characters[i].charId);
            }

            PopulateParty(opponentRevealParty, opponentCharacters);
        });
    }

    private void PopulateCpuRevealSide()
    {
        if (opponentRevealProfileUI == null)
        {
            Debug.LogWarning("[Hensei] opponentRevealProfileUiが未設定のため、CPU側プロフィール表示をスキップしました");
        }
        else
        {
            opponentRevealProfileUI.ApplyManualRaw(cpuDisplayName, cpuIconSprite, cpuFrameSprite);
        }

        if (cpuIconSprite == null || cpuFrameSprite == null)
            Debug.LogWarning("[Hensei] cpuIconSprite/cpuFrameSpriteが未設定です。Inspectorで設定してください");

        if (cpuRevealCharacterData == null || cpuRevealCharacterData.Length == 0)
            Debug.LogWarning("[Hensei] cpuRevealCharacterDataが未設定です。Inspectorで5体分設定してください");

        PopulateParty(opponentRevealParty, cpuRevealCharacterData);
    }

    // 各スロットにCharacterDataを流し込む共通ヘルパー。自分側/相手側/CPU側で共用
    private void PopulateParty(PartyMemberDisplayUI[] slots, CharacterData[] data)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            CharacterData charData = (data != null && i < data.Length) ? data[i] : null;
            if (charData == null)
            {
                slots[i].Clear();
                continue;
            }

            slots[i].SetData(charData, ConvertRoleGroupToRole(charData.RoleGroup));
        }
    }

    // CPU戦: 相手はFirestore上のプロフィールを持たないため、cpuDisplayName/cpuIconSprite等の
    // プレースホルダーをそのまま「相手側」の表示欄に流し込む(オンラインのPopulateOpponentRevealSideに相当)
    private IEnumerator ShowCpuRevealThenLoadDuelScene()
    {
        SeManager.PlayReveal();
        if (revealPanel != null) revealPanel.SetActive(true);

        PopulateMyRevealSide();
        PopulateCpuRevealSide();

        yield return new WaitForSeconds(cpuRevealSeconds);

        SceneManager.LoadScene("DuelScene");
    }

    public void OnClickCharacterBox(int characterIndex)
    {
        isCharacterIconsActive = !isCharacterIconsActive;
        characterInformation.SetActive(isCharacterIconsActive);
        characterDetailsPanel.SetActive(false);

        nowCharacterBoxIndex = characterIndex; // 選択されたキャラクターボックスのインデックスを更新

        if (isCharacterIconsActive) PopulateCharacterPicker();
    }

    // キャラを選ぶグリッドを、実在するキャラ数ぶんだけInstantiateして並べる
    private void PopulateCharacterPicker()
    {
        foreach (Transform child in characterIconGridParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var data in CharacterRegistry.Instance.AllCharacters)
        {
            var entry = Instantiate(characterEntryPrefab, characterIconGridParent);
            entry.Setup(data, OnClickCharacterIcon);
        }
    }

    public void OnClickCharacterIcon(CharacterData selectedCharacter)
    {
        nowSelectedCharacterData = selectedCharacter; // 選択されたキャラクターを更新

        characterDetailsPanel.SetActive(true);
        characterPanel.sprite = selectedCharacter.characterSprite;
        FitImageToHeightPreservingAspect(characterPanel, characterPanelHeight); // 横長スプライトを縮めず、縦基準で合わせて左右をクリップする
        characterNameText.text = selectedCharacter.characterName;
        characterHPText.text = $"{selectedCharacter.maxHP}";
        characterAtkText.text = $"{selectedCharacter.attack}";
        characterDefText.text = $"{selectedCharacter.defense}";
        characterUltGaugeText.text = $"{selectedCharacter.maxUltGauge}";

        if (characterDataArray[nowCharacterBoxIndex] == selectedCharacter)
        {
            SelectButtonImage.sprite = SelectButtonSprite[1]; // 選択済みのスプライトに変更
        }
        else
        {
            SelectButtonImage.sprite = SelectButtonSprite[0]; // 選択可能のスプライトに変更
        }

        OnClickCharacterInfoTab(); // キャラを選び直したら「キャラ」タブへ戻す
    }

    // 横長スプライトを画面幅に合わせて縮めるのではなく、枠の高さいっぱいに合わせて表示する
    // (幅は枠より大きくなるが、親のRectMask2Dで左右がクリップされる想定)
    // 高さは毎回RectTransformから読み直さず、呼び出し側が渡す固定値を使う
    // (Anchorがストレッチ設定だとsizeDeltaは「追加分」として扱われ、読み書きを繰り返すと膨張してしまうため)
    private void FitImageToHeightPreservingAspect(Image image, float height)
    {
        if (image.sprite == null) return;

        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
    }

    // characterBoxImages[index]にスプライトをセットしつつ、アスペクト比を保って高さ基準で合わせる
    private void SetCharacterBoxSprite(int index, Sprite sprite)
    {
        characterBoxImages[index].sprite = sprite;
        FitImageToHeightPreservingAspect(characterBoxImages[index], characterBoxHeight);
    }

    // 「キャラ」タブボタンのOnClickから呼ぶ: 名前は空、説明はキャラの説明文
    public void OnClickCharacterInfoTab()
    {
        if (nowSelectedCharacterData == null) return;

        subNameText.text = "";
        descriptionText.text = nowSelectedCharacterData.description;
    }

    // 「スキル」タブボタンのOnClickから呼ぶ: 名前はスキル名、説明はスキル説明
    public void OnClickSkillTab()
    {
        if (nowSelectedCharacterData == null) return;

        subNameText.text = nowSelectedCharacterData.skillName;
        descriptionText.text = nowSelectedCharacterData.skillDescription;
    }

    // 「ウルト」タブボタンのOnClickから呼ぶ: 名前はウルト名、説明はウルト説明
    public void OnClickUltTab()
    {
        if (nowSelectedCharacterData == null) return;

        subNameText.text = nowSelectedCharacterData.ultName;
        descriptionText.text = nowSelectedCharacterData.ultDescription;
    }

    public void OnCharacterSelected()
    {
        CharacterData selectedCharacter = nowSelectedCharacterData;
        if (selectedCharacter == null) return;
        if (characterDataArray[nowCharacterBoxIndex] == selectedCharacter)
        {
            return;
        }
        characterInformation.SetActive(false);
        characterDetailsPanel.SetActive(false);
        isCharacterIconsActive = false;
        int existingIndex = Array.IndexOf(characterDataArray, selectedCharacter);
        if (existingIndex != -1)
        {
            if (characterDataArray[nowCharacterBoxIndex] == null)
            {
                SetCharacterBoxSprite(existingIndex, defaultCharacterBoxSprite);
                characterDataArray[existingIndex] = null;
            }
            else
            {
                SetCharacterBoxSprite(existingIndex, characterBoxImages[nowCharacterBoxIndex].sprite);
                characterDataArray[existingIndex] = characterDataArray[nowCharacterBoxIndex];
            }
            SetCharacterBoxSprite(nowCharacterBoxIndex, selectedCharacter.characterSprite);
            characterDataArray[nowCharacterBoxIndex] = selectedCharacter;
        }
        else
        {
            SetCharacterBoxSprite(nowCharacterBoxIndex, selectedCharacter.characterSprite);
            characterDataArray[nowCharacterBoxIndex] = selectedCharacter;
        }
    }

    public void OnClickRemoveCharacterButton()
    {
        ClearAllCatalystsFromCharacter(nowCharacterBoxIndex);

        characterDataArray[nowCharacterBoxIndex] = null;
        SetCharacterBoxSprite(nowCharacterBoxIndex, defaultCharacterBoxSprite);

        characterInformation.SetActive(false);
        isCharacterIconsActive = false;
    }


    /// <summary>指定したキャラクター枠が装備可能なカタリスト一覧(ロールで絞り込み済み)を返す。</summary>
    private List<CatalystData> GetAvailableCatalystsForBox(int characterBoxIndex)
    {
        Role currentRole = ConvertRoleGroupToRole(characterDataArray[characterBoxIndex].RoleGroup);
        return CatalystDataRegistry.Instance.AllCatalysts
            .Where(c => c.restrictedRole == Role.None || c.restrictedRole == currentRole)
            .ToList();
    }

    private Role ConvertRoleGroupToRole(int roleGroup)
    {
        switch (roleGroup)
        {
            case 1: return Role.Duelist;
            case 2: return Role.Guardian;
            case 3: return Role.Controller;
            case 4: return Role.Support;
            default:
                Debug.LogWarning($"未知のRoleGroup: {roleGroup}");
                return Role.Duelist; // 仮のフォールバック
        }
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

    // ----- カタリスト枠クリック(グリッドを開く) -----

    public void OnClickCatalystBox(int catalystIndex)
    {
        if (characterDataArray[catalystIndex] == null)
        {
            return; // キャラが選ばれていない枠は何もしない
        }

        isCatalystIconsActive = !isCatalystIconsActive;
        catalystInformation.SetActive(isCatalystIconsActive);
        catalystDetailsPanel.SetActive(false);

        nowCatalystBoxIndex = catalystIndex;

        if (isCatalystIconsActive) PopulateCatalystPicker();
    }

    // カタリストを選ぶグリッドを、そのキャラが装備可能な候補数ぶんだけInstantiateして並べる
    private void PopulateCatalystPicker()
    {
        foreach (Transform child in catalystIconGridParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var data in GetAvailableCatalystsForBox(nowCatalystBoxIndex))
        {
            var entry = Instantiate(catalystEntryPrefab, catalystIconGridParent);
            entry.Setup(data, OnClickCatalystIcon);
        }
    }

    // ----- カタリストアイコンクリック(詳細表示) -----

    public void OnClickCatalystIcon(CatalystData selectedCatalyst)
    {
        nowSelectedCatalystData = selectedCatalyst;

        catalystDetailsPanel.SetActive(true);
        catalystPanel.sprite = selectedCatalyst.icon;
        catalystNameText.text = $"{selectedCatalyst.catalystName}";
        catalystDescriptionText.text = $"{selectedCatalyst.description}";
        catalystRestrictedRoleText.text = GetRoleDisplayName(selectedCatalyst.restrictedRole);

        bool alreadyOwnedByThisCharacter =
            catalystDataByCharacter[nowCatalystBoxIndex] != null &&
            catalystDataByCharacter[nowCatalystBoxIndex].Contains(selectedCatalyst);

        catalystSelectButtonImage.sprite = alreadyOwnedByThisCharacter
            ? SelectButtonSprite[1]
            : SelectButtonSprite[0];
    }

    // ----- 確定ボタン -----

    public void OnCatalystSelected()
    {
        CatalystData selectedCatalyst = nowSelectedCatalystData;
        if (selectedCatalyst == null) return;

        // 既に他のキャラ(または自分自身)が持っているか確認
        bool alreadyOwned = false;
        int ownerIndex = -1;
        for (int i = 0; i < catalystDataByCharacter.Length; i++)
        {
            if (catalystDataByCharacter[i] == null) continue;
            if (catalystDataByCharacter[i].Contains(selectedCatalyst))
            {
                alreadyOwned = true;
                ownerIndex = i;
                break;
            }
        }

        // 自分自身が既に持っているものを選び直しただけなら何もしない
        if (alreadyOwned && ownerIndex == nowCatalystBoxIndex)
        {
            return;
        }

        // 新規追加(誰も持っていなかった)の場合だけ上限チェック。上限なら何もせず終了
        if (!alreadyOwned && GetTotalCatalystCount() >= MaxTotalCatalystCount)
        {
            return;
        }

        catalystInformation.SetActive(false);
        catalystDetailsPanel.SetActive(false);
        isCatalystIconsActive = false;

        // 他キャラが持っていた場合はそちらから外す(移動)
        if (alreadyOwned)
        {
            RemoveCatalystFromCharacter(ownerIndex, selectedCatalyst);
        }

        if (catalystDataByCharacter[nowCatalystBoxIndex] == null)
            catalystDataByCharacter[nowCatalystBoxIndex] = new List<CatalystData>();

        catalystDataByCharacter[nowCatalystBoxIndex].Add(selectedCatalyst);
        AddCatalystBox(nowCatalystBoxIndex, selectedCatalyst);
    }

    private int GetTotalCatalystCount()
    {
        int total = 0;
        foreach (var list in catalystDataByCharacter)
        {
            if (list != null) total += list.Count;
        }
        return total;
    }

    private const int MaxTotalCatalystCount = 10;

    // ----- グリッドへのUI追加/削除 -----

    private void AddCatalystBox(int characterBoxIndex, CatalystData data)
    {
        CatalystBoxUI box = Instantiate(catalystBoxPrefab, catalystGridContents[characterBoxIndex]);
        box.Bind(data);
        box.Setup(characterBoxIndex, this);

        // 追加ボタンの位置に割り込ませる(追加ボタンは自動的に1つ後ろへずれる)
        int addBoxSiblingIndex = addCatalystBoxes[characterBoxIndex].GetSiblingIndex();
        box.transform.SetSiblingIndex(addBoxSiblingIndex);
    }

    //Remove系
    private void RemoveCatalystBox(int characterBoxIndex, CatalystData data)
    {
        Transform grid = catalystGridContents[characterBoxIndex];
        foreach (Transform child in grid)
        {
            CatalystBoxUI box = child.GetComponent<CatalystBoxUI>();
            if (box != null && box.CurrentData == data)
            {
                Destroy(box.gameObject);
                break;
            }
        }
    }

    public void OnClickRemoveCatalystButton()
    {
        CatalystData selectedCatalyst = nowSelectedCatalystData;
        if (selectedCatalyst == null) return;

        bool isOwnedByCurrentCharacter =
            catalystDataByCharacter[nowCatalystBoxIndex] != null &&
            catalystDataByCharacter[nowCatalystBoxIndex].Contains(selectedCatalyst);

        if (!isOwnedByCurrentCharacter)
        {
            return;
        }

        RemoveCatalystFromCharacter(nowCatalystBoxIndex, selectedCatalyst);

        catalystInformation.SetActive(false);
        catalystDetailsPanel.SetActive(false);
        isCatalystIconsActive = false;
    }

    private void RemoveCatalystFromCharacter(int characterBoxIndex, CatalystData data)
    {
        if (catalystDataByCharacter[characterBoxIndex] != null)
        {
            catalystDataByCharacter[characterBoxIndex].Remove(data);
        }
        RemoveCatalystBox(characterBoxIndex, data);
    }


    private void ClearAllCatalystsFromCharacter(int characterBoxIndex)
    {
        if (catalystDataByCharacter[characterBoxIndex] != null)
        {
            catalystDataByCharacter[characterBoxIndex].Clear();
        }

        Transform grid = catalystGridContents[characterBoxIndex];

        foreach (Transform child in grid)
        {
            Destroy(child.gameObject);
        }

        CatalystBoxUI newAddBox = Instantiate(catalystBoxPrefab, grid);
        newAddBox.Setup(characterBoxIndex, this); 
        addCatalystBoxes[characterBoxIndex] = newAddBox.transform;
    }
    //Close系
    public void OnClickCloseCharacterPanel()
    {
        characterInformation.SetActive(false);
        characterDetailsPanel.SetActive(false);
        isCharacterIconsActive = false;
    }

    public void OnClickCloseCatalystPanel()
    {
        catalystInformation.SetActive(false);
        catalystDetailsPanel.SetActive(false);
        isCatalystIconsActive = false;
    }

    public void OnPresetClick(int number)
    {
        if (nowPresetIndex == number)
        {
            Debug.Log($"プリセット{number}は既に選択中です。");
            return;
        }

        // 切り替え前の編集内容を、今のプリセット番号として保存
        SaveCurrentPreset(nowPresetIndex);

        nowPresetIndex = number;
        Hensei.SelectedPresetIndex = number; // DuelScene引き継ぎ用

        LoadPresetIntoEditor(number);
    }

    private void SaveCurrentPreset(int presetSlot, Action<bool> onDone = null)
    {
        var entries = new List<PresetCharacterEntry>();

        for (int i = 0; i < characterDataArray.Length; i++)
        {
            if (characterDataArray[i] == null)
            {
                Debug.LogWarning($"編成スロット{i}が空です。保存を中断します。");
                onDone?.Invoke(false);
                return;
            }

            List<int> catalystIds = catalystDataByCharacter[i] != null
                ? catalystDataByCharacter[i].Select(c => (int)c.id).ToList()
                : new List<int>();

            entries.Add(new PresetCharacterEntry(characterDataArray[i].id, catalystIds));
        }

        var preset = new TeamPreset($"プリセット{presetSlot}", entries);
        string uid = LocalUser.GetOrCreateUid();

        FirestoreBridge.Instance.SavePreset(uid, presetSlot, preset.name, preset.ToFirestoreCharacterList(), onDone);
    }
    private void LoadPresetIntoEditor(int presetSlot)
    {
        string uid = LocalUser.GetOrCreateUid();

        FirestoreBridge.Instance.LoadPreset(uid, presetSlot, doc =>
        {
            // 一旦全枠クリア
            for (int i = 0; i < characterDataArray.Length; i++)
            {
                characterDataArray[i] = null;
                SetCharacterBoxSprite(i, defaultCharacterBoxSprite);
                ClearAllCatalystsFromCharacter(i);
            }

            TeamPreset loaded = TeamPreset.FromFirestoreDocument(doc);
            if (loaded == null || loaded.characters.Count == 0)
            {
                Debug.Log($"プリセット{presetSlot}は未保存のため空の状態を表示します");
                return;
            }

            for (int i = 0; i < loaded.characters.Count && i < characterDataArray.Length; i++)
            {
                var entry = loaded.characters[i];
                CharacterData data = CharacterRegistry.Instance.GetData(entry.charId);
                if (data == null)
                {
                    Debug.LogWarning($"CharacterId {entry.charId} が見つかりません");
                    continue;
                }

                characterDataArray[i] = data;
                SetCharacterBoxSprite(i, data.characterSprite);

                catalystDataByCharacter[i] = new List<CatalystData>();
                foreach (int catalystIdInt in entry.catalystIds)
                {
                    CatalystData catalystData = CatalystDataRegistry.Instance.GetData((CatalystId)catalystIdInt);
                    if (catalystData == null) continue;

                    catalystDataByCharacter[i].Add(catalystData);
                    AddCatalystBox(i, catalystData);
                }
            }
        });
    }

}
