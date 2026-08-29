/// <summary>
/// プロフィールフレームのID。現時点ではコスメ枠(Echo購入型・実績型)の詳細が未設計なので、
/// Noneのみ用意してある。デザインが固まり次第ここに項目を足していく。
/// </summary>
public enum ProfileFrameId
{
    None = 0,

    // 本番素材
    NormalFrame = 100,

    // 動作確認用の仮フレーム(適当な素材でOK、本番用に差し替えたら名前も付け直すこと)
    Test1 = 1,
    Test2 = 2,
}
