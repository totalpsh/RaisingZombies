using System.Collections;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비 파기 포인트로 진행하는 실제 시간 뽑기 연구를 표시합니다.
public sealed class BodyResearchPopup : BaseUI
{
    [SerializeField] private TMP_Text researchLevelText; // 현재 적용 중인 연구 레벨을 표시한다
    [SerializeField] private TMP_Text researchPointText; // 보유 연구 포인트를 표시한다
    [SerializeField] private TMP_Text requiredPointText; // 다음 연구에 필요한 포인트를 표시한다
    [SerializeField] private TMP_Text durationText; // 다음 연구에 필요한 실제 시간을 표시한다
    [SerializeField] private TMP_Text currentProbabilityText; // 현재 레벨의 12개 확률을 표시한다
    [SerializeField] private TMP_Text nextProbabilityText; // 다음 레벨의 12개 확률을 표시한다
    [SerializeField] private TMP_Text timerText; // 진행 중인 연구의 남은 시간을 표시한다
    [SerializeField] private TMP_Text messageText; // 연구 시작 실패와 완료 상태를 표시한다
    [SerializeField] private Button startButton; // 포인트를 소비하고 다음 연구를 시작한다
    [SerializeField] private Button closeButton; // 연구 팝업을 닫는다
    [SerializeField] private GameObject timerRoot; // 연구 중에만 표시할 Timer 영역
    private BodyEquipmentManager _manager; // 연구 상태와 확률의 실제 원본
    private Coroutine _timerCoroutine; // 팝업이 열린 동안만 동작할 1초 Timer
    private static Task<BodyResearchPopup> _openingTask; // 중복 비동기 생성 방지 작업

    // 현재 연구 상태를 기존 UIManager 팝업으로 엽니다.
    public static async Task<BodyResearchPopup> ShowAsync()
    {
        if (!UIManager.HasInstance) return null;
        BodyResearchPopup existing = UIManager.Instance.GetUI<BodyResearchPopup>(); // 이미 열린 연구 팝업
        if (existing != null) { existing.Refresh(); return existing; }
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<BodyResearchPopup>(null, UILayer.PopUp);
            return await _openingTask;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드 없는 플레이에서도 이전 비동기 작업을 지웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // 연구 이벤트, 버튼과 열린 동안만 필요한 Timer를 연결합니다.
    private void OnEnable()
    {
        _manager = BodyEquipmentManager.EnsureInstance();
        _manager.StateChanged -= Refresh;
        _manager.StateChanged += Refresh;
        AddButtonListener(startButton, StartResearch);
        AddButtonListener(closeButton, Close);
        Refresh();
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(RefreshTimerRoutine());
    }

    // 숨겨진 팝업의 이벤트와 Timer를 정리합니다.
    private void OnDisable()
    {
        if (_manager != null) _manager.StateChanged -= Refresh;
        RemoveButtonListener(startButton, StartResearch);
        RemoveButtonListener(closeButton, Close);
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = null;
    }

    // 현재 레벨, 비용, 시간과 현재·다음 확률을 한 번에 갱신합니다.
    private void Refresh()
    {
        if (_manager == null || _manager.ResearchSettings == null) return;
        BodyDrawResearchSettingsSO settings = _manager.ResearchSettings; // 연구 밸런스 원본
        BodyDrawResearchLevelDefinition current = settings.GetLevel(_manager.ResearchLevel); // 현재 단계와 다음 연구 조건
        bool maximum = _manager.ResearchLevel >= settings.MaximumResearchLevel; // 더 진행할 연구가 없는지 여부
        SetText(researchLevelText, $"뽑기 연구 Lv.{_manager.ResearchLevel}");
        SetText(researchPointText, _manager.ResearchPoints.ToString());
        SetText(requiredPointText, maximum || current == null ? "MAX" : current.requiredResearchPoints.ToString());
        SetText(durationText, maximum || current == null ? "MAX" : BodyEquipmentUIFormatter.FormatDuration(current.durationSeconds));
        SetText(currentProbabilityText, BuildProbabilityText(_manager.ResearchLevel));
        SetText(nextProbabilityText, maximum ? "최고 연구 단계" : BuildProbabilityText(_manager.ResearchLevel + 1));
        bool canStart = !maximum && !_manager.IsResearching && current != null && _manager.ResearchPoints >= current.requiredResearchPoints; // 연구 시작 버튼 활성 조건
        if (startButton != null) startButton.interactable = canStart;
        SetActive(timerRoot, _manager.IsResearching);
        RefreshTimerText();
    }

    // 현재 포인트와 진행 상태가 허용할 때 다음 단계 연구를 시작합니다.
    private void StartResearch()
    {
        bool success = _manager != null && _manager.TryStartResearch(); // Manager가 판정한 실제 시작 결과
        SetText(messageText, success ? "연구를 시작했습니다." : "연구 중이거나 포인트가 부족합니다.");
        Refresh();
    }

    // 팝업이 열린 동안에만 약 1초마다 실제 UTC 남은 시간을 갱신합니다.
    private IEnumerator RefreshTimerRoutine()
    {
        WaitForSecondsRealtime interval = new(1f); // Timer UI 전용 1초 대기 객체
        while (true)
        {
            if (_manager != null)
            {
                bool completed = _manager.RefreshResearchCompletion(); // 오프라인 시간을 포함한 완료 판정
                if (!completed) RefreshTimerText();
            }
            yield return interval;
        }
    }

    // 진행 중인 연구의 남은 실제 시간만 표시합니다.
    private void RefreshTimerText()
    {
        if (_manager == null) return;
        SetText(timerText, _manager.IsResearching ? BodyEquipmentUIFormatter.FormatDuration(_manager.GetRemainingResearchSeconds()) : string.Empty);
    }

    // 지정 연구 레벨의 실제 Weight를 같은 Settings에서 정규화해 표시합니다.
    private string BuildProbabilityText(int researchLevel)
    {
        if (_manager == null || _manager.Database == null || _manager.ResearchSettings == null) return string.Empty;
        StringBuilder builder = new(); // 현재 또는 다음 레벨 확률 문자열
        for (int tier = 1; tier <= 12; tier++)
        {
            string name = _manager.Database.TryGetRarity(tier, out BodyRarityDefinitionSO rarity) ? rarity.DisplayName : $"Tier {tier}"; // 데이터 기반 레어도 이름
            float probability = _manager.GetRarityProbability(researchLevel, tier); // Draw와 같은 Weight에서 계산한 0부터 1 확률
            if (tier > 1) builder.AppendLine();
            builder.Append(name).Append("  ").Append(BodyEquipmentUIFormatter.FormatProbability(probability));
        }
        return builder.ToString();
    }

    // UIManager의 기존 풀과 모달 구조를 통해 닫습니다.
    public void Close() { if (UIManager.HasInstance) UIManager.Instance.CloseUI(this); }

    // 버튼에 중복 없이 Listener를 연결합니다.
    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 등록한 버튼 Listener를 제거합니다.
    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.RemoveListener(action); }

    // 오브젝트 참조가 있을 때만 활성 상태를 변경합니다.
    private static void SetActive(GameObject target, bool active) { if (target != null) target.SetActive(active); }

    // Text 참조가 있을 때만 문자열을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value ?? string.Empty; }
}
