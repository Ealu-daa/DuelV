using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ResultSceneUI : MonoBehaviour
{
    [SerializeField] private Sprite victorySprite;
    [SerializeField] private Sprite defeatSprite;
    [SerializeField] private Image resultImage;
    [SerializeField] private TMP_Text baseEchoText;
    [SerializeField] private TMP_Text firstWinBonusText;
    [SerializeField] private TMP_Text totalEchoText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private FadeIn fadeIn;
    [SerializeField] private Button skipButton; // 画面いっぱいの透明ボタン推奨。アニメーション中だけアクティブにし、タップで全部即座に終わらせる
    [SerializeField] private ResultBgmController resultBgmController; // 勝敗BGM(SEではなくBGM扱い。未接続でもよい)

    [Header("カウントアップ演出")]
    [SerializeField] private float countUpDuration = 0.6f;
    [SerializeField] private float delayBetweenRows = 0.15f;

    [Header("アカウントレベルXP獲得")]
    [SerializeField] private TMP_Text accountLevelText;         // 例:「Lv.5」。アニメーション中、閾値を跨いだ瞬間に切り替わる
    [SerializeField] private TMP_Text accountXpProgressText;    // 例:「40/100 +25」。マスタリー行と同じ表記
    [SerializeField] private Image accountProgressFill;         // Image Type: Filled推奨
    [SerializeField] private float accountLevelAnimationDuration = 1f;

    [Header("マスタリーXP獲得")]
    [SerializeField] private Transform masteryResultContainer; // GridLayoutGroup等を付けた入れ物
    [SerializeField] private MasteryResultRowUI masteryRowPrefab;
    [SerializeField] private float masteryAnimationDurationPerCharacter = 1f;
    [SerializeField] private float delayBetweenMasteryRows = 0.2f;

    private readonly List<MasteryResultRowUI> spawnedMasteryRows = new List<MasteryResultRowUI>();
    private List<CharacterMasteryResult> pendingMasteryResults;
    private AccountLevelResult pendingAccountLevelResult;
    private EchoBreakdown pendingBreakdown;
    private Coroutine mainAnimationCoroutine;

#if UNITY_EDITOR
    [Header("エディタテスト用(本番では未使用)")]
    [SerializeField] private bool debugUseTestData = false;
    [SerializeField] private bool debugIsVictory = true;
    [SerializeField] private int debugEndHalfTurn = 25;
    [SerializeField] private List<CharacterId> debugMasteryCharacterIds = new List<CharacterId>(); // 空なら図鑑先頭2キャラを使う
    [SerializeField] private int debugMasteryXpGain = 20;
    [SerializeField] private int debugAccountXpGain = 20;
#endif

    private Action pendingCountUpAction;

    private void Start()
    {
        var data = BattleResultData.Pending;
#if UNITY_EDITOR
        if (data == null && debugUseTestData)
        {
            Debug.LogWarning("[ResultSceneUI] Pendingがnullのため、テスト用ダミーデータを使用します");
            data = new BattleResultData
            {
                isVictory = debugIsVictory,
                endHalfTurn = debugEndHalfTurn,
                masteryResults = BuildDebugMasteryResults(),
                accountLevelResult = BuildDebugAccountLevelResult()
            };
        }
#endif
        if (data == null)
        {
            Debug.LogError("BattleResultData.Pending is null");
            return;
        }

        resultImage.sprite = data.isVictory ? victorySprite : defeatSprite;
        if (resultBgmController != null) resultBgmController.Play(data.isVictory);
        PopulateMasteryResults(data.masteryResults);
        pendingAccountLevelResult = data.accountLevelResult;
        ApplyAccountXpState(pendingAccountLevelResult?.oldXp ?? AccountLevel.TotalXp, pendingAccountLevelResult?.oldXp ?? AccountLevel.TotalXp);

        baseEchoText.text = "+0";
        firstWinBonusText.text = "+0";
        totalEchoText.text = "+0 エコー";
        confirmButton.interactable = false;
        confirmButton.onClick.AddListener(OnConfirm);
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(OnSkipClicked);
            skipButton.gameObject.SetActive(false); // アニメーション開始と同時に有効化する
        }

        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            long currentEcho = 0;
            string lastWinDate = null;
            if (profile != null)
            {
                if (profile.TryGetValue("echo", out var e)) currentEcho = Convert.ToInt64(e);
                if (profile.TryGetValue("lastWinDate", out var d)) lastWinDate = d as string;
            }

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            bool isFirstWinToday = data.isVictory && lastWinDate != today;

            var breakdown = EchoRewardCalculator.Calculate(data, isFirstWinToday);
            pendingBreakdown = breakdown;

            long newTotal = currentEcho + breakdown.Total;
            string newLastWinDate = data.isVictory ? today : lastWinDate;

            pendingCountUpAction = () =>
            {
                mainAnimationCoroutine = StartCoroutine(PlayEchoAnimation(breakdown));
                // 画面上部などのEcho残高表示(EchoDisplayUI)のカウントアップも、この「+125」演出と
                // 同じタイミングで始まるようにする(先に保存だけ済ませてここでキャッシュを更新する)
                EchoWallet.SetBalance((int)newTotal);
                if (skipButton != null) skipButton.gameObject.SetActive(true);
            };
            if (fadeIn != null)
                fadeIn.OnFadeInComplete += pendingCountUpAction;
            else
                pendingCountUpAction();

            // Firestoreへの保存自体は演出を待たずバックグラウンドで進めてよい
            FirestoreBridge.Instance.SaveEchoResult(uid, (int)newTotal, newLastWinDate);
        });
    }

    private void OnDestroy()
    {
        if (fadeIn != null && pendingCountUpAction != null)
            fadeIn.OnFadeInComplete -= pendingCountUpAction;
    }

    // 行を並べて「試合前」の見た目で確定させておくだけ。アニメーション自体はEchoの合計演出が
    // 終わった後にPlayMasteryAnimations()から順番に呼ぶ
    private void PopulateMasteryResults(List<CharacterMasteryResult> results)
    {
        spawnedMasteryRows.Clear();
        pendingMasteryResults = results;

        if (masteryResultContainer == null || masteryRowPrefab == null) return;

        foreach (Transform child in masteryResultContainer)
            Destroy(child.gameObject);

        if (results == null) return;

        foreach (var result in results)
        {
            var row = Instantiate(masteryRowPrefab, masteryResultContainer);
            row.Initialize(result);
            spawnedMasteryRows.Add(row);
        }
    }

    // currentXp: 今アニメーションで指している累計XP(カウントアップ側の基準)
    // finalXp: 最終的に到達する累計XP(残りXP = finalXp - currentXp、カウントダウン側の基準)
    private void ApplyAccountXpState(int currentXp, int finalXp)
    {
        int level = 1 + currentXp / AccountLevel.XpPerLevel;
        int xpIntoLevel = currentXp % AccountLevel.XpPerLevel;
        int remaining = Mathf.Max(0, finalXp - currentXp);
        string remainingPart = remaining > 0 ? $" +{remaining}" : "";

        if (accountLevelText != null) accountLevelText.text = $"Lv.{level}";
        if (accountProgressFill != null) accountProgressFill.fillAmount = (float)xpIntoLevel / AccountLevel.XpPerLevel;
        if (accountXpProgressText != null) accountXpProgressText.text = $"{xpIntoLevel}/{AccountLevel.XpPerLevel}{remainingPart}";
    }

    // Echoの合計演出が終わった後に呼ぶ。アカウントレベルのバー・Lv・XPを試合前→試合後の値まで同時にアニメーションさせる
    private IEnumerator PlayAccountLevelAnimation()
    {
        if (pendingAccountLevelResult == null) yield break;

        int fromXp = pendingAccountLevelResult.oldXp;
        int toXp = pendingAccountLevelResult.newXp;

        if (toXp <= fromXp) { ApplyAccountXpState(toXp, toXp); yield break; }

        float elapsed = 0f;
        while (elapsed < accountLevelAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / accountLevelAnimationDuration);
            int currentXp = Mathf.RoundToInt(Mathf.Lerp(fromXp, toXp, t));
            ApplyAccountXpState(currentXp, toXp);
            yield return null;
        }

        ApplyAccountXpState(toXp, toXp);
    }

    // Echoの合計演出が終わった後に呼ぶ。マスタリー行を1体ずつ順番に(バー・Lv・区間内XPを同時に)アニメーションさせる
    private IEnumerator PlayMasteryAnimations()
    {
        if (pendingMasteryResults == null) yield break;

        int count = Mathf.Min(pendingMasteryResults.Count, spawnedMasteryRows.Count);
        for (int i = 0; i < count; i++)
        {
            yield return StartCoroutine(spawnedMasteryRows[i].PlayAnimation(pendingMasteryResults[i], masteryAnimationDurationPerCharacter));
            yield return new WaitForSeconds(delayBetweenMasteryRows);
        }
    }

    private IEnumerator PlayEchoAnimation(EchoBreakdown breakdown)
    {
        SeManager.StartCountLoop(); // エコー→アカウントレベル→マスタリーの一連の演出が終わるまで鳴らし続ける(停止はFinishAnimationSequence側で一元管理)

        yield return StartCoroutine(CountUpText(baseEchoText, breakdown.BaseEcho, countUpDuration, "+{0}"));
        yield return new WaitForSeconds(delayBetweenRows);
        yield return StartCoroutine(CountUpText(firstWinBonusText, breakdown.FirstWinBonus, countUpDuration, "+{0}"));
        yield return new WaitForSeconds(delayBetweenRows);
        yield return StartCoroutine(CountUpText(totalEchoText, breakdown.Total, countUpDuration, "+{0} エコー"));

        // Echoの合計演出が終わったら、続けてアカウントレベル→マスタリー(1体ずつ順番)の獲得演出を流す
        yield return StartCoroutine(PlayAccountLevelAnimation());
        yield return StartCoroutine(PlayMasteryAnimations());

        // 全部のアニメーションが終わって初めて「戻る」を押せるようにする
        FinishAnimationSequence();
    }

    // アニメーションが最後まで再生し終わった時、またはスキップされた時のどちらから来ても呼ばれる
    private void FinishAnimationSequence()
    {
        SeManager.StopCountLoop(); // 自然終了・スキップどちらから来てもここを通るので、ここで止めれば取りこぼさない
        mainAnimationCoroutine = null;
        confirmButton.interactable = true;
        if (skipButton != null) skipButton.gameObject.SetActive(false);
    }

    // skipButtonのOnClickから呼ぶ(Inspectorで自動的にAddListenerされる分と合わせて重複しないよう、
    // ここではAddListener経由のみで呼ばれる想定)
    private void OnSkipClicked()
    {
        // PlayEchoAnimation内で「yield return StartCoroutine(...)」と入れ子にしているので、
        // StopCoroutine(mainAnimationCoroutine)だけでは今まさに実行中の内側のコルーチン
        // (PlayMasteryAnimations・各行のPlayAnimation等)が止まらずに動き続けてしまう。
        // このスクリプトでは他に常駐コルーチンを使っていないため、StopAllCoroutines()で確実に止める
        StopAllCoroutines();

        // 全ての表示を最終状態まで一気に反映する
        baseEchoText.text = $"+{pendingBreakdown.BaseEcho}";
        firstWinBonusText.text = $"+{pendingBreakdown.FirstWinBonus}";
        totalEchoText.text = $"+{pendingBreakdown.Total} エコー";

        if (pendingAccountLevelResult != null)
            ApplyAccountXpState(pendingAccountLevelResult.newXp, pendingAccountLevelResult.newXp);

        if (pendingMasteryResults != null)
        {
            int count = Mathf.Min(pendingMasteryResults.Count, spawnedMasteryRows.Count);
            for (int i = 0; i < count; i++)
                spawnedMasteryRows[i].ApplyFinalState(pendingMasteryResults[i]);
        }

        FinishAnimationSequence();
    }

    private IEnumerator CountUpText(TMP_Text text, int targetValue, float duration, string format)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int current = Mathf.RoundToInt(Mathf.Lerp(0, targetValue, t));
            text.text = string.Format(format, current);
            yield return null;
        }
        text.text = string.Format(format, targetValue);
    }

    private void OnConfirm()
    {
        BattleResultData.Pending = null;
        SceneManager.LoadScene("MenuScene");
    }

#if UNITY_EDITOR
    // debugUseTestData用: 指定キャラ(未指定なら図鑑先頭2キャラ)へdebugMasteryXpGain分のダミー獲得を作る。
    // 実際のCharacterMastery上のXP(あれば)を起点にするので、本物のマスタリーデータと矛盾しない
    private List<CharacterMasteryResult> BuildDebugMasteryResults()
    {
        var ids = debugMasteryCharacterIds != null && debugMasteryCharacterIds.Count > 0
            ? debugMasteryCharacterIds
            : (CharacterRegistry.Instance != null
                ? CharacterRegistry.Instance.AllCharacters.Take(2).Select(d => d.id).ToList()
                : new List<CharacterId>());

        var results = new List<CharacterMasteryResult>();
        foreach (var id in ids)
        {
            int oldXp = CharacterMastery.IsLoaded ? CharacterMastery.GetXp(id) : 0;
            int newXp = oldXp + debugMasteryXpGain;

            results.Add(new CharacterMasteryResult
            {
                characterId = id,
                xpGained = debugMasteryXpGain,
                oldXp = oldXp,
                newXp = newXp,
                oldLevel = MasteryLevels.GetLevel(oldXp),
                newLevel = MasteryLevels.GetLevel(newXp)
            });
        }
        return results;
    }

    // debugUseTestData用: debugAccountXpGain分のダミー獲得を作る。
    // 実際のAccountLevel上のXP(あれば)を起点にするので、本物のデータと矛盾しない
    private AccountLevelResult BuildDebugAccountLevelResult()
    {
        int oldXp = AccountLevel.IsLoaded ? AccountLevel.TotalXp : 0;
        int newXp = oldXp + debugAccountXpGain;

        return new AccountLevelResult
        {
            xpGained = debugAccountXpGain,
            oldXp = oldXp,
            newXp = newXp,
            oldLevel = 1 + oldXp / AccountLevel.XpPerLevel,
            newLevel = 1 + newXp / AccountLevel.XpPerLevel
        };
    }
#endif
}