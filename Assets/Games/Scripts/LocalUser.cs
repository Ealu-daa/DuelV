using UnityEngine;

/// <summary>
/// 呼び出し元から見たUIDの取得口。既存の呼び出し箇所(BattleManager, Hensei, MenuManager等)は
/// 一切変更せずに済むよう、このメソッドのシグネチャ(同期・string一つ返すだけ)は維持している。
///
/// FirebaseAuthBridge.EnsureSignedIn()が完了していれば、そこで発行された本物のFirebase UIDを返す。
/// まだサインインが完了していない場合のみ、端末生成の仮GUIDにフォールバックする
/// (基本的にはMenuManagerの起動時にEnsureSignedInを済ませてから他の画面に進む想定なので、
/// このフォールバックが使われるのは異常系のみのはず)。
/// </summary>
public static class LocalUser
{
    const string Key = "duelv_uid"; // 旧方式(認証未完了時)のフォールバック用

    public static string GetOrCreateUid()
    {
        if (FirebaseAuthBridge.Instance.IsSignedIn)
            return FirebaseAuthBridge.Instance.Uid;

        if (!PlayerPrefs.HasKey(Key))
        {
            PlayerPrefs.SetString(Key, System.Guid.NewGuid().ToString());
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetString(Key);
    }
}
