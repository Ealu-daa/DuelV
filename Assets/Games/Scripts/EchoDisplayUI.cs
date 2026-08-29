using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Echo残高を表示するコンポーネント。DuelScene/HenseiScene以外の各シーン(MenuScene・ZukanScene・
/// OnlineMatchScene・ResultScene・ProfileScene・SettingsScene等)に配置する。
///
/// 有効になった直後(シーンに入った瞬間)は即座に確定表示、それ以降にEchoWallet.Balanceが変わったら
/// (購入・対戦報酬など)表示中の値からカウントアップ/カウントダウンでアニメーションする。
/// </summary>
public class EchoDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text echoText;
    [SerializeField] private float countAnimationDuration = 0.5f;

    private int displayedValue;
    private bool hasDisplayedOnce;
    private Coroutine countRoutine;

    private void OnEnable()
    {
        // Load()完了時もOnBalanceChangedが発火するので、先に購読してから呼ぶだけでよい(二重呼び出し防止)
        EchoWallet.OnBalanceChanged += HandleBalanceChanged;
        EchoWallet.Load();
    }

    private void OnDisable()
    {
        EchoWallet.OnBalanceChanged -= HandleBalanceChanged;
    }

    private void HandleBalanceChanged()
    {
        int targetValue = EchoWallet.Balance;

        if (!hasDisplayedOnce)
        {
            // シーンに入った直後の初回表示はアニメーションさせず即座に確定させる
            hasDisplayedOnce = true;
            displayedValue = targetValue;
            ApplyDisplay(displayedValue);
            return;
        }

        if (displayedValue == targetValue) return;

        if (countRoutine != null) StopCoroutine(countRoutine);
        countRoutine = StartCoroutine(CountTo(targetValue));
    }

    private IEnumerator CountTo(int targetValue)
    {
        int fromValue = displayedValue;
        float elapsed = 0f;

        SeManager.StartCountLoop();

        while (elapsed < countAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / countAnimationDuration);
            displayedValue = Mathf.RoundToInt(Mathf.Lerp(fromValue, targetValue, t));
            ApplyDisplay(displayedValue);
            yield return null;
        }

        displayedValue = targetValue;
        ApplyDisplay(displayedValue);
        countRoutine = null;
        SeManager.StopCountLoop();
    }

    private void ApplyDisplay(int value)
    {
        if (echoText != null) echoText.text = value.ToString("N0");
    }
}
