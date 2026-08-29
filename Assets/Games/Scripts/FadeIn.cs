using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;

public class FadeIn : MonoBehaviour
{
    [SerializeField] private Image fadeImage; // 画面全体を覆う黒Image
    [SerializeField] private float delay = 1.0f;    // フェード開始までの待機秒数
    [SerializeField] private float duration = 0.8f; // フェードにかける秒数

    public event Action OnFadeInComplete;

    private void Start()
    {
        fadeImage.gameObject.SetActive(true);
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        Color c = fadeImage.color;
        c.a = 1f;
        fadeImage.color = c;
        fadeImage.gameObject.SetActive(true);

        yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeImage.color = c;
        fadeImage.gameObject.SetActive(false);
        OnFadeInComplete?.Invoke();
    }
}