using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 編成シーンの「カタリストを選ぶ」グリッドの1マス分。Prefab化して装備可能な候補数ぶんだけInstantiateする
/// (新カタリストが増えてもEditor側で枠を手で足す必要がない。ScrollView+GridLayoutGroup前提)。
/// </summary>
public class CatalystPickerEntryUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    private CatalystData data;
    private Action<CatalystData> onClicked;

    public void Setup(CatalystData data, Action<CatalystData> onClicked)
    {
        this.data = data;
        this.onClicked = onClicked;
        iconImage.sprite = data.icon;
    }

    // このマスのButtonのOnClickから呼ぶ
    public void OnClickEntry()
    {
        onClicked?.Invoke(data);
    }
}
