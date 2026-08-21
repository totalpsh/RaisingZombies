using UnityEngine;

// 전투력 환산에 사용하는 UI 전용 밸런스 값을 한 곳에서 관리합니다.
[CreateAssetMenu(fileName = "CombatPowerBalanceSettings", menuName = "Raising Zombies/Combat Power Balance Settings")]
public sealed class CombatPowerBalanceSettings : ScriptableObject
{
    [SerializeField, Min(0)] private long baseCombatPower = 1000; // 기본 좀비 군단이 표시할 기준 전투력
    [SerializeField, Min(0f)] private float offenseWeight = 0.45f; // 기대 DPS 비중
    [SerializeField, Min(0f)] private float defenseWeight = 0.30f; // 체력과 회복 기반 생존 비중
    [SerializeField, Min(0f)] private float armyWeight = 0.20f; // 실제 군단 수 시스템이 연결될 때 사용할 물량 비중
    [SerializeField, Min(0f)] private float mobilityWeight = 0.05f; // 이동속도 비중
    [SerializeField, Min(0f)] private float armySpawnExponent = 0.60f; // 실제 생산 수 연결 시 사용할 완만한 생산 수 지수
    [SerializeField, Min(0f)] private float armyMaximumExponent = 0.40f; // 실제 최대 좀비 수 연결 시 사용할 완만한 최대 수 지수
    [SerializeField, Min(0f)] private float defenseSustainSeconds = 60f; // 체력 재생을 유효 체력으로 환산할 기준 생존 시간

    public long BaseCombatPower => baseCombatPower; // 외부 계산기가 읽을 기준 전투력
    public float OffenseWeight => offenseWeight; // 외부 계산기가 읽을 공격 비중
    public float DefenseWeight => defenseWeight; // 외부 계산기가 읽을 생존 비중
    public float ArmyWeight => armyWeight; // 외부 계산기가 읽을 물량 비중
    public float MobilityWeight => mobilityWeight; // 외부 계산기가 읽을 기동 비중
    public float ArmySpawnExponent => armySpawnExponent; // 외부 계산기가 읽을 생산 수 지수
    public float ArmyMaximumExponent => armyMaximumExponent; // 외부 계산기가 읽을 최대 수 지수
    public float DefenseSustainSeconds => defenseSustainSeconds; // 외부 계산기가 읽을 회복 환산 시간

    // Inspector에서 음수 전투력 밸런스가 저장되지 않도록 보정합니다.
    private void OnValidate()
    {
        baseCombatPower = System.Math.Max(0L, baseCombatPower);
        offenseWeight = Mathf.Max(0f, offenseWeight);
        defenseWeight = Mathf.Max(0f, defenseWeight);
        armyWeight = Mathf.Max(0f, armyWeight);
        mobilityWeight = Mathf.Max(0f, mobilityWeight);
        armySpawnExponent = Mathf.Max(0f, armySpawnExponent);
        armyMaximumExponent = Mathf.Max(0f, armyMaximumExponent);
        defenseSustainSeconds = Mathf.Max(0f, defenseSustainSeconds);
    }
}
