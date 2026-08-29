using System.Collections.Generic;

public class BattleResultData
{
    public bool isVictory;
    public int endHalfTurn;
    public List<CharacterMasteryResult> masteryResults; // 対戦で獲得したマスタリーXP(BattleManager.ShowResultで設定)
    public AccountLevelResult accountLevelResult;        // 対戦で獲得したアカウントXP(BattleManager.ShowResultで設定)

    public static BattleResultData Pending;
}
