using System;
using UnityEngine;
using UnityEngine.Serialization;

public class UnitController : MonoBehaviour, ICombatTarget
{
    [SerializeField] private UnitAction unitAction;
    [FormerlySerializedAs("animation")]
    [SerializeField] private UnitAnimation anim;
    [SerializeField] private UnitTargeting targeting;
    [SerializeField] private UnitMovement movement;
    [SerializeField] private UnitCombat combat;
    [SerializeField] private Collider2D unitCollider;
    [SerializeField] private UnitData data;

    [SerializeField] private bool useHitSlow;
    [SerializeField, Range(0.1f, 1f)]
    private float hitSpeedRate = 0.6f;
    [SerializeField, Min(0f)]
    private float hitSlowTime = 0.15f;

    [Header("UI")]
    [SerializeField] private UnitHealthBar healthBar;

    private bool _isInitialized;
    private UnitModel model;
    private ICombatTarget _target;
    private float _slowEndTime;
    private BattleArea _battleArea;

    public UnitData Data => data;
    public UnitModel Model => model;
    public UnitTeam Team => data.Team;
    public Collider2D TargetCollider => unitCollider;
    public bool IsDead =>
        !_isInitialized || model.IsDead;
    public Transform TargetTransform => transform;

    public event Action<UnitController> Died;

    private void Update()
    {
        if (!_isInitialized || model.IsDead)
            return;

        float deltaTime = Time.deltaTime;

        model.TickAttackCooldown(deltaTime);
        UpdateRegeneration(deltaTime);

        if (anim.IsBusy)
        {
            if (_target != null &&
                !IsValidTarget(_target))
            {
                _target = null;
            }

            return;
        }

        _target = targeting.FindTarget();

        UpdateAction();
    }

    public void Initialize(
        UnitData data,
        UnitStats stats,
        BattleArea battleArea)
    {
        ClearTarget();
        _battleArea?.UnregisterUnit(this);
        _isInitialized = false;

        if (!ValidateInitialization(
                data,
                stats,
                battleArea))
        {
            return;
        }

        this.data = data;
        model = new UnitModel(stats);
        _battleArea = battleArea;

        _target = null;
        _slowEndTime = 0f;

        combat.Initialize(
            this,
            unitAction,
            model,
            anim);

        movement.Initialize(
            this,
            anim,
            battleArea);

        targeting.Initialize(
            this,
            battleArea);

        healthBar.SetHealth(
            model.CurrentHealth,
            model.Stats.MaxHealth);

        battleArea.RegisterUnit(this);

        _isInitialized = true;
        unitCollider.enabled = true;
        enabled = true;

        anim.ResetState();
    }

    private bool ValidateInitialization(
        UnitData unitData,
        UnitStats stats,
        BattleArea battleArea)
    {
        if (unitData == null)
        {
            Debug.LogError(
                "UnitData가 없습니다.",
                this);
            return false;
        }

        if (stats == null)
        {
            Debug.LogError(
                "UnitStats가 없습니다.",
                this);
            return false;
        }

        if (battleArea == null)
        {
            Debug.LogError(
                "BattleArea가 없습니다.",
                this);
            return false;
        }

        if (unitAction == null ||
            anim == null ||
            targeting == null ||
            movement == null ||
            combat == null ||
            unitCollider == null ||
            healthBar == null)
        {
            Debug.LogError(
                "UnitController 구성 요소가 누락되었습니다.",
                this);

            return false;
        }

        return true;
    }

    private void UpdateRegeneration(
        float deltaTime)
    {
        float healthRegen =
            model.Stats.HealthRegen;

        if (healthRegen <= 0f)
            return;

        model.Heal(
            healthRegen * deltaTime);
    }

    private void UpdateAction()
    {
        if (!IsValidTarget(_target))
        {
            ClearTarget();
            movement.MoveForward(GetMoveSpeed());
            return;
        }

        if (combat.IsInAttackRange(_target))
        {
            combat.TryAttack(_target);
            return;
        }

        movement.MoveTo(
            _target.TargetTransform.position,
            GetMoveSpeed());
    }

    public void TakeDamage(float damage)
    {
        if (!_isInitialized || model.IsDead)
            return;

        model.TakeDamage(damage);

        healthBar.SetHealth(
            model.CurrentHealth,
            model.Stats.MaxHealth);

        if (model.IsDead)
        {
            Die();
            return;
        }

        ApplyHitSlow();
        anim.PlayHit();
    }

    public void ApplyHitSlow()
    {
        if (useHitSlow)
            _slowEndTime =
                Time.time + hitSlowTime;
    }

    private float GetMoveSpeed()
    {
        return Time.time < _slowEndTime
            ? model.Stats.MoveSpeed *
              hitSpeedRate
            : model.Stats.MoveSpeed;
    }

    private void ClearTarget()
    {
        _target = null;
    }

    private void Die()
    {
        if (!_isInitialized)
            return;

        _isInitialized = false;

        ClearTarget();
        _battleArea?.UnregisterUnit(this);

        enabled = false;

        if (unitCollider != null)
            unitCollider.enabled = false;

        Died?.Invoke(this);

        anim.PlayDie(ReleaseToPool);
    }

    private void ReleaseToPool()
    {
        PoolManager.Instance.Release(gameObject);
    }

    private bool IsValidTarget(
        ICombatTarget target)
    {
        if (target == null ||
            unitAction == null)
        {
            return false;
        }

        if (target is not MonoBehaviour targetObject)
            return false;

        return targetObject.gameObject.activeInHierarchy &&
               !target.IsDead &&
               unitAction.CanTarget(this, target);
    }

    private void OnDisable()
    {
        ClearTarget();
        _battleArea?.UnregisterUnit(this);
        _battleArea = null;
        _isInitialized = false;
    }
}
