using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 기존 UIManager의 팝업 레이어에서 안내 이미지 하나만 보여줍니다.
public sealed class QuestPopup : BaseUI
{
    [SerializeField] private Image questImage; // 현재 퀘스트 정의의 안내 Sprite
    [SerializeField] private Button closeButton; // 기존 팝업 닫기 경로로 연결할 버튼
    private QuestManager _quests; // 표시 중인 목표의 변경 알림 원본
    private static Task<QuestPopup> _openingTask; // 비동기 생성 도중 중복 클릭을 합칠 작업

    // 열려 있는 팝업과 생성 중인 작업을 재사용합니다.
    public static async Task<QuestPopup> ShowAsync()
    {
        if (!UIManager.HasInstance) return null;
        QuestPopup existing = UIManager.Instance.GetUI<QuestPopup>(); // 이미 열린 팝업
        if (existing != null) { existing.RefreshImage(); return existing; }
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<QuestPopup>(null, UILayer.PopUp);
            return await _openingTask;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드를 끈 플레이에서도 이전 생성 작업을 남기지 않습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // 풀에서 다시 열렸을 때도 버튼과 목표 이벤트를 한 번만 연결합니다.
    private void OnEnable()
    {
        if (closeButton != null) { closeButton.onClick.RemoveListener(Close); closeButton.onClick.AddListener(Close); }
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= RefreshImage; _quests.Changed += RefreshImage; }
        RefreshImage();
    }

    // 닫힐 때 Listener와 이전 Sprite를 제거합니다.
    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (_quests != null) _quests.Changed -= RefreshImage;
        _quests = null;
        if (questImage != null) questImage.sprite = null;
    }

    // 현재 정의의 안내 이미지가 없으면 이미지만 숨깁니다.
    public void RefreshImage()
    {
        if (questImage == null) return;
        questImage.sprite = _quests == null ? null : _quests.CurrentQuest?.popupImage;
        questImage.enabled = questImage.sprite != null;
    }

    // UIManager의 기존 풀과 모달 스택을 통해 닫습니다.
    public void Close() { if (UIManager.HasInstance) UIManager.Instance.CloseUI(this); }
}
