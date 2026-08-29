using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// キャラ図鑑の一覧画面。グリッド表示+フィルター(ロール別/国別/所持状態)。
/// entryPrefabを実数分だけInstantiateする方式(新キャラが増えてもEditor側で枠を増やす必要がない)。
/// 未所持キャラも一覧に出す(全公開方針。ロック表示は各マス側=CharacterZukanEntryUIが担当)。
/// </summary>
public class CharacterZukanListUI : MonoBehaviour
{
    [SerializeField] private Transform gridParent; // GridLayoutGroupを付けたContentを指定する
    [SerializeField] private CharacterZukanEntryUI entryPrefab;
    [SerializeField] private ZukanUIController controller;

    [Header("フィルターUI(未接続でも動く。繋いだ分だけ絞り込みが効く)")]
    [SerializeField] private TMP_Dropdown roleFilterDropdown;   // 選択肢: 全て/Duelist/Guardian/Controller/Support(この順で用意する)
    [SerializeField] private TMP_Dropdown originFilterDropdown; // 選択肢はRefresh時に自動生成(先頭は「全て」)
    [SerializeField] private TMP_Dropdown ownedFilterDropdown;  // 選択肢: 全て/所持のみ/未所持のみ(この順で用意する)

    private readonly List<CharacterZukanEntryUI> spawned = new List<CharacterZukanEntryUI>();
    private List<string> originOptions; // index 0 は「全て」、以降が実際の国名

    // 図鑑を開くたびにZukanUIControllerから呼ばれる
    public void Refresh()
    {
        BuildOriginFilterOptionsIfNeeded();
        Populate();
    }

    private void BuildOriginFilterOptionsIfNeeded()
    {
        if (originOptions != null || originFilterDropdown == null) return;

        originOptions = new List<string> { "全て" };
        originOptions.AddRange(
            CharacterRegistry.Instance.AllCharacters
                .Select(d => d.origin)
                .Where(o => !string.IsNullOrEmpty(o))
                .Distinct()
        );

        originFilterDropdown.ClearOptions();
        originFilterDropdown.AddOptions(originOptions);
    }

    // 各フィルタードロップダウンのOnValueChangedから呼ぶ
    public void OnFilterChanged()
    {
        Populate();
    }

    private void Populate()
    {
        foreach (var e in spawned) Destroy(e.gameObject);
        spawned.Clear();

        int roleFilter = roleFilterDropdown != null ? roleFilterDropdown.value : 0; // 0=全て、1〜4=RoleGroup
        string originFilter = (originFilterDropdown != null && originFilterDropdown.value > 0)
            ? originOptions[originFilterDropdown.value]
            : null;
        int ownedFilter = ownedFilterDropdown != null ? ownedFilterDropdown.value : 0; // 0=全て 1=所持のみ 2=未所持のみ

        foreach (var data in CharacterRegistry.Instance.AllCharacters)
        {
            if (roleFilter != 0 && data.RoleGroup != roleFilter) continue;
            if (originFilter != null && data.origin != originFilter) continue;

            bool owned = PlayerCollection.IsCharacterOwned(data.id);
            if (ownedFilter == 1 && !owned) continue;
            if (ownedFilter == 2 && owned) continue;

            var entry = Instantiate(entryPrefab, gridParent);
            entry.Setup(data, OnEntryClicked);
            spawned.Add(entry);
        }
    }

    private void OnEntryClicked(CharacterData data)
    {
        controller.ShowCharacterDetail(data);
    }
}
