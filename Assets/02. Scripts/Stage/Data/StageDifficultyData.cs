using System;
using UnityEngine;

[Serializable]
public class StageDifficultyData
{
    [SerializeField, Min(0.01f)] private float healthMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float attackMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float moveSpeedMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float attackSpeedMultiplier = 1f;

    public float HealthMultiplier => healthMultiplier;
    public float AttackMultiplier => attackMultiplier;
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float AttackSpeedMultiplier => attackSpeedMultiplier;
    
    public StageDifficultyData()
    {
    }

    public StageDifficultyData(
        float healthMultiplier,
        float attackMultiplier,
        float moveSpeedMultiplier,
        float attackSpeedMultiplier)
    {
        this.healthMultiplier = Mathf.Max(0.01f, healthMultiplier);
        this.attackMultiplier = Mathf.Max(0.01f, attackMultiplier);
        this.moveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
        this.attackSpeedMultiplier = Mathf.Max(0.01f, attackSpeedMultiplier);
    }
}