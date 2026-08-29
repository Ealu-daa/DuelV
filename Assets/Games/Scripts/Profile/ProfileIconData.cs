using UnityEngine;

[CreateAssetMenu(fileName = "ProfileIconData", menuName = "DuelV/Profile Icon Data")]
public class ProfileIconData : ScriptableObject
{
    public ProfileIconId id;
    public string displayName;
    public Sprite icon;
}
