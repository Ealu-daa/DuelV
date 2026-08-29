using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// MissionIdからMissionDataを引くためのレジストリ。CharacterRegistryと同じパターン。
/// MissionDataはAssets/Resources/Missions/以下に配置しておけばResources.LoadAllで自動収集される。
/// </summary>
public class MissionRegistry : MonoBehaviour
{
    private static MissionRegistry _instance;
    public static MissionRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MissionRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("MissionRegistry (Auto)");
                    _instance = go.AddComponent<MissionRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "Missions";

    private Dictionary<MissionId, MissionData> _dataById;

    public IReadOnlyList<MissionData> AllMissions { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    private void LoadAll()
    {
        var loaded = Resources.LoadAll<MissionData>(ResourcesPath);

        _dataById = new Dictionary<MissionId, MissionData>();
        foreach (var data in loaded)
        {
            if (data.id == MissionId.None) continue;
            if (_dataById.ContainsKey(data.id))
            {
                Debug.LogWarning($"MissionId '{data.id}' が重複しています。'{data.name}' はスキップされました。");
                continue;
            }
            _dataById[data.id] = data;
        }

        AllMissions = _dataById.Values.OrderBy(d => d.category).ThenBy(d => d.id).ToList();
    }

    public MissionData GetData(MissionId id)
    {
        return _dataById.TryGetValue(id, out var data) ? data : null;
    }
}
