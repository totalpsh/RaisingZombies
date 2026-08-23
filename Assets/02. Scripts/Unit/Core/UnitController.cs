using System;
using UnityEngine;
using UnityEngine.Serialization;

public class UnitController : MonoBehaviour, ICombatTarget
{
    [SerializeField] private UnitAction unitAction;
    [SerializeField] private UnitAnimation animation;
    
    [SerializeField] private LayerMask unitLayer;
    [SerializeField, Min(0.01f)] private float targetSearchRange = 3f;

    [SerializeField]private UnitData _data;
    [SerializeField]private UnitModel _model;
    [SerializeField] private Collider2D unitCollider;

    // private UnitController _currentTarget;
    
    private bool _isInitialized;

    public Collider2D TargetCollider => unitCollider;
    private ICombatTarget _currentTarget;
    private Collider2D[] _targetBuffer;
    private ContactFilter2D _targetFilter;
    
    // 프로퍼티
    public UnitData Data => _data;
    public UnitModel Model => _model;
    public UnitTeam Team => _data.Team;
    public bool IsDead => !_isInitialized || _model.IsDead;

    public Transform TargetTransform => transform;

    public event Action<UnitController> Died; 
    
    private void Update()
    {
        if (!_isInitialized || _model.IsDead)
            return;
        
        float deltaTime = Time.deltaTime;
        
        _model.TickAttackCooldown(deltaTime);
        UpdateRegeneration(deltaTime);
        
        FindTarget();
        TryAction();
    }

    public void Initialize(UnitData data, UnitStats stats)
    {
        if (data == null)
        {
            Debug.LogError($"[{nameof(UnitController)}] UnitData가 null입니다.", this);
            return;
        }

        if (unitAction == null)
        {
            Debug.Log("UnitAction 없음");
            return;
        }

        _data = data;
        _model = new UnitModel(stats);
        
        _currentTarget = null;
        _isInitialized = true;
        
        if (unitCollider != null)
            unitCollider.enabled = true;
        
        enabled = true;
        ResetAnimation();
    }
    
    private void UpdateRegeneration(float deltaTime)
    {
        float healthRegen = _model.Stats.HealthRegen;

        if (healthRegen <= 0f)
            return;

        _model.Heal(healthRegen * deltaTime);
    }
    
    private void FindTarget()
    {
        if (IsValidTarget(_currentTarget))
            return;

        _currentTarget = null;

        Collider2D[] results = Physics2D.OverlapCircleAll(transform.position, targetSearchRange, unitLayer);

        float nearestDistance = float.MaxValue;

        foreach (Collider2D result in results)
        {
            ICombatTarget candidate = result.GetComponentInParent<ICombatTarget>();

            if (!IsValidTarget(candidate))
                continue;

            if (unitAction.RequiresTargetAhead && !IsAhead(candidate))
                continue;

            float distance = GetHorizontalDistance(candidate);

            if (distance >= nearestDistance)
                continue;

            _currentTarget = candidate;
            nearestDistance = distance;
        }
    }
    
    private bool IsAhead(ICombatTarget target)
    {
        float offset = target.TargetTransform.position.x - transform.position.x;

        return Team == UnitTeam.Zombie ? offset > 0f : offset < 0f;
    }

    private void TryAction()
    {
        if (!IsValidTarget(_currentTarget))
        {
            MoveForward();
            return;
        }

        float distance = GetHorizontalDistance(_currentTarget);

        if (distance > _model.Stats.AttackRange)
        {
            MoveForward();
            return;
        }

        TryAttack();
    }

    private void MoveForward()
    {
        animation.PlayWalk();
        
        float direction = Team == UnitTeam.Zombie ? 1f : -1f;

        float distance = _model.Stats.MoveSpeed * Time.deltaTime;

        transform.Translate(Vector3.right * (direction * distance));
    }

    private void TryAttack()
    {
        if (!_model.CanAttack)
            return;

        if (!IsValidTarget(_currentTarget))
            return;
        
        // _currentTarget.TakeDamage(_model.Stats.AttackPower);
        unitAction.Execute(this, _currentTarget, _model.Stats.AttackPower);

        _model.ResetAttackCooldown();
        animation.PlayAttack();
    }

    public void TakeDamage(float damage)
    {
        if (!_isInitialized || _model.IsDead)
            return;

        _model.TakeDamage(damage);

        if (_model.IsDead)
        {
            Die();
            return;
        }

        animation.PlayHit();
    }

    private void Die()
    {
        _currentTarget = null;
        enabled = false;

        if (unitCollider != null)
            unitCollider.enabled = false;
        
        Died?.Invoke(this);
        
        animation.PlayDie(ReleaseToPool);
    }
    
    private void ReleaseToPool()
    {
        PoolManager.Instance.Release(gameObject);
    }

    private bool IsValidTarget(ICombatTarget target)
    {
        if (target == null || unitAction == null)
            return false;
        
        MonoBehaviour targetObj = target as MonoBehaviour;
        
        return targetObj != null && targetObj.gameObject.activeInHierarchy && !target.IsDead && unitAction.CanTarget(this, target);
    }

    private float GetHorizontalDistance(ICombatTarget target)
    {
        if (unitCollider == null || target.TargetCollider == null)
        
            return Mathf.Abs(target.TargetTransform.position.x - transform.position.x);
        
        ColliderDistance2D distance = unitCollider.Distance(target.TargetCollider);

        return Mathf.Max(0f, distance.distance);
    }

    private void ResetAnimation()
    {
        animation.ResetState();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            targetSearchRange);

        if (_data == null)
            return;

        Gizmos.DrawWireSphere(
            transform.position,
            _data.AttackRange);
    }
#endif
    
}
