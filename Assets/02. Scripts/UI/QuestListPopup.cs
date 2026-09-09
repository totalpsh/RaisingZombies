using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 과거·현재·미래 퀘스트를 높은 번호가 위로 향하는 최대 50개 묶음으로 표시합니다.
public sealed class QuestListPopup : BaseUI
{
    public const int QuestBatchSize = 50; // 한 번에 생성하고 활성화할 수 있는 최대 행 수
    [SerializeField] private ScrollRect scrollRect; // 사용자가 목록을 드래그하는 기존 Scroll View
    [SerializeField] private Transform rowsRoot; // 재사용 행을 보관하는 기존 Scroll Content
    [SerializeField] private QuestListRowView rowPrefab; // 사용자가 만든 디자인을 보존한 재사용 행 프리팹
    [SerializeField, Range(0f, 0.1f)] private float edgeThreshold = 0.015f; // 다음 묶음을 불러올 Scroll 끝 감지 범위
    [SerializeField] private Button closeButton; // 기존 팝업 닫기 버튼
    private readonly List<QuestListRowView> _rows = new(QuestBatchSize); // 최초 생성 후 계속 재사용할 행 목록
    private QuestManager _quests; // 목록 ViewData를 만드는 실제 퀘스트 원본
    private int _rangeStartIndex; // 현재 표시 중인 묶음에서 가장 낮은 0 기준 퀘스트 인덱스
    private int _visibleRowCount; // 현재 묶음에서 활성화된 실제 행 수
    private int _knownCurrentQuestIndex = -1; // 진행 갱신과 다음 Quest 이동을 구분할 마지막 인덱스
    private bool _changingRange; // Scroll 위치 변경 중 중복 묶음 전환을 막는다
    private static Task<QuestListPopup> _openingTask; // 중복 비동기 생성을 합칠 작업
    public int RangeStartIndex => _rangeStartIndex; // 테스트와 상태 확인용 현재 묶음 시작 인덱스
    public int CreatedRowCount => _rows.Count; // 최대 50개 제한 검증용 생성 개수
    public int VisibleRowCount => _visibleRowCount; // 현재 구간의 과거·현재·미래 활성 행 개수

    // 현재 퀘스트가 포함된 묶음으로 목록을 열고 기존 팝업은 재사용합니다.
    public static async Task<QuestListPopup> ShowAsync()
    {
        if (!UIManager.HasInstance) return null;
        QuestListPopup existing = UIManager.Instance.GetUI<QuestListPopup>(); // 이미 열린 목록
        if (existing != null) { existing.ShowCurrentQuestRange(); return existing; }
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<QuestListPopup>(null, UILayer.PopUp);
            QuestListPopup popup = await _openingTask; // 처음 생성된 목록
            if (popup != null) popup.ShowCurrentQuestRange();
            return popup;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드를 끈 플레이에서도 이전 생성 작업을 남기지 않습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // 풀에서 다시 열릴 때 버튼, Scroll과 상태 이벤트를 중복 없이 연결합니다.
    private void OnEnable()
    {
        AddButtonListener(closeButton, Close);
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
            scrollRect.onValueChanged.AddListener(HandleScrollChanged);
        }
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= HandleQuestChanged; _quests.Changed += HandleQuestChanged; }
        ShowCurrentQuestRange();
    }

    // 숨겨진 목록은 Listener만 해제하고 생성한 행은 다음 열기에 재사용합니다.
    private void OnDisable()
    {
        RemoveButtonListener(closeButton, Close);
        if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
        if (_quests != null) _quests.Changed -= HandleQuestChanged;
        _quests = null;
    }

    // 현재 퀘스트가 포함된 50개 구간을 역순 표시하고 현재 행이 보이는 위치로 이동합니다.
    public void ShowCurrentQuestRange()
    {
        int currentIndex = _quests == null ? 0 : Mathf.Max(0, _quests.CurrentQuestIndex); // 현재 접근 가능한 마지막 인덱스
        _knownCurrentQuestIndex = currentIndex;
        int startIndex = currentIndex / QuestBatchSize * QuestBatchSize; // 현재 퀘스트가 포함된 묶음 시작점
        int count = GetRangeCount(startIndex); // 정수 최대값을 넘지 않는 현재 구간 행 수
        float currentPosition = count <= 1 ? 0f : (float)(currentIndex - startIndex) / (count - 1); // 역순 목록에서 현재 행이 있는 위치
        BindRange(startIndex, currentPosition);
    }

    // 현재 Quest가 바뀌면 새 행으로 이동하고 같은 Quest 진행 갱신이면 보고 있던 위치를 유지합니다.
    private void HandleQuestChanged()
    {
        if (_quests == null) return;
        if (_knownCurrentQuestIndex != _quests.CurrentQuestIndex) { ShowCurrentQuestRange(); return; }
        long rangeEnd = (long)_rangeStartIndex + _visibleRowCount; // 현재 표시 범위의 다음 인덱스
        if (_quests.CurrentQuestIndex < _rangeStartIndex || _quests.CurrentQuestIndex >= rangeEnd) return;
        float position = scrollRect == null ? 0f : scrollRect.verticalNormalizedPosition; // 진행 갱신 전 사용자가 보던 Scroll 위치
        BindRange(_rangeStartIndex, position);
    }

    // 위쪽 끝으로 이동했을 때 번호가 더 높은 미래 Quest 구간을 같은 행으로 표시합니다.
    public void ShowHigherQuestRange()
    {
        long nextStart = (long)_rangeStartIndex + QuestBatchSize; // 다음 미래 구간의 첫 인덱스
        if (nextStart > int.MaxValue) return;
        BindRange((int)nextStart, 0.1f);
    }

    // 아래쪽 끝으로 이동했을 때 번호가 더 낮은 과거 Quest 구간을 같은 행으로 표시합니다.
    public void ShowLowerQuestRange()
    {
        if (_rangeStartIndex <= 0) return;
        BindRange(Mathf.Max(0, _rangeStartIndex - QuestBatchSize), 0.9f);
    }

    // Scroll의 양 끝에 도달했을 때만 이전 또는 다음 데이터 묶음을 재바인딩합니다.
    private void HandleScrollChanged(Vector2 normalizedPosition)
    {
        if (_changingRange || _quests == null) return;
        if (normalizedPosition.y >= 1f - edgeThreshold) ShowHigherQuestRange();
        else if (normalizedPosition.y <= edgeThreshold) ShowLowerQuestRange();
    }

    // 지정 범위의 Quest ViewData를 만들고 기존 행에 한 번에 적용합니다.
    private void BindRange(int startIndex, float normalizedPosition)
    {
        if (_quests == null || rowsRoot == null || rowPrefab == null) return;
        _rangeStartIndex = Mathf.Max(0, startIndex);
        _visibleRowCount = GetRangeCount(_rangeStartIndex);
        EnsureRows(_visibleRowCount);
        for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            bool visible = rowIndex < _visibleRowCount; // 현재 범위에서 실제로 존재하는 행인지 여부
            QuestListRowView row = _rows[rowIndex]; // 이번 위치에 재사용할 행
            row.gameObject.SetActive(visible);
            if (!visible) continue;
            int questIndex = (int)((long)_rangeStartIndex + _visibleRowCount - 1L - rowIndex); // 높은 번호가 위에 오도록 뒤집은 실제 인덱스
            QuestDefinition quest = _quests.GetQuest(questIndex); // 정의 또는 결정적으로 재구성한 과거 정의
            if (quest == null) { row.gameObject.SetActive(false); continue; }
            QuestListRowData data = new(
                questIndex,
                questIndex + 1,
                quest.title,
                quest.description,
                _quests.GetQuestStatus(questIndex),
                questIndex == _quests.CurrentQuestIndex,
                questIndex > _quests.CurrentQuestIndex,
                quest.rewardType,
                quest.unlockReward,
                quest.rewardIcon);
            row.Bind(data, HandleRowClicked);
        }
        SetScrollPosition(normalizedPosition);
    }

    // 지정 시작점부터 int 범위 안에서 만들 수 있는 행 수를 최대 50개로 제한합니다.
    private static int GetRangeCount(int startIndex)
    {
        return (int)System.Math.Min(QuestBatchSize, (long)int.MaxValue - startIndex + 1L);
    }

    // 한 번 생성한 행을 유지하며 필요한 개수까지만 최대 50개로 늘립니다.
    private void EnsureRows(int required)
    {
        int target = Mathf.Clamp(required, 0, QuestBatchSize); // 성능 제한을 적용한 목표 행 수
        while (_rows.Count < target)
        {
            QuestListRowView row = Instantiate(rowPrefab, rowsRoot); // 다른 묶음에서도 계속 재사용할 행
            _rows.Add(row);
        }
    }

    // 레이아웃 계산 후 현재 행 또는 새 묶음의 자연스러운 위치로 Scroll을 옮깁니다.
    private void SetScrollPosition(float normalizedPosition)
    {
        if (scrollRect == null) return;
        _changingRange = true;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
        _changingRange = false;
    }

    // 행 클릭은 보상을 지급하지 않고 기존 상세 팝업에 선택 인덱스만 전달합니다.
    private async void HandleRowClicked(int questIndex)
    {
        if (!UIManager.HasInstance) return;
        try { await QuestPopup.ShowAsync(questIndex); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // UIManager의 기존 풀과 모달 스택을 통해 닫습니다.
    public void Close() { if (UIManager.HasInstance) UIManager.Instance.CloseUI(this); }

    // 버튼 Listener를 중복 없이 연결합니다.
    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 등록했던 버튼 Listener만 해제합니다.
    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.RemoveListener(action); }
}
