using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CommonEffectData", menuName = "DuelV/CommonEffectData")]
public class CommonEffectData : ScriptableObject
{
    [System.Serializable]
    public class EffectIconEntry
    {
        public string effectName; // StatusEffect.effectNameと完全一致させる
        public Sprite icon;
        [TextArea] public string description; // 追加
    }

    public List<EffectIconEntry> effectIcons;

    private static CommonEffectData _instance;
    public static CommonEffectData Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<CommonEffectData>("CommonEffectData");
            return _instance;
        }
    }

    private Dictionary<string, EffectIconEntry> lookup;

    private void BuildLookupIfNeeded()
    {
        if (lookup == null)
        {
            lookup = new Dictionary<string, EffectIconEntry>();
            foreach (var entry in effectIcons)
                lookup[entry.effectName] = entry;
        }
    }

    public Sprite GetIcon(string effectName)
    {
        BuildLookupIfNeeded();
        return lookup.TryGetValue(effectName, out var entry) ? entry.icon : null;
    }

    public string GetDescription(string effectName)
    {
        BuildLookupIfNeeded();
        return lookup.TryGetValue(effectName, out var entry) ? entry.description : string.Empty;
    }

    // 両方まとめて取得したい場合用
    public EffectIconEntry GetEntry(string effectName)
    {
        BuildLookupIfNeeded();
        return lookup.TryGetValue(effectName, out var entry) ? entry : null;
    }
}