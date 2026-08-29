using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CharactersInfo : MonoBehaviour
{
    [SerializeField] GameObject charactersInfoPanel;
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI defenseText;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI skillUltText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] GameObject statusButton;
    [SerializeField] GameObject skillButton;
    [SerializeField] GameObject ultButton;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject catalystButtons;
    [SerializeField] private CatalystButtonUI catalystButtonPrefab;

    private string charactersName;
    private int charactersCurrentAttack;
    private int charactersCurrentDefense;
    private string charactersSkillName;
    private string charactersSkillDescription;
    private string charactersUltName;
    private string charactersUltDescription;
    
    private string catalystCName;
    private int remainingCTurns;
    private string catalystCDescription;

    private bool isOpen = false;

    private bool firstCharacterSelected = false;

    public void Awake()
    {
        charactersInfoPanel.SetActive(true);
    }
    public void CharactersOpen()
    {
        isOpen = !isOpen;
        if(isOpen)
        {
            charactersInfoPanel.SetActive(true);
            nameText.text = "";
            skillUltText.text = "";
            attackText.text = "";
            defenseText.text = "";
            descriptionText.text = "";
            skillButton.SetActive(true);
            ultButton.SetActive(true);
        }
        else
        {
            charactersInfoPanel.SetActive(false);
        }
    }

    public void StartAfterBind()
    {
        charactersInfoPanel.SetActive(false);
    }


    public void CharacterSelected(string name, int currentAttack, int currentDefense, string skillName, string skillDescription, string ultName, string ultDescription, List<CatalystInstance> catalystList)
    {
        firstCharacterSelected = true;
        charactersName = name;
        charactersCurrentAttack = currentAttack;
        charactersCurrentDefense = currentDefense;
        charactersSkillName = skillName;
        charactersSkillDescription = skillDescription;
        charactersUltName = ultName;
        charactersUltDescription = ultDescription;

        nameText.text = charactersName;
        skillUltText.text = "";
        descriptionText.text = $"";
        attackText.text = $"攻撃力: {charactersCurrentAttack}";
        defenseText.text = $"防御力: {charactersCurrentDefense}";

        skillButton.SetActive(true);
        ultButton.SetActive(true);
        statusButton.SetActive(true);

        catalystButtons.SetActive(true);
        GenerateButtons(catalystList);
    }

    public void StatusSelected()
    {
        if(!firstCharacterSelected)
        {
            return;
        }

        nameText.text = charactersName;
        skillUltText.text = "";
        descriptionText.text = $"";
        attackText.text = $"攻撃力: {charactersCurrentAttack}";
        defenseText.text = $"防御力: {charactersCurrentDefense}";

        skillButton.SetActive(true);
        ultButton.SetActive(true);
        statusButton.SetActive(true);

        catalystButtons.SetActive(true);
    }
    public void SkillSelected()
    {
        if(!firstCharacterSelected)
        {
        return;
        }
        nameText.text = charactersName;
        skillUltText.text = $"{charactersSkillName}";
        attackText.text = "";
        defenseText.text = "";
        descriptionText.text = $"{charactersSkillDescription}";

        skillButton.SetActive(false);
        ultButton.SetActive(false);
        statusButton.SetActive(true);

        catalystButtons.SetActive(false);
    }
    public void UltSelected()
    {
        if (!firstCharacterSelected)
        {
            return;
        }
        nameText.text = charactersName;
        skillUltText.text = $"{charactersUltName}";
        attackText.text = "";
        defenseText.text = "";
        descriptionText.text = $"{charactersUltDescription}";

        skillButton.SetActive(false);
        ultButton.SetActive(false);
        statusButton.SetActive(true);

        catalystButtons.SetActive(false);
    }

    public void CatalystSelected(string catalystName, int remainingTurns, string catalystDescription)
    {
        catalystCName = catalystName;
        remainingCTurns = remainingTurns;
        catalystCDescription = catalystDescription;

        catalystButtons.SetActive(false);
    }
    public void CatalystToggle(bool active)
    {
        if(active)
        {
            firstCharacterSelected = false;
            skillUltText.text = catalystCName;
            nameText.text = $"残り{remainingCTurns}ターン";
            attackText.text = "";
            defenseText.text = "";
            descriptionText.text = $"{catalystCDescription}";
            skillButton.SetActive(false);
            ultButton.SetActive(false);
            statusButton.SetActive(false);

            catalystButtons.SetActive(false);
        }
    }

    void GenerateButtons(List<CatalystInstance> catalystList)
    {
        foreach (var data in catalystList)
        {
            var buttonUI = Instantiate(catalystButtonPrefab, buttonContainer);
            buttonUI.Setup(data.data, OnCatalystSelected);
        }
    }

    void OnCatalystSelected(CatalystData data)
    {
        descriptionText.text = data.description;
        catalystButtons.SetActive(false);
    }
}
