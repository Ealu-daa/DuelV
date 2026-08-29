using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ProfileIconIdからProfileIconDataを引くためのレジストリ。CharacterRegistryと同じパターン。
/// ProfileIconDataはAssets/Resources/ProfileIcons/以下に配置しておけばResources.LoadAllで自動収集される。
/// </summary>
public class ProfileIconRegistry : MonoBehaviour
{
    private static ProfileIconRegistry _instance;
    public static ProfileIconRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ProfileIconRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("ProfileIconRegistry (Auto)");
                    _instance = go.AddComponent<ProfileIconRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "ProfileIcons";

    private Dictionary<ProfileIconId, ProfileIconData> _dataById;

    public IReadOnlyList<ProfileIconData> AllIcons { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    private void LoadAll()
    {
        var loaded = Resources.LoadAll<ProfileIconData>(ResourcesPath);

        _dataById = new Dictionary<ProfileIconId, ProfileIconData>();
        foreach (var data in loaded)
        {
            if (data.id == ProfileIconId.None) continue;
            if (_dataById.ContainsKey(data.id))
            {
                Debug.LogWarning($"ProfileIconId '{data.id}' が重複しています。'{data.name}' はスキップされました。");
                continue;
            }
            _dataById[data.id] = data;
        }

        AllIcons = _dataById.Values.OrderBy(d => d.id).ToList();
    }

    public ProfileIconData GetData(ProfileIconId id)
    {
        return _dataById.TryGetValue(id, out var data) ? data : null;
    }
}
