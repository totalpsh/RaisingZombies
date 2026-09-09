using UnityEngine;

// 한 레어도의 성능 Roll과 파기 보상을 관리합니다.
[CreateAssetMenu(fileName = "BodyRarity", menuName = "Raising Zombies/Body Equipment/Rarity Definition")]
public sealed class BodyRarityDefinitionSO : ScriptableObject
{
    [SerializeField, Range(1, 12)] private int tier = 1; // 1부터 시작하는 레어도 단계
    [SerializeField] private string displayName = string.Empty; // UI에 표시할 레어도 이름
    [SerializeField] private Color uiColor = Color.white; // 카드와 확률 UI에 적용할 색
    [SerializeField] private Sprite frameSprite; // 장비 카드에 사용할 선택적 프레임 이미지
    [SerializeField, Min(0f)] private float mainRollMin = 1f; // Main Stat 기준 Roll 최소값
    [SerializeField, Min(0f)] private float mainRollMax = 10f; // Main Stat 기준 Roll 최대값
    [SerializeField, Min(0)] private int subStatMinCount; // 보조 스탯 최소 개수
    [SerializeField, Min(0)] private int subStatMaxCount; // 보조 스탯 최대 개수
    [SerializeField, Min(0f)] private float subStatMultiplier = 1f; // 보조 스탯 기본 Roll에 곱할 배율
    [SerializeField, Min(0)] private int dismantleResearchPoint = 1; // 파기할 때 지급할 연구 포인트

    public int Tier => tier; // 확률표와 저장에서 사용할 단계
    public string DisplayName => displayName; // UI 표시 이름
    public Color UiColor => uiColor; // UI 강조 색
    public Sprite FrameSprite => frameSprite; // 장비 카드 프레임
    public float MainRollMin => Mathf.Max(0f, mainRollMin); // 안전한 Main Roll 최소값
    public float MainRollMax => Mathf.Max(MainRollMin, mainRollMax); // 최소값보다 작지 않은 Main Roll 최대값
    public int SubStatMinCount => Mathf.Max(0, subStatMinCount); // 안전한 보조 스탯 최소 개수
    public int SubStatMaxCount => Mathf.Max(SubStatMinCount, subStatMaxCount); // 최소 개수보다 작지 않은 최대 개수
    public float SubStatMultiplier => Mathf.Max(0f, subStatMultiplier); // 음수를 제거한 보조 스탯 배율
    public int DismantleResearchPoint => Mathf.Max(0, dismantleResearchPoint); // 음수를 제거한 파기 포인트

    // 레어도 단계, 표시명과 Roll 범위를 검증합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (tier < 1 || tier > 12) { error = "Rarity Tier는 1~12여야 합니다."; return false; }
        if (string.IsNullOrWhiteSpace(displayName)) { error = $"Tier {tier} 표시명이 비어 있습니다."; return false; }
        if (mainRollMin < 0f || mainRollMax < mainRollMin) { error = $"Tier {tier} Main Roll Min/Max가 잘못됐습니다."; return false; }
        if (subStatMinCount < 0 || subStatMaxCount < subStatMinCount) { error = $"Tier {tier} SubStat 개수가 잘못됐습니다."; return false; }
        if (subStatMultiplier < 0f || dismantleResearchPoint < 0) { error = $"Tier {tier} 배율 또는 파기 포인트가 음수입니다."; return false; }
        return true;
    }

    // Inspector에서 잘못된 레어도 설정을 즉시 알립니다.
    private void OnValidate()
    {
        if (!TryValidate(out string error)) Debug.LogWarning($"[BodyRarityDefinition] {error}", this);
    }
}
