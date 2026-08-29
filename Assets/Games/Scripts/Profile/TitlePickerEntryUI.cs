using System;
using TMPro;
using UnityEngine;

/// <summary>
/// ProfileSceneの「称号を選ぶ」グリッドの1マス分。Prefab化してInstantiateする(Hensei/Zukanと同パターン)。
/// メイン枠・サブ枠どちらの選択にも同じPrefab/画面を使い回す(呼び出し側がどの枠向けか覚えておく)。
/// </summary>
public class TitlePickerEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleNameText;

    private TitleData data;
    private Action<TitleData> onClicked;

    public void Setup(TitleData data, Action<TitleData> onClicked)
    {
        this.data = data;
        this.onClicked = onClicked;

        if (titleNameText != null) titleNameText.text = data.titleName;
    }

    // このマスのButtonのOnClickから呼ぶ
    public void OnClickEntry()
    {
        onClicked?.Invoke(data);
    }
}
