using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 선택한 퀘스트 하나의 실제 진행과 관련 강화 상태를 보여줍니다.
public sealed class QuestPopup : BaseUI
{
    [SerializeField] private Image questImage; // 수동 퀘스트 또는 조건 종류의 안내 Sprite
    [SerializeField] private TMP_Text questNumberText; // 목록 기준 퀘스트 번호
    [SerializeField] private TMP_Text questDescriptionText; // 선택 퀘스트 내용
    [SerializeField] private TMP_Text progressText; // 현재 진행도
    [SerializeField] private TMP_Text statusText; // InProgress, Claimable, Claimed 상태
    [SerializeField] private TMP_Text relatedProgressText; // Stage 또는 실제 Upgrade 상세 상태
    [SerializeField] private Image rewardIcon; // 선택 퀘스트 보상 이미지
    [SerializeField] private TMP_Text rewardAmountText; // 선택 퀘스트 보상 수량
    [SerializeField] private Button claimButton; // 현재 Claimable 퀘스트만 수령할 버튼
    [SerializeField] private Button closeButton; // 기존 팝업 닫기 경로로 연결할 버튼
    private QuestManager _quests; // 선택 퀘스트의 실제 진행 원본
    private int _questIndex = -1; // 목록에서 선택한 0 기준 인덱스
    private static Task<QuestPopup> _openingTask; // 비동기 생성 중 중복 클릭을 합칠 작업

    // 기존 호출부가 현재 퀘스트 상세를 열 수 있도록 유지하는 진입점입니다.
    public static Task<QuestPopup> ShowAsync()
    {
        int questIndex = QuestManager.HasInstance ? QuestManager.Instance.CurrentQuestIndex : 0; // 현재 표시할 순차 인덱스
        return ShowAsync(questIndex);
    }

    // 열려 있는 팝업과 생성 중인 작업을 재사용합니다.
    public static async Task<QuestPopup> ShowAsync(int questIndex)
    {
        if (!UIManager.HasInstance) return null;
        QuestPopup existing = UIManager.Instance.GetUI<QuestPopup>(); // 이미 열린 상세 팝업
        if (existing != null) { existing.SetQuestIndex(questIndex); return existing; }
        if (_openingTask != null)
        {
            QuestPopup opening = await _openingTask; // 먼저 요청된 생성 작업
            if (opening != null) opening.SetQuestIndex(questIndex);
            return opening;
        }
        try
        {
            _openingTask = UIManager.Instance.OpenUI<QuestPopup>(questIndex, UILayer.PopUp);
            return await _openingTask;
        }
        finally { _openingTask = null; }
    }

    // UIManager가 전달한 선택 인덱스를 적용합니다.
    public override void Init(object param = null)
    {
        if (param is int questIndex) SetQuestIndex(questIndex);
        else if (_quests != null) SetQuestIndex(_quests.CurrentQuestIndex);
    }

    // 도메인 재로드를 끈 플레이에서도 이전 생성 작업을 남기지 않습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // 풀에서 다시 열렸을 때도 버튼과 목표 이벤트를 한 번만 연결합니다.
    private void OnEnable()
    {
        AddButtonListener(closeButton, Close);
        AddButtonListener(claimButton, Claim);
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= RefreshDetail; _quests.Changed += RefreshDetail; }
        if (_questIndex < 0 && _quests != null) _questIndex = _quests.CurrentQuestIndex;
        RefreshDetail();
    }

    // 닫힐 때 Listener와 이전 Sprite를 제거합니다.
    private void OnDisable()
    {
        RemoveButtonListener(closeButton, Close);
        RemoveButtonListener(claimButton, Claim);
        if (_quests != null) _quests.Changed -= RefreshDetail;
        _quests = null;
        if (questImage != null) questImage.sprite = null;
        if (rewardIcon != null) rewardIcon.sprite = null;
    }

    // 목록에서 선택한 퀘스트로 상세 내용을 재바인딩합니다.
    public void SetQuestIndex(int questIndex)
    {
        _questIndex = Mathf.Max(0, questIndex);
        RefreshDetail();
    }

    // 실제 원본 진행도와 Snapshot 정보를 상세 UI에 반영합니다.
    public void RefreshDetail()
    {
        QuestDefinition quest = _quests == null ? null : _quests.GetQuest(_questIndex); // 선택한 정의 하나
        QuestStatus status = _quests == null ? QuestStatus.InProgress : _quests.GetQuestStatus(_questIndex); // 선택한 상태
        SetText(questNumberText, quest == null ? "" : $"Quest {_questIndex + 1}");
        SetText(questDescriptionText, quest?.description ?? "퀘스트 정보 없음");
        SetText(progressText, quest == null ? "" : $"{_quests.GetProgress(quest)} / {_quests.GetTarget(quest)}");
        SetText(statusText, GetStatusText(status));
        SetText(relatedProgressText, quest == null ? "" : _quests.GetRelatedProgressText(quest));
        SetText(rewardAmountText, quest == null || quest.rewardType == QuestRewardType.None ? "" : quest.rewardAmount.ToString());
        SetImage(questImage, quest?.popupImage);
        SetImage(rewardIcon, quest == null || quest.rewardType == QuestRewardType.None ? null : quest.rewardIcon);
        if (claimButton != null) claimButton.gameObject.SetActive(quest != null && _questIndex == _quests.CurrentQuestIndex && status == QuestStatus.Claimable);
    }

    // 상세 화면에서도 Main Quest와 같은 Claim API만 호출합니다.
    public void Claim()
    {
        if (_quests == null || _questIndex != _quests.CurrentQuestIndex || !_quests.TryClaimCurrentQuest()) return;
        Close();
    }

    // UIManager의 기존 풀과 모달 스택을 통해 닫습니다.
    public void Close() { if (UIManager.HasInstance) UIManager.Instance.CloseUI(this); }

    // 상태 Enum을 사용자 표시 문자열로 변환합니다.
    private static string GetStatusText(QuestStatus status) => status switch
    {
        QuestStatus.Claimable => "보상 수령 가능", QuestStatus.Claimed => "수령 완료", _ => "진행 중"
    };

    // Sprite가 없으면 이전 항목의 이미지를 숨깁니다.
    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    // 참조가 있는 텍스트만 안전하게 변경합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value; }

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
