using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 목록의 한 행을 새 퀘스트 인덱스에 반복해서 재바인딩합니다.
public sealed class QuestListRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text questNumberText; // 목록 기준 퀘스트 번호
    [SerializeField] private TMP_Text descriptionText; // 퀘스트 내용
    [SerializeField] private TMP_Text progressText; // 실제 진행도
    [SerializeField] private TMP_Text statusText; // 수령 완료·가능·진행 중 상태
    [SerializeField] private Image rewardIcon; // 정의에서 지정한 보상 이미지
    [SerializeField] private TMP_Text rewardAmountText; // 보상 수량
    [SerializeField] private Button rowButton; // 상세 또는 현재 보상 수령 클릭 영역
    private QuestManager _quests; // 현재 행의 진행 원본
    private int _questIndex; // 재바인딩된 퀘스트 인덱스

    // 행 버튼의 단일 Listener를 준비합니다.
    private void Awake()
    {
        if (rowButton == null) return;
        rowButton.onClick.RemoveListener(HandleClick);
        rowButton.onClick.AddListener(HandleClick);
    }

    // 풀이나 프리팹 해제 시 등록한 Listener를 제거합니다.
    private void OnDestroy() { if (rowButton != null) rowButton.onClick.RemoveListener(HandleClick); }

    // 기존 행 객체에 선택 페이지의 퀘스트 하나를 덮어씁니다.
    public void Bind(QuestManager quests, int questIndex)
    {
        _quests = quests;
        _questIndex = questIndex;
        Refresh();
    }

    // 실제 상태와 데이터 기반 표시를 갱신합니다.
    public void Refresh()
    {
        QuestDefinition quest = _quests == null ? null : _quests.GetQuest(_questIndex); // 표시할 정의 하나
        if (quest == null) { gameObject.SetActive(false); return; }
        QuestStatus status = _quests.GetQuestStatus(_questIndex); // 순차 인덱스가 결정한 상태
        SetText(questNumberText, $"Quest {_questIndex + 1}");
        SetText(descriptionText, quest.description);
        SetText(progressText, $"{_quests.GetProgress(quest)} / {_quests.GetTarget(quest)}");
        SetText(statusText, status == QuestStatus.Claimed ? "수령 완료" : status == QuestStatus.Claimable ? "수령 가능" : "진행 중");
        SetText(rewardAmountText, quest.rewardType == QuestRewardType.None ? "" : quest.rewardAmount.ToString());
        if (rewardIcon != null)
        {
            rewardIcon.sprite = quest.rewardType == QuestRewardType.None ? null : quest.rewardIcon;
            rewardIcon.enabled = rewardIcon.sprite != null;
        }
        gameObject.SetActive(true);
    }

    // Claimable 현재 행은 수령하고 그 외 상태는 상세 팝업을 엽니다.
    private async void HandleClick()
    {
        if (_quests == null) return;
        if (_questIndex == _quests.CurrentQuestIndex && _quests.TryClaimCurrentQuest()) return;
        if (!UIManager.HasInstance) return;
        try { await QuestPopup.ShowAsync(_questIndex); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // 연결된 Text가 있을 때만 값을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value; }
}
