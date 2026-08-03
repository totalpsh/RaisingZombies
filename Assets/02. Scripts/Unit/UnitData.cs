using UnityEngine;
using UnityEngine.Serialization;

public enum UnitTeam
{
    Zombie,
    Human
}

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [FormerlySerializedAs("_team")] [SerializeField] private UnitTeam team;

    [Header("Health")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 10f;

    [Min(0f)]
    [SerializeField] private float healthRegen;

    [Header("Attack")]
    [Min(0f)]
    [SerializeField] private float attackPower = 1f;

    [Min(0.01f)]
    [SerializeField] private float attackInterval = 1f;

    [Min(0f)]
    [SerializeField] private float attackRange = 1f;

    [Header("Movement")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 1f;

    public string Id => id;
    public string DisplayName => displayName;
    public UnitTeam Team => team;

    public float MaxHealth => maxHealth;
    public float HealthRegen => healthRegen;
    public float AttackPower => attackPower;
    public float AttackInterval => attackInterval;
    public float AttackRange => attackRange;
    public float MoveSpeed => moveSpeed;
}
