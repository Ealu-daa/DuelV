using UnityEngine;

[CreateAssetMenu(fileName = "CatalystData", menuName = "DuelV/CatalystData")]
public class CatalystData : ScriptableObject
{
    public CatalystId id;
    public string catalystName;           // 表示名(例: "渾身")
    [TextArea] public string description; // 効果テキスト(UI表示用)
    public Role restrictedRole;
    public Sprite icon;                     // アイコン画像
    public int price; // 図鑑・ショップでの購入価格(エコー)。未設定なら0

}