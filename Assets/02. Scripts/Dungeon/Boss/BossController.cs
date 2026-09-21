using System;
using UnityEngine;

public class BossController : MonoBehaviour, ICombatTarget
{
    [SerializeField] private Collider2D bossCollider;
    [SerializeField] private UnitAnimation anim;
    [SerializeField] private UnitHealthBar healthBar;

    private UnitModel _model;
    private BattleArea _battleArea;
    private bool _isInitialized;

    public UnitTeam Team => UnitTeam.Human;
    public bool IsDead => !_isInitialized || _model == null || _model.IsDead;
    public Transform TargetTransform => transform;
    public Collider2D TargetCollider => bossCollider;
    public float CurrentHealth => _model?.CurrentHealth ?? 0f;
    public float MaxHealth => _model?.Stats.MaxHealth ?? 0f;
    public event Action<BossController> Died;

    public bool Initialize(UnitStats stats, BattleArea battleArea)
    {
        UnregisterTarget();
        _isInitialized = false;
        if (stats == null || stats.MaxHealth <= 0f || battleArea == null || bossCollider == null)
        {
            Debug.LogError("[BossController] Valid stats, BattleArea and collider are required.", this);
            return false;
        }

        _model = new UnitModel(stats);
        _battleArea = battleArea;
        bossCollider.enabled = true;
        if (anim != null)
            anim.ResetState();
        if (healthBar != null)
            healthBar.SetHealth(CurrentHealth, MaxHealth);

        _isInitialized = true;
        _battleArea.RegisterAdditionalTarget(this);
        return true;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return;

        _model.TakeDamage(damage);
        if (healthBar != null)
            healthBar.SetHealth(CurrentHealth, MaxHealth);

        if (!_model.IsDead)
        {
            if (anim != null)
                anim.PlayHit();
            return;
        }

        _isInitialized = false;
        UnregisterTarget();
        bossCollider.enabled = false;
        Died?.Invoke(this);
        if (anim != null)
            anim.PlayDie(ReleaseToPool);
        else
            ReleaseToPool();
    }

    private void ReleaseToPool()
    {
        if (this != null && gameObject.activeInHierarchy && PoolManager.HasInstance)
            PoolManager.Instance.Release(gameObject);
    }

    private void UnregisterTarget()
    {
        if (_battleArea != null)
            _battleArea.UnregisterAdditionalTarget(this);
        _battleArea = null;
    }

    private void OnDisable()
    {
        UnregisterTarget();
        _isInitialized = false;
    }
}
