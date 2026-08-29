/// <summary>
/// 1試合で1キャラが獲得したマスタリーXPの結果。ResultSceneでの表示用にCharacterMastery.GrantMatchXpが返す。
/// </summary>
public class CharacterMasteryResult
{
    public CharacterId characterId;
    public int xpGained;
    public int oldXp;
    public int newXp;
    public int oldLevel;
    public int newLevel;

    public bool LeveledUp => newLevel > oldLevel;
}
