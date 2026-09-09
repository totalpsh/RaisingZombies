using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 연구 단계에서 실제 Draw에 쓰는 12개 레어도 확률을 표시합니다.
public sealed class BodyRarityProbabilityPopup : BaseUI
{
    [SerializeField] private TMP_Text researchLevelText; // 현재 적용 중인 뽑기 연구 레벨을 표시한다
    [SerializeField] private TMP_Text probabilityText; // 실제 Weight에서 계산한 12개 확률을 표시한다
    [SerializeField] private Button closeButton; // 확률 팝업을 닫는다
    private BodyEquipmentManager _manager; // 확률 계산과 같은 Settings를 사용하는 원본
    private static Task<BodyRarityProbabilityPopup> _openingTask; // 중복 비동기 생성 방지 작업

    // 현재 확률을 기존 UIManager 팝업으로 엽니다.
    public static async Task<BodyRarityProbabilityPopup> ShowAsync()
    {
        if (!UIManager.HasInstance) return null;
        BodyRarityProbabilityPopup existing = UIManager.Instance.GetUI<BodyRarityProbabilityPopup>(); // 이미 열린 확률 팝업
        if (existing != null) { existing.Refresh(); return existing; }
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<BodyRarityProbabilityPopup>(null, UILayer.PopUp);
            return await _openingTask;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드 없는 플레이에서도 이전 비동기 작업을 지웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // 연구 변경 이벤트와 닫기 버튼을 연결합니다.
    private void OnEnable()
    {
        _manager = BodyEquipmentManager.EnsureInstance();
        _manager.StateChanged -= Refresh;
        _manager.StateChanged += Refresh;
        AddButtonListener(closeButton, Close);
        Refresh();
    }

    // 숨겨진 팝업의 Listener를 정리합니다.
    private void OnDisable()
    {
        if (_manager != null) _manager.StateChanged -= Refresh;
        RemoveButtonListener(closeButton, Close);
    }

    // 실제 연구 Weight 한 곳에서 12개 정규화 확률을 계산해 표시합니다.
    private void Refresh()
    {
        if (_manager == null) return;
        StringBuilder builder = new(); // 이번 이벤트 갱신에 사용할 확률 문자열
        var probabilities = _manager.GetCurrentRarityProbabilities(); // Draw와 같은 Weight 원본의 정규화 결과
        for (int index = 0; index < probabilities.Count; index++)
        {
            BodyRarityProbability item = probabilities[index]; // 표시할 한 레어도 확률
            if (index > 0) builder.AppendLine();
            builder.Append(item.DisplayName).Append("  ").Append(BodyEquipmentUIFormatter.FormatProbability(item.Probability));
        }
        SetText(researchLevelText, $"뽑기 연구 Lv.{_manager.ResearchLevel}");
        SetText(probabilityText, builder.ToString());
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

    // Text 참조가 있을 때만 문자열을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value ?? string.Empty; }
}
