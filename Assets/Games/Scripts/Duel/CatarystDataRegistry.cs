using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// CatalystId から CatalystData を引くためのレジストリ。
/// CharacterRegistryと同じ Resources.LoadAll による自動収集パターン。
/// (スキル発動ディスパッチ用の CatalystRegistry とは別物のため、
///  このクラス名は CatalystDataRegistry としている)
/// CatalystDataは Assets/Resources/Catalysts/ 以下に配置しておけば
/// サブフォルダ(Duelist/Guardian/等)に分けても自動的に収集される。
/// </summary>
public class CatalystDataRegistry : MonoBehaviour
{
    private static CatalystDataRegistry _instance;
    public static CatalystDataRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CatalystDataRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("CatalystDataRegistry (Auto)");
                    _instance = go.AddComponent<CatalystDataRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "Catalysts";

    private Dictionary<CatalystId, CatalystData> _dataById;

    /// <summary>CatalystId順に並んだ全カタリストデータ。一覧表示等に使う。</summary>
    public IReadOnlyList<CatalystData> AllCatalysts { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAllCatalystData();
    }

    private void LoadAllCatalystData()
    {
        CatalystData[] loaded = Resources.LoadAll<CatalystData>(ResourcesPath);

        _dataById = new Dictionary<CatalystId, CatalystData>();
        foreach (var data in loaded)
        {
            if (_dataById.ContainsKey(data.id))
            {
                Debug.LogWarning($"CatalystId '{data.id}' が重複しています。'{data.name}' はスキップされました。");
                continue;
            }

            _dataById[data.id] = data;
        }

        AllCatalysts = _dataById.Values.OrderBy(d => d.id).ToList();
    }

    /// <summary>指定したIDのCatalystDataを取得する。存在しない場合はnull。</summary>
    public CatalystData GetData(CatalystId id)
    {
        return _dataById.TryGetValue(id, out var data) ? data : null;
    }
}
