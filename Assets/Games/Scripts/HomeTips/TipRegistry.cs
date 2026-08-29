using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ホーム画面Tips用のデータレジストリ。CharacterRegistry/MissionRegistryと同じパターン。
/// TipDataはAssets/Resources/Tips/以下に配置しておけばResources.LoadAllで自動収集される。
/// </summary>
public class TipRegistry : MonoBehaviour
{
    private static TipRegistry _instance;
    public static TipRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TipRegistry>();
                if (_instance == null)
                {
                    var go = new GameObject("TipRegistry (Auto)");
                    _instance = go.AddComponent<TipRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private const string ResourcesPath = "Tips";
    private static readonly TipCategory[] AllCategories = (TipCategory[])Enum.GetValues(typeof(TipCategory));

    private Dictionary<TipCategory, List<TipData>> _dataByCategory;

    public IReadOnlyList<TipData> AllTips { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    private void LoadAll()
    {
        var loaded = Resources.LoadAll<TipData>(ResourcesPath);

        _dataByCategory = new Dictionary<TipCategory, List<TipData>>();
        foreach (var category in AllCategories)
            _dataByCategory[category] = new List<TipData>();

        foreach (var data in loaded)
            _dataByCategory[data.category].Add(data);

        AllTips = loaded.ToList();
    }

    /// <summary>
    /// 4カテゴリを完全均等ランダムで選ぶ(先にカテゴリを1/4で選び、そのカテゴリ内からTipを選ぶ2段階抽選。
    /// カテゴリごとのTip登録数に偏りがあっても、カテゴリの出現頻度自体は均等になる)。
    /// excludeを渡すと、直前と同じTipが連続して出ないよう避ける(タップで切り替える時用、渡さなければ完全ランダム)。
    /// </summary>
    public TipData GetRandomTip(TipData exclude = null)
    {
        if (_dataByCategory == null || AllTips == null || AllTips.Count == 0) return null;

        // 中身が1件も無いカテゴリは対象から外す(空のカテゴリを引いてハズレを引き続けないように)
        var nonEmptyCategories = AllCategories.Where(c => _dataByCategory[c].Count > 0).ToList();
        if (nonEmptyCategories.Count == 0) return null;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            var category = nonEmptyCategories[UnityEngine.Random.Range(0, nonEmptyCategories.Count)];
            var pool = _dataByCategory[category];
            var candidate = pool[UnityEngine.Random.Range(0, pool.Count)];

            if (candidate != exclude || AllTips.Count == 1) return candidate;
        }

        // 8回試しても除外対象しか引けなかった(登録Tipが極端に少ない)場合は諦めてそのまま返す
        var fallbackCategory = nonEmptyCategories[UnityEngine.Random.Range(0, nonEmptyCategories.Count)];
        var fallbackPool = _dataByCategory[fallbackCategory];
        return fallbackPool[UnityEngine.Random.Range(0, fallbackPool.Count)];
    }
}
