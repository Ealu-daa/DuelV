using UnityEngine;

[CreateAssetMenu(fileName = "MissionData", menuName = "DuelV/Mission Data")]
public class MissionData : ScriptableObject
{
    public MissionId id;
    public MissionCategory category;
    public string title;
    [TextArea] public string description;

    // 現時点では達成/未達成の2値しか使っていないが、「3回勝利する」のような回数ミッションに
    // 対応する余地としてtargetCountを残してある(今はどこからも回数を積み上げていない)
    public int targetCount = 1;

    public int echoReward;
    public TitleId titleReward; // Noneなら称号報酬なし
}
