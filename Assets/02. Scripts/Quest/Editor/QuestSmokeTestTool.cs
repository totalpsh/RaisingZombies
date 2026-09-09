#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 기존 Editor 스모크 패턴으로 수동 수령, 무한 생성, 저장과 50행 재사용을 검증합니다.
public static class QuestSmokeTestTool
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic; // 테스트에서만 접근할 원본 필드 범위
    private const string NavigationPath = "Assets/03. Prefabs/UI/Common/MainNavigationController.prefab"; // 실제 메인 QuestBox 프리팹
    private const string MenuPath = "Assets/03. Prefabs/UI/Upgrade/UpgradeMenuController.prefab"; // 실제 강화 메뉴 프리팹
    private const string DetailPath = "Assets/03. Prefabs/UI/Common/QuestPopup.prefab"; // 실제 상세 팝업 프리팹
    private const string ListPath = "Assets/03. Prefabs/UI/Common/QuestListPopup.prefab"; // 실제 목록 팝업 프리팹
    private const string RowPath = "Assets/03. Prefabs/UI/Common/QuestListRowView.prefab"; // 실제 재사용 행 프리팹

    // 사용자 저장과 분리된 미리보기 씬에서 퀘스트의 주요 요구사항을 검사합니다.
    [MenuItem("Tools/Raising Zombies/Quest/Run Quest Smoke Test")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "플레이 종료 후 실행하세요.");
        string directory = Path.Combine(Path.GetTempPath(), "RaisingZombiesQuestSmoke_" + Guid.NewGuid().ToString("N")); // 이번 검증 전용 저장 위치
        SaveFileService files = new(directory); // 사용자 저장과 격리한 파일 서비스
        Scene preview = EditorSceneManager.NewPreviewScene(); // 게임 씬을 변경하지 않는 검증 씬
        object oldSave = SingletonField<SaveManager>().GetValue(null); // 기존 저장 싱글턴
        object oldUpgrade = SingletonField<UpgradeManager>().GetValue(null); // 기존 강화 싱글턴
        object oldWallet = SingletonField<CurrencyWalletManager>().GetValue(null); // 기존 타입형 지갑 싱글턴
        object oldBodyEquipment = SingletonField<BodyEquipmentManager>().GetValue(null); // 기존 신체 장비 싱글턴
        object oldQuest = SingletonField<QuestManager>().GetValue(null); // 기존 퀘스트 싱글턴
        object oldUI = SingletonField<UIManager>().GetValue(null); // 기존 UI 싱글턴
        QuestSettings settings = null; // 원본 에셋을 바꾸지 않는 밸런스 복제본
        UnitData humanData = null; // 실제 인간 사망 검증용 데이터
        UnitData zombieData = null; // 좀비 사망 제외 검증용 데이터
        Action<UnitController> deathListener = null; // 테스트 전용 전역 사망 구독
        List<GameObject> instances = new(); // 미리보기 씬에 만든 프리팹 인스턴스
        try
        {
            SaveManager save = Create<SaveManager>(preview); // 격리된 통합 저장 매니저
            Set(save, "_fileService", files);
            Set(save, "_saveData", GameSaveData.CreateNew());
            SingletonField<SaveManager>().SetValue(null, save);

            UpgradeManager upgrade = Create<UpgradeManager>(preview); // 실제 뽑기·재화·강화 원본
            SingletonField<UpgradeManager>().SetValue(null, upgrade);
            Set(upgrade, "balanceSettings", AssetDatabase.LoadAssetAtPath<UpgradeBalanceSettings>("Assets/02. Scripts/Upgrade/UpgradeBalanceSettings_Default.asset"));
            Set(upgrade, "currencyUpgradeBalance", AssetDatabase.LoadAssetAtPath<CurrencyUpgradeBalanceSettings>("Assets/02. Scripts/Upgrade/CurrencyUpgradeBalanceSettings_Default.asset"));
            CurrencyWalletManager wallet = Create<CurrencyWalletManager>(preview); // 신체 뽑기권 보상과 소비 원본
            SingletonField<CurrencyWalletManager>().SetValue(null, wallet);
            Set(wallet, "_save", save);
            BodyEquipmentManager bodyEquipment = Create<BodyEquipmentManager>(preview); // Quest가 읽을 실제 신체 장비 진행 원본
            SingletonField<BodyEquipmentManager>().SetValue(null, bodyEquipment);
            Set(bodyEquipment, "database", AssetDatabase.LoadAssetAtPath<BodyEquipmentDatabaseSO>("Assets/Resources/BodyEquipment/BodyEquipmentDatabase_Default.asset"));
            Set(bodyEquipment, "researchSettings", AssetDatabase.LoadAssetAtPath<BodyDrawResearchSettingsSO>("Assets/Resources/BodyEquipment/BodyDrawResearchSettings_Default.asset"));
            Set(bodyEquipment, "_wallet", wallet);
            Set(bodyEquipment, "_save", save);
            StageManager stage = Create<StageManager>(preview); // 실제 Stage 진행 원본
            QuestManager quests = Create<QuestManager>(preview); // 자동 부트스트랩 없이 연결할 퀘스트 매니저
            SingletonField<QuestManager>().SetValue(null, quests);
            settings = UnityEngine.Object.Instantiate(Resources.Load<QuestSettings>("QuestSettings_Default"));
            Check(settings != null && settings.TryValidate(out _), "퀘스트 밸런스 검증 실패");
            Set(quests, "settings", settings);
            Set(quests, "_save", save);
            Check(save.RegisterProvider(upgrade) && save.RegisterProvider(wallet) && save.RegisterProvider(bodyEquipment) && save.RegisterProvider(stage) && save.RegisterProvider(quests), "기존 Save Provider 등록 실패");
            Set(wallet, "_ready", true);
            Set(bodyEquipment, "_ready", true);
            Set(quests, "_ready", true);
            Call(quests, "BindUpgrade", upgrade);
            Call(quests, "BindBodyEquipment", bodyEquipment);
            Call(quests, "BindWallet", wallet);
            Call(quests, "BindStage", stage);
            save.SaveLoaded += quests.RefreshProgress;
            save.SaveReset += quests.RefreshProgress;
            deathListener = (Action<UnitController>)Delegate.CreateDelegate(typeof(Action<UnitController>), quests, typeof(QuestManager).GetMethod("HandleUnitDied", Private));
            UnitController.GlobalDied += deathListener;
            Check(quests.CurrentQuestIndex == 0 && quests.CurrentStatus == QuestStatus.InProgress, "신규 Quest 상태 오류");
            Check(!quests.CurrencyUpgradeUnlocked && !quests.ProductionUpgradeUnlocked, "신규 Unlock 기본값 오류");

            UpgradeMenuController menu = InstantiatePrefab<UpgradeMenuController>(MenuPath, preview, instances); // 실제 카테고리 잠금 표시
            Call(menu, "OnEnable");
            menu.ShowCurrencyUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.CategorySelection && !Get<Button>(menu, "currencyUpgradeButton").interactable, "재화 강화 초기 잠금 우회");
            menu.ShowProductionUpgrade();
            Check(menu.CurrentState == UpgradeMenuState.CategorySelection && !Get<Button>(menu, "productionUpgradeButton").interactable, "생산 강화 초기 잠금 우회");

            QuestController view = InstantiatePrefab<QuestController>(NavigationPath, preview, instances, true); // 사용자가 만든 실제 QuestBox
            Call(view, "OnEnable");
            Check(Get<TMP_Text>(view, "questLevelText").text == "0 / 3", "메인 Quest 초기 진행도 오류");
            Check(Get<TMP_Text>(view, "questRewardText").text == "100", "메인 Quest 보상 표시 오류");

            int beforeDraw = upgrade.Currency; // 조건 달성 전 재화
            Check(bodyEquipment.TryDraw(1, out _) && bodyEquipment.TryDraw(1, out _) && bodyEquipment.TryDraw(1, out _), "Q1 실제 신체 장비 뽑기 실패");
            int afterCondition = upgrade.Currency; // 자동 보상 여부를 판정할 조건 달성 직후 재화
            Check(afterCondition == beforeDraw && quests.CurrentQuestIndex == 0, "신체 뽑기가 기존 재화를 소비했거나 조건 달성만으로 Q1이 이동함");
            Check(quests.CurrentStatus == QuestStatus.Claimable && Get<TMP_Text>(view, "questLevelText").text == "3 / 3", "Q1 Claimable 표시 오류");
            Check(!quests.CurrencyUpgradeUnlocked, "Claim 전 재화 강화가 해금됨");
            view.HandleQuestClick();
            Check(quests.CurrentQuestIndex == 1 && upgrade.Currency == afterCondition + 100, "메인 UI 수동 Claim 오류");
            Check(!quests.TryClaimCurrentQuest() && upgrade.Currency == afterCondition + 100, "Q1 더블 클릭 중복 보상");
            Check(quests.GetQuestStatus(0) == QuestStatus.Claimed, "지나간 Q1 Claimed 판정 오류");

            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 2 });
            CheckClaimableWithoutAdvance(quests, 1, "Q2 Stage");
            ClaimAndCheck(quests, upgrade, 2, 100, "Q2 Stage");

            humanData = ScriptableObject.CreateInstance<UnitData>();
            zombieData = ScriptableObject.CreateInstance<UnitData>();
            SetTeam(humanData, UnitTeam.Human);
            SetTeam(zombieData, UnitTeam.Zombie);
            UnitController unit = Create<UnitController>(preview); // 실제 Die 중복 방지 경로를 실행할 대상
            UnitAnimation animation = unit.gameObject.AddComponent<UnitAnimation>();
            Set(animation, "_isDead", true);
            Set(unit, "anim", animation);
            Set(unit, "data", humanData);
            Call(unit, "OnDisable");
            Call(unit, "Die");
            Check(quests.EnemyKillCount == 0, "Despawn을 Kill로 집계함");
            Kill(unit, 3);
            Check(quests.EnemyKillCount == 3, "인간 실제 사망 누적 오류");
            CheckClaimableWithoutAdvance(quests, 2, "Q3 Kill");
            ClaimAndCheck(quests, upgrade, 3, 100, "Q3 Kill");
            Set(unit, "data", zombieData);
            Kill(unit, 1);
            Check(quests.EnemyKillCount == 3, "좀비 사망을 적 처치로 집계함");
            Set(unit, "data", humanData);

            Check(bodyEquipment.TryDraw(1, out _) && bodyEquipment.TryDraw(1, out _) && bodyEquipment.TryDraw(1, out _), "Q4 누적 6회 신체 장비 뽑기 실패");
            CheckClaimableWithoutAdvance(quests, 3, "Q4 Unlock");
            Check(!quests.CurrencyUpgradeUnlocked && !Get<Button>(menu, "currencyUpgradeButton").interactable, "Q4 Claim 전 재화 강화 해제");
            ClaimAndCheck(quests, upgrade, 4, 100, "Q4 Unlock");
            Check(quests.CurrencyUpgradeUnlocked && Get<Button>(menu, "currencyUpgradeButton").interactable, "Q4 Claim 후 재화 강화 미해금");

            for (int index = 0; index < 3; index++) Check(upgrade.TryUpgradeCurrency(CurrencyUpgradeType.CurrencyPerSecond), "Q5 재화 강화 실패");
            Check(quests.GetRelatedProgressText(quests.CurrentQuest).Contains("현재 효과"), "재화 강화 상세 실제 효과 누락");
            ClaimAndCheck(quests, upgrade, 5, 100, "Q5 Currency Upgrade");
            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 3 });
            ClaimAndCheck(quests, upgrade, 6, 100, "Q6 Stage");
            Kill(unit, 7);
            ClaimAndCheck(quests, upgrade, 7, 100, "Q7 Kill");
            for (int index = 0; index < 4; index++) Check(bodyEquipment.TryDraw(1, out _), "Q8 누적 10회 신체 장비 뽑기 실패");
            Check(bodyEquipment.TotalBodyDrawCount == 10, "실제 신체 장비 뽑기 누적 원본 오류");
            ClaimAndCheck(quests, upgrade, 8, 100, "Q8 Gacha");
            for (int index = 0; index < 2; index++) Check(upgrade.TryUpgradeCurrency(CurrencyUpgradeType.CurrencyPerSecond), "Q9 재화 강화 실패");
            ClaimAndCheck(quests, upgrade, 9, 100, "Q9 Currency Upgrade");
            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 4 });
            CheckClaimableWithoutAdvance(quests, 9, "Q10 Production Unlock");
            Check(!quests.ProductionUpgradeUnlocked && !Get<Button>(menu, "productionUpgradeButton").interactable, "Q10 Claim 전 생산 강화 해제");
            ClaimAndCheck(quests, upgrade, 10, 100, "Q10 Production Unlock");
            Check(quests.ProductionUpgradeUnlocked && quests.CurrentQuest != null, "Q10 Claim 후 생산 해금 또는 Q11 생성 실패");
            Check(Get<Button>(menu, "productionUpgradeButton").interactable, "Q10 Claim 후 생산 버튼 잠금 잔류");

            stage.RestoreSaveData(new StageProgressState { version = 2, currentStageNumber = 6 });
            Check(quests.CurrentStatus == QuestStatus.Claimable, "Q11 반복 Stage 판정 실패");
            int claimableIndex = quests.CurrentQuestIndex; // 저장 후에도 유지돼야 할 수령 대기 인덱스
            int claimableCurrency = upgrade.Currency; // 수령 전 그대로 유지돼야 할 재화
            Check(save.SaveGame(), "Claimable 저장 실패");
            quests.RestoreSaveData(new QuestState());
            Check(save.LoadGame(), "Claimable 저장 로드 실패");
            Check(quests.CurrentQuestIndex == claimableIndex && quests.CurrentStatus == QuestStatus.Claimable && upgrade.Currency == claimableCurrency, "Claimable 저장 복원 오류");
            ClaimAndCheck(quests, upgrade, 11, settings.infiniteBaseReward, "Q11 Infinite Stage");

            BodyEquipmentState bodyState = (BodyEquipmentState)bodyEquipment.CaptureSaveData(); // Q12 실제 누적 신체 뽑기 원본 갱신용 DTO
            bodyState.totalBodyDrawCount = 15;
            bodyEquipment.RestoreSaveData(bodyState);
            ClaimAndCheck(quests, upgrade, 12, settings.infiniteBaseReward, "Q12 Infinite Gacha");
            quests.RestoreSaveData(new QuestState { version = 2, currentQuestIndex = 12, currencyUpgradeUnlocked = true, productionUpgradeUnlocked = true, enemyKillCount = 30 });
            ClaimAndCheck(quests, upgrade, 13, settings.infiniteBaseReward, "Q13 Infinite Kill");
            UpgradeState currencyState = (UpgradeState)upgrade.CaptureSaveData(); // Q14 실제 재화 강화 레벨 원본 갱신용 DTO
            currencyState.currencyPerSecondLevel = 10;
            upgrade.RestoreSaveData(currencyState);
            ClaimAndCheck(quests, upgrade, 14, settings.infiniteBaseReward, "Q14 Infinite Currency");

            QuestDefinition generatedA = settings.GetQuest(999); // 결정성 비교 첫 결과
            QuestDefinition generatedB = settings.GetQuest(999); // 결정성 비교 두 번째 결과
            Check(generatedA != null && generatedB != null && generatedA.type == generatedB.type && generatedA.target == generatedB.target && generatedA.rewardAmount == generatedB.rewardAmount && generatedA.description == generatedB.description, "Quest 1000 결정적 생성 실패");
            Check(settings.GetQuest(49) != null && settings.GetQuest(50) != null && settings.GetQuest(99) != null && settings.GetQuest(100) != null, "Quest 50/51/100/101 생성 실패");
            int oldBaseTarget = settings.infiniteRules[0].baseTarget; // 오버플로 테스트 후 복원할 설정
            int oldTargetGrowth = settings.infiniteRules[0].targetIncreasePerCycle; // 오버플로 테스트 후 복원할 증가량
            int oldReward = settings.infiniteBaseReward; // 오버플로 테스트 후 복원할 보상
            int oldRewardGrowth = settings.rewardIncreasePerCycle; // 오버플로 테스트 후 복원할 보상 증가량
            settings.infiniteRules[0].baseTarget = int.MaxValue;
            settings.infiniteRules[0].targetIncreasePerCycle = int.MaxValue;
            settings.infiniteBaseReward = int.MaxValue;
            settings.rewardIncreasePerCycle = int.MaxValue;
            QuestDefinition overflow = settings.GetQuest(int.MaxValue); // 최대 인덱스의 안전한 목표와 보상
            Check(overflow != null && overflow.target > 0 && overflow.rewardAmount == int.MaxValue, "높은 Quest Index 오버플로 보호 실패");
            settings.infiniteRules[0].baseTarget = oldBaseTarget;
            settings.infiniteRules[0].targetIncreasePerCycle = oldTargetGrowth;
            settings.infiniteBaseReward = oldReward;
            settings.rewardIncreasePerCycle = oldRewardGrowth;

            InfiniteQuestRule sourceRule = settings.infiniteRules[0]; // 실제 생산 원본 연결 API 검증에 잠시 사용할 규칙
            QuestType oldRuleType = sourceRule.type; // 검증 후 복원할 조건 종류
            int oldRuleTarget = sourceRule.baseTarget; // 검증 후 복원할 목표
            sourceRule.type = QuestType.ProductionUpgradeLevel;
            sourceRule.baseTarget = 5;
            FakeProductionSource production = new(5); // 프로젝트 생산 시스템이 구현할 인터페이스 대역
            quests.RestoreSaveData(new QuestState { version = 2, currentQuestIndex = 10, currencyUpgradeUnlocked = true, productionUpgradeUnlocked = true });
            quests.RegisterProductionProgressSource(production);
            Check(quests.CurrentStatus == QuestStatus.Claimable && quests.GetRelatedProgressText(quests.CurrentQuest).Contains("Lv.5"), "생산 강화 원본 연결 API 실패");
            quests.UnregisterProductionProgressSource(production);
            Check(quests.CurrentStatus == QuestStatus.InProgress, "생산 강화 원본 해제 실패");
            sourceRule.type = oldRuleType;
            sourceRule.baseTarget = oldRuleTarget;

            ValidatePrefabReferences();
            UIManager ui = Create<UIManager>(preview); // 기존 Popup 풀을 직접 사용하는 UIManager
            SingletonField<UIManager>().SetValue(null, ui);
            Set(ui, "_isInitialized", true);
            GameObject layer = new("QuestSmokePopupLayer", typeof(RectTransform)); // 기존 PopUp Canvas의 Transform 대역
            layer.SetActive(true);
            SceneManager.MoveGameObjectToScene(layer, preview);
            Get<Dictionary<UILayer, Transform>>(ui, "_layers")[UILayer.PopUp] = layer.transform;
            QuestPopup detail = InstantiatePrefab<QuestPopup>(DetailPath, preview, instances); // 풀에서 꺼낼 상세 인스턴스
            QuestListPopup list = InstantiatePrefab<QuestListPopup>(ListPath, preview, instances); // 풀에서 꺼낼 목록 인스턴스
            detail.gameObject.SetActive(false);
            list.gameObject.SetActive(false);
            Get<Dictionary<string, Stack<BaseUI>>>(ui, "_pooledUI")[nameof(QuestPopup)] = new Stack<BaseUI>(new BaseUI[] { detail });
            Get<Dictionary<string, Stack<BaseUI>>>(ui, "_pooledUI")[nameof(QuestListPopup)] = new Stack<BaseUI>(new BaseUI[] { list });

            quests.RestoreSaveData(new QuestState { version = 2, currentQuestIndex = 999, currencyUpgradeUnlocked = true, productionUpgradeUnlocked = true, enemyKillCount = 100000 });
            Call(list, "OnEnable"); // EditMode에서는 활성 전환 생명주기를 직접 재현
            var firstList = QuestListPopup.ShowAsync(); // 첫 목록 생성 요청
            var secondList = QuestListPopup.ShowAsync(); // 연속 목록 요청
            Check(firstList.IsCompleted && secondList.IsCompleted && firstList.Result == secondList.Result, "Quest List Popup 중복 생성");
            Check(list.RangeStartIndex == 950 && list.CreatedRowCount == 50 && list.VisibleRowCount == 50, $"Quest 1000 현재 범위 또는 50행 제한 오류: Start={list.RangeStartIndex}, Rows={list.CreatedRowCount}");
            List<QuestListRowView> rows = Get<List<QuestListRowView>>(list, "_rows"); // Scroll 범위 이동 전 재사용 대상
            QuestListRowView firstRow = rows[0]; // 같은 인스턴스 유지 확인 대상
            list.ShowHigherQuestRange();
            Check(list.RangeStartIndex == 1000 && list.CreatedRowCount == 50 && ReferenceEquals(firstRow, rows[0]), "미래 Scroll 묶음 이동 행 재사용 실패");
            quests.RestoreSaveData(new QuestState { version = 2, currentQuestIndex = 63, currencyUpgradeUnlocked = true, productionUpgradeUnlocked = true });
            list.ShowCurrentQuestRange();
            Check(list.RangeStartIndex == 50 && list.CreatedRowCount == 50 && CountActive(rows) == 50, "Quest 64 현재·미래 Scroll 묶음 표시 개수 오류");
            Check(Get<TMP_Text>(rows[0], "questNumberText").text == "100" && Get<GameObject>(rows[0], "lockObject").activeSelf, "높은 번호 우선 정렬 또는 미래 Quest 잠금 표시 오류");
            list.ShowLowerQuestRange();
            Check(list.RangeStartIndex == 0 && list.CreatedRowCount == 50 && CountActive(rows) == 50, "과거 Scroll 묶음 이동 오류");
            list.ShowHigherQuestRange();
            Check(list.RangeStartIndex == 50 && ReferenceEquals(firstRow, rows[0]), "미래 Scroll 묶음 복귀 재사용 실패");
            QuestListRowView claimedRow = rows[49]; // Quest 51 수령 완료 행
            QuestListRowView currentRow = rows[36]; // Quest 64 현재 행
            Check(Get<TMP_Text>(claimedRow, "questNumberText").text == "51" && Get<TMP_Text>(claimedRow, "titleText").text == settings.GetQuest(50).title && Get<TMP_Text>(claimedRow, "descriptionText").text == settings.GetQuest(50).description, "재사용 행 QuestNumber/Title/Descript 잔류 또는 연결 오류");
            Check(Get<GameObject>(claimedRow, "normalObject").activeSelf && !Get<GameObject>(claimedRow, "focusObject").activeSelf && Get<GameObject>(claimedRow, "disabledObject").activeSelf && Get<GameObject>(claimedRow, "checkObject").activeSelf && !Get<GameObject>(claimedRow, "lockObject").activeSelf, "Claimed 행 Normal/Disabled/Check 상태 오류");
            Check(!Get<GameObject>(currentRow, "normalObject").activeSelf && Get<GameObject>(currentRow, "focusObject").activeSelf && !Get<GameObject>(currentRow, "disabledObject").activeSelf && !Get<GameObject>(currentRow, "checkObject").activeSelf && !Get<GameObject>(currentRow, "lockObject").activeSelf, "현재 행 Focus 상태 오류");
            currentRow.Bind(new QuestListRowData(63, 64, "Claimable Test", "Claimable Test", QuestStatus.Claimable, true, false), null);
            Check(!Get<GameObject>(currentRow, "normalObject").activeSelf && Get<GameObject>(currentRow, "focusObject").activeSelf && !Get<GameObject>(currentRow, "disabledObject").activeSelf && !Get<GameObject>(currentRow, "checkObject").activeSelf, "Claimable 현재 행 Focus 유지 오류");
            claimedRow.Bind(new QuestListRowData(50, 51, "Claimed Test", "Claimed Test", QuestStatus.Claimed, false, false), null);
            Graphic[] claimedLines = Get<Graphic[]>(claimedRow, "borderAndLineGraphics"); // 완료 색이 적용되는 기존 상하단 선
            Check(claimedLines.Length > 0 && claimedLines[0].color == Get<Color>(claimedRow, "completedColor"), "Claimed Border/Line 완료 색 적용 오류");
            list.ShowCurrentQuestRange();

            Call(detail, "OnEnable"); // EditMode에서는 활성 전환 생명주기를 직접 재현
            var firstDetail = QuestPopup.ShowAsync(0); // Claimed 퀘스트 상세 열기
            var secondDetail = QuestPopup.ShowAsync(0); // 연속 상세 요청
            Check(firstDetail.IsCompleted && secondDetail.IsCompleted && firstDetail.Result == secondDetail.Result, "Quest Detail Popup 중복 생성");
            Check(Get<TMP_Text>(detail, "statusText").text == "수령 완료", "Claimed 상세 상태 표시 오류");
            Check(Get<TMP_Text>(detail, "progressText").text.Length > 0 && !Get<Button>(detail, "claimButton").gameObject.activeSelf, "Claimed 상세 Progress 또는 버튼 오류");
            ui.CloseUI(detail);
            Call(claimedRow, "Awake"); // EditMode에서 동적 Row의 Button 생명주기를 직접 재현
            Get<Button>(rows[49], "rowButton").onClick.Invoke();
            Check(ui.GetUI<QuestPopup>() == detail && Get<TMP_Text>(detail, "questNumberText").text == "Quest 51", "Quest List 행 클릭 상세 Popup 연결 오류");
            ui.CloseUI(detail);
            BodyEquipmentState inProgressState = (BodyEquipmentState)bodyEquipment.CaptureSaveData(); // Q1을 진행 중으로 되돌릴 실제 신체 뽑기 원본
            inProgressState.totalBodyDrawCount = 0;
            bodyEquipment.RestoreSaveData(inProgressState);
            quests.RestoreSaveData(new QuestState { version = 2, currentQuestIndex = 0 });
            ExecuteEvents.Execute(view.gameObject, new PointerEventData(null) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Check(ui.GetUI<QuestListPopup>() == list && ui.GetUI<QuestPopup>() == null, "메인 QuestBox 클릭 목록 Popup 연결 오류");
            ui.CloseUI(list);

            Call(view, "OnDisable");
            Call(view, "OnEnable");
            Call(view, "OnEnable");
            Check(CountListeners(quests, "Changed", view) == 1, "QuestBox 이벤트 중복 구독");
            Call(list, "OnDisable");
            Call(list, "OnEnable");
            Call(list, "OnEnable");
            Check(CountListeners(quests, "Changed", list) == 1, "Quest List 이벤트 중복 구독");
            Call(detail, "OnDisable");
            Call(detail, "OnEnable");
            Call(detail, "OnEnable");
            Check(CountListeners(quests, "Changed", detail) == 1, "Quest Detail 이벤트 중복 구독");

            quests.RestoreSaveData(JsonUtility.FromJson<QuestState>("{\"version\":1,\"currentQuestIndex\":7,\"currencyUpgradeUnlocked\":true,\"enemyKillCount\":22}"));
            QuestState migrated = (QuestState)quests.CaptureSaveData(); // 구버전에서 보존된 최소 상태
            Check(migrated.version == 2 && migrated.currentQuestIndex == 7 && migrated.currencyUpgradeUnlocked && migrated.enemyKillCount == 22, "Quest Save v1 마이그레이션 실패");
            Check(save.SaveGame() && save.LoadGame(), "최종 Quest 통합 저장·로드 실패");
            Debug.Log("[QuestSmokeTest] PASS: 수동 Claim/중복 방지/Claim 전 Index·Unlock 유지/Q1~Q14/Stage·Gacha·Kill·Currency·Production 원본/무한 결정 생성/고인덱스 오버플로/Claimable 저장/상세·목록 Popup 중복 방지/50행 Scroll 묶음 이동·재사용/행 상태·클릭/이벤트 중복 방지를 검증했습니다.");
        }
        finally
        {
            if (deathListener != null) UnitController.GlobalDied -= deathListener;
            EditorSceneManager.ClosePreviewScene(preview);
            SingletonField<SaveManager>().SetValue(null, oldSave);
            SingletonField<UpgradeManager>().SetValue(null, oldUpgrade);
            SingletonField<CurrencyWalletManager>().SetValue(null, oldWallet);
            SingletonField<BodyEquipmentManager>().SetValue(null, oldBodyEquipment);
            SingletonField<QuestManager>().SetValue(null, oldQuest);
            SingletonField<UIManager>().SetValue(null, oldUI);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            if (humanData != null) UnityEngine.Object.DestroyImmediate(humanData);
            if (zombieData != null) UnityEngine.Object.DestroyImmediate(zombieData);
            files.DeleteAll();
            if (Directory.Exists(directory)) Directory.Delete(directory, false);
        }
    }

    // 현재 목표가 이동하지 않은 Claimable 상태인지 확인합니다.
    private static void CheckClaimableWithoutAdvance(QuestManager quests, int expectedIndex, string label)
    {
        Check(quests.CurrentQuestIndex == expectedIndex && quests.CurrentStatus == QuestStatus.Claimable, label + " Claim 전 상태 오류");
    }

    // 중앙 Claim API가 보상을 한 번만 지급하고 지정 인덱스로 이동하는지 확인합니다.
    private static void ClaimAndCheck(QuestManager quests, UpgradeManager upgrade, int expectedIndex, int reward, string label)
    {
        int before = upgrade.Currency; // 이번 Claim 직전 재화
        Check(quests.CurrentStatus == QuestStatus.Claimable, label + " Claimable 아님");
        Check(quests.TryClaimCurrentQuest(), label + " Claim 실패");
        Check(quests.CurrentQuestIndex == expectedIndex && upgrade.Currency == Math.Min(int.MaxValue, (long)before + reward), label + " Index 또는 Reward 오류");
        int after = upgrade.Currency; // 첫 Claim 완료 재화
        Check(!quests.TryClaimCurrentQuest() && upgrade.Currency == after, label + " 중복 Claim 허용");
    }

    // UnitController의 실제 사망 생명주기를 지정 횟수만큼 발생시킵니다.
    private static void Kill(UnitController unit, int count)
    {
        for (int index = 0; index < count; index++)
        {
            Set(unit, "_isInitialized", true);
            Call(unit, "Die");
            Call(unit, "Die");
        }
    }

    // Quest 전용 세 프리팹의 필수 컴포넌트와 Inspector 참조를 검사합니다.
    private static void ValidatePrefabReferences()
    {
        GameObject detailRoot = PrefabUtility.LoadPrefabContents(DetailPath); // 상세 프리팹 내용
        GameObject listRoot = PrefabUtility.LoadPrefabContents(ListPath); // 목록 프리팹 내용
        GameObject rowRoot = PrefabUtility.LoadPrefabContents(RowPath); // 행 프리팹 내용
        try
        {
            QuestPopup detail = detailRoot.GetComponent<QuestPopup>();
            QuestListPopup list = listRoot.GetComponent<QuestListPopup>();
            QuestListRowView row = rowRoot.GetComponent<QuestListRowView>();
            Check(detail != null && Get<TMP_Text>(detail, "questDescriptionText") != null && Get<Button>(detail, "claimButton") != null && Get<Button>(detail, "closeButton") != null, "Quest Detail 프리팹 참조 누락");
            Check(list != null && Get<ScrollRect>(list, "scrollRect") != null && Get<Transform>(list, "rowsRoot") != null && Get<Transform>(list, "rowsRoot").childCount == 0 && Get<QuestListRowView>(list, "rowPrefab") != null, "Quest List Scroll 프리팹 참조 또는 수동 샘플 잔류 오류");
            Check(row != null && Get<Button>(row, "rowButton") != null && Get<TMP_Text>(row, "questNumberText") != null && Get<TMP_Text>(row, "titleText") != null && Get<TMP_Text>(row, "descriptionText") != null && Get<GameObject>(row, "normalObject") != null && Get<GameObject>(row, "focusObject") != null && Get<GameObject>(row, "disabledObject") != null && Get<GameObject>(row, "lockObject") != null && Get<GameObject>(row, "checkObject") != null && Get<Graphic[]>(row, "borderAndLineGraphics").Length > 0, "Quest Row 상태/텍스트/버튼 프리팹 참조 누락");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(detailRoot);
            PrefabUtility.UnloadPrefabContents(listRoot);
            PrefabUtility.UnloadPrefabContents(rowRoot);
        }
    }

    // 페이지에 현재 활성화된 재사용 행 수를 셉니다.
    private static int CountActive(List<QuestListRowView> rows)
    {
        int count = 0; // 활성 행 누적값
        foreach (QuestListRowView row in rows) if (row.gameObject.activeSelf) count++;
        return count;
    }

    // 특정 객체가 이벤트에 중복 없이 한 번만 연결됐는지 셉니다.
    private static int CountListeners(object source, string eventField, object target)
    {
        Delegate listeners = Get<Delegate>(source, eventField); // 현재 이벤트 Delegate
        if (listeners == null) return 0;
        int count = 0; // 지정 객체의 구독 수
        foreach (Delegate listener in listeners.GetInvocationList()) if (ReferenceEquals(listener.Target, target)) count++;
        return count;
    }

    // 실제 프리팹 에셋을 미리보기 씬에 인스턴스화하고 원하는 컴포넌트를 찾습니다.
    private static T InstantiatePrefab<T>(string path, Scene scene, List<GameObject> instances, bool includeChildren = false) where T : Component
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 인스턴스화할 실제 프리팹 에셋
        Check(asset != null, path + " 프리팹 누락");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene); // 원본을 변경하지 않는 미리보기 인스턴스
        instances.Add(instance);
        T component = includeChildren ? instance.GetComponentInChildren<T>(true) : instance.GetComponent<T>();
        Check(component != null, path + " 컴포넌트 누락: " + typeof(T).Name);
        return component;
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

    // 실제 컴포넌트의 private 저장값이나 참조를 테스트에서만 설정합니다.
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }

    // 실제 Inspector 참조나 런타임 목록을 검사합니다.
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }

    // 실제 이벤트 처리 경로와 생명주기 함수를 실행합니다.
    private static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, Private).Invoke(target, args); }

    // 원본 UnitData 에셋을 변경하지 않고 테스트 메모리 데이터의 팀만 설정합니다.
    private static void SetTeam(UnitData data, UnitTeam team)
    {
        SerializedObject serialized = new(data); // 메모리 데이터 편집 객체
        serialized.FindProperty("team").enumValueIndex = (int)team;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 검증 실패 지점을 Console 예외로 남깁니다.
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("[QuestSmokeTest] " + message); }

    // 실제 생산 시스템이 구현할 진행 원본 계약의 테스트 대역입니다.
    private sealed class FakeProductionSource : IProductionUpgradeProgressSource
    {
        private readonly int _level; // 테스트에서 반환할 생산 강화 레벨

        // 지정 레벨을 반환하는 테스트 원본을 만듭니다.
        public FakeProductionSource(int level) { _level = level; }

        // 생산 강화 항목별 실제 레벨 조회 계약을 검증합니다.
        public int GetProductionUpgradeLevel(int upgradeIndex) { return _level; }
    }
}
#endif
