using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

[System.Serializable]
public class Team
{
    public List<CharacterState> forwards = new List<CharacterState>(); // FW 2体
    public List<CharacterState> backups = new List<CharacterState>();  // BK 3体

    public CatalystId? lastUsedCatalystId; // 模倣用: このチームが最後に使ったカタリスト

    [System.Serializable]
    public class CharacterCatalystLoadout
    {
        public CharacterData character;
        public List<CatalystData> catalysts;
    }

    // CharacterDataのリストからTeamを組み立てる
    public static Team CreateFrom(List<CharacterData> fwData, List<CharacterData> bkData, List<CharacterCatalystLoadout> loadouts)
    {
        var team = new Team();
        foreach (var d in fwData) team.forwards.Add(CreateCharacterWithCatalysts(d, loadouts));
        foreach (var d in bkData) team.backups.Add(CreateCharacterWithCatalysts(d, loadouts));
        return team;
    }

    public IEnumerable<CharacterState> AllCharacters()
    {
        foreach (var c in forwards) yield return c;
        foreach (var c in backups) yield return c;
    }

    public bool IsDefeated()
    {
        foreach (var c in AllCharacters())
        {
            if (!c.IsDefeated) return false;
        }
        return true;
    }

    public void ResetAllTurnStates()
    {
        foreach (var c in AllCharacters())
        {
            c.ResetTurnState();
        }
    }
    private static CharacterState CreateCharacterWithCatalysts(CharacterData d, List<CharacterCatalystLoadout> loadouts)
    {
        var state = new CharacterState(d);
        var loadout = loadouts.Find(l => l.character == d);
        if (loadout != null)
        {
            foreach (var catalystData in loadout.catalysts)
            {
                state.catalysts.Add(new CatalystInstance(catalystData));
            }
        }
        return state;
    }

}
