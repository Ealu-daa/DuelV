using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// カタリスト図鑑の一覧画面。グリッド表示+フィルター(ロール別/所持状態)。
/// entryPrefabを候補数ぶんだけInstantiateする方式(新カタリストが増えてもEditor側で枠を増やす必要がない)。
/// 未所持カタリストも一覧に出す(全公開方針。ロック表示は各マス側=CatalystZukanEntryUIが担当)。
/// </summary>
public class CatalystZukanListUI : MonoBehaviour
{
    [SerializeField] private Transform gridParent; // GridLayoutGroupを付けたContentを指定する
    [SerializeField] private CatalystZukanEntryUI entryPrefab;
    [SerializeField] private ZukanUIController controller;

    [Header("フィルターUI(未接続でも動く。繋いだ分だけ絞り込みが効く)")]
    [SerializeField] private TMP_Dropdown roleFilterDropdown;  // 選択肢: 全て/Duelist/Guardian/Controller/Support(この順で用意する)
    [SerializeField] private TMP_Dropdown ownedFilterDropdown; // 選択肢: 全て/所持のみ/未所持のみ(この順で用意する)

    private readonly List<CatalystZukanEntryUI> spawned = new List<CatalystZukanEntryUI>();

    // 図鑑を開くたびにZukanUIControllerから呼ばれる
    public void Refresh()
    {
        Populate();
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

        int roleFilter = roleFilterDropdown != null ? roleFilterDropdown.value : 0; // 0=全て、1〜4=Role
        int ownedFilter = ownedFilterDropdown != null ? ownedFilterDropdown.value : 0; // 0=全て 1=所持のみ 2=未所持のみ

        foreach (var data in CatalystDataRegistry.Instance.AllCatalysts)
        {
            // ロール限定でも、誰でも使える(restrictedRole == None)ものはどのロールで絞っても表示する
            if (roleFilter != 0 && data.restrictedRole != Role.None && (int)data.restrictedRole != roleFilter) continue;

            bool owned = PlayerCollection.IsCatalystOwned(data.id);
            if (ownedFilter == 1 && !owned) continue;
            if (ownedFilter == 2 && owned) continue;

            var entry = Instantiate(entryPrefab, gridParent);
            entry.Setup(data, OnEntryClicked);
            spawned.Add(entry);
        }
    }

    private void OnEntryClicked(CatalystData data)
    {
        controller.ShowCatalystDetail(data);
    }
}
