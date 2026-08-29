using System;
using UnityEngine;

/// <summary>
/// BGM/SEの音量設定。エコー等と違いアカウント(Firestore)ではなく端末側のPlayerPrefsに保存する
/// (音量は機種ごとの好みなので、アカウントに紐付ける必要がない)。
///
/// 現時点ではBGM/SE自体の再生システム(ロードマップ⑨⑩)がまだ無いので、ここでは値の保存/読込だけを行う。
/// BGM/SEを実装する際は、それぞれの再生元がOnVolumeChangedを購読するか、都度BgmVolume/SeVolumeを
/// 参照して音量に反映すること。
/// </summary>
public static class AudioSettings
{
    private const string KeyBgmVolume = "duelv_bgm_volume";
    private const string KeySeVolume = "duelv_se_volume";
    private const float DefaultVolume = 0.8f;

    public static bool IsLoaded { get; private set; }
    public static float BgmVolume { get; private set; } = DefaultVolume;
    public static float SeVolume { get; private set; } = DefaultVolume;

    /// <summary>音量が変わるたびに呼ばれる(Load完了時・SetXxxVolume呼び出し時)</summary>
    public static event Action OnVolumeChanged;

    public static void Load()
    {
        BgmVolume = PlayerPrefs.GetFloat(KeyBgmVolume, DefaultVolume);
        SeVolume = PlayerPrefs.GetFloat(KeySeVolume, DefaultVolume);
        IsLoaded = true;
        OnVolumeChanged?.Invoke();
    }

    public static void SetBgmVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KeyBgmVolume, BgmVolume);
        PlayerPrefs.Save();
        OnVolumeChanged?.Invoke();
    }

    public static void SetSeVolume(float volume)
    {
        SeVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KeySeVolume, SeVolume);
        PlayerPrefs.Save();
        OnVolumeChanged?.Invoke();
    }
}
