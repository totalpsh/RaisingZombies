using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// QuestManager와 분리해서 한 행에 전달할 표시 데이터입니다.
public readonly struct QuestListRowData
{
    public readonly int QuestIndex; // 상세 팝업에 전달할 0 기준 인덱스
    public readonly int QuestNumber; // 사용자에게 표시할 1 기준 번호
    public readonly string Title; // Quest Definition에서 읽은 제목
    public readonly string Description; // Quest Definition에서 읽은 목표 설명
    public readonly QuestStatus Status; // 수령 전후를 구분할 실제 상태
    public readonly bool IsCurrent; // 현재 진행 중인 퀘스트 여부
    public readonly bool IsLocked; // 향후 미래 퀘스트 표시 확장용 잠금 여부
    public readonly QuestRewardType RewardType; // 행에 표시할 보상 종류
    public readonly QuestUnlockType UnlockReward; // 전용 아이콘으로 표시할 해금 보상 종류

    // 목록 행에 필요한 값만 보관하는 읽기 전용 데이터를 만듭니다.
    public QuestListRowData(int questIndex, int questNumber, string title, string description, QuestStatus status, bool isCurrent, bool isLocked,
        QuestRewardType rewardType = QuestRewardType.None, QuestUnlockType unlockReward = QuestUnlockType.None)
    {
        QuestIndex = questIndex;
        QuestNumber = questNumber;
        Title = title;
        Description = description;
        Status = status;
        IsCurrent = isCurrent;
        IsLocked = isLocked;
        RewardType = rewardType;
        UnlockReward = unlockReward;
    }
}

// 행 디자인에 전달받은 Quest ViewData만 표시합니다.
public sealed class QuestListRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text questNumberText; // Quest 번호를 숫자로 표시한다
    [SerializeField] private TMP_Text titleText; // Quest Definition의 제목을 표시한다
    [SerializeField] private TMP_Text descriptionText; // Quest Definition의 목표 설명을 표시한다

    [SerializeField] private Image rewardIconImage; // 현재 Quest의 보상 종류 아이콘을 표시한다
    [SerializeField] private Sprite rewardCurrencyIconImage; // 일반 재화 보상 아이콘
    [SerializeField] private Sprite rewardUpgradeCurrencyIconImage; // 재화 강화 해금 보상 아이콘
    [SerializeField] private Sprite unlockLabIconImage; // 생산 강화 또는 연구소 해금 보상 아이콘

    [SerializeField] private GameObject normalObject; // 과거 완료 Quest의 일반 상태를 표시한다
    [SerializeField] private GameObject focusObject; // 현재 진행 중인 Quest의 강조 상태를 표시한다
    [SerializeField] private GameObject disabledObject; // Reward 수령 완료 상태를 표시한다
    [SerializeField] private GameObject lockObject; // 향후 잠긴 Quest 상태를 표시한다
    [SerializeField] private GameObject checkObject; // Reward 수령 완료 체크를 표시한다
    [SerializeField] private Graphic[] borderAndLineGraphics = Array.Empty<Graphic>(); // 상태 색을 적용할 기존 Border와 Line
    [SerializeField] private Color normalColor = Color.white; // 기존 일반 상태 Border와 Line 색
    [SerializeField] private Color focusColor = Color.white; // 기존 현재 상태 Border와 Line 색
    [SerializeField] private Color completedColor = Color.gray; // 기존 완료 상태 Border와 Line 색
    [SerializeField] private Button rowButton; // 상세 팝업을 여는 행 전체 클릭 영역
    private Action<int> _clicked; // 목록 Controller가 제공한 클릭 처리
    private int _questIndex; // 현재 행에 바인딩된 실제 인덱스

    // 행 전체 버튼의 단일 Listener를 준비합니다.
    private void Awake()
    {
        if (rowButton == null) return;
        rowButton.onClick.RemoveListener(HandleClick);
        rowButton.onClick.AddListener(HandleClick);
    }

    // 프리팹 해제 시 등록한 Listener와 외부 Callback을 제거합니다.
    private void OnDestroy()
    {
        if (rowButton != null) rowButton.onClick.RemoveListener(HandleClick);
        _clicked = null;
    }

    // 전달받은 Quest 데이터를 현재 Row UI 전체에 한 번에 반영합니다.
    public void Bind(QuestListRowData data, Action<int> clicked)
    {
        _questIndex = data.QuestIndex;
        _clicked = clicked;
        SetText(questNumberText, data.QuestNumber.ToString());
        SetText(titleText, data.Title);
        SetText(descriptionText, data.Description);
        SetRewardIcon(data);
        RefreshVisualState(data);
        gameObject.SetActive(true);
    }

    // 현재, 수령 완료, 잠금 상태에 맞춰 서로 충돌하지 않게 기존 오브젝트를 전환합니다.
    private void RefreshVisualState(QuestListRowData data)
    {
        bool claimed = data.Status == QuestStatus.Claimed; // Reward까지 수령한 상태
        bool focused = data.IsCurrent && !data.IsLocked; // InProgress와 Claimable 모두 유지할 현재 강조 상태
        SetActive(normalObject, !focused);
        SetActive(focusObject, focused);
        SetActive(disabledObject, claimed);
        SetActive(checkObject, claimed);
        SetActive(lockObject, data.IsLocked);
        Color stateColor = claimed ? completedColor : focused ? focusColor : normalColor; // 기존 디자인에서 설정한 상태 색
        for (int index = 0; index < borderAndLineGraphics.Length; index++)
        {
            if (borderAndLineGraphics[index] != null) borderAndLineGraphics[index].color = stateColor;
        }
    }

    // 목록 Controller의 상세 열기 Callback만 호출하며 보상 로직은 실행하지 않습니다.
    private void HandleClick() { _clicked?.Invoke(_questIndex); }

    // Quest 보상과 해금 종류에 맞춰 사용자가 연결한 Sprite를 표시합니다.
    private void SetRewardIcon(QuestListRowData data)
    {
        if (rewardIconImage == null) return;
        Sprite sprite = data.UnlockReward switch // 일반 재화보다 별도 해금 보상 아이콘을 우선한다
        {
            QuestUnlockType.CurrencyUpgrade => rewardUpgradeCurrencyIconImage,
            QuestUnlockType.ProductionUpgrade => unlockLabIconImage,
            _ => data.RewardType == QuestRewardType.Currency ? rewardCurrencyIconImage : null
        };
        rewardIconImage.sprite = sprite;
        rewardIconImage.enabled = sprite != null;
    }

    // 연결된 오브젝트가 있을 때만 활성 상태를 변경합니다.
    private static void SetActive(GameObject target, bool active) { if (target != null) target.SetActive(active); }

    // 연결된 Text가 있을 때만 값을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value ?? string.Empty; }
}
