using System;
using System.Collections.Generic;
using UnityEngine;

// 뽑힐 수 있는 신체 장비 원형과 슬롯별 Stat Pool을 관리합니다.
[CreateAssetMenu(fileName = "BodyEquipment", menuName = "Raising Zombies/Body Equipment/Equipment Definition")]
public sealed class BodyEquipmentDefinitionSO : ScriptableObject
{
    [SerializeField] private string definitionId = string.Empty; // 저장된 Instance가 참조할 안정적인 원형 ID
    [SerializeField] private string displayName = string.Empty; // UI에 표시할 장비 이름
    [SerializeField] private string slotDisplayName = string.Empty; // enum 대신 UI에 표시할 슬롯 이름
    [SerializeField] private BodyEquipmentSlot slot; // 장비가 장착되는 신체 슬롯
    [SerializeField] private Sprite icon; // 인벤토리와 결과 카드에 표시할 아이콘
    [SerializeField] private Sprite equipmentSprite; // 장착 화면에 표시할 선택적 장비 이미지
    [SerializeField, Min(0f)] private float drawWeight = 1f; // 같은 레어도 안에서 이 원형이 선택될 가중치
    [SerializeField] private EquipmentStatDefinitionSO[] possibleMainStats = Array.Empty<EquipmentStatDefinitionSO>(); // 이 부위에 허용된 주 스탯 Pool
    [SerializeField] private EquipmentStatDefinitionSO[] possibleSubStats = Array.Empty<EquipmentStatDefinitionSO>(); // 이 부위에 허용된 보조 스탯 Pool
    [SerializeField, TextArea] private string description = string.Empty; // 장비 상세 설명

    public string DefinitionId => definitionId; // Instance 저장과 Database 조회에 사용할 ID
    public string DisplayName => displayName; // 장비 표시 이름
    public string SlotDisplayName => slotDisplayName; // 슬롯 표시 이름
    public BodyEquipmentSlot Slot => slot; // 장착할 신체 슬롯
    public Sprite Icon => icon; // 목록과 결과 아이콘
    public Sprite EquipmentSprite => equipmentSprite; // 장착 화면 이미지
    public float DrawWeight => Mathf.Max(0f, drawWeight); // 음수를 제거한 장비 선택 가중치
    public IReadOnlyList<EquipmentStatDefinitionSO> PossibleMainStats => possibleMainStats; // 주 스탯 후보 목록
    public IReadOnlyList<EquipmentStatDefinitionSO> PossibleSubStats => possibleSubStats; // 보조 스탯 후보 목록
    public string Description => description; // 상세 설명

    // 장비 원형의 ID, 표시명, Weight와 Stat Pool을 검증합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(definitionId)) { error = "Definition ID가 비어 있습니다."; return false; }
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(slotDisplayName)) { error = $"{definitionId} 표시명이 비어 있습니다."; return false; }
        if (drawWeight < 0f) { error = $"{definitionId} Draw Weight가 음수입니다."; return false; }
        if (possibleMainStats == null || possibleMainStats.Length == 0) { error = $"{definitionId} Main Stat Pool이 비어 있습니다."; return false; }
        if (possibleSubStats == null) { error = $"{definitionId} Sub Stat Pool이 null입니다."; return false; }
        for (int index = 0; index < possibleMainStats.Length; index++)
            if (possibleMainStats[index] == null) { error = $"{definitionId} Main Stat Pool에 누락 참조가 있습니다."; return false; }
        for (int index = 0; index < possibleSubStats.Length; index++)
            if (possibleSubStats[index] == null) { error = $"{definitionId} Sub Stat Pool에 누락 참조가 있습니다."; return false; }
        return true;
    }

    // Inspector에서 잘못된 장비 설정을 즉시 알립니다.
    private void OnValidate()
    {
        if (!TryValidate(out string error)) Debug.LogWarning($"[BodyEquipmentDefinition] {error}", this);
    }
}
