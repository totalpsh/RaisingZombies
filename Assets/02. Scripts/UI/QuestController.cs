using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 기존 QuestBox에 현재 퀘스트와 직접 보상 수령 동작을 연결합니다.
public class QuestController : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI questLevelText; // 현재 진행도와 목표 표시
    [SerializeField] private TextMeshProUGUI questInfoText; // 현재 퀘스트 설명
    [SerializeField] private TextMeshProUGUI questRewardText; // 보상 재화 수량
    [SerializeField] private Image questRewardImage; // 정의에서 지정한 보상 이미지
    [SerializeField] private Image questRewardGlowImage; // 수령 가능 상태에 켤 기존 강조 이미지
    [SerializeField] private Sprite crystalRewardIcon; // 기존 사용자 참조 보존용
    [SerializeField] private Sprite upgradeRewardIcon; // 기존 사용자 참조 보존용
    private QuestManager _quests; // 화면과 독립적으로 유지되는 퀘스트 원본

    // 화면 생성과 재진입 시 최신 저장값을 표시합니다.
    private void OnEnable()
    {
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= Refresh; _quests.Changed += Refresh; }
        Refresh();
    }

    // 숨겨진 화면의 이벤트를 정리합니다.
    private void OnDisable()
    {
        if (_quests != null) _quests.Changed -= Refresh;
        _quests = null;
    }

    // 텍스트와 이미지에는 현재 정의와 실제 원본 진행도만 반영합니다.
    public void Refresh()
    {
        QuestDefinition quest = _quests == null ? null : _quests.CurrentQuest; // 현재 하나만 보여줄 목표
        if (questInfoText != null) questInfoText.text = quest == null ? "퀘스트 준비 중" : quest.description;
        if (questLevelText != null) questLevelText.text = quest == null ? "" : $"{_quests.GetProgress(quest)} / {_quests.GetTarget(quest)}";
        if (questRewardText != null) questRewardText.text = quest == null || quest.rewardType == QuestRewardType.None ? "" : quest.rewardAmount.ToString();
        Sprite icon = quest == null || quest.rewardType == QuestRewardType.None ? null : quest.rewardIcon; // 미지정 시 이전 퀘스트 이미지 제거
        if (questRewardImage != null) { questRewardImage.sprite = icon; questRewardImage.enabled = icon != null; }
        if (questRewardGlowImage != null) questRewardGlowImage.enabled = quest != null && _quests.CurrentStatus == QuestStatus.Claimable;
    }

    // 받을 수 있을 때만 수령하고 그 외에는 현재 퀘스트 상세를 엽니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        HandleQuestClick();
    }

    // Inspector Button에서도 동일한 단일 Claim 경로를 호출할 수 있습니다.
    public async void HandleQuestClick()
    {
        if (_quests == null || _quests.CurrentQuest == null) return;
        if (_quests.TryClaimCurrentQuest()) return;
        if (!UIManager.HasInstance) return;
        try { await QuestPopup.ShowAsync(_quests.CurrentQuestIndex); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // 기존 Inspector 연결 이름을 유지하면서 현재 클릭 규칙을 실행합니다.
    public void OpenPopup() { HandleQuestClick(); }

    // 별도 목록 버튼에서 과거와 현재 퀘스트 목록을 엽니다.
    public async void OpenQuestList()
    {
        if (!UIManager.HasInstance) return;
        try { await QuestListPopup.ShowAsync(); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }
}
