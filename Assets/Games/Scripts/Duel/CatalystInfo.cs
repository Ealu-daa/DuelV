using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class CatalystButtonUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText; // これは子でOKなら
    [SerializeField] private Button button;

    private CatalystData data;

    public void Setup(CatalystData data, System.Action<CatalystData> onClick)
    {
        this.data = data;
        nameText.text = data.catalystName;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(data));
    }
}
