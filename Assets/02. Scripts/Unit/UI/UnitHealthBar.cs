using UnityEngine;

public class UnitHealthBar : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private SpriteRenderer fillSprite;

    private Vector3 _fullScale;
    private Vector3 _fullPosition;
    private float _fullWidth;
    private float _left;

    private void Awake()
    {
        _fullScale = fill.localScale;
        _fullPosition = fill.localPosition;

        _fullWidth = fillSprite.sprite.bounds.size.x * Mathf.Abs(_fullScale.x);
        _left = _fullPosition.x - _fullWidth * 0.5f;
    }

    public void SetHealth(float current, float max)
    {
        float rate = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        Vector3 scale = _fullScale;
        scale.x *= rate;
        fill.localScale = scale;

        Vector3 position = _fullPosition;
        position.x = _left + _fullWidth * rate * 0.5f;
        fill.localPosition = position;
    }

    public void ResetBar()
    {
        fill.localScale = _fullScale;
        fill.localPosition = _fullPosition;
    }
}
