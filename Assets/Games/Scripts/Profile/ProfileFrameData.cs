using UnityEngine;

[CreateAssetMenu(fileName = "ProfileFrameData", menuName = "DuelV/Profile Frame Data")]
public class ProfileFrameData : ScriptableObject
{
    public ProfileFrameId id;
    public string displayName;
    public Sprite frameSprite; // アイコンの周りに重ねて表示する枠画像
}
