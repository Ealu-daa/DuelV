using System.Collections;
using TMPro;
using UnityEngine;
public class TurnBanner : MonoBehaviour
{
    [SerializeField] private RectTransform bannerRect;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private FadeIn fadeIn;
    [SerializeField] private float offScreenX = 600f;
    [SerializeField] private float onScreenX = 0f;
    [SerializeField] private float slideInDuration = 0.35f;
    [SerializeField] private float holdDuration = 1.0f;
    [SerializeField] private float slideOutDuration = 0.35f;

    [Header("イージング")]
    [SerializeField] private AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve slideOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine currentRoutine;
    private void Start()
    {
        bannerRect.anchoredPosition = new Vector2(offScreenX, bannerRect.anchoredPosition.y);
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnHalfTurnChanged += ShowTurn;
        if (fadeIn != null)
            fadeIn.OnFadeInComplete += ShowInitialTurn;
    }
    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnHalfTurnChanged -= ShowTurn;
        if (fadeIn != null)
            fadeIn.OnFadeInComplete -= ShowInitialTurn;
    }
    private void ShowInitialTurn()
    {
        ShowTurn(TurnManager.Instance.currentTurnNumber, TurnManager.Instance.isPlayerTurnNow);
    }
    public void ShowTurn(int turnNumber, bool isPlayerTurn)
    {
        SeManager.PlayTurnStart();

        if (isPlayerTurn)
        {
            turnText.text = $"Turn {turnNumber} - you";
        }
        else
        {
            turnText.text = $"Turn {turnNumber} - enemy";
        }
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(SlideRoutine());
    }
    private IEnumerator SlideRoutine()
    {
        float y = bannerRect.anchoredPosition.y;
        float t = 0f;
        while (t < slideInDuration)
        {
            t += Time.deltaTime;
            float eased = slideInCurve.Evaluate(t / slideInDuration);
            float x = Mathf.LerpUnclamped(offScreenX, onScreenX, eased);
            bannerRect.anchoredPosition = new Vector2(x, y);
            yield return null;
        }
        bannerRect.anchoredPosition = new Vector2(onScreenX, y);
        yield return new WaitForSeconds(holdDuration);
        t = 0f;
        while (t < slideOutDuration)
        {
            t += Time.deltaTime;
            float eased = slideOutCurve.Evaluate(t / slideOutDuration);
            float x = Mathf.LerpUnclamped(onScreenX, -offScreenX, eased);
            bannerRect.anchoredPosition = new Vector2(x, y);
            yield return null;
        }
        bannerRect.anchoredPosition = new Vector2(-offScreenX, y);
    }
}