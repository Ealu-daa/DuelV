using System;
using System.Collections.Generic;

/// <summary>
/// アカウントレベル。マスタリーとは別に、独立したXP(Firestoreの users/{uid}.accountXp)を持つ。
/// 「進行の可視化がメイン」の位置付けなので、解放要素は持たせない(プリセット枠・ランクマ等は
/// 常に全解放済み扱い)。
///
/// XP付与ルール(対戦終了時、BattleManager.ShowResultから呼ぶ):
///   試合のターン数 + (勝ったら+5 / 負けたら+2) を、試合ごとに1回だけ加算する(キャラ単位ではない)。
///
/// レベル換算: 100XPごとに+1レベル、上限なし。Lv1スタート(0〜99XPがLv1)。
/// </summary>
public static class AccountLevel
{
    public const int XpPerLevel = 100;
    public const int WinBonus = 5;
    public const int LossBonus = 2;

    public static bool IsLoaded { get; private set; }
    public static int TotalXp { get; private set; }

    public static int Level => 1 + TotalXp / XpPerLevel;
    public static int XpIntoCurrentLevel => TotalXp % XpPerLevel;

    /// <summary>FirestoreからアカウントXPを読み込む(画面表示前に呼ぶ)。取得できなければ0XP扱いになる</summary>
    public static void Load(Action onLoaded = null)
    {
        string uid = LocalUser.GetOrCreateUid();
        FirestoreBridge.Instance.GetUserProfile(uid, profile =>
        {
            TotalXp = 0;
            if (profile != null && profile.TryGetValue("accountXp", out var x))
                TotalXp = Convert.ToInt32(x);

            IsLoaded = true;
            onLoaded?.Invoke();
        });
    }

    /// <summary>
    /// 対戦終了時にBattleManagerから呼ぶ。試合のターン数+(勝利なら+5/敗北なら+2)を加算しFirestoreへ保存する。
    /// ローカルキャッシュへの反映は同期的に終わるので、戻り値(ResultScene表示用)はその場で使える。
    /// </summary>
    public static AccountLevelResult GrantMatchXp(int matchTurnCount, bool won, Action<bool> onDone = null)
    {
        if (!IsLoaded)
        {
            // 未ロードのまま加算すると、ローカルキャッシュ(0)を起点にした誤った値でaccountXpを
            // 上書きしてしまい、既存の蓄積を消してしまう。安全のため何もせず終える
            UnityEngine.Debug.LogWarning("[AccountLevel] 未ロードのためXP付与をスキップしました。");
            onDone?.Invoke(false);
            return null;
        }

        int gain = matchTurnCount + (won ? WinBonus : LossBonus);
        int oldXp = TotalXp;
        int newXp = oldXp + gain;
        TotalXp = newXp;

        var result = new AccountLevelResult
        {
            xpGained = gain,
            oldXp = oldXp,
            newXp = newXp,
            oldLevel = 1 + oldXp / XpPerLevel,
            newLevel = 1 + newXp / XpPerLevel
        };

        FirestoreBridge.Instance.SaveProfileFields(
            LocalUser.GetOrCreateUid(),
            new Dictionary<string, object> { { "accountXp", newXp } },
            onDone
        );

        return result;
    }
}
