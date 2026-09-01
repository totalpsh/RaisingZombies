using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class UnitController : MonoBehaviour, ICombatTarget
{
    [SerializeField] private UnitAction unitAction;
    [SerializeField] private UnitAnimation animation;
    [SerializeField] private UnitTargeting targeting;
    [SerializeField] private UnitMovement movement;
    [SerializeField] private Collider2D unitCollider;
    [SerializeField] private UnitData data;
    [SerializeField] private UnitModel model;

    [SerializeField] private bool useHit = true;
    [SerializeField] private bool useHitSlow;
    [SerializeField, Range(0.1f, 1f)] private float hitSpeedRate = 0.6f;
    [SerializeField, Min(0f)] private float hitSlowTime = 0.15f;
    
    [Header("전투")]
    [SerializeField] private Vector2 combatYBounds = new(-1.5f, 1.5f);
    [SerializeField, Min(0.01f)] private float slotTolerance = 0.05f;
    [SerializeField, Min(0f)] private float slotLeeway = 0.25f;
    [SerializeField, Min(0f)] private float forwardStopDistance = 0.15f;
    [SerializeField] private float targetSearchWidth = 3f;
    [SerializeField] private float targetSearchHeight = 6f;
    
    [Header("UI")]
    [SerializeField] private UnitHealthBar healthBar;
    
    // private UnitController _currentTarget;
    
    private bool _isInitialized;
    
    private CombatSlots _slots;
    private ICombatTarget _currentTarget;
    private float _slowEndTime;
    private BattleArea _battleArea;
    
    // 프로퍼티
    public UnitData Data => data;
    public UnitModel Model => model;
    public UnitTeam Team => data.Team;
    public Collider2D TargetCollider => unitCollider;
    public bool IsDead => !_isInitialized || model.IsDead;

    public Transform TargetTransform => transform;

    public event Action<UnitController> Died; 
    
    private void Update()
    {
        if (!_isInitialized || model.IsDead)
            return;
        
        float deltaTime = Time.deltaTime;
        
        model.TickAttackCooldown(deltaTime);
        UpdateRegeneration(deltaTime);
        
        ChangeTarget(targeting.FindTarget());
        
        TryAction();
    }

    public void Initialize(UnitData data, UnitStats stats, BattleArea battleArea)
    {
        if (data == null)
        {
            Debug.LogError($"[{nameof(UnitController)}] UnitData가 null입니다.", this);
            return;
        }

        if (unitAction == null)
        {
            Debug.Log("UnitAction 없음", this);
            return;
        }

        if (targeting == null)
        {
            Debug.LogError("UnitTargeting 없음", this);
            return;
        }
        
        if (battleArea == null)
        {
            Debug.LogError($"[{nameof(UnitController)}] BattleArea가 없습니다.", this);
            return;
        }

        this.data = data;
        model = new UnitModel(stats);
        _battleArea = battleArea;

        _currentTarget = null;
        _slots = null;
        _slowEndTime = 0f;
        
        healthBar.SetHealth(model.CurrentHealth, model.Stats.MaxHealth);
        
        movement.Initialize(this, animation, battleArea);
        targeting.Initialize(this, battleArea);
        battleArea.Register(this);
        
        _isInitialized = true;
        
        if (unitCollider != null)
            unitCollider.enabled = true;
        
        enabled = true;
        
        ResetAnimation();
    }
    
    private void UpdateRegeneration(float deltaTime)
    {
        float healthRegen = model.Stats.HealthRegen;

        if (healthRegen <= 0f)
            return;

        model.Heal(healthRegen * deltaTime);
    }
    
    private ICombatTarget FindBestTargetGlobal()
    {
        ICombatTarget[] targets =
            FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<ICombatTarget>()
                .ToArray();

        ICombatTarget bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (ICombatTarget candidate in targets)
        {
            if (!IsValidTarget(candidate))
                continue;

            float score = GetHorizontalDistance(candidate);

            if (score >= bestScore)
                continue;

            bestTarget = candidate;
            bestScore = score;
        }

        return bestTarget;
    }
    
    private void ChangeTarget(ICombatTarget target)
    {
        if (ReferenceEquals(_currentTarget, target))
            return;
        
        string previousName =
            (_currentTarget as MonoBehaviour)?.name ?? "None";

        string newName =
            (target as MonoBehaviour)?.name ?? "None";

        Debug.Log(
            $"[{name}] Target : {previousName} -> {newName} " +
            $"Position: {transform.position}");

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
    
    private void TryAction()
    {
        if (animation.IsBusy)
            return;

        if (!IsValidTarget(_currentTarget))
        {
            movement.MoveForward(GetMoveSpeed());
            return;
        }

        if (unitAction.UseSlots)
        {
            HandleSlotCombat();
            return;
        }

        HandleDirectCombat();
    }
    
    private void HandleSlotCombat()
    {
        if (!ReserveSlot(out Vector3 slotPosition))
            return;

        float slotDistance = Vector2.Distance(transform.position, slotPosition);
        float attackDistance = GetHorizontalDistance(_currentTarget);

        if (slotDistance <= slotLeeway && attackDistance <= model.Stats.AttackRange)
        {
            TryAttack();
            return;
        }

        movement.MoveTo(slotPosition, GetMoveSpeed());
    }
    
    private void HandleDirectCombat()
    {
        float attackDistance = GetHorizontalDistance(_currentTarget);

        if (attackDistance <= model.Stats.AttackRange)
        {
            TryAttack();
            return;
        }

        movement.MoveTo(_currentTarget.TargetTransform.position, GetMoveSpeed());
    }


    private void TryAttack()
    {
        if (!model.CanAttack || !IsValidTarget(_currentTarget))
            return;

        ICombatTarget target = _currentTarget;
        float power = model.Stats.AttackPower;

        animation.PlayAttack(() =>
        {
            if (IsValidTarget(target))
                unitAction.Execute(this, target, power);
        });

        model.ResetAttackCooldown();
    }
    
    public void TakeDamage(float damage)
    {
        if (!_isInitialized || model.IsDead)
            return;

        float before = model.CurrentHealth;

        model.TakeDamage(damage);
        Debug.Log($"{name}: {before} → {model.CurrentHealth}, Damage: {damage}");
        
        healthBar.SetHealth(model.CurrentHealth, model.Stats.MaxHealth);

        if (model.IsDead)
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
        return Time.time < _slowEndTime ? model.Stats.MoveSpeed * hitSpeedRate : model.Stats.MoveSpeed;
    }

    private void Die()
    {
        if (!_isInitialized)
            return;
        
        _isInitialized = false;
        
        ChangeTarget(null);
        _battleArea?.Unregister(this);
        
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
        _battleArea?.Unregister(this);
        CombatTargetTracker.Unregister(_currentTarget);
        _currentTarget = null;
        _battleArea = null;
        _isInitialized = false;
    }
}
