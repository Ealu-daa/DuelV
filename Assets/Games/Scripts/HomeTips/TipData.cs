using UnityEngine;

// MissionData等と同じScriptableObjectパターン。Assets/Resources/Tips/以下に配置しておけば
// TipRegistryのResources.LoadAllで自動収集される。
[CreateAssetMenu(fileName = "TipData", menuName = "DuelV/Home Tip Data")]
public class TipData : ScriptableObject
{
    public TipCategory category;
    [TextArea] public string text;
}
