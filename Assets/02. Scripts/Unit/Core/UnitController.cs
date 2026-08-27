using System;
using UnityEngine;

public class UnitController : MonoBehaviour, ICombatTarget
{
    [SerializeField] private UnitAction unitAction;
    [SerializeField] private UnitAnimation animation;
    
    [SerializeField] private LayerMask unitLayer;
    [SerializeField, Min(0.01f)] private float targetSearchRange = 3f;

    [SerializeField]private UnitData _data;
    [SerializeField]private UnitModel _model;
    [SerializeField] private Collider2D unitCollider;

    [SerializeField] private bool useHit = true;
    [SerializeField] private bool useHitSlow;
    [SerializeField, Range(0.1f, 1f)] private float hitSpeedRate = 0.6f;
    [SerializeField, Min(0f)] private float hitSlowTime = 0.15f;
    
    [Header("전투")]
    [SerializeField, Min(0.05f)] private float targetReevaluationInterval;
    [SerializeField, Min(0f)] private float attackerCountPenalty = 1f;
    [SerializeField, Min(0f)] private float currentTargetBonus = 1.5f;
    [SerializeField] private Vector2 combatYBounds = new(-1.5f, 1.5f);
    [SerializeField, Min(0.01f)] private float slotTolerance = 0.05f;
    [SerializeField, Min(0f)] private float slotLeeway = 0.25f;
    
    [Header("UI")]
    [SerializeField] private UnitHealthBar healthBar;
    
    private float _targetReevaluationTimer;
    
    // private UnitController _currentTarget;
    
    private bool _isInitialized;

    public Collider2D TargetCollider => unitCollider;
    private CombatSlots _slots;
    private ICombatTarget _currentTarget;
    private Collider2D[] _targetBuffer;
    private ContactFilter2D _targetFilter;
    private float _slowEndTime;
    
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

        healthBar.SetHealth(_model.CurrentHealth, _model.Stats.MaxHealth);
        
        ChangeTarget(null);
        
        _targetReevaluationTimer = UnityEngine.Random.Range(0f, targetReevaluationInterval);
        
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
        _targetReevaluationTimer -= Time.deltaTime;
        
        bool currentTargetValid = IsValidTarget(_currentTarget);
        
        if (currentTargetValid && _targetReevaluationTimer > 0f)
            return;

        _targetReevaluationTimer = targetReevaluationInterval;
        
        ICombatTarget bestTarget = currentTargetValid ? _currentTarget : null;
        float bestScore = currentTargetValid ? CalculateTargetScore(_currentTarget) : float.MaxValue;

        Collider2D[] results = Physics2D.OverlapCircleAll(transform.position, targetSearchRange, unitLayer);

        foreach (Collider2D result in results)
        {
            ICombatTarget candidate =
                result.GetComponentInParent<ICombatTarget>();

            if (!IsValidTarget(candidate))
                continue;

            if (unitAction.RequiresTargetAhead && !IsAhead(candidate))
                continue;

            float score = CalculateTargetScore(candidate);

            if (score >= bestScore)
                continue;

            bestTarget = candidate;
            bestScore = score;
        }
        
        ChangeTarget(bestTarget);
    }
    
    private float CalculateTargetScore(ICombatTarget target)
    {
        float distance = GetHorizontalDistance(target);
        int attackerCount = CombatTargetTracker.GetAttackerCount(target);
        float score = distance + attackerCount * attackerCountPenalty;

        if (target == _currentTarget)
            score -= currentTargetBonus;

        return score;
    }
    
    private void ChangeTarget(ICombatTarget target)
    {
        if (ReferenceEquals(_currentTarget, target))
            return;

        ReleaseSlot();
        CombatTargetTracker.Unregister(_currentTarget);

        _currentTarget = target;

        CombatTargetTracker.Register(_currentTarget);
    }
    
    private bool ReserveSlot(out Vector3 position)
    {
        position = transform.position;

        if (_currentTarget == null)
            return false;

        if (_slots == null)
        {
            MonoBehaviour target = _currentTarget as MonoBehaviour;

            if (target != null)
                _slots = target.GetComponent<CombatSlots>();
        }

        return _slots != null && _slots.Reserve(this, out position);
    }
    
    private void ReleaseSlot()
    {
        if (_slots != null)
            _slots.Release(this);

        _slots = null;
    }
    
    private bool IsAhead(ICombatTarget target)
    {
        float offset = target.TargetTransform.position.x - transform.position.x;

        return Team == UnitTeam.Zombie ? offset > 0f : offset < 0f;
    }

    private void TryAction()
    {
        if (animation.IsBusy)
            return;

        if (!IsValidTarget(_currentTarget))
        {
            MoveForward();
            return;
        }

        float attackDistance = GetHorizontalDistance(_currentTarget);

        if (unitAction.UseSlots)
        {
            if (!ReserveSlot(out Vector3 slot))
                return;

            float slotDistance = Vector2.Distance(transform.position, slot);

            if (attackDistance <= _model.Stats.AttackRange &&
                slotDistance <= slotLeeway)
            {
                TryAttack();
                return;
            }

            MoveToSlot(slot);
            return;
        }

        if (attackDistance > _model.Stats.AttackRange)
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
        float distance = GetMoveSpeed() * Time.deltaTime;
        transform.Translate(Vector3.right * (direction * distance));
    }

    private void TryAttack()
    {
        if (!_model.CanAttack || !IsValidTarget(_currentTarget))
            return;

        ICombatTarget target = _currentTarget;
        float power = _model.Stats.AttackPower;

        animation.PlayAttack(() =>
        {
            if (IsValidTarget(target))
                unitAction.Execute(this, target, power);
        });

        _model.ResetAttackCooldown();
    }

    private void MoveToSlot(Vector3 position)
    {
        animation.PlayWalk();
        transform.position = Vector3.MoveTowards(transform.position, position, GetMoveSpeed() * Time.deltaTime);
    }
    
    public void TakeDamage(float damage)
    {
        if (!_isInitialized || _model.IsDead)
            return;

        float before = _model.CurrentHealth;

        _model.TakeDamage(damage);
        Debug.Log($"{name}: {before} → {_model.CurrentHealth}, Damage: {damage}");
        
        healthBar.SetHealth(_model.CurrentHealth, _model.Stats.MaxHealth);

        if (_model.IsDead)
        {
            Die();
            return;
        }

        ApplyHitSlow();
        animation.PlayHit();
    }
    
    public void ApplyHitSlow()
    {
        if (useHitSlow)
            _slowEndTime = Time.time + hitSlowTime;
    }

    private float GetMoveSpeed()
    {
        return Time.time < _slowEndTime ? _model.Stats.MoveSpeed * hitSpeedRate : _model.Stats.MoveSpeed;
    }

    private void Die()
    {
        ChangeTarget(null);
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

    private void OnDisable()
    {
        ReleaseSlot();
        CombatTargetTracker.Unregister(_currentTarget);
        _currentTarget = null;
    }
}
