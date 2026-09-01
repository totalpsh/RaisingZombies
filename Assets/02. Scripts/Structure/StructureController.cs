using System;
using UnityEngine;

public enum StructureType
{
    DefenseLine,
    ZombieCamp,
    HumanFortress
}

public class StructureController : MonoBehaviour, ICombatTarget
{
    [SerializeField] private StructureType structureType;
    [SerializeField] private UnitTeam team;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private Collider2D structureCollider;

    private BattleArea _battleArea;
    public Collider2D TargetCollider => structureCollider;
    private float _runtimeMaxHealth;
    private float _currentHealth;
    private bool _isDestroyed;

    public StructureType StructureType => structureType;
    public UnitTeam Team => team;
    public bool IsDead => _isDestroyed;
    public Transform TargetTransform => transform;
    public Transform SpawnPoint => spawnPoint;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _runtimeMaxHealth;

    public event Action<StructureController> Destroyed;

    public void Initialize(BattleArea battleArea, float healthMultiplier = 1f)
    {
        _runtimeMaxHealth = maxHealth * Mathf.Max(0.01f, healthMultiplier);
        _currentHealth = _runtimeMaxHealth;
        
        _isDestroyed = false;

        gameObject.SetActive(true);
        
        if (structureCollider != null)
            structureCollider.enabled = true;
        
        _battleArea = battleArea;
        _battleArea.RegisterStructure(this);
        
        enabled = true;
    }
 
    public void TakeDamage(float damage)
    {
        if (_isDestroyed || damage <= 0f)
            return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);

        if (_currentHealth <= 0f)
            DestroyStructure();
    }
    
    private void DestroyStructure()
    {
        if (_isDestroyed)
            return;

        _isDestroyed = true;
        
        _battleArea?.UnregisterStructure(this);
        
        if (structureCollider != null)
            structureCollider.enabled = false;

        gameObject.SetActive(false);
        
        Destroyed?.Invoke(this);
    }
}
