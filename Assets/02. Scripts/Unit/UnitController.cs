using System;
using UnityEngine;

public class UnitController : MonoBehaviour
{
    [SerializeField] private UnitData data;

    [SerializeField] private LayerMask unitLayer;
    [SerializeField, Min(0.01f)] private float targetSearchRange = 3f;
    
    private UnitModel _model;
    private UnitController _currentTarget;
    private bool _isInitialized;

    private Collider2D[] _targetBuffer;
    private ContactFilter2D _targetFilter;
    
    // 프로퍼티
    public UnitModel Model => _model;
    public UnitTeam Team => data.Team;
    public bool IsDead => _model == null || _model.IsDead;

    
    private void Awake()
    {
        Init();
    }

    private void Update()
    {
        Move();
        
        float deltaTime = Time.deltaTime;
        
        _model.TickAttackCooldown(deltaTime);
        UpdateRegeneration(deltaTime);
        
        FindTarget();
        TryAction();
    }

    private void Init()
    {
        if (data == null)
        {
            enabled = false;
            return;
        }

        UnitStats stats = new UnitStats(data);
        _model = new UnitModel(stats);
    }
    
    private void Move()
    {
        float direction = Team == UnitTeam.Zombie ? 1f : -1f;
        float distance = _model.Stats.MoveSpeed * Time.deltaTime;

        transform.Translate(Vector3.right * (direction * distance));
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
        if (IsValidEnemy(_currentTarget))
            return;

        _currentTarget = null;

        Collider2D[] results = Physics2D.OverlapCircleAll(
            transform.position,
            targetSearchRange,
            unitLayer);

        float nearestDistance = float.MaxValue;

        foreach (Collider2D result in results)
        {
            UnitController candidate =
                result.GetComponentInParent<UnitController>();

            if (!IsValidEnemy(candidate))
                continue;

            float distance = GetHorizontalDistance(candidate);

            if (distance >= nearestDistance)
                continue;

            _currentTarget = candidate;
            nearestDistance = distance;
        }
    }

    private void TryAction()
    {
        if (!IsValidEnemy(_currentTarget))
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
        float direction =
            Team == UnitTeam.Zombie ? 1f : -1f;

        float distance =
            _model.Stats.MoveSpeed * Time.deltaTime;

        transform.Translate(
            Vector3.right * direction * distance);
    }

    private void TryAttack()
    {
        if (!_model.CanAttack)
            return;

        if (!IsValidEnemy(_currentTarget))
            return;

        _currentTarget.TakeDamage(
            _model.Stats.AttackPower);

        _model.ResetAttackCooldown();
    }

    public void TakeDamage(float damage)
    {
        if (!_isInitialized || _model.IsDead)
            return;

        _model.TakeDamage(damage);

        if (_model.IsDead)
            Die();
    }

    private void Die()
    {
        _currentTarget = null;
        enabled = false;

        Destroy(gameObject);
    }

    private bool IsValidEnemy(UnitController target)
    {
        return target != null
               && target != this
               && !target.IsDead
               && target.Team != Team;
    }

    private float GetHorizontalDistance(
        UnitController target)
    {
        return Mathf.Abs(
            target.transform.position.x
            - transform.position.x);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            targetSearchRange);

        if (data == null)
            return;

        Gizmos.DrawWireSphere(
            transform.position,
            data.AttackRange);
    }
#endif
    
}
