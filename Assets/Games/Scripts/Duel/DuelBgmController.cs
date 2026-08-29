using UnityEngine;

/// <summary>
/// DuelScene中、ターン数に応じてBGMを切り替える(序盤/中盤/終盤の3段階、1曲固定ではない)。
///
/// BGMは「シリーズ」単位で複数セット用意しておき、対戦開始時にランダムで1シリーズだけ選ぶ
/// (例: シリーズ0/1/2それぞれに序盤/中盤/終盤の3曲があり、対戦ごとにどのシリーズを使うかが変わる)。
///
/// 切り替えタイミングは「今の曲が最後まで鳴り終わってから」(曲の途中でぶった切らない)。
/// 各AudioClipはloop=falseで1回再生しきる想定で、鳴り終わるたびにその時点のフェーズの曲を
/// 改めて選び直して再生する。同じフェーズが続いている間は同じ曲を選び直すだけなので、
/// 結果的に途切れず流れ続ける(=見た目上ループする)。フェーズが進んでいれば次の曲に切り替わる。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DuelBgmController : MonoBehaviour
{
    [System.Serializable]
    public class BgmSeries
    {
        public AudioClip earlyGameBgm; // 序盤
        public AudioClip midGameBgm;   // 中盤
        public AudioClip lateGameBgm;  // 終盤
    }

    [Header("BGMシリーズ(対戦開始時にこの中からランダムで1つ選ばれる)")]
    [SerializeField] private BgmSeries[] bgmSeriesList = new BgmSeries[3];

    [Header("フェーズが切り替わるターン数(このターン数に達したら該当フェーズへ)")]
    [SerializeField] private int midGameStartTurn = 4;
    [SerializeField] private int lateGameStartTurn = 8;

    private AudioSource audioSource;
    private BgmSeries selectedSeries;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false; // 曲の終わりを自前で検知して繋ぐため、Unity側のループ機能は使わない
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        selectedSeries = PickRandomSeries();

        ApplyVolume();
        AudioSettings.OnVolumeChanged += ApplyVolume;

        PlayClipForCurrentPhase();
    }

    private void OnDestroy()
    {
        AudioSettings.OnVolumeChanged -= ApplyVolume;
    }

    private void Update()
    {
        // 曲が鳴り終わったタイミングでだけフェーズを再評価する(曲の途中では切り替えない)
        if (!audioSource.isPlaying)
            PlayClipForCurrentPhase();
    }

    private BgmSeries PickRandomSeries()
    {
        if (bgmSeriesList == null || bgmSeriesList.Length == 0) return null;
        return bgmSeriesList[Random.Range(0, bgmSeriesList.Length)];
    }

    private void PlayClipForCurrentPhase()
    {
        AudioClip target = GetClipForCurrentPhase();
        if (target == null) return; // 該当フェーズの曲が未設定ならそのまま無音(Editorで設定するまでの暫定)

        audioSource.clip = target;
        audioSource.Play();
    }

    // 選ばれたシリーズの中で、目的のフェーズの曲が未設定の場合は手前のフェーズの曲へフォールバックする
    // (終盤用だけ用意できていない、等の設定漏れでも無音にならないように)
    private AudioClip GetClipForCurrentPhase()
    {
        if (selectedSeries == null) return null;

        int turn = TurnManager.Instance != null ? TurnManager.Instance.currentTurnNumber : 1;

        if (turn >= lateGameStartTurn && selectedSeries.lateGameBgm != null) return selectedSeries.lateGameBgm;
        if (turn >= midGameStartTurn && selectedSeries.midGameBgm != null) return selectedSeries.midGameBgm;
        if (selectedSeries.earlyGameBgm != null) return selectedSeries.earlyGameBgm;

        return selectedSeries.midGameBgm != null ? selectedSeries.midGameBgm : selectedSeries.lateGameBgm;
    }

    private void ApplyVolume()
    {
        if (audioSource != null) audioSource.volume = AudioSettings.BgmVolume;
    }
}
