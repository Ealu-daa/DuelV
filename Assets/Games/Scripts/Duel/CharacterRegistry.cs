using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// CharacterId から CharacterData を引くためのレジストリ。
/// SkillRegistry/CatalystRegistryと同じDictionaryディスパッチパターン。
/// CharacterDataは Assets/Resources/Characters/ 以下に配置しておけば
/// Resources.LoadAllで自動的に収集される(サブフォルダに分けてもOK)。
/// </summary>
public class CharacterRegistry : MonoBehaviour
{
    private static CharacterRegistry _instance;
    public static CharacterRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CharacterRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("CharacterRegistry (Auto)");
                    _instance = go.AddComponent<CharacterRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "Characters";

    private Dictionary<CharacterId, CharacterData> _dataById;

    /// <summary>CharacterId昇順に並んだ全キャラクターデータ。グリッド表示等に使う。</summary>
    public IReadOnlyList<CharacterData> AllCharacters { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAllCharacterData();
    }

    private void LoadAllCharacterData()
    {
        CharacterData[] loaded = Resources.LoadAll<CharacterData>(ResourcesPath);

        _dataById = new Dictionary<CharacterId, CharacterData>();
        foreach (var data in loaded)
        {
            if (data.id == CharacterId.None)
            {
                Debug.LogWarning($"CharacterData '{data.name}' の Id が None のままです。設定を確認してください。");
                continue;
            }

            if (_dataById.ContainsKey(data.id))
            {
                Debug.LogWarning($"CharacterId '{data.id}' が重複しています。'{data.name}' はスキップされました。");
                continue;
            }

            _dataById[data.id] = data;
        }

        AllCharacters = _dataById.Values.OrderBy(d => d.id).ToList();
    }

    /// <summary>指定したIDのCharacterDataを取得する。存在しない場合はnull。</summary>
    public CharacterData GetData(CharacterId id)
    {
        return _dataById.TryGetValue(id, out var data) ? data : null;
    }
}