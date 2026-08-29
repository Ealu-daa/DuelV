using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System;

public class CharacterPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image characterImage;
    [SerializeField] private GameObject characterImageObj;

    [SerializeField] private SpriteRenderer effectRenderer; // 子オブジェクトのSpriteRenderer
    [SerializeField] private Animator effectAnimator;

    [SerializeField] private bool isPlayerTeam;

    public enum PanelType { Forward, Backup }
    [SerializeField] private PanelType panelType;
    [SerializeField] private int characterIndex; // FWなら0/1、BKなら0/1/2

    private CharacterState characterState;

    [SerializeField] private Transform effectIconContainer; // GridLayoutGroupをアタッチ
    [SerializeField] private StatusEffectIcon iconPrefab;
    [SerializeField] private bool isCharactersPanel = false;
    [SerializeField] private CharactersInfo charactersInfo;

    [Header("フェード演出設定")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeHoldDuration = 1f;
    [SerializeField] private float fadeOutDuration = 0.2f;

    [Header("ULT演出")]
    [SerializeField] private SpriteRenderer characterRenderer; // ULTスライド演出用の立ち絵
    [SerializeField] private float ultSlideInDuration = 0.4f;
    [SerializeField] private float ultSlideOutDuration = 0.3f;
    [SerializeField] private float ultFadeStartDelay = 0.25f;
    [SerializeField] private float ultHoldBeforeSlideOut = 0.3f;

    private const float PlayerStartX = 1200f;
    private const float PlayerHoldX = -745f;
    private const float EnemyStartX = -1200f;
    private const float EnemyHoldX = 745f;



    private float lerpSpeed = 2f;

    private float displayedHP;
    private int targetHP;
    private bool isLerping = false;

    public event Action OnDisplayCaughtUp; // Lerpが目標値に追いついたら発火

    private void Start()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnTeamsReady += HandleTeamsReady;
    }

    private void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnTeamsReady -= HandleTeamsReady;
    }

    private void HandleTeamsReady()
    {
        var team = isPlayerTeam ? BattleManager.Instance.PlayerTeam : BattleManager.Instance.EnemyTeam;
        CharacterState state = panelType == PanelType.Forward
            ? team.forwards[characterIndex]
            : team.backups[characterIndex];
        Bind(state);

        if(isCharactersPanel)
            charactersInfo.StartAfterBind();
    }

    void Update()
    {
        if (!isLerping) return;

        displayedHP = Mathf.Lerp(displayedHP, targetHP, Time.deltaTime * lerpSpeed);

        if (Mathf.Abs(displayedHP - targetHP) < 0.5f)
        {
            displayedHP = targetHP;
            isLerping = false;
            RefreshBarInstant();
            OnDisplayCaughtUp?.Invoke(); // 追いつき終わったことを外部に知らせる
            return;
        }

        RefreshBarInstant();
    }

    public void Bind(CharacterState state)
    {
        if (characterState != null)
        {
            characterState.OnStateChanged -= UpdateDisplay;
        }

        characterState = state;

        displayedHP = characterState.currentHP;
        targetHP = characterState.currentHP;
        isLerping = false;


        UpdateDisplay();
        characterState.OnStateChanged += UpdateDisplay;
    }


    private void UpdateDisplay()
    {

        if (PanelType.Forward == panelType && !isCharactersPanel)
        {
            characterImage.sprite = characterState.GetDisplaySprite();
            if(characterState.IsDefeated)
            {
                characterImageObj.SetActive(false);
            }
            else
            {
                characterImageObj.SetActive(true);
            }
        }
        nameText.text = characterState.data.characterName;

        targetHP = characterState.currentHP;
        isLerping = true;

        RefreshEffectIcons(characterState, TurnManager.Instance.currentHalfTurn);
    }

    public void RefreshEffectIcons(CharacterState character, int currentHalfTurn)
    {
        foreach (Transform child in effectIconContainer)
            Destroy(child.gameObject);

        bool enableTooltip = panelType == PanelType.Forward;

        foreach (var effect in character.activeEffects)
        {
            var iconUI = Instantiate(iconPrefab, effectIconContainer);
            if(isCharactersPanel)
                iconUI.SetupCharacters(effect, currentHalfTurn, enableTooltip, charactersInfo);
            else
                iconUI.Setup(effect, currentHalfTurn, enableTooltip);
        }
    }

    private void RefreshBarInstant()
    {
        int max = characterState.currentMaxHP;
        hpBarFill.fillAmount = max > 0 ? displayedHP / max : 0f;
        hpText.text = $"{Mathf.RoundToInt(displayedHP)}/{max}";
    }

    public void PlayAttackEffect(RuntimeAnimatorController controller, float duration, Action onFinished)
    {
        Color c = effectRenderer.color;
        c.a = 1f;
        effectRenderer.color = c; 

        effectRenderer.gameObject.SetActive(true);
        effectAnimator.enabled = true;
        effectAnimator.runtimeAnimatorController = controller;
        effectAnimator.Play(0, -1, 0f);

        StartCoroutine(WaitForClipEnd(duration, onFinished));
    }

    private IEnumerator WaitForClipEnd(float duration, Action onFinished)
    {
        yield return new WaitForSeconds(duration);
        effectAnimator.enabled = false;
        effectRenderer.gameObject.SetActive(false);
        onFinished?.Invoke();
    }
    public void CharacterButtonClicked()
    {
        charactersInfo.CharacterSelected(characterState.data.characterName, characterState.currentAttack, characterState.currentDefense, characterState.data.skillName, characterState.data.skillDescription, characterState.data.ultName, characterState.data.ultDescription, characterState.catalysts);
    }
    public void PlayFadeEffect(Sprite sprite, Action onFinished)
    {
        effectAnimator.enabled = false;
        effectRenderer.gameObject.SetActive(true);
        effectRenderer.sprite = sprite;
        StartCoroutine(FadeRoutine(onFinished));
    }

    private IEnumerator FadeRoutine(Action onFinished)
    {
        yield return StartCoroutine(FadeAlpha(0f, 1f, fadeInDuration));
        yield return new WaitForSeconds(fadeHoldDuration);
        yield return StartCoroutine(FadeAlpha(1f, 0f, fadeOutDuration));

        effectRenderer.gameObject.SetActive(false);
        onFinished?.Invoke();
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = effectRenderer.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(from, to, t);
            effectRenderer.color = c;
            yield return null;
        }

        c.a = to;
        effectRenderer.color = c;
    }
    // 汎用フェードイン単体
    public void PlayFadeIn(Sprite sprite, Action onFinished)
    {
        effectRenderer.gameObject.SetActive(true);
        effectRenderer.sprite = sprite;
        StartCoroutine(FadeAlpha(0f, 1f, fadeInDuration, onFinished));
    }

    // 汎用フェードアウト単体
    public void PlayFadeOut(Action onFinished)
    {
        StartCoroutine(FadeAlphaOutThenHide(onFinished));
    }

    private IEnumerator FadeAlphaOutThenHide(Action onFinished)
    {
        yield return StartCoroutine(FadeAlpha(1f, 0f, fadeOutDuration));
        effectRenderer.gameObject.SetActive(false);
        onFinished?.Invoke();
    }

    // FadeAlphaにコールバックを持たせるオーバーロード
    private IEnumerator FadeAlpha(float from, float to, float duration, Action onFinished = null)
    {
        float elapsed = 0f;
        Color c = effectRenderer.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(from, to, t);
            effectRenderer.color = c;
            yield return null;
        }
        c.a = to;
        effectRenderer.color = c;
        onFinished?.Invoke();
    }
    public void PlayUltEffect(Sprite characterSprite, Sprite ultFadeSprite, List<CharacterPanelUI> fadeTargetPanels, Action onFinished)
    {
        StartCoroutine(UltRoutine(characterSprite, ultFadeSprite, fadeTargetPanels, onFinished));
    }

    private IEnumerator UltRoutine(Sprite characterSprite, Sprite ultFadeSprite, List<CharacterPanelUI> fadeTargetPanels, Action onFinished)
    {
        characterRenderer.sprite = characterSprite;
        characterRenderer.gameObject.SetActive(true);

        Vector3 basePos = characterRenderer.transform.localPosition;
        float startX = isPlayerTeam ? PlayerStartX : EnemyStartX;
        float holdX = isPlayerTeam ? PlayerHoldX : EnemyHoldX;
        Vector3 startPos = new Vector3(startX, basePos.y, basePos.z);
        Vector3 holdPos = new Vector3(holdX, basePos.y, basePos.z);

        characterRenderer.transform.localPosition = startPos;

        // スライドイン開始(完了を待つ)
        Coroutine slideIn = StartCoroutine(SlideRoutine(startPos, holdPos, ultSlideInDuration, EaseOutCubic));

        // 少し遅れてフェード開始(完了を待たず、裏で独立して進行させる)
        yield return new WaitForSeconds(ultFadeStartDelay);
        foreach (var panel in fadeTargetPanels)
        {
            panel.PlayFadeEffect(ultFadeSprite, null); // フェードイン→fadeHoldDuration→フェードアウトを裏で実行
        }

        // スライドイン完了を待つ
        yield return slideIn;

        // スライドイン完了を起点に、ULT専用のキープ時間だけ待つ(フェードの状態は無視)
        yield return new WaitForSeconds(ultHoldBeforeSlideOut);

        // スライドアウト退場
        yield return StartCoroutine(SlideRoutine(holdPos, startPos, ultSlideOutDuration, EaseInCubic));

        // 通常画面に復帰
        characterRenderer.transform.localPosition = basePos;
        characterRenderer.gameObject.SetActive(false);

        onFinished?.Invoke();
    }
    private IEnumerator SlideRoutine(Vector3 from, Vector3 to, float duration, Func<float, float> easing, Action onFinished = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easing(t);
            characterRenderer.transform.localPosition = Vector3.Lerp(from, to, eased);
            yield return null;
        }
        characterRenderer.transform.localPosition = to;
        onFinished?.Invoke();
    }

    private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private float EaseInCubic(float t) => t * t * t;
}
