using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ButtonClickSoundを手作業で1個ずつアタッチするのが面倒なので、まとめて付けるためのEditor拡張。
/// 「Editor」フォルダ配下に置くことでビルドには含まれない(Unityの規約)。
/// </summary>
public static class ButtonClickSoundBulkAttacher
{
    [MenuItem("Tools/DuelV/SE/開いてるシーンの全Buttonに ButtonClickSound を付ける")]
    private static void AttachToAllButtonsInScene()
    {
        // 非アクティブなGameObject上のButtonも含めて拾う(inactiveなパネルの中のボタン等も取りこぼさないように)
        var buttons = Object.FindObjectsOfType<Button>(true);
        int added = AttachToButtons(buttons);

        Debug.Log($"[ButtonClickSoundBulkAttacher] シーン内のButton {buttons.Length}個中、{added}個に ButtonClickSound を追加しました(既に付いていたものはスキップ)");
    }

    [MenuItem("Tools/DuelV/SE/選択中のオブジェクト配下の全Buttonに ButtonClickSound を付ける")]
    private static void AttachToSelectedHierarchy()
    {
        int added = 0;
        int totalButtons = 0;

        foreach (var go in Selection.gameObjects)
        {
            var buttons = go.GetComponentsInChildren<Button>(true);
            totalButtons += buttons.Length;
            added += AttachToButtons(buttons);
        }

        Debug.Log($"[ButtonClickSoundBulkAttacher] 選択中のオブジェクト配下のButton {totalButtons}個中、{added}個に ButtonClickSound を追加しました(既に付いていたものはスキップ)");
    }

    [MenuItem("Tools/DuelV/SE/開いてるシーンの全Buttonから ButtonClickSound を外す")]
    private static void RemoveFromAllButtonsInScene()
    {
        var sounds = Object.FindObjectsOfType<ButtonClickSound>(true);
        foreach (var sound in sounds)
            Undo.DestroyObjectImmediate(sound);

        Debug.Log($"[ButtonClickSoundBulkAttacher] ButtonClickSound を {sounds.Length}個 取り外しました");
    }

    // Prefabアセット自体(シーンに配置していない、実行時にInstantiateされるものも含む)のButtonに付けたい場合はこちら。
    // シーン内のGameObjectと違い、Prefabアセットの書き換えはUndo(Ctrl+Z)が効かない直接のファイル編集になるため、
    // 確認ダイアログを挟む。心配ならバージョン管理でコミット/バックアップしてから実行すること
    [MenuItem("Tools/DuelV/SE/プロジェクト内の全PrefabのButtonに ButtonClickSound を付ける")]
    private static void AttachToAllButtonsInPrefabs()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "ButtonClickSound を Prefab に一括追加",
            "プロジェクト内の全Prefabアセットを直接書き換えます。この操作はUndo(Ctrl+Z)で戻せません。\n" +
            "心配な場合は先にバージョン管理でコミット/バックアップしてから実行してください。\n\n続行しますか？",
            "続行", "キャンセル");
        if (!confirmed) return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int prefabsTouched = 0;
        int buttonsAdded = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var buttons = root.GetComponentsInChildren<Button>(true);
                if (buttons.Length == 0) continue;

                int addedInThisPrefab = 0;
                foreach (var button in buttons)
                {
                    if (button.GetComponent<ButtonClickSound>() != null) continue;
                    button.gameObject.AddComponent<ButtonClickSound>();
                    addedInThisPrefab++;
                }

                if (addedInThisPrefab > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabsTouched++;
                    buttonsAdded += addedInThisPrefab;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ButtonClickSoundBulkAttacher] Prefab {prefabsTouched}個、合計Button {buttonsAdded}個に ButtonClickSound を追加しました");
    }

    private static int AttachToButtons(Button[] buttons)
    {
        int added = 0;
        foreach (var button in buttons)
        {
            if (button.GetComponent<ButtonClickSound>() != null) continue;

            Undo.AddComponent<ButtonClickSound>(button.gameObject);
            added++;
        }
        return added;
    }
}
