using System.Collections.Generic;
using System.Linq;

/// <summary>
/// FirestoreからロードしたTeamPresetを、既存のTeam.CreateFrom()が受け取れる形式に変換する。
/// characters[0],[1] = FW / characters[2],[3],[4] = BK という並び順を前提とする
/// (HenseiSceneの characterDataArray の並びと一致させること)。
/// </summary>
public static class TeamPresetConverter
{
    public static Team ToTeam(TeamPreset preset)
    {
        if (preset == null || preset.characters.Count < 5)
        {
            UnityEngine.Debug.LogError("[TeamPresetConverter] プリセットが不正です(5人揃っていません)");
            return null;
        }

        var fwData = new List<CharacterData>();
        var bkData = new List<CharacterData>();
        var loadouts = new List<Team.CharacterCatalystLoadout>();

        for (int i = 0; i < preset.characters.Count; i++)
        {
            var entry = preset.characters[i];
            CharacterData charData = CharacterRegistry.Instance.GetData(entry.charId);
            if (charData == null)
            {
                UnityEngine.Debug.LogWarning($"[TeamPresetConverter] CharacterId {entry.charId} が見つかりません");
                continue;
            }

            if (i < 2) fwData.Add(charData);
            else bkData.Add(charData);

            var catalystDataList = entry.catalystIds
                .Select(id => CatalystDataRegistry.Instance.GetData((CatalystId)id))
                .Where(c => c != null)
                .ToList();

            loadouts.Add(new Team.CharacterCatalystLoadout
            {
                character = charData,
                catalysts = catalystDataList
            });
        }

        return Team.CreateFrom(fwData, bkData, loadouts);
    }
}