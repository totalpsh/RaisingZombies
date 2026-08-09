using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 열려 있을 때만 네 가지 재화 강화 행을 이벤트 기반으로 갱신합니다.
public sealed class CurrencyUpgradePanel : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager; // 재화 상태와 계산을 제공할 매니저
    [SerializeField] private TMP_Text currencyText; // 현재 재화 표시
    [SerializeField] private TMP_Text productionText; // 현재 초당 재화 표시
    [SerializeField] private Transform rowsRoot; // 강화 행 생성 부모
    [SerializeField] private CurrencyUpgradeRowView rowPrefab; // 재사용할 강화 행 프리팹
    private readonly Dictionary<CurrencyUpgradeType, CurrencyUpgradeRowView> _rows = new(); // 종류별 생성된 행

    // 활성화 중에만 상태 변경 이벤트를 구독하고 최신 값을 표시합니다.
    private void OnEnable()
    {
        upgradeManager = UpgradeManager.Instance;
        SetUpgradeManager(upgradeManager);
        Subscribe();
        Refresh();
    }

    // 화면이 닫히면 상태 변경 이벤트를 해제합니다.
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 런타임이나 테스트에서 사용할 매니저를 연결합니다.
    public void SetUpgradeManager(UpgradeManager manager)
    {
        Unsubscribe();
        upgradeManager = manager;
        Subscribe();
        Refresh();
    }

    // 네 강화 행과 공통 재화 표시를 최신 상태로 갱신합니다.
    public void Refresh()
    {
        if (upgradeManager == null)
        {
            SetText(currencyText, "현재 재화: -");
            SetText(productionText, "UpgradeManager 연결 필요");
            return;
        }

        SetText(currencyText, $"현재 재화: {upgradeManager.Currency:N0}");
        SetText(productionText, $"초당 재화: {upgradeManager.GetCurrencyPerSecond():0.##}");
        foreach (CurrencyUpgradeType type in Enum.GetValues(typeof(CurrencyUpgradeType))) // 표시할 고정 강화 종류
        {
            CurrencyUpgradeRowView row = GetOrCreateRow(type); // 해당 종류의 행
            if (row != null) row.Bind(upgradeManager, type);
        }
    }

    // 상태 변경 이벤트를 중복 없이 구독합니다.
    private void Subscribe()
    {
        if (!isActiveAndEnabled || upgradeManager == null) return;
        upgradeManager.stateChanged -= Refresh;
        upgradeManager.stateChanged += Refresh;
    }

    // 연결된 상태 변경 이벤트를 해제합니다.
    private void Unsubscribe()
    {
        if (upgradeManager != null) upgradeManager.stateChanged -= Refresh;
    }

    // 지정 종류의 행을 재사용하거나 최초 한 번 생성합니다.
    private CurrencyUpgradeRowView GetOrCreateRow(CurrencyUpgradeType type)
    {
        if (_rows.TryGetValue(type, out CurrencyUpgradeRowView row) && row != null) return row;
        if (rowPrefab == null || rowsRoot == null) return null;
        row = Instantiate(rowPrefab, rowsRoot);
        row.name = $"{type}Row";
        _rows[type] = row;
        return row;
    }

    // TMP 참조가 있을 때만 문자열을 설정합니다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
