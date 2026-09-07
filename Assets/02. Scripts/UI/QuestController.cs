using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 기존 QuestBox의 표시 참조를 유지하며 현재 목표와 안내 팝업을 연결합니다.
public class QuestController : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI questLevelText; // 기존 라벨에 현재 진행도와 목표를 표시
    [SerializeField] private TextMeshProUGUI questInfoText; // 현재 퀘스트 설명
    [SerializeField] private TextMeshProUGUI questRewardText; // 보상 재화 수량
    
    [SerializeField] private Image questRewardImage; // 정의에서 지정한 보상 이미지
    [SerializeField] private Image questRewardGlowImage; // 이미지가 있을 때만 유지할 기존 빛 효과
    
    [SerializeField] private Sprite crystalRewardIcon; // 기존 사용자 참조 보존용이며 보상 정의가 이미지 원본
    [SerializeField] private Sprite upgradeRewardIcon; // 기존 사용자 참조 보존용이며 새 재화는 추가하지 않음
    private QuestManager _quests; // 화면과 독립적으로 유지되는 퀘스트 원본

    // 화면 생성과 재진입 시 최신 저장값을 표시합니다.
    private void OnEnable()
    {
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= Refresh; _quests.Changed += Refresh; }
        Refresh();
    }

    // 숨겨진 화면의 이벤트와 팝업을 정리합니다.
    private void OnDisable()
    {
        if (_quests != null) _quests.Changed -= Refresh;
        _quests = null;
    }

    // 텍스트와 이미지에는 현재 정의와 실제 원본 진행도만 반영합니다.
    public void Refresh()
    {
        QuestDefinition quest = _quests == null ? null : _quests.CurrentQuest; // 현재 하나만 보여줄 목표
        if (questInfoText != null) questInfoText.text = quest == null ? (_quests == null ? "퀘스트 준비 중" : "초반 퀘스트 완료") : quest.description;
        if (questLevelText != null) questLevelText.text = quest == null ? "" : $"{_quests.GetProgress(quest)} / {_quests.GetTarget(quest)}";
        if (questRewardText != null) questRewardText.text = quest == null || quest.rewardType == QuestRewardType.None ? "" : quest.rewardAmount.ToString();
        Sprite icon = quest == null || quest.rewardType == QuestRewardType.None ? null : quest.rewardIcon; // 미지정 시 이전 퀘스트 이미지 제거
        if (questRewardImage != null) { questRewardImage.sprite = icon; questRewardImage.enabled = icon != null; }
        if (questRewardGlowImage != null) questRewardGlowImage.enabled = icon != null;
    }

    // 기존 QuestBox의 자식 Graphic 클릭을 안내 팝업 열기로 연결합니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) OpenPopup();
    }

    // Inspector Button에서도 호출할 수 있는 안내창 열기 함수입니다.
    public async void OpenPopup()
    {
        if (_quests == null || _quests.CurrentQuest == null || !UIManager.HasInstance) return;
        try { await QuestPopup.ShowAsync(); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }
}
