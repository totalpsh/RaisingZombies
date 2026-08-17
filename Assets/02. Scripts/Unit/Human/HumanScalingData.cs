using UnityEngine;

[CreateAssetMenu(fileName = "HumanScalingData", menuName = "Game/Stage/HumanScalingData")]
public class HumanScalingData : ScriptableObject
{
    [SerializeField, Min(0f)] private float healthGrowthPerStage = 0.1f;
    [SerializeField, Min(0f)] private float attackGrowthPerStage = 0.08f;
    
    public float HealthGrowthPerStage => healthGrowthPerStage;
    public float AttackGrowthPerStage => attackGrowthPerStage;
}