using UnityEngine;

/// <summary>
/// 効果音(SE)の再生窓口。SEを使いたい各シーン(DuelScene/ResultScene/MenuScene等)に1つずつ配置するシングルトン。
/// PlayOneShotで鳴らすため、複数のSEが同時に発生しても(例: 全体攻撃で複数キャラに同時ヒット)
/// 重なって再生されるだけで、後勝ちで前の音が止まったりはしない。
///
/// シーンごとに関係する枠だけ設定すればいい(使わない枠は未設定のままでOK、Play系はnullを安全に無視する)。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SeManager : MonoBehaviour
{
    public static SeManager Instance { get; private set; }

    [Header("共通SE(単発)")]
    [SerializeField] private AudioClip buttonClickSe;       // ボタン操作全般(ButtonClickSoundから鳴らす)
    [SerializeField] private AudioClip turnStartSe;         // ターン開始バナー表示時
    [SerializeField] private AudioClip characterDefeatedSe; // キャラが戦闘不能になった時
    [SerializeField] private AudioClip catalystUseSe;       // カタリスト使用時
    [SerializeField] private AudioClip swapSe;              // 交代
    [SerializeField] private AudioClip defenseSe;           // 防御
    [SerializeField] private AudioClip purchaseSe;          // 購入確定(Zukan/ProfileScene等の購入モーダル)
    [SerializeField] private AudioClip revealSe;            // HenseiScene: お披露目演出開始時

    [Header("カウントアップ演出中のループSE(チック音)")]
    [SerializeField] private AudioClip countLoopSe; // ResultSceneのエコー/レベル/マスタリー演出、EchoDisplayUIの残高カウント中に鳴らし続ける

    private AudioSource audioSource;   // 単発SE(PlayOneShot)用
    private AudioSource loopAudioSource; // カウントアップ中のループSE専用(単発SEと混ざらないよう別チャンネル)

    private void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        loopAudioSource = gameObject.AddComponent<AudioSource>();
        loopAudioSource.playOnAwake = false;
        loopAudioSource.loop = true;
    }

    /// <summary>どこからでも呼べる再生ショートカット。clipが未設定でもInstanceが無くても安全に無視する</summary>
    public static void Play(AudioClip clip)
    {
        if (clip == null || Instance == null || Instance.audioSource == null) return;
        Instance.audioSource.PlayOneShot(clip, AudioSettings.SeVolume);
    }

    public static void PlayButtonClick() => Play(Instance != null ? Instance.buttonClickSe : null);
    public static void PlayTurnStart() => Play(Instance != null ? Instance.turnStartSe : null);
    public static void PlayCharacterDefeated() => Play(Instance != null ? Instance.characterDefeatedSe : null);
    public static void PlayCatalystUse() => Play(Instance != null ? Instance.catalystUseSe : null);
    public static void PlaySwap() => Play(Instance != null ? Instance.swapSe : null);
    public static void PlayDefense() => Play(Instance != null ? Instance.defenseSe : null);
    public static void PlayPurchase() => Play(Instance != null ? Instance.purchaseSe : null);
    public static void PlayReveal() => Play(Instance != null ? Instance.revealSe : null);

    /// <summary>カウントアップ演出の開始時に呼ぶ。既に鳴っていれば何もしない(区間をまたいで連続で呼んでも途切れない)</summary>
    public static void StartCountLoop()
    {
        if (Instance == null || Instance.loopAudioSource == null || Instance.countLoopSe == null) return;
        if (Instance.loopAudioSource.isPlaying) return;

        Instance.loopAudioSource.clip = Instance.countLoopSe;
        Instance.loopAudioSource.volume = AudioSettings.SeVolume;
        Instance.loopAudioSource.Play();
    }

    /// <summary>カウントアップ演出が終わった(自然終了・スキップどちらでも)ら呼ぶ</summary>
    public static void StopCountLoop()
    {
        if (Instance == null || Instance.loopAudioSource == null) return;
        Instance.loopAudioSource.Stop();
    }
}
