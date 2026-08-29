/// <summary>
/// 1試合で獲得したアカウントXPの結果。ResultSceneでの表示用にAccountLevel.GrantMatchXpが返す。
/// </summary>
public class AccountLevelResult
{
    public int xpGained;
    public int oldXp;
    public int newXp;
    public int oldLevel;
    public int newLevel;

    public bool LeveledUp => newLevel > oldLevel;
}
