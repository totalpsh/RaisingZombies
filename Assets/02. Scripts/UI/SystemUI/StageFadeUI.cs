using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class StageFadeUI : BaseUI
{
    [SerializeField, Min(0.1f)] private float fadeDuration = 0.5f;
    
    private CanvasGroup _canvasGroup;
    private Tween _fadeTween;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        
        _canvasGroup.alpha = 0f;
        SetInputBlock(false);
    }

    private void SetInputBlock(bool block)
    {
        _canvasGroup.blocksRaycasts = block;
        _canvasGroup.interactable = block;
    }
    
    public async Task FadeOutAsync()
    {
        SetInputBlock(true);
        await FadeAsync(1f);
    }

    public async Task FadeInAsync()
    {
        await FadeAsync(0f);
        SetInputBlock(false);
    }

    private async Task FadeAsync(float targetAlpha)
    {
        _fadeTween?.Kill();

        _fadeTween = _canvasGroup
            .DOFade(targetAlpha, fadeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true);

        await _fadeTween.AsyncWaitForCompletion();
        _fadeTween = null;
    }
    
    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }
}
