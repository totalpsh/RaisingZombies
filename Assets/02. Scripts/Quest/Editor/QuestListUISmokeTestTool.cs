#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 실제 저장을 사용하지 않고 Quest List 프리팹과 행 상태만 검사합니다.
public static class QuestListUISmokeTestTool
{
    private const string PopupPath = "Assets/03. Prefabs/UI/Common/QuestListPopup.prefab"; // 검사할 실제 목록 프리팹
    private const string RowPath = "Assets/03. Prefabs/UI/Common/QuestListRowView.prefab"; // 검사할 실제 재사용 행 프리팹

    // 프리팹 참조, ViewData 상태, 클릭 Callback과 데이터 정의를 안전하게 검사합니다.
    [MenuItem("Tools/Raising Zombies/Quest/Run Quest List UI Smoke Test")]
    public static void Run()
    {
        GameObject popupRoot = PrefabUtility.LoadPrefabContents(PopupPath); // 저장하지 않고 읽을 팝업 내용
        GameObject rowRoot = PrefabUtility.LoadPrefabContents(RowPath); // 저장하지 않고 읽을 행 내용
        GameObject managerObject = null; // 저장 초기화 없이 목록 데이터만 제공할 테스트 Manager 오브젝트
        try
        {
            QuestListPopup popup = popupRoot.GetComponent<QuestListPopup>(); // 실제 목록 Controller
            QuestListRowView row = rowRoot.GetComponent<QuestListRowView>(); // 실제 행 View
            Check(popup != null && row != null, "Popup 또는 Row Component가 없습니다.");
            SerializedObject popupSerialized = new(popup); // Popup Inspector 참조 검사 객체
            ScrollRect scroll = GetObject<ScrollRect>(popupSerialized, "scrollRect"); // 기존 Scroll View
            Transform content = GetObject<Transform>(popupSerialized, "rowsRoot"); // 기존 Scroll Content
            Check(scroll != null && content != null && scroll.content == content && scroll.vertical && !scroll.horizontal, "Scroll View/Content 연결 또는 방향이 잘못됐습니다.");
            Check(content.childCount == 0 && GetObject<QuestListRowView>(popupSerialized, "rowPrefab") != null, "수동 샘플 행이 남았거나 Row Prefab 연결이 없습니다.");
            Check(QuestListPopup.QuestBatchSize == 50, "최대 재사용 행 제한이 50이 아닙니다.");

            SerializedObject rowSerialized = new(row); // Row Inspector 참조 검사 객체
            TMP_Text numberText = GetObject<TMP_Text>(rowSerialized, "questNumberText"); // 번호 Text
            TMP_Text titleText = GetObject<TMP_Text>(rowSerialized, "titleText"); // 제목 Text
            TMP_Text descriptionText = GetObject<TMP_Text>(rowSerialized, "descriptionText"); // 설명 Text
            Image rewardIcon = GetObject<Image>(rowSerialized, "rewardIconImage"); // 실제 보상 Image
            Sprite currencyIcon = GetObject<Sprite>(rowSerialized, "rewardCurrencyIconImage"); // 일반 재화 Sprite
            Sprite currencyUnlockIcon = GetObject<Sprite>(rowSerialized, "rewardUpgradeCurrencyIconImage"); // 재화 강화 해금 Sprite
            Sprite labUnlockIcon = GetObject<Sprite>(rowSerialized, "unlockLabIconImage"); // 생산 강화 해금 Sprite
            GameObject normal = GetObject<GameObject>(rowSerialized, "normalObject"); // 일반 상태
            GameObject focus = GetObject<GameObject>(rowSerialized, "focusObject"); // 현재 상태
            GameObject disabled = GetObject<GameObject>(rowSerialized, "disabledObject"); // 수령 완료 상태
            GameObject lockObject = GetObject<GameObject>(rowSerialized, "lockObject"); // 잠금 상태
            GameObject checkObject = GetObject<GameObject>(rowSerialized, "checkObject"); // 완료 체크
            Button rowButton = GetObject<Button>(rowSerialized, "rowButton"); // 행 전체 Button
            SerializedProperty graphicsProperty = rowSerialized.FindProperty("borderAndLineGraphics"); // 완료 색 대상
            Check(numberText != null && titleText != null && descriptionText != null && rewardIcon != null && currencyIcon != null && currencyUnlockIcon != null && labUnlockIcon != null && normal != null && focus != null && disabled != null && lockObject != null && checkObject != null && rowButton != null && graphicsProperty.arraySize > 0, "Row 필수 Inspector 참조 또는 Reward Sprite가 누락됐습니다.");

            int clickedIndex = -1; // 행 클릭 Callback으로 전달된 인덱스
            Call(row, "Awake");
            row.Bind(new QuestListRowData(50, 51, "스탯 가챠", "스탯 가챠 10회 진행", QuestStatus.Claimed, false, false, QuestRewardType.Currency), index => clickedIndex = index);
            Check(numberText.text == "51" && titleText.text == "스탯 가챠" && descriptionText.text == "스탯 가챠 10회 진행", "QuestNumber/Title/Descript 바인딩이 잘못됐습니다.");
            Check(rewardIcon.enabled && rewardIcon.sprite == currencyIcon, "일반 재화 보상 Sprite가 표시되지 않습니다.");
            Check(normal.activeSelf && !focus.activeSelf && disabled.activeSelf && checkObject.activeSelf && !lockObject.activeSelf, "Claimed 상태의 Normal/Disabled/Check 표시가 잘못됐습니다.");
            Color completedColor = rowSerialized.FindProperty("completedColor").colorValue; // 프리팹에서 읽은 기존 완료 색
            Graphic completedLine = (Graphic)graphicsProperty.GetArrayElementAtIndex(0).objectReferenceValue; // 완료 색이 적용될 기존 선
            Check(completedLine.color == completedColor, "Claimed Border/Line 완료 색이 적용되지 않았습니다.");
            rowButton.onClick.Invoke();
            Check(clickedIndex == 50, "행 전체 Button이 선택 인덱스를 전달하지 않습니다.");

            row.Bind(new QuestListRowData(63, 64, "적 처치", "적 30명 처치", QuestStatus.Claimable, true, false, QuestRewardType.Currency, QuestUnlockType.CurrencyUpgrade), null);
            Check(!normal.activeSelf && focus.activeSelf && !disabled.activeSelf && !checkObject.activeSelf && !lockObject.activeSelf, "Claimable 현재 Quest가 Focus를 유지하지 않습니다.");
            Check(rewardIcon.sprite == currencyUnlockIcon, "재화 강화 해금 보상 Sprite가 표시되지 않습니다.");
            row.Bind(new QuestListRowData(64, 65, "미래", "미래", QuestStatus.InProgress, false, true, QuestRewardType.Currency, QuestUnlockType.ProductionUpgrade), null);
            Check(normal.activeSelf && !focus.activeSelf && lockObject.activeSelf, "미래 Quest의 Normal/Icon_Lock 제어가 잘못됐습니다.");
            Check(rewardIcon.sprite == labUnlockIcon, "생산 강화 또는 연구소 해금 보상 Sprite가 표시되지 않습니다.");

            QuestSettings settings = Resources.Load<QuestSettings>("QuestSettings_Default"); // 실제 기본 Quest 정의
            Check(settings != null, "QuestSettings_Default를 불러오지 못했습니다.");
            Check(settings.TryValidate(out string error), "QuestSettings 제목 또는 정의 검증 실패: " + error);
            Check(!string.IsNullOrWhiteSpace(settings.GetQuest(0).title) && !string.IsNullOrWhiteSpace(settings.GetQuest(999).title), "수동 또는 무한 Quest Title 생성이 누락됐습니다.");

            managerObject = new GameObject("QuestListUISmoke_QuestManager");
            managerObject.SetActive(false);
            QuestManager quests = managerObject.AddComponent<QuestManager>(); // Awake와 SaveManager를 실행하지 않는 데이터 원본
            SetPrivate(quests, "settings", settings);
            SetPrivate(quests, "_state", new QuestState { version = 2, currentQuestIndex = 63 });
            SetPrivate(popup, "_quests", quests);
            popup.ShowCurrentQuestRange();
            List<QuestListRowView> rows = GetPrivate<List<QuestListRowView>>(popup, "_rows"); // 동적으로 생성된 최대 50개 행
            Check(popup.RangeStartIndex == 50 && popup.VisibleRowCount == 50 && rows.Count == 50, "Quest 64가 포함된 50개 구간 생성이 잘못됐습니다.");
            Check(GetPrivate<TMP_Text>(rows[0], "questNumberText").text == "100" && GetPrivate<GameObject>(rows[0], "lockObject").activeSelf, "위쪽 미래 Quest 정렬 또는 잠금 표시가 잘못됐습니다.");
            Check(GetPrivate<TMP_Text>(rows[36], "questNumberText").text == "64" && GetPrivate<GameObject>(rows[36], "focusObject").activeSelf, "현재 Quest 위치 또는 Focus 표시가 잘못됐습니다.");
            Check(GetPrivate<TMP_Text>(rows[37], "questNumberText").text == "63" && GetPrivate<GameObject>(rows[37], "disabledObject").activeSelf, "현재 Quest 아래 과거 완료 정렬이 잘못됐습니다.");
            popup.ShowHigherQuestRange();
            Check(popup.RangeStartIndex == 100 && GetPrivate<TMP_Text>(rows[49], "questNumberText").text == "101" && GetPrivate<GameObject>(rows[49], "lockObject").activeSelf, "위쪽 끝에서 다음 미래 50개 구간을 불러오지 못했습니다.");
            popup.ShowLowerQuestRange();
            Check(popup.RangeStartIndex == 50 && GetPrivate<TMP_Text>(rows[0], "questNumberText").text == "100", "아래쪽 끝에서 이전 구간으로 복귀하지 못했습니다.");
            Debug.Log("[QuestListUISmokeTest] PASS: Scroll/Content, 역순 미래 50행 이동·재사용, 모든 Row 참조, 보상 종류별 Sprite, Claimed/Claimable/미래 Locked 상태, Border 색, 클릭 Callback, 수동·무한 Title을 검증했습니다.");
        }
        finally
        {
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            PrefabUtility.UnloadPrefabContents(popupRoot);
            PrefabUtility.UnloadPrefabContents(rowRoot);
        }
    }

    // SerializedField의 오브젝트 참조를 타입에 맞게 읽습니다.
    private static T GetObject<T>(SerializedObject serialized, string propertyName) where T : UnityEngine.Object
    {
        return serialized.FindProperty(propertyName).objectReferenceValue as T;
    }

    // EditMode에서 행 Button의 Awake 연결만 직접 실행합니다.
    private static void Call(object target, string methodName)
    {
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    // 테스트 대상의 private 상태나 Inspector 참조를 읽습니다.
    private static T GetPrivate<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    // 저장 초기화 없이 테스트에 필요한 private 원본만 설정합니다.
    private static void SetPrivate(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    // 검증 실패 지점을 Console 예외로 남깁니다.
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("[QuestListUISmokeTest] " + message); }
}
#endif
