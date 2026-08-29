using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 1キャラ分のプリセット構成(キャラ + 付与サブカード)。
/// HenseiSceneの編成データとFirestoreの間の橋渡し用。
/// </summary>
[System.Serializable]
public class PresetCharacterEntry
{
    public CharacterId charId;
    public List<int> catalystIds; // カタリストIDの配列(重複可・空でも可)

    public PresetCharacterEntry(CharacterId charId, List<int> catalystIds)
    {
        this.charId = charId;
        this.catalystIds = catalystIds ?? new List<int>();
    }
}

/// <summary>
/// プリセット1件分(5人構成)。
/// </summary>
public class TeamPreset
{
    public string name;
    public List<PresetCharacterEntry> characters; // 5件想定(FW2+BK3)

    public TeamPreset(string name, List<PresetCharacterEntry> characters)
    {
        this.name = name;
        this.characters = characters;
    }

    // ---------- Firestore用に変換 ----------

    /// <summary>SavePresetにそのまま渡せる形式に変換</summary>
    public List<Dictionary<string, object>> ToFirestoreCharacterList()
    {
        return characters.Select(c => new Dictionary<string, object>
        {
            { "charId", (int)c.charId },
            { "catalystIds", c.catalystIds.Select(id => (object)id).ToList() }
        }).ToList();
    }

    /// <summary>LoadPresetで受け取ったDictionaryからTeamPresetを復元</summary>
    public static TeamPreset FromFirestoreDocument(Dictionary<string, object> doc)
    {
        if (doc == null) return null;

        string name = doc.TryGetValue("name", out var n) ? n as string : "";
        var charsRaw = doc.TryGetValue("characters", out var c) ? c as List<object> : null;

        var characters = new List<PresetCharacterEntry>();
        if (charsRaw != null)
        {
            foreach (var item in charsRaw)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;

                CharacterId charId = (CharacterId)(long)dict["charId"];
                var catalystIdsRaw = dict.TryGetValue("catalystIds", out var cids) ? cids as List<object> : null;
                var catalystIds = catalystIdsRaw?.Select(x => (int)(long)x).ToList() ?? new List<int>();

                characters.Add(new PresetCharacterEntry(charId, catalystIds));
            }
        }

        return new TeamPreset(name, characters);
    }
}