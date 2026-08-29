using UnityEngine;

[CreateAssetMenu(fileName = "TitleData", menuName = "DuelV/Title Data")]
public class TitleData : ScriptableObject
{
    public TitleId id;
    public string titleName; // プロフィールに表示される文言
    [TextArea] public string description; // 入手条件の説明(図鑑のツールチップ等で使う想定)
}
