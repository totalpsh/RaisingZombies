using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 과거와 현재 퀘스트를 최대 50개 행으로 페이지 표시합니다.
public sealed class QuestListPopup : BaseUI
{
    public const int QuestPageSize = 50; // 한 페이지에 존재할 수 있는 최대 행 수
    [SerializeField] private Transform rowsRoot; // 최대 50개 행을 보관할 부모
    [SerializeField] private QuestListRowView rowPrefab; // 모든 페이지가 공유할 행 프리팹
    [SerializeField] private TMP_Text pageText; // 현재 페이지와 마지막 페이지 표시
    [SerializeField] private Button previousButton; // 이전 50개로 이동
    [SerializeField] private Button nextButton; // 다음 50개로 이동
    [SerializeField] private Button closeButton; // 기존 팝업 닫기 버튼
    private readonly List<QuestListRowView> _rows = new(QuestPageSize); // 최초 생성 후 계속 재사용할 행 목록
    private QuestManager _quests; // 목록에 표시할 결정적 퀘스트 원본
    private int _currentPage; // 현재 표시 중인 0 기준 페이지
    private static Task<QuestListPopup> _openingTask; // 중복 비동기 생성을 합칠 작업
    public int CurrentPage => _currentPage; // 테스트와 외부 페이지 표시가 읽는 값
    public int CreatedRowCount => _rows.Count; // 50개 제한 검증용 생성 개수

    // 현재 퀘스트가 포함된 페이지로 목록을 열고 기존 팝업은 재사용합니다.
    public static async Task<QuestListPopup> ShowAsync()
    {
        if (!UIManager.HasInstance) return null;
        QuestListPopup existing = UIManager.Instance.GetUI<QuestListPopup>(); // 이미 열린 목록
        if (existing != null) { existing.ShowCurrentQuestPage(); return existing; }
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<QuestListPopup>(null, UILayer.PopUp);
            QuestListPopup popup = await _openingTask; // 처음 생성된 목록
            if (popup != null) popup.ShowCurrentQuestPage();
            return popup;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드를 끈 플레이에서도 이전 생성 작업을 남기지 않습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // 풀에서 다시 열릴 때 버튼과 상태 이벤트를 중복 없이 연결합니다.
    private void OnEnable()
    {
        AddButtonListener(previousButton, ShowPreviousPage);
        AddButtonListener(nextButton, ShowNextPage);
        AddButtonListener(closeButton, Close);
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= RefreshPage; _quests.Changed += RefreshPage; }
        ShowCurrentQuestPage();
    }

    // 숨겨진 목록은 Listener만 해제하고 생성한 행은 다음 열기에 재사용합니다.
    private void OnDisable()
    {
        RemoveButtonListener(previousButton, ShowPreviousPage);
        RemoveButtonListener(nextButton, ShowNextPage);
        RemoveButtonListener(closeButton, Close);
        if (_quests != null) _quests.Changed -= RefreshPage;
        _quests = null;
    }

    // 현재 퀘스트가 포함된 50개 단위 페이지를 기본으로 선택합니다.
    public void ShowCurrentQuestPage()
    {
        _currentPage = _quests == null ? 0 : _quests.CurrentQuestIndex / QuestPageSize;
        RefreshPage();
    }

    // 존재하는 과거 페이지로 한 칸 이동합니다.
    public void ShowPreviousPage()
    {
        if (_currentPage <= 0) return;
        _currentPage--;
        RefreshPage();
    }

    // 현재 진행 범위를 넘지 않는 다음 페이지로 이동합니다.
    public void ShowNextPage()
    {
        int lastPage = _quests == null ? 0 : _quests.CurrentQuestIndex / QuestPageSize; // 접근 가능한 마지막 페이지
        if (_currentPage >= lastPage) return;
        _currentPage++;
        RefreshPage();
    }

    // 필요한 수만큼만 행을 추가하고 페이지 이동에서는 기존 행을 재바인딩합니다.
    public void RefreshPage()
    {
        if (_quests == null || rowsRoot == null || rowPrefab == null) return;
        int lastPage = _quests.CurrentQuestIndex / QuestPageSize; // 현재 퀘스트를 포함한 마지막 페이지
        _currentPage = Mathf.Clamp(_currentPage, 0, lastPage);
        long startLong = (long)_currentPage * QuestPageSize; // 정수 오버플로 없는 첫 인덱스
        int startIndex = (int)System.Math.Min(int.MaxValue, startLong);
        int available = (int)System.Math.Min(QuestPageSize, (long)_quests.CurrentQuestIndex - startIndex + 1L); // 미래 항목을 제외한 실제 표시 수
        EnsureRows(available);
        for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++) // 보유 행은 최대 50개
        {
            bool visible = rowIndex < available; // 이 페이지에서 실제로 존재하는 행인지 여부
            _rows[rowIndex].gameObject.SetActive(visible);
            if (visible) _rows[rowIndex].Bind(_quests, startIndex + rowIndex);
        }
        if (pageText != null) pageText.text = $"{_currentPage + 1} / {lastPage + 1}";
        if (previousButton != null) previousButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < lastPage;
    }

    // 한 번 생성한 행을 유지하며 필요한 개수까지만 최대 50개로 늘립니다.
    private void EnsureRows(int required)
    {
        int target = Mathf.Clamp(required, 0, QuestPageSize); // 성능 제한을 적용한 목표 행 수
        while (_rows.Count < target)
        {
            QuestListRowView row = Instantiate(rowPrefab, rowsRoot); // 이후 페이지에서 계속 재사용할 행
            _rows.Add(row);
        }
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
