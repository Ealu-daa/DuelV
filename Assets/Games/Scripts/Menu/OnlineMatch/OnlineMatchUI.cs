using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineMatchUI : MonoBehaviour
{
    [Header("部屋作成")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private GameObject roomCodeDisplayPanel; // コード表示欄の親(作成前は非表示)
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private Button copyButton;
    [SerializeField] private GameObject menu;

    [Header("戻るボタン(トップ状態=menu表示中だけ出す)")]
    [SerializeField] private GameObject backButtonObj;

    [Header("入室")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private Button joinRoomButton;

    [Header("状態表示")]
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private GameObject waitingPanel; // 「相手を待っています...」
    [SerializeField] private float messageDisplaySeconds = 2.5f; // メッセージを表示しておく秒数

    private Coroutine hideMessageRoutine;

    void Start()
    {
        createRoomButton.onClick.AddListener(OnClickCreateRoom);
        copyButton.onClick.AddListener(OnClickCopyCode);
        joinRoomButton.onClick.AddListener(OnClickJoinRoom);

        roomCodeDisplayPanel.SetActive(false);
        waitingPanel.SetActive(false);
        if (backButtonObj != null) backButtonObj.SetActive(true); // トップ状態なので表示
        SetError("");
    }

    private void OnClickCreateRoom()
    {
        SetError("");
        createRoomButton.interactable = false;
        menu.SetActive(false);
        if (backButtonObj != null) backButtonObj.SetActive(false);

        RoomManager.Instance.CreateRoom(
            onCreated: code =>
            {
                roomCodeText.text = code;
                roomCodeDisplayPanel.SetActive(true);
                waitingPanel.SetActive(true); // 相手の入室待ち
            },
            onError: msg =>
            {
                createRoomButton.interactable = true;
                SetError(msg);
            }
        );
    }

    private void OnClickCopyCode()
    {
        GUIUtility.systemCopyBuffer = roomCodeText.text;
        SetError("コピーしました");
    }

    private void OnClickJoinRoom()
    {
        SetError("");
        string code = codeInputField.text;

        if (string.IsNullOrWhiteSpace(code))
        {
            SetError("コードを入力してください");
            return;
        }

        joinRoomButton.interactable = false;

        RoomManager.Instance.JoinRoom(
            code,
            onJoined: () =>
            {
                menu.SetActive(false);
                waitingPanel.SetActive(true);
                if (backButtonObj != null) backButtonObj.SetActive(false);
                // status:"matched"への更新はJoinRoom内で即座に送っているため
                // 直後にRoomManager側のポーリングでHenseiSceneへ遷移する
            },
            onError: msg =>
            {
                joinRoomButton.interactable = true;
                SetError(msg);
            }
        );
    }

    private void SetError(string message)
    {
        if (hideMessageRoutine != null)
        {
            StopCoroutine(hideMessageRoutine);
            hideMessageRoutine = null;
        }

        errorText.text = message;
        errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));

        if (!string.IsNullOrEmpty(message))
        {
            hideMessageRoutine = StartCoroutine(HideMessageAfterDelay());
        }
    }

    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplaySeconds);
        errorText.gameObject.SetActive(false);
        hideMessageRoutine = null;
    }

    public void OnCpuClick()
    {
        SceneManager.LoadScene("HenseiScene");
    }

    // 「戻る」ボタンのOnClickから呼ぶ(トップ状態=menu表示中のみボタン自体が出ている)
    public void OnBackButtonClicked()
    {
        // 部屋作成/入室していた場合、Firestoreの監視や状態が残ったままだと
        // 次に部屋を作り直した時に古い監視結果が紛れ込むことがあるので、必ず後片付けしてから戻る
        if (RoomManager.Instance != null) RoomManager.Instance.LeaveRoom();

        SceneManager.LoadScene("MenuScene");
    }
}