using UnityEngine;

public class ProjectileController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [SerializeField] private bool rotateToDirection = true;

    private UnitTeam _ownerTeam;

    private Vector2 _startPosition;
    private Vector2 _targetPosition;
    private Vector2 _previousPosition;

    private float _damage;
    private float _flightDuration;
    private float _elapsedTime;

    private bool _isFlying;
    private bool _isReleased;

    private void FixedUpdate()
    {
        if (!_isFlying || _isReleased)
            return;

        _elapsedTime += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(_elapsedTime / _flightDuration);
        Vector2 linearPosition = Vector2.Lerp(_startPosition, _targetPosition, normalizedTime);
        float height = 4f * arcHeight * normalizedTime * (1f - normalizedTime);

        Vector2 nextPosition = linearPosition + Vector2.up * height;

        UpdateRotation(nextPosition);

        rigidbody2D.MovePosition(nextPosition);
        _previousPosition = nextPosition;
        
        if (normalizedTime >= 1f)
            Release();
    }

    public void Initialize(UnitTeam ownerTeam, Vector2 targetPosition, float damage, float moveSpeed)
    {
        _ownerTeam = ownerTeam;
        _targetPosition = targetPosition;
        _damage = Mathf.Max(0f, damage);

        _startPosition = transform.position;
        _previousPosition = _startPosition;

        float distance = Vector2.Distance(_startPosition, _targetPosition);
        _flightDuration = Mathf.Max(0.05f, distance / Mathf.Max(0.01f, moveSpeed));

        _elapsedTime = 0f;
        _isReleased = false;
        _isFlying = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isFlying || _isReleased)
            return;

        ICombatTarget target = other.GetComponentInParent<ICombatTarget>();

        if (!IsValidEnemy(target))
            return;

        target.TakeDamage(_damage);
        Release();
    }

    private bool IsValidEnemy(ICombatTarget target)
    {
        if (target == null || target.IsDead || target.Team == _ownerTeam)
            return false;

        MonoBehaviour targetObject = target as MonoBehaviour;

        return targetObject != null && targetObject.gameObject.activeInHierarchy;
    }

    private void UpdateRotation(Vector2 nextPosition)
    {
        if (!rotateToDirection)
            return;

        Vector2 direction = nextPosition - _previousPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rigidbody2D.SetRotation(angle);
    }

    private void Release()
    {
        if (_isReleased)
            return;

        _isReleased = true;
        _isFlying = false;

        PoolManager.Instance.Release(gameObject);
    }

    private void OnDisable()
    {
        _isFlying = false;
        _isReleased = false;
        _elapsedTime = 0f;
    }
}
