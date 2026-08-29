/// <summary>
/// ミッションの種別。リセットのタイミングが変わる。
/// Permanent: リセットなし(一度達成したらそれきり)
/// Daily: UTC日付が変わったらリセット
/// Weekly: UTC週(月曜始まり)が変わったらリセット
/// </summary>
public enum MissionCategory
{
    Permanent,
    Daily,
    Weekly,
}
