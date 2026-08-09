using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    private float _fadeSpeed = 3f;

    private void Awake()
    {
        HideImmediate();
    }

    public void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);
    }

    public async Task ShowAsync()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += Time.unscaledDeltaTime * _fadeSpeed;
            await Task.Yield();
        }

        canvasGroup.alpha = 1f;
    }

    public async Task HideAsync()
    {
        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.unscaledDeltaTime * _fadeSpeed;
            await Task.Yield();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);
    }

    public void SetProgress(float value)
    {
        if (progressBar != null)
            progressBar.value = value;
    }

    public void SetText(string text)
    {
        if (loadingText != null)
            loadingText.text = text;
    }

}