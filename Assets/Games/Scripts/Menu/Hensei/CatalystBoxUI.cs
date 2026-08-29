using UnityEngine;
using UnityEngine.UI;

public class CatalystBoxUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button targetButton; // クリックで詳細/選択グリッドを開く

    private CatalystData _data;

    public CatalystData CurrentData => _data;

    public void Bind(CatalystData data)
    {
        _data = data;
        if (iconImage != null) iconImage.sprite = data.icon;
    }

    public void Setup(int characterBoxIndex, Hensei hensei)
    {
        targetButton.onClick.AddListener(() => hensei.OnClickCatalystBox(characterBoxIndex));
    }
}