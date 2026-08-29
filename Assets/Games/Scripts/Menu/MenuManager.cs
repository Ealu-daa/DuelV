using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Google連携表示(未連携なら左、連携済みなら右を表示)")]
    [SerializeField] private GameObject googleLinkButtonObj;
    [SerializeField] private GameObject googleLinkedObj;
    [SerializeField] private TMP_Text googleLinkedEmailText; // 未接続でもよい

    [Header("ミッションポップアップ")]
    [SerializeField] private GameObject missionPanel; // 中にMissionListUIを配置しておく(開くたびに自動で最新状態に更新される)

    // 「ミッション」ボタンのOnClickから呼ぶ
    public void OnMissionButtonClicked()
    {
        if (missionPanel != null) missionPanel.SetActive(true);
    }

    // ポップアップの「閉じる」ボタンのOnClickから呼ぶ
    public void OnCloseMissionPanelClicked()
    {
        if (missionPanel != null) missionPanel.SetActive(false);
    }

    public void OnCpuBattleButtonClicked()
    {
        SceneManager.LoadScene("OnlineMatchScene");
    }

    public void OnZukanButtonClicked()
    {
        SceneManager.LoadScene("ZukanScene");
    }

    public void OnProfileButtonClicked()
    {
        SceneManager.LoadScene("ProfileScene");
    }

    public void OnSettingsButtonClicked()
    {
        SceneManager.LoadScene("SettingsScene");
    }

    // MenuSceneから直接編成を見に行く(オンライン対戦を介さない)ルート
    public void OnHenseiButtonClicked()
    {
        Hensei.EnteredFromMenu = true;
        SceneManager.LoadScene("HenseiScene");
    }

    private void Start()
    {
        FirebaseAuthBridge.Instance.OnGoogleLinked += OnGoogleLinked;

        // MenuSceneがアプリの起点なので、ここでサインインを済ませてから他の画面に進む
        FirebaseAuthBridge.Instance.EnsureSignedIn(success =>
        {
            if (!success)
            {
                Debug.LogError("[MenuManager] サインインに失敗しました");
                return;
            }

            // リロード等で中断された進行中のオンライン対戦があれば、メニューを出さずそのままDuelSceneへ復帰する
            RoomManager.Instance.CheckForActiveMatch(hasActiveMatch =>
            {
                if (hasActiveMatch)
                {
                    SceneManager.LoadScene("DuelScene");
                    return;
                }

                RefreshGoogleLinkDisplay();
                CheckDailyLoginMission();
            });
        });
    }

    // デイリーログインミッションの判定。PlayerProfile→MissionProgressの順に読み込んでから、
    // 称号報酬の付与に必要なPlayerProfileが確実に揃った状態でCompleteAndGrantを呼ぶ
    private void CheckDailyLoginMission()
    {
        PlayerProfile.Load(() =>
        {
            MissionProgress.Load(() =>
            {
                var loginMission = MissionRegistry.Instance != null ? MissionRegistry.Instance.GetData(MissionId.DailyLogin) : null;
                if (loginMission != null) MissionProgress.CompleteAndGrant(loginMission);
            });
        });
    }

    private void OnDestroy()
    {
        if (FirebaseAuthBridge.Instance != null)
            FirebaseAuthBridge.Instance.OnGoogleLinked -= OnGoogleLinked;
    }

    // Googleリンクが成立した瞬間に呼ばれる(そのままボタン表示を切り替える)
    private void OnGoogleLinked(string email)
    {
        RefreshGoogleLinkDisplay(email);
    }

    private void RefreshGoogleLinkDisplay(string email = null)
    {
        bool linked = FirebaseAuthBridge.Instance.IsLinkedWithGoogle;

        if (googleLinkButtonObj != null) googleLinkButtonObj.SetActive(!linked);
        if (googleLinkedObj != null) googleLinkedObj.SetActive(linked);
        if (linked && googleLinkedEmailText != null && !string.IsNullOrEmpty(email))
            googleLinkedEmailText.text = email;
    }

}