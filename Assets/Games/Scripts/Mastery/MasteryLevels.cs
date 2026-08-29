/// <summary>
/// キャラマスタリーのレベル判定テーブル。XPは対戦ごとに「そのキャラが出ていたターン数n」+勝利ボーナス+5が
/// 上限無く加算され続ける、シンプルな累計XP制。
///
/// 各Lvに到達するための累計XP: Lv1=10 / Lv2=50 / Lv3=200 / Lv4=400 / Lv5=1000
/// (間隔で言うとLv1=10, Lv2=+40, Lv3=+150, Lv4=+200, Lv5=+600)
///
/// 報酬(称号/アイコン/スプライットバリアント)の中身はまだ無い。報酬を後から実装する際は、
/// GetLevel(xp)は既に蓄積済みのXPからその場で判定できるので、「達成済みなら即座に受け取れる」を
/// 特別なバックフィル処理無しに実現できる(claim側は「Lvがxx以上か」を見るだけでよい)。
/// </summary>
public static class MasteryLevels
{
    // 添字0=Lv1, 1=Lv2, ... の「そのLvに到達するための累計XP」
    public static readonly int[] CumulativeXpThresholds = { 10, 50, 200, 400, 1000 };
    public const int MaxLevel = 5;

    /// <summary>累計XPから現在のレベル(0〜5、0はLv1未満)を返す</summary>
    public static int GetLevel(int xp)
    {
        int level = 0;
        for (int i = 0; i < CumulativeXpThresholds.Length; i++)
        {
            if (xp < CumulativeXpThresholds[i]) break;
            level = i + 1;
        }
        return level;
    }

    /// <summary>次のレベルに到達するまでの残りXP。既に最大レベルなら-1</summary>
    public static int GetXpToNextLevel(int xp)
    {
        int level = GetLevel(xp);
        if (level >= MaxLevel) return -1;
        return CumulativeXpThresholds[level] - xp;
    }

    /// <summary>現在のレベルの開始XPと次のレベルの開始XP(進捗バー表示用)。最大レベルならnextはcurrentと同じ値を返す</summary>
    public static void GetProgressRange(int xp, out int currentLevelStartXp, out int nextLevelStartXp)
    {
        int level = GetLevel(xp);
        currentLevelStartXp = level == 0 ? 0 : CumulativeXpThresholds[level - 1];
        nextLevelStartXp = level >= MaxLevel ? currentLevelStartXp : CumulativeXpThresholds[level];
    }

    /// <summary>
    /// Lv1〜5を実際のXP間隔に関わらず均等5等分にした場合の進捗バー用fillAmount(0〜1)。
    /// Zukanのキャラ詳細・ResultSceneのマスタリー行など、同じ見た目の進捗バーを出す箇所は
    /// これを共通で使うこと(見た目の計算式を1箇所にまとめておく)。
    /// </summary>
    public static float GetEqualSegmentFillAmount(int xp)
    {
        int level = GetLevel(xp);
        if (level >= MaxLevel) return 1f;

        GetProgressRange(xp, out int rangeStart, out int rangeEnd);
        int rangeSize = rangeEnd - rangeStart;
        float withinLevelFraction = rangeSize <= 0 ? 1f : (float)(xp - rangeStart) / rangeSize;

        return (level + withinLevelFraction) / MaxLevel;
    }
}
