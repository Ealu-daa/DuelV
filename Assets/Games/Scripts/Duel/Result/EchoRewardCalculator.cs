using UnityEngine;

public struct EchoBreakdown
{
    public int BaseEcho;
    public int FirstWinBonus;
    public int Total => BaseEcho + FirstWinBonus;
}

public static class EchoRewardCalculator
{
    public static EchoBreakdown Calculate(BattleResultData data, bool isFirstWinToday)
    {
        int flat = data.isVictory ? 125 : 75;
        int baseValue = 10 * data.endHalfTurn + flat;
        float multiplier = Mathf.Min(5f * data.endHalfTurn, 100f) / 100f;

        var result = new EchoBreakdown
        {
            BaseEcho = Mathf.CeilToInt(baseValue * multiplier)
        };

        if (data.isVictory && isFirstWinToday) result.FirstWinBonus = 20;

        return result;
    }
}