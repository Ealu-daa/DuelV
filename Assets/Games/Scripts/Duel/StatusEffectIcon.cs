using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusEffectIcon : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button iconButton;
    [SerializeField] private GameObject tooltipPanel; // アイコンの近くに配置した子オブジェクト、初期非表示
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private bool isCharactersPanel = false;
    [SerializeField] private CharactersInfo charactersInfo;
    [SerializeField] private bool catalystOpen = false;
    private void Awake()
    {
        iconButton.onClick.AddListener(ToggleTooltip);
        if(!isCharactersPanel)
            tooltipPanel.SetActive(false);
    }

    public void Setup(StatusEffect effect, int currentHalfTurn, bool enableTooltip = true)
    {
        var commonEntry = CommonEffectData.Instance != null
            ? CommonEffectData.Instance.GetEntry(effect.effectName)
            : null;

        iconImage.sprite = effect.icon != null ? effect.icon : commonEntry?.icon;
        iconButton.interactable = enableTooltip;
        tooltipPanel.SetActive(false);

        if (enableTooltip)
        {
            int remainingHt = effect.expiresAtHalfTurn - currentHalfTurn;
            int remainingTurns = remainingHt / 2;
            string description = !string.IsNullOrEmpty(effect.description) ? effect.description : commonEntry?.description;
            tooltipText.text = string.IsNullOrEmpty(description)
                ? $"{effect.effectName}\n残り{remainingTurns}ターン"
                : $"{effect.effectName}\n{description}\n残り{remainingTurns}ターン";
        }
    }

    public void SetupCharacters(StatusEffect effect, int currentHalfTurn, bool enableTooltip, CharactersInfo charactersInfo)
    {

        this.charactersInfo = charactersInfo;

        var commonEntry = CommonEffectData.Instance != null
            ? CommonEffectData.Instance.GetEntry(effect.effectName)
            : null;

        iconImage.sprite = effect.icon != null ? effect.icon : commonEntry?.icon;
        iconButton.interactable = enableTooltip;

        if (enableTooltip)
        {
            int remainingHt = effect.expiresAtHalfTurn - currentHalfTurn;
            int remainingTurns = remainingHt / 2;
            string description = !string.IsNullOrEmpty(effect.description) ? effect.description : commonEntry?.description;
            charactersInfo.CatalystSelected(effect.effectName , remainingTurns , description);
        }
    }

    private void ToggleTooltip()
    {
        if (!isCharactersPanel)
        {
            tooltipPanel.SetActive(!tooltipPanel.activeSelf);
        }
        else
        {
            catalystOpen = !catalystOpen;
            charactersInfo.CatalystToggle(catalystOpen);
        }
    }
}