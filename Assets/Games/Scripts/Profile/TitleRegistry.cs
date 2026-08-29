using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TitleIdからTitleDataを引くためのレジストリ。CharacterRegistryと同じパターン。
/// TitleDataはAssets/Resources/Titles/以下に配置しておけばResources.LoadAllで自動収集される。
/// </summary>
public class TitleRegistry : MonoBehaviour
{
    private static TitleRegistry _instance;
    public static TitleRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TitleRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("TitleRegistry (Auto)");
                    _instance = go.AddComponent<TitleRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "Titles";

    private Dictionary<TitleId, TitleData> _dataById;

    public IReadOnlyList<TitleData> AllTitles { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    private void LoadAll()
    {
        var loaded = Resources.LoadAll<TitleData>(ResourcesPath);

        _dataById = new Dictionary<TitleId, TitleData>();
        foreach (var data in loaded)
        {
            if (data.id == TitleId.None) continue;
            if (_dataById.ContainsKey(data.id))
            {
                Debug.LogWarning($"TitleId '{data.id}' が重複しています。'{data.name}' はスキップされました。");
                continue;
            }
            _dataById[data.id] = data;
        }

        AllTitles = _dataById.Values.OrderBy(d => d.id).ToList();
    }

    public TitleData GetData(TitleId id)
    {
        return _dataById.TryGetValue(id, out var data) ? data : null;
    }
}
