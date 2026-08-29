using UnityEngine;

/// <summary>
/// ResultSceneのBGM。勝敗が確定した時点でどちらか一方を鳴らす。
/// SEではなくBGM扱い(AudioSettings.BgmVolumeに準拠、ループ再生)なので、DuelBgmControllerと同じ音量系統に属する。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ResultBgmController : MonoBehaviour
{
    [SerializeField] private AudioClip victoryBgm;
    [SerializeField] private AudioClip defeatBgm;

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
    }

    private void OnDestroy()
    {
        AudioSettings.OnVolumeChanged -= ApplyVolume;
    }

    /// <summary>ResultSceneUI.Start()から、勝敗が判明した時点で1回だけ呼ぶ</summary>
    public void Play(bool isVictory)
    {
        AudioClip clip = isVictory ? victoryBgm : defeatBgm;
        if (clip == null) return;

        audioSource.clip = clip;
        audioSource.Play();
    }

    private void ApplyVolume()
    {
        if (audioSource != null) audioSource.volume = AudioSettings.BgmVolume;
    }
}
