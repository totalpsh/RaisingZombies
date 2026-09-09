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
using UnityEngine.UI;

// 사용자 저장과 게임 씬을 건드리지 않고 신체 장비 핵심 흐름을 검증합니다.
public static class BodyEquipmentSmokeTestTool
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic; // 테스트에서만 private 원본에 접근할 범위
    private const string DatabasePath = "Assets/Resources/BodyEquipment/BodyEquipmentDatabase_Default.asset"; // 검증할 실제 Database Asset
    private const string ResearchPath = "Assets/Resources/BodyEquipment/BodyDrawResearchSettings_Default.asset"; // 검증할 실제 연구 Asset
    private const string UpgradeMenuPath = "Assets/03. Prefabs/UI/Upgrade/UpgradeMenuController.prefab"; // 교체된 강화 메뉴 Prefab
    private const string UIFolder = "Assets/03. Prefabs/UI/Upgrade/BodyEquipment"; // 새 장비 UI Prefab 폴더

    // 격리 저장과 미리보기 씬에서 Draw부터 Combat 반영까지 주요 회귀 항목을 검사합니다.
    [MenuItem("Tools/Raising Zombies/Body Equipment/Run Body Equipment Smoke Test")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "플레이 종료 후 실행하세요.");
        string directory = Path.Combine(Path.GetTempPath(), "RaisingZombiesBodyEquipmentSmoke_" + Guid.NewGuid().ToString("N")); // 사용자 저장과 분리된 임시 위치
        SaveFileService files = new(directory); // 격리 JSON 파일 서비스
        Scene preview = EditorSceneManager.NewPreviewScene(); // 실제 게임 씬을 변경하지 않는 검증 씬
        object oldSave = SingletonField<SaveManager>().GetValue(null); // 테스트 후 복원할 Save 싱글턴
        object oldWallet = SingletonField<CurrencyWalletManager>().GetValue(null); // 테스트 후 복원할 Wallet 싱글턴
        object oldEquipment = SingletonField<BodyEquipmentManager>().GetValue(null); // 테스트 후 복원할 장비 싱글턴
        object oldQuest = SingletonField<QuestManager>().GetValue(null); // 테스트 후 복원할 Quest 싱글턴
        UnityEngine.Random.State oldRandomState = UnityEngine.Random.state; // 테스트 후 복원할 전역 Random 상태
        UnitData unitData = null; // 전투 적용 검증용 메모리 UnitData
        QuestSettings questSettings = null; // 원본 Quest Asset을 변경하지 않는 테스트 복제본
        try
        {
            BodyEquipmentDatabaseSO database = AssetDatabase.LoadAssetAtPath<BodyEquipmentDatabaseSO>(DatabasePath); // 실제 기본 Database
            BodyDrawResearchSettingsSO research = AssetDatabase.LoadAssetAtPath<BodyDrawResearchSettingsSO>(ResearchPath); // 실제 기본 연구표
            string databaseError = database == null ? "Asset 누락" : string.Empty; // Database 검증 실패 이유
            string researchError = research == null ? "Asset 누락" : string.Empty; // 연구표 검증 실패 이유
            Check(database != null && database.TryInitialize(out databaseError), "Database 검증 실패: " + databaseError);
            Check(research != null && research.TryValidate(out researchError), "Research Settings 검증 실패: " + researchError);
            Check(database.EquipmentDefinitions.Count == 8 && database.RarityDefinitions.Count == 12 && database.StatDefinitions.Count == 13, "8개 부위, 12개 레어도 또는 13개 스탯 정의 누락");

            SaveManager save = Create<SaveManager>(preview); // 격리된 통합 저장 원본
            Set(save, "_fileService", files);
            Set(save, "_saveData", GameSaveData.CreateNew());
            SingletonField<SaveManager>().SetValue(null, save);

            CurrencyWalletManager wallet = Create<CurrencyWalletManager>(preview); // 타입형 재화 원본
            SingletonField<CurrencyWalletManager>().SetValue(null, wallet);
            Set(wallet, "_save", save);
            Check(save.RegisterProvider(wallet), "Currency Wallet Provider 등록 실패");
            Set(wallet, "_ready", true);

            BodyEquipmentManager manager = Create<BodyEquipmentManager>(preview); // 실제 장비 Draw 원본
            SingletonField<BodyEquipmentManager>().SetValue(null, manager);
            Set(manager, "database", database);
            Set(manager, "researchSettings", research);
            Set(manager, "_wallet", wallet);
            Set(manager, "_save", save);
            Check(save.RegisterProvider(manager), "Body Equipment Provider 등록 실패");
            Set(manager, "_ready", true);

            Check(wallet.GetAmount(GameCurrencyType.BodyDrawTicket) == 10L, "새 저장의 기본 신체 뽑기권 오류");
            Check(wallet.TrySpendCurrency(GameCurrencyType.BodyDrawTicket, 10L) && !manager.CanDraw(1), "뽑기권 부족 차단 실패");
            Check(manager.AddBodyDrawTickets(1L), "공통 신체 뽑기권 1개 지급 API 실패");
            Check(wallet.AddCurrency(GameCurrencyType.PremiumCurrency, 7L) && wallet.GetAmount(GameCurrencyType.BodyDrawTicket) == 1L, "재화 타입 간 값 분리 실패");
            UnityEngine.Random.InitState(20260909);
            Check(manager.TryDraw(1, out IReadOnlyList<BodyDrawResult> one) && one.Count == 1 && wallet.GetAmount(GameCurrencyType.BodyDrawTicket) == 0L, "Ticket 1개 1회 Draw 결과 또는 소비 오류");
            Check(manager.AddBodyDrawTickets(10L) && manager.TryDraw(10, out IReadOnlyList<BodyDrawResult> ten) && ten.Count == 10 &&
                  wallet.GetAmount(GameCurrencyType.BodyDrawTicket) == 0L, "Ticket 10개 10회 Draw 결과 또는 소비 오류");
            Check(manager.TotalBodyDrawCount == 11L && manager.InventoryCount == 11, "Draw Quest 누적 또는 Inventory 저장 오류");
            ValidateInventory(manager);
            ValidateProbabilities(manager, research);
            ValidateForcedRarityRolls(manager, research);
            Check(manager.AddBodyDrawTickets(200L), "장착 교체 검증용 Ticket 지급 실패");

            BodyEquipmentInstance first = null; // 장착 교체 검증의 첫 장비
            BodyEquipmentInstance second = null; // 같은 슬롯의 교체 대상 장비
            while (!TryFindSameSlotPair(manager, out first, out second) && manager.InventoryCount < 101)
                Check(manager.TryDraw(10, out _), "장착 교체 검증용 추가 Draw 실패");
            Check(first != null && second != null, "같은 슬롯 장비 두 개를 준비하지 못했습니다.");
            int equippedStatsChangedCount = 0; // 실제 전투 갱신 전용 이벤트 호출 횟수
            Action equippedStatsChanged = () => equippedStatsChangedCount++; // 장착 변경만 집계할 테스트 Callback
            manager.EquippedStatsChanged += equippedStatsChanged;
            Check(manager.TryEquip(first.uniqueId, out string initialReplaced) && string.IsNullOrEmpty(initialReplaced), "첫 장착 실패");
            Check(!manager.TryEquip(first.uniqueId, out _), "동일 장비 중복 장착을 허용함");
            Check(manager.TryEquip(second.uniqueId, out string replaced) && replaced == first.uniqueId, "같은 슬롯 교체 또는 이전 ID 반환 실패");
            Check(equippedStatsChangedCount == 2, "실제 장착 변경 이벤트 횟수 오류");
            Check(manager.GetEquipment(first.uniqueId) != null && manager.GetEquipped(GetSlot(manager, second))?.uniqueId == second.uniqueId, "교체한 이전 장비 보존 또는 새 장착 실패");
            Check(!manager.TryDismantle(second.uniqueId, out _), "장착 중 장비 파기를 허용함");
            Check(manager.TrySetLocked(second.uniqueId, true), "장비 잠금 실패");
            BodyEquipmentSlot slot = GetSlot(manager, second); // 장착과 해제를 검증할 실제 슬롯
            Check(manager.TryUnequip(slot) && !manager.TryDismantle(second.uniqueId, out _), "잠긴 장비 파기 차단 실패");
            Check(equippedStatsChangedCount == 3, "장착 해제 전투 갱신 이벤트 누락");
            Check(manager.TrySetLocked(second.uniqueId, false), "장비 잠금 해제 실패");
            int pointsBefore = manager.ResearchPoints; // 파기 직전 연구 포인트
            Check(manager.TryDismantle(second.uniqueId, out int granted) && granted > 0 && manager.ResearchPoints == pointsBefore + granted, "파기와 연구 포인트 지급 실패");
            Check(manager.TryEquip(first.uniqueId, out _), "전투 Modifier 검증용 재장착 실패");
            Check(equippedStatsChangedCount == 4, "재장착 전투 갱신 이벤트 누락");
            manager.EquippedStatsChanged -= equippedStatsChanged;
            ValidateModifierAndCombat(manager, research, ref unitData);

            BodyEquipmentState mutableState = Get<BodyEquipmentState>(manager, "_state"); // 연구 시작 조건을 준비할 격리 저장 원본
            BodyDrawResearchLevelDefinition levelOne = research.GetLevel(manager.ResearchLevel); // 현재 연구 단계
            mutableState.researchPoints = Mathf.Max(0, levelOne.requiredResearchPoints - 1);
            Check(!manager.TryStartResearch(), "필요 포인트 충족 전 연구를 시작함");
            mutableState.researchPoints = levelOne.requiredResearchPoints;
            float tierOneBefore = manager.GetRarityProbability(manager.ResearchLevel, 1); // 연구 전 일반 확률
            Check(manager.TryStartResearch() && manager.IsResearching && manager.ResearchPoints == 0, "연구 시작 또는 포인트 소비 실패");
            Check(!manager.TryStartResearch(), "동시 연구를 두 번 시작함");
            long researchEnd = mutableState.researchEndUtc; // 진행 중 연구의 저장할 UTC 종료 시각
            Check(save.SaveGame(), "진행 중 연구 저장 실패");
            manager.ResetSaveData();
            Check(save.LoadGame(), "진행 중 연구 로드 실패");
            mutableState = Get<BodyEquipmentState>(manager, "_state");
            Check(manager.IsResearching && mutableState.researchEndUtc == researchEnd, "진행 중 연구 상태 또는 UTC 복원 실패");
            mutableState.researchEndUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1L;
            Check(manager.RefreshResearchCompletion() && manager.ResearchLevel == 2 && !manager.IsResearching, "UTC 연구 자동 완료 실패");
            Check(!Mathf.Approximately(tierOneBefore, manager.GetRarityProbability(manager.ResearchLevel, 1)), "연구 완료 후 확률표가 변경되지 않음");
            while (manager.InventoryCount < 120) Check(manager.TryDraw(10, out _), "수백 개 Inventory UI 검증용 Draw 실패");
            ValidateInventory(manager);

            int savedCount = manager.InventoryCount; // 통합 저장 후 복원할 장비 수
            int savedLevel = manager.ResearchLevel; // 통합 저장 후 복원할 연구 레벨
            string savedEquippedId = manager.GetEquipped(GetSlot(manager, first))?.uniqueId; // 통합 저장 후 복원할 장착 ID
            long savedTickets = wallet.GetAmount(GameCurrencyType.BodyDrawTicket); // 통합 저장 후 복원할 뽑기권
            Check(save.SaveGame(), "통합 저장 실패");
            manager.ResetSaveData();
            wallet.ResetSaveData();
            Check(save.LoadGame(), "통합 저장 로드 실패");
            Check(manager.InventoryCount == savedCount && manager.ResearchLevel == savedLevel && manager.GetEquipment(savedEquippedId) != null &&
                  wallet.GetAmount(GameCurrencyType.BodyDrawTicket) == savedTickets, "Inventory, 장착, 연구 또는 Ticket 저장 복원 실패");

            ValidateQuestConnection(save, wallet, manager, preview, ref questSettings);
            ValidateUIPrefabs(manager, preview);
            ValidateLegacyQuestMigration();
            Debug.Log("[BodyEquipmentSmokeTest] PASS: 기본 데이터/12단계 Weight/1회·10회 Ticket Draw/고유 ID·Roll·Sub 중복 방지/Inventory/장착·교체/잠금·파기/연구 UTC 완료·확률 변경/실제 UnitStats·방어 계산/통합 저장 복원/50개 UI 카드 제한/Prefab 참조/기존 Quest 이전을 검증했습니다.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            SingletonField<SaveManager>().SetValue(null, oldSave);
            SingletonField<CurrencyWalletManager>().SetValue(null, oldWallet);
            SingletonField<BodyEquipmentManager>().SetValue(null, oldEquipment);
            SingletonField<QuestManager>().SetValue(null, oldQuest);
            UnityEngine.Random.state = oldRandomState;
            if (unitData != null) UnityEngine.Object.DestroyImmediate(unitData);
            if (questSettings != null) UnityEngine.Object.DestroyImmediate(questSettings);
            files.DeleteAll();
            if (Directory.Exists(directory)) Directory.Delete(directory, false);
        }
    }

    // Body Draw 누적 조건, Ticket 보상과 Dungeon Hook이 기존 Quest 원본에서 동작하는지 검사합니다.
    private static void ValidateQuestConnection(SaveManager save, CurrencyWalletManager wallet, BodyEquipmentManager manager, Scene preview, ref QuestSettings settingsCopy)
    {
        QuestSettings source = AssetDatabase.LoadAssetAtPath<QuestSettings>("Assets/Resources/QuestSettings_Default.asset"); // 복제할 실제 Quest Settings
        Check(source != null && source.quests != null && source.quests.Length > 0, "Quest Settings 또는 첫 Quest 누락");
        settingsCopy = UnityEngine.Object.Instantiate(source);
        settingsCopy.quests[0].type = QuestType.BodyDrawCount;
        settingsCopy.quests[0].target = 1;
        settingsCopy.quests[0].rewardType = QuestRewardType.BodyDrawTicket;
        settingsCopy.quests[0].rewardAmount = 3;
        QuestManager quests = Create<QuestManager>(preview); // 신체 장비 조건을 읽을 기존 Quest Manager
        SingletonField<QuestManager>().SetValue(null, quests);
        Set(quests, "settings", settingsCopy);
        Set(quests, "_save", save);
        Check(save.RegisterProvider(quests), "Quest Provider 등록 실패");
        Set(quests, "_ready", true);
        Call(quests, "BindBodyEquipment", manager);
        Call(quests, "BindWallet", wallet);
        quests.RefreshProgress();
        Check(quests.GetRawProgress(settingsCopy.quests[0]) == manager.TotalBodyDrawCount && quests.CurrentStatus == QuestStatus.Claimable, "Quest가 Body Draw 누적 원본을 읽지 못함");
        long ticketBefore = wallet.GetAmount(GameCurrencyType.BodyDrawTicket); // Quest 수령 직전 Ticket
        Check(quests.TryClaimCurrentQuest() && wallet.GetAmount(GameCurrencyType.BodyDrawTicket) == ticketBefore + 3L, "Quest Body Draw Ticket 보상 지급 실패");
        quests.NotifyDungeonCleared();
        QuestState state = (QuestState)quests.CaptureSaveData(); // Dungeon Hook이 기록된 Quest 저장 상태
        Check(state.dungeonClearCount == 1L, "Dungeon Clear Quest Hook 기록 실패");
    }

    // 모든 장비의 ID, Definition, Rarity, Roll 범위와 보조 스탯 중복을 검사합니다.
    private static void ValidateInventory(BodyEquipmentManager manager)
    {
        HashSet<string> ids = new(StringComparer.Ordinal); // 중복 Instance ID 검사용 집합
        foreach (BodyEquipmentInstance equipment in manager.Inventory)
        {
            Check(equipment != null && ids.Add(equipment.uniqueId), "장비 Instance ID 누락 또는 중복");
            Check(manager.Database.TryGetEquipment(equipment.definitionId, out BodyEquipmentDefinitionSO definition), "없는 Equipment Definition ID");
            Check(manager.Database.TryGetRarity(equipment.rarityTier, out BodyRarityDefinitionSO rarity), "없는 Rarity Tier 참조");
            Check(manager.Database.TryGetStat(equipment.mainStat.statType, out EquipmentStatDefinitionSO mainDefinition), "없는 Main Stat 참조");
            float minimum = rarity.MainRollMin * mainDefinition.MainStatScale; // 허용되는 주 스탯 최소값
            float maximum = rarity.MainRollMax * mainDefinition.MainStatScale; // 허용되는 주 스탯 최대값
            Check(equipment.mainStat.value >= minimum - 0.001f && equipment.mainStat.value <= maximum + 0.001f, "Main Stat Roll 범위 이탈");
            Check(ContainsStat(definition.PossibleMainStats, equipment.mainStat.statType), "슬롯 Main Stat Pool 이탈");
            Check(equipment.subStats.Count >= Mathf.Min(rarity.SubStatMinCount, definition.PossibleSubStats.Count) &&
                  equipment.subStats.Count <= Mathf.Min(rarity.SubStatMaxCount, definition.PossibleSubStats.Count), "Rarity Sub Stat 개수 이탈");
            HashSet<EquipmentStatType> subTypes = new() { equipment.mainStat.statType }; // Main과 Sub를 포함한 중복 검사 집합
            foreach (EquipmentStatRoll roll in equipment.subStats)
            {
                Check(roll != null && subTypes.Add(roll.statType), "동일 Sub Stat 또는 Main Stat 중복");
                Check(ContainsStat(definition.PossibleSubStats, roll.statType), "슬롯 Sub Stat Pool 이탈");
                Check(manager.Database.TryGetStat(roll.statType, out EquipmentStatDefinitionSO subDefinition), "없는 Sub Stat 참조");
                float subMinimum = subDefinition.SubStatMin * rarity.SubStatMultiplier; // 허용되는 보조 스탯 최소값
                float subMaximum = subDefinition.SubStatMax * rarity.SubStatMultiplier; // 허용되는 보조 스탯 최대값
                Check(roll.value >= subMinimum - 0.001f && roll.value <= subMaximum + 0.001f, "Sub Stat Roll 범위 이탈");
            }
        }
    }

    // 현재 연구의 12개 확률이 Draw Weight와 같고 합계가 100%인지 검사합니다.
    private static void ValidateProbabilities(BodyEquipmentManager manager, BodyDrawResearchSettingsSO settings)
    {
        List<BodyRarityProbability> probabilities = manager.GetCurrentRarityProbabilities(); // UI가 실제로 사용하는 확률 결과
        Check(probabilities.Count == 12, "확률 UI용 12개 결과 누락");
        float sum = 0f; // 정규화 확률 합계
        int previousDismantlePoint = -1; // 이전 Tier의 파기 포인트
        for (int index = 0; index < probabilities.Count; index++)
        {
            BodyRarityProbability item = probabilities[index]; // 현재 Tier의 표시 확률
            Check(item.Tier == index + 1 && item.Probability >= 0f && Mathf.Approximately(item.Probability, settings.GetProbability(manager.ResearchLevel, item.Tier)), "확률 UI와 실제 Weight 불일치");
            Check(manager.Database.TryGetRarity(item.Tier, out BodyRarityDefinitionSO rarity) && rarity.DismantleResearchPoint > previousDismantlePoint, "높은 Rarity의 파기 포인트가 증가하지 않음");
            previousDismantlePoint = rarity.DismantleResearchPoint;
            sum += item.Probability;
        }
        Check(Mathf.Abs(sum - 1f) < 0.0001f, "정규화 확률 합계가 100%가 아님");
    }

    // 각 Tier 하나만 양수 Weight로 만든 복제 설정에서 실제 Rarity Roll 12종을 모두 검사합니다.
    private static void ValidateForcedRarityRolls(BodyEquipmentManager manager, BodyDrawResearchSettingsSO settings)
    {
        BodyDrawResearchSettingsSO copy = UnityEngine.Object.Instantiate(settings); // 원본 확률표를 바꾸지 않을 테스트 복제본
        MethodInfo rollMethod = typeof(BodyEquipmentManager).GetMethod("RollRarity", Private); // 실제 Draw가 호출하는 Rarity 결정 함수
        Check(copy != null && rollMethod != null, "Rarity Roll 검증 준비 실패");
        try
        {
            Set(manager, "researchSettings", copy);
            BodyDrawResearchLevelDefinition level = copy.GetLevel(manager.ResearchLevel); // 현재 연구 단계의 복제 Weight 표
            Check(level != null && level.rarityWeights != null && level.rarityWeights.Length == 12, "Rarity Roll 검증 Weight 누락");
            for (int targetTier = 1; targetTier <= 12; targetTier++)
            {
                foreach (BodyRarityWeight weight in level.rarityWeights) weight.weight = weight.rarityTier == targetTier ? 1f : 0f;
                BodyRarityDefinitionSO rolled = (BodyRarityDefinitionSO)rollMethod.Invoke(manager, null); // 양수 Weight 하나에서 결정된 실제 Rarity
                Check(rolled != null && rolled.Tier == targetTier, $"Rarity Tier {targetTier} 실제 Roll 실패");
            }
        }
        finally
        {
            Set(manager, "researchSettings", settings);
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    // 같은 슬롯에 속한 서로 다른 두 장비를 찾습니다.
    private static bool TryFindSameSlotPair(BodyEquipmentManager manager, out BodyEquipmentInstance first, out BodyEquipmentInstance second)
    {
        Dictionary<BodyEquipmentSlot, BodyEquipmentInstance> firstBySlot = new(); // 슬롯별 처음 발견한 장비
        foreach (BodyEquipmentInstance equipment in manager.Inventory)
        {
            BodyEquipmentSlot slot = GetSlot(manager, equipment); // 현재 장비의 실제 슬롯
            if (firstBySlot.TryGetValue(slot, out first)) { second = equipment; return true; }
            firstBySlot[slot] = equipment;
        }
        first = null;
        second = null;
        return false;
    }

    // 장착 Modifier가 UnitStats와 피해 방어 공식에 실제 반영되는지 검사합니다.
    private static void ValidateModifierAndCombat(BodyEquipmentManager manager, BodyDrawResearchSettingsSO research, ref UnitData unitData)
    {
        EquipmentModifierSnapshot modifiers = manager.CurrentModifiers; // 현재 장착 장비 합산 결과
        Check(SumModifiers(modifiers) > 0f, "장착 후 Equipment Modifier가 비어 있음");
        unitData = ScriptableObject.CreateInstance<UnitData>();
        SerializedObject serialized = new(unitData); // 전투 기준값을 설정할 메모리 UnitData
        serialized.FindProperty("maxHealth").floatValue = 1000f;
        serialized.FindProperty("healthRegen").floatValue = 1f;
        serialized.FindProperty("attackPower").floatValue = 100f;
        serialized.FindProperty("attackInterval").floatValue = 2f;
        serialized.FindProperty("attackRange").floatValue = 1f;
        serialized.FindProperty("moveSpeed").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnitStats equippedStats = UnitStats.CreateZombie(unitData, manager); // 실제 ZombieSpawner가 사용하는 생성 경로
        Check(equippedStats != null && (equippedStats.MaxHealth > 1000f || equippedStats.AttackPower > 100f || equippedStats.Defense > 0f ||
              equippedStats.AttackInterval < 2f || equippedStats.MoveSpeed > 1f || equippedStats.CriticalChance > 0f || equippedStats.HealthRegen > 1f), "장비가 실제 좀비 UnitStats에 반영되지 않음");
        CombatPowerBalanceSettings powerBalance = AssetDatabase.LoadAssetAtPath<CombatPowerBalanceSettings>("Assets/02. Scripts/Upgrade/CombatPowerBalanceSettings.asset"); // 실제 전투력 밸런스
        CombatPowerSnapshot basePower = CombatPowerCalculator.Calculate(unitData, (BodyEquipmentManager)null, powerBalance); // 장비가 없는 기준 전투력
        CombatPowerSnapshot equippedPower = CombatPowerCalculator.Calculate(unitData, manager, powerBalance); // 현재 장비가 반영된 전투력
        Check(powerBalance != null && equippedPower.CombatPower > basePower.CombatPower, "장착 장비가 전투력 표시에 반영되지 않음");

        float[] values = new float[Enum.GetValues(typeof(EquipmentStatType)).Length]; // 방어 공식 전용 Modifier 값
        values[(int)EquipmentStatType.Defense] = 100f;
        values[(int)EquipmentStatType.DamageReduction] = 20f;
        UnitStats defensiveStats = new(unitData, new EquipmentModifierSnapshot(values), research); // 방어와 피해 감소를 가진 실제 스탯
        UnitModel model = new(defensiveStats); // 실제 피해 적용 모델
        float actualDamage = model.TakeDamage(100f); // 방어식 적용 후 실제 피해
        Check(actualDamage > 0f && actualDamage < 100f && Mathf.Approximately(model.CurrentHealth, defensiveStats.MaxHealth - actualDamage), "방어력 또는 피해 감소 실제 적용 실패");
        float healthRatio = model.CurrentHealth / model.Stats.MaxHealth; // 장착 갱신 전 남은 체력 비율
        model.ResetAttackCooldown();
        UnitStats refreshedStats = new(2000f, 2f, 200f, 1f, 1f, 2f); // 이미 활성인 좀비에 적용할 새 계산 결과
        model.ApplyStats(refreshedStats);
        Check(Mathf.Approximately(model.CurrentHealth, refreshedStats.MaxHealth * healthRatio) && Mathf.Approximately(model.AttackCooldown, refreshedStats.AttackInterval),
              "활성 좀비 스탯 갱신 시 체력 또는 공격 대기 비율 보존 실패");
    }

    // 새 UI Prefab 참조와 Inventory 최대 50개 재사용 제한을 검사합니다.
    private static void ValidateUIPrefabs(BodyEquipmentManager manager, Scene preview)
    {
        string itemPath = $"{UIFolder}/BodyEquipmentItemView.prefab"; // 카드 Prefab 경로
        string inventoryPath = $"{UIFolder}/BodyInventoryPopup.prefab"; // 인벤토리 Popup 경로
        string resultPath = $"{UIFolder}/BodyDrawResultPopup.prefab"; // 결과 Popup 경로
        string probabilityPath = $"{UIFolder}/BodyRarityProbabilityPopup.prefab"; // 확률 Popup 경로
        string researchPath = $"{UIFolder}/BodyResearchPopup.prefab"; // 연구 Popup 경로
        GameObject itemAsset = AssetDatabase.LoadAssetAtPath<GameObject>(itemPath); // 실제 카드 Asset
        GameObject inventoryAsset = AssetDatabase.LoadAssetAtPath<GameObject>(inventoryPath); // 실제 인벤토리 Asset
        GameObject resultAsset = AssetDatabase.LoadAssetAtPath<GameObject>(resultPath); // 실제 결과 Asset
        GameObject probabilityAsset = AssetDatabase.LoadAssetAtPath<GameObject>(probabilityPath); // 실제 확률 Asset
        GameObject researchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(researchPath); // 실제 연구 Asset
        Check(itemAsset != null && inventoryAsset != null && resultAsset != null && probabilityAsset != null && researchAsset != null, "새 UI Prefab 누락");
        BodyDrawResultPopup resultPrefab = resultAsset.GetComponent<BodyDrawResultPopup>(); // 결과 Inspector 참조 검사 대상
        BodyRarityProbabilityPopup probabilityPrefab = probabilityAsset.GetComponent<BodyRarityProbabilityPopup>(); // 확률 Inspector 참조 검사 대상
        BodyResearchPopup researchPrefab = researchAsset.GetComponent<BodyResearchPopup>(); // 연구 Inspector 참조 검사 대상
        Check(itemAsset.GetComponent<BodyEquipmentItemView>() != null && resultPrefab != null && probabilityPrefab != null && researchPrefab != null, "새 UI Controller 누락");
        Check(Get<Transform>(resultPrefab, "itemsRoot") != null && Get<BodyEquipmentItemView>(resultPrefab, "itemPrefab") != null &&
              Get<Button>(resultPrefab, "closeButton") != null && Get<GameObject>(resultPrefab, "confirmationRoot") != null, "Result Popup 필수 Inspector 참조 누락");
        Check(Get<TMP_Text>(probabilityPrefab, "researchLevelText") != null && Get<TMP_Text>(probabilityPrefab, "probabilityText") != null &&
              Get<Button>(probabilityPrefab, "closeButton") != null, "Probability Popup 필수 Inspector 참조 누락");
        Check(Get<TMP_Text>(researchPrefab, "researchLevelText") != null && Get<TMP_Text>(researchPrefab, "researchPointText") != null &&
              Get<TMP_Text>(researchPrefab, "currentProbabilityText") != null && Get<TMP_Text>(researchPrefab, "nextProbabilityText") != null &&
              Get<Button>(researchPrefab, "startButton") != null && Get<Button>(researchPrefab, "closeButton") != null && Get<GameObject>(researchPrefab, "timerRoot") != null,
              "Research Popup 필수 Inspector 참조 누락");
        BodyInventoryPopup inventoryPrefab = inventoryAsset.GetComponent<BodyInventoryPopup>(); // Inspector 참조를 검사할 인벤토리 컴포넌트
        Check(inventoryPrefab != null && Get<Transform>(inventoryPrefab, "itemsRoot") != null && Get<BodyEquipmentItemView>(inventoryPrefab, "itemPrefab") != null &&
              Get<Button>(inventoryPrefab, "closeButton") != null && Get<GameObject>(inventoryPrefab, "confirmationRoot") != null, "Inventory Popup 필수 Inspector 참조 누락");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(inventoryAsset, preview); // 50개 제한을 실제 실행할 미리보기 인스턴스
        BodyInventoryPopup inventory = instance.GetComponent<BodyInventoryPopup>(); // 실행할 인벤토리 Popup
        Call(inventory, "OnEnable");
        Call(inventory, "OnEnable");
        inventory.Init();
        inventory.Refresh();
        List<BodyEquipmentItemView> items = Get<List<BodyEquipmentItemView>>(inventory, "_items"); // 실제 만들어진 재사용 카드
        Check(manager.InventoryCount >= 100 && items.Count == BodyInventoryPopup.PageSize && items.Count <= 50, "수백 개 Inventory에서 한 페이지 50개 제한 실패");
        Check(CountListeners(manager, "StateChanged", inventory) == 1, "Inventory 상태 이벤트 중복 구독");
        Check(CountListeners(CurrencyWalletManager.Instance, "CurrencyChanged", inventory) == 1, "Inventory Ticket 이벤트 중복 구독");
        Call(inventory, "OnDisable");
        UnityEngine.Object.DestroyImmediate(instance);

        GameObject menuAsset = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradeMenuPath); // 실제 강화 메뉴 Asset
        BodyDrawPanel drawPanel = menuAsset == null ? null : menuAsset.GetComponentInChildren<BodyDrawPanel>(true); // 교체된 장비 Draw 화면
        Check(drawPanel != null && Get<Button>(drawPanel, "drawOneButton") != null && Get<Button>(drawPanel, "drawTenButton") != null &&
              Get<Button>(drawPanel, "researchButton") != null && Get<Button>(drawPanel, "probabilityButton") != null && Get<Button>(drawPanel, "inventoryButton") != null,
              "UpgradeMenu 신체 장비 화면 또는 버튼 참조 누락");
        GameObject menuInstance = (GameObject)PrefabUtility.InstantiatePrefab(menuAsset, preview); // 버튼 중복 실행을 검사할 메뉴 인스턴스
        BodyDrawPanel drawInstance = menuInstance.GetComponentInChildren<BodyDrawPanel>(true); // 인스턴스의 새 Draw 화면
        drawInstance.gameObject.SetActive(true);
        Call(drawInstance, "OnEnable");
        Call(drawInstance, "OnEnable");
        long drawCountBefore = manager.TotalBodyDrawCount; // 버튼 한 번 전 누적 Draw 수
        Get<Button>(drawInstance, "drawOneButton").onClick.Invoke();
        Check(manager.TotalBodyDrawCount == drawCountBefore + 1L, "Draw 버튼 Listener가 중복 실행됨");
        Check(CountListeners(manager, "StateChanged", drawInstance) == 1 && CountListeners(CurrencyWalletManager.Instance, "CurrencyChanged", drawInstance) == 1,
              "Draw Panel 이벤트 중복 구독");
        Call(drawInstance, "OnDisable");
        UnityEngine.Object.DestroyImmediate(menuInstance);
        UpgradePanel[] legacyPanels = menuAsset.GetComponentsInChildren<UpgradePanel>(true); // 보존하되 비활성화한 기존 Stat Gacha UI
        for (int index = 0; index < legacyPanels.Length; index++) Check(!legacyPanels[index].gameObject.activeSelf, "기존 Stat Gacha UI가 활성 상태로 남아 있음");
    }

    // 기본 Quest Asset의 기존 스탯 가챠 조건이 신체 장비 Draw로 이전됐는지 검사합니다.
    private static void ValidateLegacyQuestMigration()
    {
        QuestSettings settings = AssetDatabase.LoadAssetAtPath<QuestSettings>("Assets/Resources/QuestSettings_Default.asset"); // 실제 기본 Quest Asset
        Check(settings != null && settings.TryValidate(out _), "기본 Quest Settings 검증 실패");
        foreach (QuestDefinition quest in settings.quests) Check(quest == null || quest.type != QuestType.StatGachaCount, "수동 Quest에 기존 Stat Gacha 조건이 남아 있음");
        foreach (InfiniteQuestRule rule in settings.infiniteRules) Check(rule == null || rule.type != QuestType.StatGachaCount, "반복 Quest에 기존 Stat Gacha 조건이 남아 있음");
    }

    // 장비 원형의 허용 스탯 목록에 지정 종류가 있는지 확인합니다.
    private static bool ContainsStat(IReadOnlyList<EquipmentStatDefinitionSO> values, EquipmentStatType type)
    {
        if (values == null) return false;
        for (int index = 0; index < values.Count; index++) if (values[index] != null && values[index].StatType == type) return true;
        return false;
    }

    // 장비 Definition에서 실제 신체 슬롯을 반환합니다.
    private static BodyEquipmentSlot GetSlot(BodyEquipmentManager manager, BodyEquipmentInstance equipment)
    {
        Check(manager.Database.TryGetEquipment(equipment.definitionId, out BodyEquipmentDefinitionSO definition), "장비 슬롯 Definition 누락");
        return definition.Slot;
    }

    // 장착 Snapshot의 모든 지원 값을 합산합니다.
    private static float SumModifiers(EquipmentModifierSnapshot value)
    {
        return value.Attack + value.Health + value.Defense + value.AttackSpeedPercent + value.MoveSpeedPercent + value.LifeStealPercent +
               value.HealthRegen + value.DamagePercent + value.HealthPercent + value.DefensePercent + value.CriticalChancePercent +
               value.CriticalDamagePercent + value.DamageReductionPercent;
    }

    // 특정 객체가 이벤트에 중복 없이 연결된 횟수를 셉니다.
    private static int CountListeners(object source, string eventField, object target)
    {
        Delegate listeners = Get<Delegate>(source, eventField); // 현재 이벤트 Delegate
        if (listeners == null) return 0;
        int count = 0; // 지정 객체의 실제 구독 수
        foreach (Delegate listener in listeners.GetInvocationList()) if (ReferenceEquals(listener.Target, target)) count++;
        return count;
    }

    // Awake를 실행하지 않는 비활성 테스트 Component를 만듭니다.
    private static T Create<T>(Scene scene) where T : Component
    {
        GameObject root = new("BodyEquipmentSmoke_" + typeof(T).Name); // 미리보기 씬의 비활성 테스트 오브젝트
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.AddComponent<T>();
    }

    // 테스트 종료 시 복원할 Singleton 정적 필드를 반환합니다.
    private static FieldInfo SingletonField<T>() where T : Singleton<T> { return typeof(Singleton<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic); }

    // 테스트에서만 private 필드 값을 설정합니다.
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }

    // 테스트에서만 private 필드 값을 읽습니다.
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }

    // 테스트에서만 private 생명주기 함수를 실행합니다.
    private static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, Private).Invoke(target, args); }

    // 검증 실패 지점을 Console 예외로 남깁니다.
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("[BodyEquipmentSmokeTest] " + message); }
}
#endif
