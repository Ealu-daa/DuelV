/// <summary>
/// プロフィールアイコンのID。CharacterId/CatalystIdと同じ命名パターン。
/// 現時点では中身(実際のアイコン素材・入手経路)がまだ無いので、Noneのみ用意してある。
/// マスタリー報酬(Lv2・Lv4)等でアイコンを追加する際、ここに項目を足していく。
/// </summary>
public enum ProfileIconId
{
    None = 0,

    // 本番素材
    DuelV = 100,

    // 動作確認用の仮アイコン(適当な素材でOK、本番用に差し替えたら名前も付け直すこと)
    Test1 = 1,
    Test2 = 2,
    Test3 = 3,
}
