using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 図鑑シーンの画面遷移を管理する。トップ→(キャラ一覧→キャラ詳細)/(カタリスト一覧→カタリスト詳細)。
/// 各詳細パネルはそれぞれの一覧パネルの子(一覧の上に被せて出す構成)なので、
/// 一覧を閉じる時だけ親ごと消す。詳細を開閉する時は一覧はアクティブなまま、詳細だけ出し入れする。
///
/// 「戻る」ボタンは全画面で1つのプレハブ/OnClickを使い回せるように、今どの画面にいるかを
/// currentScreenで覚えておいて OnBackButtonClicked() 側で行き先を判断する方式にしている。
/// </summary>
public class ZukanUIController : MonoBehaviour
{
    private enum ZukanScreen { Top, CharacterList, CharacterDetail, CatalystList, CatalystDetail }
    private ZukanScreen currentScreen = ZukanScreen.Top;

    [SerializeField] private GameObject topPanel;

    [Header("キャラ")]
    [SerializeField] private GameObject characterListPanel;
    [SerializeField] private GameObject characterDetailPanel; // characterListPanelの子
    [SerializeField] private CharacterZukanListUI characterListUI;
    [SerializeField] private CharacterZukanDetailUI characterDetailUI;

    [Header("カタリスト")]
    [SerializeField] private GameObject catalystListPanel;
    [SerializeField] private GameObject catalystDetailPanel; // catalystListPanelの子
    [SerializeField] private CatalystZukanListUI catalystListUI;
    [SerializeField] private CatalystZukanDetailUI catalystDetailUI;

    private void Start()
    {
        ShowTop();
        PlayerCollection.Load(); // 所持状況は裏で読み込んでおく(一覧を開く頃には大抵間に合う)
        CharacterMastery.Load(); // マスタリー進捗も同様に裏で読み込んでおく
    }

    public void ShowTop()
    {
        currentScreen = ZukanScreen.Top;
        topPanel.SetActive(true);
        characterListPanel.SetActive(false); // 子のcharacterDetailPanelも連動して非表示になる
        catalystListPanel.SetActive(false);  // 子のcatalystDetailPanelも連動して非表示になる
    }

    // トップの「キャラ図鑑」ボタンから呼ぶ
    public void OnCharacterZukanButtonClicked()
    {
        currentScreen = ZukanScreen.CharacterList;
        topPanel.SetActive(false);
        characterListPanel.SetActive(true);
        characterDetailPanel.SetActive(false); // 一覧を開き直した時は詳細を閉じておく

        if (PlayerCollection.IsLoaded) characterListUI.Refresh();
        else PlayerCollection.Load(characterListUI.Refresh);
    }

    // キャラ一覧のグリッド項目から呼ぶ
    public void ShowCharacterDetail(CharacterData data)
    {
        currentScreen = ZukanScreen.CharacterDetail;
        // characterListPanelは親なのでアクティブなまま。詳細だけ上に被せて出す
        characterDetailPanel.SetActive(true);
        characterDetailUI.Show(data);
    }

    // 詳細画面で購入が成立した時に呼ぶ。裏に隠れている一覧のロック表示をその場で更新する
    public void RefreshCharacterList()
    {
        characterListUI.Refresh();
    }

    // トップの「カタリスト図鑑」ボタンから呼ぶ
    public void OnCatalystZukanButtonClicked()
    {
        currentScreen = ZukanScreen.CatalystList;
        topPanel.SetActive(false);
        catalystListPanel.SetActive(true);
        catalystDetailPanel.SetActive(false); // 一覧を開き直した時は詳細を閉じておく

        if (PlayerCollection.IsLoaded) catalystListUI.Refresh();
        else PlayerCollection.Load(catalystListUI.Refresh);
    }

    // カタリスト一覧のグリッド項目から呼ぶ
    public void ShowCatalystDetail(CatalystData data)
    {
        currentScreen = ZukanScreen.CatalystDetail;
        // catalystListPanelは親なのでアクティブなまま。詳細だけ上に被せて出す
        catalystDetailPanel.SetActive(true);
        catalystDetailUI.Show(data);
    }

    // 詳細画面で購入が成立した時に呼ぶ。裏に隠れている一覧のロック表示をその場で更新する
    public void RefreshCatalystList()
    {
        catalystListUI.Refresh();
    }

    // 「戻る」ボタン共通のOnClickから呼ぶ(どの画面でも同じボタン・同じメソッドでOK)
    public void OnBackButtonClicked()
    {
        switch (currentScreen)
        {
            case ZukanScreen.Top:
                SceneManager.LoadScene("MenuScene");
                break;
            case ZukanScreen.CharacterList:
            case ZukanScreen.CatalystList:
                ShowTop();
                break;
            case ZukanScreen.CharacterDetail:
            case ZukanScreen.CatalystDetail:
                ShowTop(); // 一覧を経由せず、詳細から直接トップへ
                break;
        }
    }
}
