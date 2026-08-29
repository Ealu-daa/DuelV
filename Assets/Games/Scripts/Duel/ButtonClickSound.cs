using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 付けたButtonがクリックされるたびに、SeManager経由で共通のクリックSEを鳴らす。
/// 個別のOnClickハンドラ(ActionUI等)を1つずつ触らずに済むよう、Buttonへ後付けでリスナーを追加するだけの汎用部品。
/// SEを鳴らしたいButtonに直接アタッチする。
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(SeManager.PlayButtonClick);
    }
}
