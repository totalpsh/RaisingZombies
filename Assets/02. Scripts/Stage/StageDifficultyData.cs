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
}