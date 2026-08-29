using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UltGaugeUI : MonoBehaviour
{
    [SerializeField] private RectTransform origin;
    [SerializeField] private GameObject pipPrefab;
    [SerializeField] private Sprite pipNoneSprite;
    [SerializeField] private Sprite pipFullSprite;
    [SerializeField] private Sprite pipNoneWhiteSprite;
    [SerializeField] private Sprite pipFullWhiteSprite;
    [SerializeField] private float pipSpacing = 35f;
    [SerializeField] private bool alignmentRight = false;
    [SerializeField] private bool isWhite = false;

    [SerializeField] private bool isPlayerTeam;
    public enum PanelType { Forward, Backup }
    [SerializeField] private PanelType panelType;
    [SerializeField] private int characterIndex;

    private List<Image> pips = new List<Image>();
    private int maxGauge;
    private CharacterState characterState;

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
    }

    public void Bind(CharacterState state)
    {
        if (characterState != null)
        {
            characterState.OnStateChanged -= UpdateDisplay;
        }

        if (state == null)
        {
            characterState = null;
            Initialize(0); // ピップを0個にする=何も表示しない
            return;
        }

        characterState = state;
        Initialize(characterState.data.maxUltGauge);
        characterState.OnStateChanged += UpdateDisplay;
        UpdateDisplay();
    }

    private void Initialize(int maxGauge)
    {
        this.maxGauge = maxGauge;

        foreach (var pip in pips)
        {
            Destroy(pip.gameObject);
        }
        pips.Clear();

        for (int i = 0; i < maxGauge; i++)
        {
            GameObject pipObj = Instantiate(pipPrefab, origin);
            RectTransform pipRect = pipObj.GetComponent<RectTransform>();
            pipRect.anchoredPosition = alignmentRight
                ? new Vector2(-i * pipSpacing, 0f)
                : new Vector2(i * pipSpacing, 0f);
            pipRect.anchorMin = origin.anchorMin;
            pipRect.anchorMax = origin.anchorMax;
            pipRect.pivot = origin.pivot;
            pips.Add(pipObj.GetComponent<Image>());
        }
    }

    private void UpdateDisplay()
    {
        for (int i = 0; i < pips.Count; i++)
        {
            if(!isWhite)
                pips[i].sprite = i < characterState.currentUltGauge ? pipFullSprite : pipNoneSprite;
            else
                pips[i].sprite = i < characterState.currentUltGauge ? pipFullWhiteSprite : pipNoneWhiteSprite;
        }
    }
}