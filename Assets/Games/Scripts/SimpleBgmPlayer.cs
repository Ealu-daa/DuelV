using UnityEngine;

/// <summary>
/// シンプルに1シーンにつき1曲だけループ再生するBGM。DuelScene(ターン数で切り替わる)やResultScene(勝敗で
/// 切り替わる)のような複雑な切り替えロジックが要らない各シーン(Menu/Zukan/OnlineMatch/Hensei/Profile/
/// Settings等)向け。AudioClipを1つ設定して置くだけで、シーンに入った瞬間からループ再生される。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SimpleBgmPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip bgm;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        ApplyVolume();
        AudioSettings.OnVolumeChanged += ApplyVolume;

        if (bgm != null)
        {
            audioSource.clip = bgm;
            audioSource.Play();
        }
    }

    private void OnDestroy()
    {
        AudioSettings.OnVolumeChanged -= ApplyVolume;
    }

    private void ApplyVolume()
    {
        if (audioSource != null) audioSource.volume = AudioSettings.BgmVolume;
    }
}
