using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 設定画面。BGM/SE音量調整とログアウト。MenuSceneから遷移してくる。
///
/// 音量調整は値の保存/読込のみ(AudioSettings)を行う。BGM/SE自体の再生システムは
/// ロードマップ⑨⑩でまだ未実装なので、今はスライダーを動かしても実際の音は変わらない。
/// </summary>
public class SettingsSceneUI : MonoBehaviour
{
    [Header("音量")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider seVolumeSlider;

    [Header("ログアウト")]
    [SerializeField] private GameObject logoutConfirmPanel; // 「本当にログアウトしますか？」の確認パネル

    private void Start()
    {
        AudioSettings.Load();

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(AudioSettings.BgmVolume);
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (seVolumeSlider != null)
        {
            seVolumeSlider.SetValueWithoutNotify(AudioSettings.SeVolume);
            seVolumeSlider.onValueChanged.AddListener(OnSeVolumeChanged);
        }

        if (logoutConfirmPanel != null) logoutConfirmPanel.SetActive(false);
    }

    private void OnBgmVolumeChanged(float value) => AudioSettings.SetBgmVolume(value);
    private void OnSeVolumeChanged(float value) => AudioSettings.SetSeVolume(value);

    // ---------------- ログアウト ----------------

    // ログアウトボタンのOnClickから呼ぶ。即座にはログアウトせず、確認パネルを開くだけ
    public void OnLogoutButtonClicked()
    {
        if (logoutConfirmPanel != null) logoutConfirmPanel.SetActive(true);
    }

    // 確認パネルの「はい」ボタンから呼ぶ
    public void OnLogoutConfirmed()
    {
        if (logoutConfirmPanel != null) logoutConfirmPanel.SetActive(false);
        FirebaseAuthBridge.Instance.SignOut();
        SceneManager.LoadScene("MenuScene"); // MenuScene起動時に新しい匿名セッションが作られる
    }

    // 確認パネルの「いいえ」ボタンから呼ぶ
    public void OnLogoutCancelled()
    {
        if (logoutConfirmPanel != null) logoutConfirmPanel.SetActive(false);
    }

    // ---------------- 戻る ----------------

    // 「戻る」ボタンのOnClickから呼ぶ
    public void OnBackClicked()
    {
        SceneManager.LoadScene("MenuScene");
    }
}
