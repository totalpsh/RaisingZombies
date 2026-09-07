#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 기존 Editor 스모크 패턴으로 사용자 저장과 분리한 실제 퀘스트 연결을 검증합니다.
public static class QuestSmokeTestTool
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic; // 테스트에서만 접근할 원본 필드 범위

    // 임시 저장과 미리보기 씬에서 신규 진행, 복원, 보상, 해금, UI를 검사합니다.
    [MenuItem("Tools/Raising Zombies/Quest/Run Quest Smoke Test")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "플레이 종료 후 실행하세요.");
        string directory = Path.Combine(Path.GetTempPath(), "RaisingZombiesQuestSmoke_" + Guid.NewGuid().ToString("N")); // 이번 검증 전용 저장 위치
        SaveFileService files = new(directory); // 실제 사용자 파일과 격리된 저장 서비스
        Scene preview = EditorSceneManager.NewPreviewScene(); // 게임 씬을 변경하지 않는 검증 씬
        object oldSave = SingletonField<SaveManager>().GetValue(null); // 기존 저장 싱글턴
        object oldUpgrade = SingletonField<UpgradeManager>().GetValue(null); // 기존 강화 싱글턴
        object oldQuest = SingletonField<QuestManager>().GetValue(null); // 기존 퀘스트 싱글턴
        object oldUI = SingletonField<UIManager>().GetValue(null); // 기존 UI 싱글턴
        QuestSettings settings = null; // 이미지 변경 검증용 메모리 복제본
        GameObject navigation = null; // 기존 퀘스트 표시 프리팹 미리보기
        GameObject menuRoot = null; // 기존 강화 메뉴 미리보기
        GameObject popupRoot = null; // 새 안내 팝업 미리보기
        UnitData humanData = null; // 사망 이벤트 검사용 인간 데이터
        UnitData zombieData = null; // 비인간 사망 제외 검사용 데이터
        Action<UnitController> deathListener = null; // 테스트 마지막에 반드시 제거할 구독
        try
        {
            SaveManager save = Create<SaveManager>(preview); // 격리된 통합 저장 매니저
            Set(save, "_fileService", files);
            Set(save, "_saveData", GameSaveData.CreateNew());
            SingletonField<SaveManager>().SetValue(null, save);
            UpgradeManager upgrade = Create<UpgradeManager>(preview); // 기존 계산과 저장 API를 그대로 실행할 원본
            SingletonField<UpgradeManager>().SetValue(null, upgrade);
            Set(upgrade, "balanceSettings", AssetDatabase.LoadAssetAtPath<UpgradeBalanceSettings>("Assets/02. Scripts/Upgrade/UpgradeBalanceSettings_Default.asset"));
            Set(upgrade, "currencyUpgradeBalance", AssetDatabase.LoadAssetAtPath<CurrencyUpgradeBalanceSettings>("Assets/02. Scripts/Upgrade/CurrencyUpgradeBalanceSettings_Default.asset"));
            StageManager stage = Create<StageManager>(preview); // 전투 시작 없이 실제 Stage 저장 API를 검증할 원본
            QuestManager quests = Create<QuestManager>(preview); // 자동 부트스트랩을 실행하지 않는 퀘스트 매니저
            SingletonField<QuestManager>().SetValue(null, quests);
            settings = UnityEngine.Object.Instantiate(Resources.Load<QuestSettings>("QuestSettings_Default"));
            Check(settings != null && settings.TryValidate(out _), "퀘스트 데이터 검증 실패");
            Set(quests, "settings", settings);
            Set(quests, "_save", save);
            Check(save.RegisterProvider(quests) && save.RegisterProvider(upgrade) && save.RegisterProvider(stage), "기존 Provider 등록 실패");
            Set(quests, "_ready", true);
            Call(quests, "BindUpgrade", upgrade);
            Call(quests, "BindStage", stage);
            save.SaveLoaded += quests.RefreshProgress;
            save.SaveReset += quests.RefreshProgress;
            deathListener = (Action<UnitController>)Delegate.CreateDelegate(typeof(Action<UnitController>), quests, typeof(QuestManager).GetMethod("HandleUnitDied", Private));
            UnitController.AnyDied += deathListener;
            Check(quests.CurrentQuestIndex == 0 && !quests.CurrencyUpgradeUnlocked && !quests.ProductionUpgradeUnlocked, "신규 게임 기본 상태 오류");

            menuRoot = PrefabUtility.LoadPrefabContents("Assets/03. Prefabs/UI/Upgrade/UpgradeMenuController.prefab");
            UpgradeMenuController menu = menuRoot.GetComponent<UpgradeMenuController>(); // 실제 카테고리 버튼과 진입 API
            Call(menu, "OnEnable");
            menu.ShowCurrencyUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.CategorySelection && !Get<Button>(menu, "currencyUpgradeButton").interactable, "재화 잠금 우회");
            menu.ShowProductionUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.CategorySelection && !Get<Button>(menu, "productionUpgradeButton").interactable, "생산 잠금 우회");
            menu.ShowStatUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.StatUpgrade, "초기 스탯 접근 실패");

            navigation = PrefabUtility.LoadPrefabContents("Assets/03. Prefabs/UI/Common/MainNavigationController.prefab");
            QuestController view = navigation.GetComponentInChildren<QuestController>(true); // 사용자가 만든 QuestBox
            Check(view != null, "기존 QuestBox 누락");
            Call(view, "OnEnable");
            Check(Get<TMP_Text>(view, "questLevelText").text == "0 / 3", "신규 UI 진행도 오류");
            Check(Get<TMP_Text>(view, "questInfoText").text == settings.quests[0].description && Get<TMP_Text>(view, "questRewardText").text == "100", "내용/보상 수량 오류");
            Check(Get<Image>(view, "questRewardImage").sprite == settings.quests[0].rewardIcon, "보상 Icon 연결 오류");
            Check(view.GetComponentInChildren<Graphic>(true) != null, "QuestBox 클릭 Graphic 누락");

            Check(upgrade.TryDrawOne(out _), "단일 가챠 실패");
            Check(upgrade.TotalDrawCount == 1 && Get<TMP_Text>(view, "questLevelText").text == "1 / 3", "실제 1회 반영 실패");
            int afterFirst = upgrade.Currency; // 첫 뽑기 후 보상 전 재화
            int nextCost = upgrade.GetDrawCostForCount(2); // 남은 두 번의 실제 비용
            Check(upgrade.TryDrawOne(out _) && upgrade.TryDrawOne(out _), "Q1 목표 뽑기 실패");
            Check(quests.CurrentQuestIndex == 1 && upgrade.Currency == afterFirst - nextCost + 100, "Q1 이동/보상 오류");
            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 2 });
            Check(quests.CurrentQuestIndex == 2, "StageChanged로 클리어 반영 실패");

            humanData = ScriptableObject.CreateInstance<UnitData>();
            zombieData = ScriptableObject.CreateInstance<UnitData>();
            SetTeam(humanData, UnitTeam.Human);
            SetTeam(zombieData, UnitTeam.Zombie);
            UnitController human = Create<UnitController>(preview); // 실제 Die 함수의 중복 방지 검사 대상
            UnitAnimation animation = human.gameObject.AddComponent<UnitAnimation>(); // 풀 반환 콜백은 테스트에서 차단할 애니메이션
            Set(animation, "_isDead", true);
            Set(human, "anim", animation);
            Set(human, "data", humanData);
            Call(human, "OnDisable");
            Call(human, "Die");
            Check(quests.EnemyKillCount == 0, "Despawn이 Kill로 계산됨");
            for (int i = 0; i < 3; i++) // 서로 다른 생명 주기의 실제 사망 세 건
            {
                Set(human, "_isInitialized", true);
                Call(human, "Die");
                Call(human, "Die");
            }
            Check(quests.EnemyKillCount == 3 && quests.CurrentQuestIndex == 3, "실제 사망/중복 방지 오류");
            Set(human, "data", zombieData);
            Set(human, "_isInitialized", true);
            Call(human, "Die");
            Check(quests.EnemyKillCount == 3, "좀비 사망을 적 처치로 집계함");
            Set(human, "data", humanData);
            Check(upgrade.TryDrawFive(out IReadOnlyList<GachaDrawResult> batch), "현재 연속 뽑기 실패");
            Check(upgrade.TotalDrawCount == 3 + batch.Count && batch.Count == 5, "버튼 횟수와 실제 뽑기 수 혼동");
            Check(quests.CurrentQuestIndex == 4 && quests.CurrencyUpgradeUnlocked && !quests.ProductionUpgradeUnlocked, "Q4 해금 실패");
            Check(Get<Button>(menu, "currencyUpgradeButton").interactable, "즉시 버튼 해금 실패");
            menu.ShowCurrencyUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.CurrencyUpgrade, "해금 후 재화 진입 실패");
            for (int i = 0; i < 3; i++) Check(upgrade.TryUpgradeCurrency(CurrencyUpgradeType.CurrencyPerSecond), "재화 레벨 구매 실패"); // 실제 레벨 목표 달성
            Check(quests.CurrentQuestIndex == 5, "강화 레벨 이벤트 반영 실패");
            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 3 });
            for (int i = 3; i < 10; i++) { Set(human, "_isInitialized", true); Call(human, "Die"); } // 누적 처치 목표
            Check(quests.CurrentQuestIndex == 7, "Q6/Q7 진행 오류");
            Check(upgrade.TryDrawOne(out _) && upgrade.TryDrawOne(out _), "누적 10회 도달 실패");
            Check(upgrade.TotalDrawCount == 10 && quests.CurrentQuestIndex == 8, "실제 누적 10회 반영 실패");
            Check(upgrade.TryUpgradeCurrency(CurrencyUpgradeType.CurrencyPerSecond) && upgrade.TryUpgradeCurrency(CurrencyUpgradeType.CurrencyPerSecond), "Lv.5 도달 실패");
            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 4 });
            Check(quests.CurrentQuest == null && quests.ProductionUpgradeUnlocked, "Q10 완료/해금 실패");
            menu.ShowProductionUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.ProductionUpgrade, "생산 화면 접근 실패");
            int beforeRefresh = upgrade.Currency; // 중복 이벤트 이전 지급 완료 재화
            quests.RefreshProgress(); quests.RefreshProgress();
            Check(upgrade.Currency == beforeRefresh, "같은 완료 보상 중복 지급");
            Check(save.SaveGame() && save.LoadGame(), "통합 파일 저장/복원 실패");
            Check(quests.CurrentQuestIndex == 10 && quests.CurrencyUpgradeUnlocked && quests.ProductionUpgradeUnlocked && quests.EnemyKillCount == 10 && upgrade.TotalDrawCount == 10, "저장된 진행/해금 유실");
            Check(((QuestState)quests.CaptureSaveData()).rewardedQuestIds.Count == 10, "완료 ID 중복 저장");
            quests.ResetSaveData(); // 기존 진행도가 이미 충분한 저장의 연쇄 완료 검사
            Set(quests, "_state", new QuestState { enemyKillCount = 10 });
            quests.RefreshProgress();
            Check(quests.CurrentQuestIndex == 10, "이미 달성한 Stage/Level 조건 인식 실패");
            Check(save.LoadGame() && ((QuestState)quests.CaptureSaveData()).rewardedQuestIds.Count == 10, "로드 중 중간 데이터로 중복 완료됨");
            Check(save.ResetSave() && quests.CurrentQuestIndex == 0 && !quests.CurrencyUpgradeUnlocked && upgrade.TotalDrawCount == 0, "전체 Reset 기본값 오류");
            Check(!Get<Button>(menu, "currencyUpgradeButton").interactable, "Reset 잠금 갱신 실패");

            UIManager ui = Create<UIManager>(preview); // Addressables 대신 기존 팝업 풀을 주입할 UIManager
            SingletonField<UIManager>().SetValue(null, ui);
            Set(ui, "_isInitialized", true);
            GameObject layer = new("QuestSmokePopupLayer", typeof(RectTransform)); // 별도 Canvas 없는 기존 레이어 대역
            SceneManager.MoveGameObjectToScene(layer, preview);
            Get<Dictionary<UILayer, Transform>>(ui, "_layers")[UILayer.PopUp] = layer.transform;
            popupRoot = PrefabUtility.LoadPrefabContents("Assets/03. Prefabs/UI/Common/QuestPopup.prefab");
            SceneManager.MoveGameObjectToScene(layer, popupRoot.scene); // 같은 미리보기 씬 안에서만 팝업 부모를 변경
            QuestPopup popup = popupRoot.GetComponent<QuestPopup>(); // 실제 이미지와 닫기 버튼을 연결한 프리팹
            Get<Dictionary<string, Stack<BaseUI>>>(ui, "_pooledUI")[nameof(QuestPopup)] = new Stack<BaseUI>(new BaseUI[] { popup });
            settings.quests[0].popupImage = settings.quests[0].rewardIcon;
            Call(popup, "OnEnable");
            var firstOpen = QuestPopup.ShowAsync(); // 첫 UIManager 열기
            var secondOpen = QuestPopup.ShowAsync(); // 연속 클릭의 재사용 확인
            Check(firstOpen.IsCompleted && secondOpen.IsCompleted && firstOpen.Result == secondOpen.Result, "팝업 중복 생성");
            ExecuteEvents.Execute(view.gameObject, new PointerEventData(null) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Check(ui.GetUI<QuestPopup>() == popup, "QuestBox 실제 포인터 이벤트 연결 실패");
            Check(Get<Image>(popup, "questImage").sprite == settings.quests[0].popupImage, "퀘스트별 팝업 이미지 오류");
            settings.quests[0].popupImage = null;
            popup.RefreshImage();
            Check(!Get<Image>(popup, "questImage").enabled, "null 팝업 이미지 오류");
            Get<Button>(popup, "closeButton").onClick.Invoke();
            Check(ui.GetUI<QuestPopup>() == null && !popup.gameObject.activeSelf, "닫기/풀 반환 오류");
            popup.transform.SetParent(null); // 미리보기 프리팹 언로드 전 루트 관계 복원
            Call(view, "OnDisable"); Call(view, "OnEnable"); Call(view, "OnEnable");
            Check(Get<TMP_Text>(view, "questLevelText").text == "0 / 3", "UI 재진입 표시 오류");
            int viewListeners = 0; // 중복 생명주기 호출 후 해당 UI의 구독 개수
            foreach (Delegate listener in Get<Delegate>(quests, "Changed").GetInvocationList()) // 연결된 목표 표시 Listener
                if (ReferenceEquals(listener.Target, view)) viewListeners++;
            Check(viewListeners == 1, "Quest UI 중복 이벤트 구독");
            settings.quests[0].rewardIcon = null;
            view.Refresh();
            Check(!Get<Image>(view, "questRewardImage").enabled, "null 보상 Icon 잔류");
            upgrade.RestoreSaveData(JsonUtility.FromJson<UpgradeState>("{\"version\":2,\"currency\":1000,\"gachaLevel\":2,\"drawsAtCurrentLevel\":4}"));
            long legacyDraws = upgrade.BalanceSettings.GetGachaLevel(1).drawsToNextLevel + 4L; // 기존 진행도에서 확실하게 복원할 횟수
            Check(upgrade.TotalDrawCount == legacyDraws, "구버전 누적 뽑기 복원 오류");
            Check(save.SaveGame() && save.LoadGame() && upgrade.TotalDrawCount == legacyDraws, "누적 뽑기 이전 중복 적용");
            Debug.Log("[QuestSmokeTest] PASS: 신규/누적/실제 사망과 Despawn 구분/중복 사망/1회 및 실제 5회 뽑기/누적 10회/Stage·레벨 원본/자동 보상/10개 진행/해금·진입 차단/통합 저장·Load·Reset/UI·Icon·Popup·닫기·재사용.");
        }
        finally
        {
            if (deathListener != null) UnitController.AnyDied -= deathListener;
            if (popupRoot != null) PrefabUtility.UnloadPrefabContents(popupRoot);
            if (navigation != null) PrefabUtility.UnloadPrefabContents(navigation);
            if (menuRoot != null) PrefabUtility.UnloadPrefabContents(menuRoot);
            EditorSceneManager.ClosePreviewScene(preview);
            SingletonField<SaveManager>().SetValue(null, oldSave);
            SingletonField<UpgradeManager>().SetValue(null, oldUpgrade);
            SingletonField<QuestManager>().SetValue(null, oldQuest);
            SingletonField<UIManager>().SetValue(null, oldUI);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            if (humanData != null) UnityEngine.Object.DestroyImmediate(humanData);
            if (zombieData != null) UnityEngine.Object.DestroyImmediate(zombieData);
            files.DeleteAll();
            if (Directory.Exists(directory)) Directory.Delete(directory, false);
        }
    }

    // Awake와 게임 시작 로직을 실행하지 않는 테스트 컴포넌트를 만듭니다.
    private static T Create<T>(Scene scene) where T : Component
    {
        GameObject root = new("QuestSmoke_" + typeof(T).Name); // 격리 검증 오브젝트
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.AddComponent<T>();
    }

    // 테스트 종료 시 복원할 기존 싱글턴 필드입니다.
    private static FieldInfo SingletonField<T>() where T : Singleton<T> { return typeof(Singleton<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic); }

    // 실제 컴포넌트의 private 저장/참조를 테스트에서만 설정합니다.
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }

    // 실제 Inspector 참조나 저장 원본을 검사합니다.
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }

    // 실제 이벤트 처리 경로와 생명주기 함수를 검증합니다.
    private static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, Private).Invoke(target, args); }

    // 원본 UnitData 에셋을 변경하지 않고 팀을 설정합니다.
    private static void SetTeam(UnitData data, UnitTeam team)
    {
        SerializedObject serialized = new(data); // 메모리 데이터 편집 객체
        serialized.FindProperty("team").enumValueIndex = (int)team;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 검증 실패 지점을 콘솔에서 확인 가능한 예외로 남깁니다.
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("[QuestSmokeTest] " + message); }
}
#endif
