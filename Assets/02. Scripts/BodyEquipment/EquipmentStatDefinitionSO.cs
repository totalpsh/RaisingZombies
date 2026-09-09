using UnityEngine;

// 장비 스탯 하나의 Roll과 전투 적용 규칙을 관리합니다.
[CreateAssetMenu(fileName = "EquipmentStat", menuName = "Raising Zombies/Body Equipment/Stat Definition")]
public sealed class EquipmentStatDefinitionSO : ScriptableObject
{
    [SerializeField] private string statId = string.Empty; // 저장과 Database 조회에 사용할 안정적인 ID
    [SerializeField] private EquipmentStatType statType; // 코드에서 합산할 스탯 종류
    [SerializeField] private string displayName = string.Empty; // UI에 표시할 한국어 이름
    [SerializeField] private EquipmentStatValueKind valueKind; // 고정값과 비율값 구분
    [SerializeField, Min(0f)] private float mainStatScale = 1f; // Rarity 기준 Main Roll에 곱할 스탯별 계수
    [SerializeField, Min(0f)] private float subStatMin; // 보조 스탯의 기본 최소 Roll
    [SerializeField, Min(0f)] private float subStatMax = 1f; // 보조 스탯의 기본 최대 Roll
    [SerializeField] private string uiFormat = "+{0:0.##}"; // UI 수치 출력 형식
    [SerializeField, Min(0f)] private float maximumValue; // 0이면 무제한인 장착 합산 상한

    public string StatId => statId; // 외부 저장과 Database가 읽는 안정적인 ID
    public EquipmentStatType StatType => statType; // Aggregator가 읽는 스탯 종류
    public string DisplayName => displayName; // UI에서 enum 대신 사용할 표시 이름
    public EquipmentStatValueKind ValueKind => valueKind; // Flat 또는 Percent 구분
    public float MainStatScale => Mathf.Max(0f, mainStatScale); // 음수를 제거한 Main Roll 계수
    public float SubStatMin => Mathf.Max(0f, subStatMin); // 안전한 보조 스탯 최소값
    public float SubStatMax => Mathf.Max(SubStatMin, subStatMax); // 최소값보다 작지 않은 최대값
    public string UiFormat => uiFormat; // 장비 상세 UI에서 사용할 출력 형식
    public float MaximumValue => Mathf.Max(0f, maximumValue); // 음수를 제거한 적용 상한

    // 한 스탯 정의의 ID, 표시명과 Roll 범위를 검증합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(statId)) { error = "Stat ID가 비어 있습니다."; return false; }
        if (string.IsNullOrWhiteSpace(displayName)) { error = $"{statId} 표시명이 비어 있습니다."; return false; }
        if (mainStatScale < 0f) { error = $"{statId} Main Scale이 음수입니다."; return false; }
        if (subStatMin < 0f || subStatMax < subStatMin) { error = $"{statId} SubStat Min/Max가 잘못됐습니다."; return false; }
        if (string.IsNullOrWhiteSpace(uiFormat)) { error = $"{statId} UI Format이 비어 있습니다."; return false; }
        return true;
    }

    // Inspector에서 잘못된 스탯 설정을 즉시 알립니다.
    private void OnValidate()
    {
        if (!TryValidate(out string error)) Debug.LogWarning($"[EquipmentStatDefinition] {error}", this);
    }
}
