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

    [Header("UI")]
    [SerializeField] private UnitHealthBar healthBar;

    private bool _isInitialized;
    private UnitModel model;
    private ICombatTarget _target;
    private BattleArea _battleArea;

    private BodyEquipmentManager _equipmentManager; // 살아 있는 좀비의 장착 스탯 변경 원본
    
    // 프로퍼티
    public UnitData Data => data;
    public UnitModel Model => model;
    public UnitTeam Team => data.Team;
    public Collider2D TargetCollider => unitCollider;
    public bool IsDead =>
        !_isInitialized || model.IsDead;
    public Transform TargetTransform => transform;

    public event Action<UnitController> Died;
    public static event Action<UnitController> GlobalDied;

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

        battleArea.RegisterUnit(this);

        healthBar.SetHealth(
            model.CurrentHealth,
            model.Stats.MaxHealth);

        _isInitialized = true;
        BodyEquipmentManager.AvailabilityChanged -= BindEquipmentStats;
        if (data.Team == UnitTeam.Zombie)
        {
            BodyEquipmentManager.AvailabilityChanged += BindEquipmentStats;
            BindEquipmentStats(BodyEquipmentManager.HasInstance ? BodyEquipmentManager.Instance : null);
        }
        else BindEquipmentStats(null);
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
            movement.MoveForward(
                model.Stats.MoveSpeed);
            return;
        }

        if (combat.IsInAttackRange(_target))
        {
            combat.TryAttack(_target);
            return;
        }

        movement.MoveTo(
            _target.TargetTransform.position,
            model.Stats.MoveSpeed);
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

        anim.PlayHit();
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

    // 원본 생성 및 제거 시 기존 장착 이벤트 구독을 교체한다.
    private void BindEquipmentStats(BodyEquipmentManager manager)
    {
        if (_equipmentManager != null) _equipmentManager.EquippedStatsChanged -= RefreshEquipmentStats;
        _equipmentManager = manager;
        if (_equipmentManager != null) _equipmentManager.EquippedStatsChanged += RefreshEquipmentStats;
        RefreshEquipmentStats();
    }

    // AI와 전투 상태는 유지하고 기본 데이터부터 장착 스탯만 다시 계산한다.
    private void RefreshEquipmentStats()
    {
        if (!_isInitialized || model == null || data.Team != UnitTeam.Zombie) return;
        model.Stats.ApplyZombieEquipment(data, _equipmentManager == null ? default : _equipmentManager.CurrentModifiers);
        model.Heal(0f); // 현재 체력은 늘리지 않고 감소한 최대 체력 안으로만 제한한다
        healthBar.SetHealth(model.CurrentHealth, model.Stats.MaxHealth);
    }

    // 풀 반환 시 장착 스탯 이벤트와 기존 전투 등록을 정리한다.
    private void OnDisable()
    {
        ClearTarget();
        BodyEquipmentManager.AvailabilityChanged -= BindEquipmentStats;
        if (_equipmentManager != null) _equipmentManager.EquippedStatsChanged -= RefreshEquipmentStats;
        _equipmentManager = null;
        ClearAssignment();
        _battleArea?.UnregisterUnit(this);
        _battleArea = null;
        _isInitialized = false;
    }
}
