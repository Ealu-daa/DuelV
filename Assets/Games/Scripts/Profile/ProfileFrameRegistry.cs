using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ProfileFrameIdからProfileFrameDataを引くためのレジストリ。CharacterRegistryと同じパターン。
/// ProfileFrameDataはAssets/Resources/ProfileFrames/以下に配置しておけばResources.LoadAllで自動収集される。
/// </summary>
public class ProfileFrameRegistry : MonoBehaviour
{
    private static ProfileFrameRegistry _instance;
    public static ProfileFrameRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ProfileFrameRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("ProfileFrameRegistry (Auto)");
                    _instance = go.AddComponent<ProfileFrameRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "ProfileFrames";

    private Dictionary<ProfileFrameId, ProfileFrameData> _dataById;

    public IReadOnlyList<ProfileFrameData> AllFrames { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    private void LoadAll()
    {
        var loaded = Resources.LoadAll<ProfileFrameData>(ResourcesPath);

        _dataById = new Dictionary<ProfileFrameId, ProfileFrameData>();
        foreach (var data in loaded)
        {
            if (data.id == ProfileFrameId.None) continue;
            if (_dataById.ContainsKey(data.id))
            {
                Debug.LogWarning($"ProfileFrameId '{data.id}' が重複しています。'{data.name}' はスキップされました。");
                continue;
            }
            _dataById[data.id] = data;
        }

        AllFrames = _dataById.Values.OrderBy(d => d.id).ToList();
    }

    public ProfileFrameData GetData(ProfileFrameId id)
    {
        return _dataById.TryGetValue(id, out var data) ? data : null;
    }
}
