using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// キャラごとのマスタリーXPを管理する。PlayerCollection/PlayerProfileと同じパターン。
/// 実体はFirestoreの users/{uid} ドキュメントの masteryXp フィールド(キー=CharacterIdの文字列、値=累計XP)。
///
/// XP付与ルール(対戦終了時、BattleManager.ShowResultから呼ぶ):
///   そのキャラが1回でも自チームのFWとして出た場合、そのキャラへ「試合のターン数」+「勝ったら+5」を加算する。
///   BKのまま一度もFWに出なかったキャラは対象外。
/// </summary>
public static class CharacterMastery
{
    public const int WinBonus = 5;

    public static bool IsLoaded { get; private set; }

    private static readonly Dictionary<int, int> xpByCharacterId = new Dictionary<int, int>();

    /// <summary>Firestoreからマスタリー状況を読み込む(画面表示前に呼ぶ)。取得できなければ全員0XP扱いになる</summary>
    public static void Load(Action onLoaded = null)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            xpByCharacterId.Clear();

            if (profile != null && profile.TryGetValue("masteryXp", out var m) && m is Dictionary<string, object> map)
            {
                foreach (var kv in map)
                {
                    if (int.TryParse(kv.Key, out int charId))
                        xpByCharacterId[charId] = Convert.ToInt32(kv.Value);
                }
            }

            IsLoaded = true;
            onLoaded?.Invoke();
        });
    }

    public static int GetXp(CharacterId id) => xpByCharacterId.TryGetValue((int)id, out var xp) ? xp : 0;
    public static int GetLevel(CharacterId id) => MasteryLevels.GetLevel(GetXp(id));

    /// <summary>
    /// 対戦終了時にBattleManagerから呼ぶ。自チームでFWとして1回でも出たキャラ全員へ
    /// 「試合のターン数(+勝利なら+5)」を加算し、まとめてFirestoreへ保存する。
    /// ローカルキャッシュへの反映は同期的に終わるので、戻り値(ResultScene表示用の内訳)はその場で使える。
    /// </summary>
    public static List<CharacterMasteryResult> GrantMatchXp(IEnumerable<CharacterState> playerTeamCharacters, int matchTurnCount, bool won, Action<bool> onDone = null)
    {
        var results = new List<CharacterMasteryResult>();

        if (!IsLoaded)
        {
            // 未ロードのまま保存すると、ローカルキャッシュが空/不完全な状態でmasteryXp全体を
            // 上書きしてしまい、他キャラの既存XPを消してしまう。安全のため何もせず終える
            UnityEngine.Debug.LogWarning("[CharacterMastery] 未ロードのためXP付与をスキップしました。");
            onDone?.Invoke(false);
            return results;
        }

        var participants = playerTeamCharacters.Where(c => c.everActiveAsForward).ToList();
        if (participants.Count == 0) { onDone?.Invoke(true); return results; }

        int gain = matchTurnCount + (won ? WinBonus : 0);
        if (gain <= 0) { onDone?.Invoke(true); return results; }

        foreach (var c in participants)
        {
            int id = (int)c.data.id;
            int oldXp = GetXp(c.data.id);
            int newXp = oldXp + gain;
            xpByCharacterId[id] = newXp;

            results.Add(new CharacterMasteryResult
            {
                characterId = c.data.id,
                xpGained = gain,
                oldXp = oldXp,
                newXp = newXp,
                oldLevel = MasteryLevels.GetLevel(oldXp),
                newLevel = MasteryLevels.GetLevel(newXp)
            });
        }

        // Firestoreの部分更新はフィールド単位の上書きなので、masteryXp全体をローカルキャッシュから作り直して送る
        var mapPayload = new Dictionary<string, object>();
        foreach (var kv in xpByCharacterId)
            mapPayload[kv.Key.ToString()] = kv.Value;

        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object> { { "masteryXp", mapPayload } },
            onDone
        );

        return results;
    }
}
