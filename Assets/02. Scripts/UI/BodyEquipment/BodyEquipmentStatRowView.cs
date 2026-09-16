using TMPro;
using UnityEngine;

// ItemDetail에 이미 배치된 스탯 한 줄을 실제 장비 Roll에 연결한다.
public sealed class BodyEquipmentStatRowView : MonoBehaviour
{
    [SerializeField] private EquipmentStatType statType; // 이 고정 행이 표시할 실제 스탯 종류
    [SerializeField] private TMP_Text statNameText; // Stat Definition의 표시 이름
    [SerializeField] private TMP_Text statValueText; // Flat 또는 Percent 형식의 실제 수치
    [SerializeField] private GameObject upIcon; // 새 장비 수치가 더 높을 때 표시한다
    [SerializeField] private GameObject downIcon; // 새 장비 수치가 더 낮을 때 표시한다

    public EquipmentStatType StatType => statType; // 비교할 스탯 종류

    // 실제 장비에 존재하는 수치와 비교 결과를 기존 행에 반영한다.
    public void Bind(BodyEquipmentManager manager, bool hasValue, float value, bool showComparison, bool hasComparisonTarget, float comparisonValue)
    {
        gameObject.SetActive(hasValue);
        if (!hasValue) return;
        if (manager != null && manager.Database != null && manager.Database.TryGetStat(statType, out EquipmentStatDefinitionSO definition))
            SetText(statNameText, definition.DisplayName);
        SetText(statValueText, BodyEquipmentUIFormatter.FormatStatValue(manager, statType, value));
        bool canCompare = showComparison && hasComparisonTarget; // 현재 장비가 있을 때만 증감을 표시한다
        SetActive(upIcon, canCompare && value > comparisonValue);
        SetActive(downIcon, canCompare && value < comparisonValue);
    }

    // 장비가 없거나 현재 장비 영역이면 비교 아이콘을 모두 숨긴다.
    public void Clear()
    {
        gameObject.SetActive(false);
        SetActive(upIcon, false);
        SetActive(downIcon, false);
    }

    // 연결된 Text에만 값을 반영한다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    // 연결된 아이콘의 활성 상태만 바꾼다.
    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }
}
