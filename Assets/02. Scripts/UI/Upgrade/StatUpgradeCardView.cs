using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

// 새 StatUpgrade 화면의 카드 하나를 실제 업그레이드 데이터와 연결합니다.
public sealed class StatUpgradeCardView : MonoBehaviour
{
    [SerializeField] private UpgradeStatType[] statTypes = Array.Empty<UpgradeStatType>(); // 이 카드가 표시하고 반응할 실제 스탯 종류
    [SerializeField] private TMP_Text statNameText; // 밸런스 데이터의 실제 스탯 이름을 표시한다.
    [SerializeField] private TMP_Text valueText; // 실제 전투에 적용되는 최종 보너스를 표시한다.
    [SerializeField] private GameObject drawEffectObject; // 이번 뽑기에 선택된 카드를 강조하는 기존 오브젝트
    [SerializeField, Min(1f)] private float effectScale = 1.08f; // 뽑기 강조 시 사용할 최대 크기 배율
    [SerializeField, Min(0.05f)] private float effectDuration = 0.3f; // 강조 확대와 복귀에 사용할 전체 시간

    private UpgradeManager upgradeManager; // 실제 스탯 저장값과 계산 결과를 제공하는 기존 매니저
    private Vector3 defaultScale; // 프리팹에 저장된 카드의 원래 크기
    private Tween drawTween; // 현재 실행 중인 중복 방지용 강조 Tween
    private bool hasDefaultScale; // 원래 크기를 안전하게 기록했는지 여부

    // 프리팹의 원래 크기를 기록하고 강조 오브젝트를 초기화합니다.
    private void Awake()
    {
        EnsureDefaultScale();
        SetDrawEffectActive(false);
    }

    // 화면이 닫힐 때 진행 중인 Effect를 원래 상태로 되돌립니다.
    private void OnDisable()
    {
        StopDrawEffect();
    }

    // 카드가 제거될 때 남아 있는 Tween 참조를 정리합니다.
    private void OnDestroy()
    {
        StopDrawEffect();
    }

    // 이 카드가 표시할 실제 업그레이드 매니저를 연결합니다.
    public void Bind(UpgradeManager manager)
    {
        upgradeManager = manager;
        Refresh();
    }

    // 뽑기 결과의 스탯이 이 카드에 포함되는지 확인합니다.
    public bool ContainsStat(UpgradeStatType type)
    {
        if (statTypes == null) return false;
        foreach (UpgradeStatType statType in statTypes) // 카드에 직렬화된 실제 스탯 종류
        {
            if (statType == type) return true;
        }

        return false;
    }

    // 현재 저장 상태와 밸런스 계산 결과를 카드 텍스트에 반영합니다.
    public void Refresh()
    {
        UpgradeBalanceSettings balanceSettings = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 실제 표시명과 결과 형식의 원본 데이터
        if (balanceSettings == null || statTypes == null || statTypes.Length == 0)
        {
            SetUnavailableState();
            return;
        }

        string displayName = string.Empty; // 카드에 표시할 실제 스탯 이름 묶음
        string displayValue = string.Empty; // 카드에 표시할 실제 최종 보너스 묶음
        for (int index = 0; index < statTypes.Length; index++) // 이 카드가 함께 표시하는 스탯 순번
        {
            UpgradeStatType statType = statTypes[index]; // 현재 표시할 실제 스탯 종류
            UpgradeStatDefinition definition = balanceSettings.GetStat(statType); // 밸런스 에셋의 실제 스탯 정의
            if (index > 0)
            {
                displayName += " / ";
                displayValue += " / ";
            }

            displayName += definition == null || string.IsNullOrWhiteSpace(definition.displayName)
                ? statType.ToString()
                : definition.displayName;
            displayValue += BuildValueText(statType, definition);
        }

        SetText(statNameText, displayName);
        SetText(valueText, displayValue);
    }

    // 이번 뽑기에 선택된 카드만 기존 Focus와 짧은 크기 Effect로 강조합니다.
    public void PlayDrawEffect()
    {
        StopDrawEffect();
        EnsureDefaultScale();
        SetDrawEffectActive(true);

        float halfDuration = Mathf.Max(0.025f, effectDuration * 0.5f); // 확대 또는 복귀 한 구간의 시간
        Vector3 highlightedScale = defaultScale * Mathf.Max(1f, effectScale); // 강조 중 카드 크기
        Sequence sequence = DOTween.Sequence(); // 이번 강조만 담당하는 안전한 Tween 묶음
        sequence.SetUpdate(true);
        sequence.Append(transform.DOScale(highlightedScale, halfDuration).SetEase(Ease.OutQuad));
        sequence.Append(transform.DOScale(defaultScale, halfDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(CompleteDrawEffect);
        drawTween = sequence;
    }

    // 현재 스탯의 잠금 상태와 최종 적용 보너스를 표시 문자열로 만듭니다.
    private string BuildValueText(UpgradeStatType statType, UpgradeStatDefinition definition)
    {
        if (definition == null) return "-";
        if (!upgradeManager.IsUnlocked(statType)) return "잠김";

        UpgradeStatSnapshot snapshot = upgradeManager.GetStatSnapshot(statType); // 기존 계산식이 만든 실제 최종 스탯 결과
        if (definition.resultKind == UpgradeResultKind.Integer)
        {
            return $"+{Mathf.FloorToInt(snapshot.FinalBonus):N0}";
        }

        return $"+{snapshot.FinalBonus * 100f:0.##}%";
    }

    // 실행 중인 Effect를 중단하고 카드의 원래 크기와 상태를 복구합니다.
    private void StopDrawEffect()
    {
        if (drawTween != null && drawTween.IsActive()) drawTween.Kill();
        drawTween = null;
        EnsureDefaultScale();
        transform.localScale = defaultScale;
        SetDrawEffectActive(false);
    }

    // Effect가 정상 완료된 뒤 강조 오브젝트와 Tween 참조를 정리합니다.
    private void CompleteDrawEffect()
    {
        transform.localScale = defaultScale;
        SetDrawEffectActive(false);
        drawTween = null;
    }

    // Awake 이전 호출에도 프리팹의 원래 크기를 한 번만 안전하게 기록합니다.
    private void EnsureDefaultScale()
    {
        if (hasDefaultScale) return;
        defaultScale = transform.localScale;
        hasDefaultScale = true;
    }

    // 기존 Focus 오브젝트가 연결된 경우에만 활성 상태를 변경합니다.
    private void SetDrawEffectActive(bool active)
    {
        if (drawEffectObject != null) drawEffectObject.SetActive(active);
    }

    // 데이터 또는 참조가 없을 때 임시 수치가 노출되지 않게 합니다.
    private void SetUnavailableState()
    {
        SetText(statNameText, "스탯 데이터 없음");
        SetText(valueText, "-");
    }

    // 연결된 TMP 텍스트가 존재할 때만 값을 적용합니다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
