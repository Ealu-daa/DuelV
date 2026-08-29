using UnityEngine;

[System.Serializable]
public class CharacterSlotUI
{
    public CharacterPanelUI panel;
    public UltGaugeUI ultGauge;

    public void Bind(CharacterState state)
    {
        panel.Bind(state);
        ultGauge.Bind(state);
    }
}