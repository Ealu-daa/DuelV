using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// オンライン対戦中のバトル状態をFirestoreへ定期的にスナップショット保存し、
/// リロード等で中断された後の再接続時にそこから復元するための仕組み。
///
/// 書き込みタイミング: 自分のFinishTurn()が完全に片付いた直後(死亡繰り上げの選択待ちなどが
/// 残っていない安定した状態)に、TurnManager.ContinueTurnStartAfterDeaths()から呼ぶ。
/// "hostXxx"/"guestXxx"という自分のロール名を付けたフィールドとして部分更新するので、
/// 相手側が書いたフィールドを上書きしてしまうことはない。
///
/// 読み込み(復元)時: 自分のロールのフィールドから自チーム+手番状態を、相手のロールのフィールドから
/// 敵チームを、それぞれ独立に復元する。
///
/// 相互参照の解決について:
/// StatusEffect.onExpireは(オンライン再接続用のスナップショットを作れるようにするため)
/// StatusEffectBehaviors側でeffectNameから名前解決する設計にしてある。
/// protectedBy/twinBurdenPartner/defeatedBy/huntBoundTo/StatusEffect.sourceなど、
/// キャラ同士が直接参照し合っている項目は、「ホスト5枠(FW2+BK3)+ゲスト5枠、計10枠」の
/// 固定絶対インデックスに変換して保存し、復元時にそのインデックスから実体を引き直す。
/// </summary>
public static class BattleSnapshot
{
    private const int SlotsPerTeam = 5; // FW2 + BK3

    // localIndex: FW0=0, FW1=1, BK0=2, BK1=3, BK2=4
    private static int SlotIndex(bool isHostTeam, int localIndex)
        => (isHostTeam ? 0 : SlotsPerTeam) + localIndex;

    // ---------------- 書き込み ----------------

    /// <summary>自分のFinishTurn()が完全に片付いた直後に呼ぶ(TurnManagerから)。オンライン対戦以外では何もしない。</summary>
    public static void WriteSnapshot()
    {
        if (BattleManager.Instance == null || !BattleManager.Instance.IsOnlineMatch) return;
        if (TurnManager.Instance == null) return;

        bool amIHost = RoomManager.IsHost;
        var myTeam = BattleManager.Instance.PlayerTeam;
        if (myTeam == null) return;

        var members = new List<object>();
        foreach (var c in myTeam.forwards) members.Add(SerializeCharacter(c, amIHost));
        foreach (var c in myTeam.backups) members.Add(SerializeCharacter(c, amIHost));

        var teamDict = new Dictionary<string, object>
        {
            { "members", members },
            { "lastUsedCatalystId", myTeam.lastUsedCatalystId?.ToString() ?? "" }
        };

        string rolePrefix = amIHost ? "host" : "guest";
        var fields = new Dictionary<string, object>
        {
            { rolePrefix + "Team", teamDict },
            { rolePrefix + "HalfTurn", TurnManager.Instance.currentHalfTurn },
            { rolePrefix + "TurnNumber", TurnManager.Instance.currentTurnNumber },
            { rolePrefix + "IsPlayerTurnNow", TurnManager.Instance.isPlayerTurnNow },
            { rolePrefix + "IsPlayerFirst", TurnManager.Instance.isPlayerFirst },
        };

        FirestoreBridge.Instance.SendUpdate(fields);
    }

    private static Dictionary<string, object> SerializeCharacter(CharacterState c, bool amIHost)
    {
        var effects = c.activeEffects.Select(e => (object)new Dictionary<string, object>
        {
            { "name", e.effectName },
            { "expiresAt", e.expiresAtHalfTurn },
            { "value", e.value },
            { "isDebuff", e.isDebuff },
            { "tickValue", e.tickValue },
            { "sourceSlot", ResolveAbsoluteSlot(e.source, amIHost) }
        }).ToList();

        var catalysts = c.catalysts.Select(ci => (object)new Dictionary<string, object>
        {
            { "id", ci.Id.ToString() },
            { "isUsed", ci.isUsed },
            { "disabledUntil", ci.disabledUntilHalfTurn }
        }).ToList();

        return new Dictionary<string, object>
        {
            { "characterId", c.data.id.ToString() },
            { "hp", c.currentHP },
            { "ultGauge", c.currentUltGauge },
            { "maxHp", c.currentMaxHP },
            { "attack", c.currentAttack },
            { "defense", c.currentDefense },
            { "isDefending", c.isDefending },
            { "defenseExpiresAt", c.defenseExpiresAtHalfTurn },
            { "hasActed", c.hasActedThisTurn },
            { "hasUsedCatalyst", c.hasUsedCatalystThisHalfTurn },
            { "isProtectingAlly", c.isProtectingAlly },
            { "protectedBySlot", ResolveAbsoluteSlot(c.protectedBy, amIHost) },
            { "twinBurdenPartnerSlot", ResolveAbsoluteSlot(c.twinBurdenPartner, amIHost) },
            { "defeatedBySlot", ResolveAbsoluteSlot(c.defeatedBy, amIHost) },
            { "defeatedAt", c.defeatedAtHalfTurn },
            { "soulAbsorbed", c.soulAbsorbed },
            { "huntBoundToSlot", ResolveAbsoluteSlot(c.huntBoundTo, amIHost) },
            { "isSuspended", c.isSuspendedAnimation },
            { "hasRevived", c.hasRevived },
            { "morpheBonus", c.morpheMaxHpBonusTotal },
            { "isTransformed", c.isTransformed },
            { "swapHandled", c.swapHandled },
            { "effects", effects },
            { "catalysts", catalysts }
        };
    }

    // 自分から見たPlayerTeam/EnemyTeamの中からcを探し、host/guestの絶対スロット番号(0-9)に変換する
    private static int ResolveAbsoluteSlot(CharacterState c, bool amIHost)
    {
        if (c == null) return -1;
        var playerTeam = BattleManager.Instance.PlayerTeam;
        var enemyTeam = BattleManager.Instance.EnemyTeam;

        int idx = playerTeam.forwards.IndexOf(c);
        if (idx != -1) return SlotIndex(amIHost, idx);
        idx = playerTeam.backups.IndexOf(c);
        if (idx != -1) return SlotIndex(amIHost, 2 + idx);

        idx = enemyTeam.forwards.IndexOf(c);
        if (idx != -1) return SlotIndex(!amIHost, idx);
        idx = enemyTeam.backups.IndexOf(c);
        if (idx != -1) return SlotIndex(!amIHost, 2 + idx);

        return -1;
    }

    // ---------------- 復元 ----------------

    /// <summary>
    /// Firestoreのゲームドキュメントから、自分のロールに応じて自チーム/敵チーム/手番状態を復元する。
    /// 必要なフィールドが揃っていなければfalseを返す(呼び出し側は通常の新規対戦組み立てにフォールバックすること)。
    /// </summary>
    public static bool TryRestore(
        Dictionary<string, object> doc,
        out Team myTeam, out Team enemyTeam,
        out int halfTurn, out int turnNumber, out bool isPlayerTurnNow, out bool isPlayerFirst)
    {
        myTeam = null; enemyTeam = null;
        halfTurn = 0; turnNumber = 1; isPlayerTurnNow = true; isPlayerFirst = true;

        if (doc == null) return false;

        bool amIHost = RoomManager.IsHost;
        string myPrefix = amIHost ? "host" : "guest";
        string enemyPrefix = amIHost ? "guest" : "host";

        if (!doc.TryGetValue(myPrefix + "Team", out var myTeamObj) || !(myTeamObj is Dictionary<string, object> myTeamDict)) return false;
        if (!doc.TryGetValue(enemyPrefix + "Team", out var enemyTeamObj) || !(enemyTeamObj is Dictionary<string, object> enemyTeamDict)) return false;

        var myMembers = (myTeamDict.TryGetValue("members", out var mm) ? mm as List<object> : null) ?? new List<object>();
        var enemyMembers = (enemyTeamDict.TryGetValue("members", out var em) ? em as List<object> : null) ?? new List<object>();
        if (myMembers.Count < SlotsPerTeam || enemyMembers.Count < SlotsPerTeam) return false;

        // 1st pass: 全キャラを作る(相互参照はまだ解決しない。indexだけ確定させておく)
        var allSlots = new CharacterState[SlotsPerTeam * 2];
        for (int i = 0; i < SlotsPerTeam; i++)
        {
            allSlots[SlotIndex(amIHost, i)] = CreateCharacterFromData(myMembers[i] as Dictionary<string, object>);
            allSlots[SlotIndex(!amIHost, i)] = CreateCharacterFromData(enemyMembers[i] as Dictionary<string, object>);
        }
        if (allSlots.Any(s => s == null)) return false;

        // 2nd pass: 数値/フラグ/相互参照を埋める
        for (int i = 0; i < SlotsPerTeam; i++)
        {
            ApplyCharacterFields(allSlots[SlotIndex(amIHost, i)], myMembers[i] as Dictionary<string, object>, allSlots);
            ApplyCharacterFields(allSlots[SlotIndex(!amIHost, i)], enemyMembers[i] as Dictionary<string, object>, allSlots);
        }

        myTeam = new Team();
        myTeam.forwards.Add(allSlots[SlotIndex(amIHost, 0)]);
        myTeam.forwards.Add(allSlots[SlotIndex(amIHost, 1)]);
        myTeam.backups.Add(allSlots[SlotIndex(amIHost, 2)]);
        myTeam.backups.Add(allSlots[SlotIndex(amIHost, 3)]);
        myTeam.backups.Add(allSlots[SlotIndex(amIHost, 4)]);
        myTeam.lastUsedCatalystId = ParseCatalystIdOrNull(myTeamDict.TryGetValue("lastUsedCatalystId", out var lc) ? lc as string : null);

        enemyTeam = new Team();
        enemyTeam.forwards.Add(allSlots[SlotIndex(!amIHost, 0)]);
        enemyTeam.forwards.Add(allSlots[SlotIndex(!amIHost, 1)]);
        enemyTeam.backups.Add(allSlots[SlotIndex(!amIHost, 2)]);
        enemyTeam.backups.Add(allSlots[SlotIndex(!amIHost, 3)]);
        enemyTeam.backups.Add(allSlots[SlotIndex(!amIHost, 4)]);
        enemyTeam.lastUsedCatalystId = ParseCatalystIdOrNull(enemyTeamDict.TryGetValue("lastUsedCatalystId", out var lc2) ? lc2 as string : null);

        halfTurn = doc.TryGetValue(myPrefix + "HalfTurn", out var ht) ? Convert.ToInt32(ht) : 0;
        turnNumber = doc.TryGetValue(myPrefix + "TurnNumber", out var tn) ? Convert.ToInt32(tn) : 1;
        isPlayerTurnNow = doc.TryGetValue(myPrefix + "IsPlayerTurnNow", out var iptn) && iptn is bool iptnB && iptnB;
        isPlayerFirst = doc.TryGetValue(myPrefix + "IsPlayerFirst", out var ipf) && ipf is bool ipfB && ipfB;

        return true;
    }

    private static CharacterState CreateCharacterFromData(Dictionary<string, object> d)
    {
        if (d == null) return null;
        if (!d.TryGetValue("characterId", out var idObj) || !(idObj is string idStr)) return null;

        CharacterId id;
        try { id = (CharacterId)Enum.Parse(typeof(CharacterId), idStr); }
        catch { return null; }

        var data = CharacterRegistry.Instance != null ? CharacterRegistry.Instance.GetData(id) : null;
        if (data == null) return null;

        var state = new CharacterState(data);

        if (d.TryGetValue("catalysts", out var catObj) && catObj is List<object> catalysts)
        {
            foreach (var raw in catalysts)
            {
                if (!(raw is Dictionary<string, object> cd)) continue;
                if (!(cd.TryGetValue("id", out var catIdObj) && catIdObj is string catIdStr)) continue;

                CatalystId catId;
                try { catId = (CatalystId)Enum.Parse(typeof(CatalystId), catIdStr); }
                catch { continue; }

                var catalystData = CatalystDataRegistry.Instance != null ? CatalystDataRegistry.Instance.GetData(catId) : null;
                if (catalystData == null) continue;

                var instance = new CatalystInstance(catalystData);
                if (cd.TryGetValue("isUsed", out var iu) && iu is bool iuB && iuB) instance.MarkUsed();
                instance.disabledUntilHalfTurn = cd.TryGetValue("disabledUntil", out var du) ? Convert.ToInt32(du) : -1;
                state.catalysts.Add(instance);
            }
        }

        return state;
    }

    private static void ApplyCharacterFields(CharacterState state, Dictionary<string, object> d, CharacterState[] allSlots)
    {
        if (state == null || d == null) return;

        state.currentHP = d.TryGetValue("hp", out var hp) ? Convert.ToInt32(hp) : state.currentHP;
        state.currentUltGauge = d.TryGetValue("ultGauge", out var ug) ? Convert.ToInt32(ug) : state.currentUltGauge;
        state.currentMaxHP = d.TryGetValue("maxHp", out var mh) ? Convert.ToInt32(mh) : state.currentMaxHP;
        state.currentAttack = d.TryGetValue("attack", out var atk) ? Convert.ToInt32(atk) : state.currentAttack;
        state.currentDefense = d.TryGetValue("defense", out var def) ? Convert.ToInt32(def) : state.currentDefense;
        state.isDefending = d.TryGetValue("isDefending", out var idf) && idf is bool idfB && idfB;
        state.defenseExpiresAtHalfTurn = d.TryGetValue("defenseExpiresAt", out var dea) ? Convert.ToInt32(dea) : -1;
        state.hasActedThisTurn = d.TryGetValue("hasActed", out var ha) && ha is bool haB && haB;
        state.hasUsedCatalystThisHalfTurn = d.TryGetValue("hasUsedCatalyst", out var huc) && huc is bool hucB && hucB;
        state.isProtectingAlly = d.TryGetValue("isProtectingAlly", out var ipa) && ipa is bool ipaB && ipaB;
        state.protectedBy = ResolveSlotRef(d, "protectedBySlot", allSlots);
        state.twinBurdenPartner = ResolveSlotRef(d, "twinBurdenPartnerSlot", allSlots);
        state.defeatedBy = ResolveSlotRef(d, "defeatedBySlot", allSlots);
        state.defeatedAtHalfTurn = d.TryGetValue("defeatedAt", out var da) ? Convert.ToInt32(da) : -1;
        state.soulAbsorbed = d.TryGetValue("soulAbsorbed", out var sa) && sa is bool saB && saB;
        state.huntBoundTo = ResolveSlotRef(d, "huntBoundToSlot", allSlots);
        state.isSuspendedAnimation = d.TryGetValue("isSuspended", out var isus) && isus is bool isusB && isusB;
        state.hasRevived = d.TryGetValue("hasRevived", out var hr) && hr is bool hrB && hrB;
        state.morpheMaxHpBonusTotal = d.TryGetValue("morpheBonus", out var mb) ? Convert.ToInt32(mb) : 0;
        state.isTransformed = d.TryGetValue("isTransformed", out var itf) && itf is bool itfB && itfB;
        state.swapHandled = d.TryGetValue("swapHandled", out var sh) && sh is bool shB && shB;

        state.activeEffects.Clear();
        if (d.TryGetValue("effects", out var effObj) && effObj is List<object> effList)
        {
            foreach (var raw in effList)
            {
                if (!(raw is Dictionary<string, object> ed)) continue;
                string name = ed.TryGetValue("name", out var n) ? n as string : null;
                if (string.IsNullOrEmpty(name)) continue;

                var entry = CommonEffectData.Instance != null ? CommonEffectData.Instance.GetEntry(name) : null;
                var effect = new StatusEffect
                {
                    effectName = name,
                    expiresAtHalfTurn = ed.TryGetValue("expiresAt", out var ea) ? Convert.ToInt32(ea) : 0,
                    value = ed.TryGetValue("value", out var v) ? Convert.ToInt32(v) : 0,
                    isDebuff = ed.TryGetValue("isDebuff", out var idb) && idb is bool idbB && idbB,
                    tickValue = ed.TryGetValue("tickValue", out var tv) ? Convert.ToInt32(tv) : 0,
                    icon = entry?.icon,
                    description = entry?.description,
                    source = ResolveSlotRef(ed, "sourceSlot", allSlots)
                };
                // AddEffect()は鎖縛によるバフブロックなど"新規付与時"のガードを持つため、
                // 過去に成立済みの効果を復元する時はガードを経由せず直接足す
                state.activeEffects.Add(effect);
            }
        }
    }

    private static CharacterState ResolveSlotRef(Dictionary<string, object> d, string key, CharacterState[] allSlots)
    {
        if (!d.TryGetValue(key, out var raw)) return null;
        int slot = Convert.ToInt32(raw);
        if (slot < 0 || slot >= allSlots.Length) return null;
        return allSlots[slot];
    }

    private static CatalystId? ParseCatalystIdOrNull(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        try { return (CatalystId)Enum.Parse(typeof(CatalystId), s); }
        catch { return null; }
    }
}
