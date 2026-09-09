using System;
using System.Collections.Generic;
using UnityEngine;

// 순차 퀘스트의 표시 및 수령 상태입니다.
public enum QuestStatus { InProgress, Claimable, Claimed }

// 실제 게임 원본으로 판정할 순차 퀘스트 조건입니다.
public enum QuestType
{
    StatGachaCount, // 이전 저장과 Asset 호환을 위해 남긴 폐기 조건
    StageClear,
    EnemyKillCount,
    CurrencyUpgradeLevel,
    StatResearchLevel, // 이전 저장과 Asset 호환을 위해 남긴 폐기 조건
    ProductionUpgradeLevel,
    BodyDrawCount,
    EquipmentEquipCount,
    EquipmentDismantleCount,
    DrawResearchLevel,
    MinimumRarityObtained,
    DungeonClearCount
}

// 현재 게임에 존재하는 재화만 보상으로 사용합니다.
public enum QuestRewardType { None, Currency, BodyDrawTicket }

// 퀘스트 보상 수령 시 접근을 허용할 강화 화면입니다.
public enum QuestUnlockType { None, CurrencyUpgrade, ProductionUpgrade }

// Inspector에서 목표와 보상 및 안내 이미지를 편집할 퀘스트 한 개입니다.
[Serializable]
public sealed class QuestDefinition
{
    public string id; // 순서를 변경해도 유지해야 할 고유 식별자
    public QuestType type; // 실제 원본에서 읽을 조건 종류
    public string title; // Quest List와 Detail에 표시할 짧은 제목
    [TextArea] public string description; // 현재 목표 설명
    [Min(1)] public int target = 1; // 누적 횟수 또는 목표 강화 레벨
    [Min(1)] public int targetStage = 1; // 클리어해야 할 스테이지 번호
    public CurrencyUpgradeType targetUpgrade; // 목표 재화 강화 종류
    public UpgradeStatType targetStat; // 목표 스탯 연구 종류
    [Min(0)] public int productionUpgradeIndex; // 외부 생산 강화 원본에서 읽을 항목 인덱스
    [Range(1, 12)] public int targetRarityTier = 1; // 특정 레어도 이상 획득 조건의 목표 Tier
    public QuestRewardType rewardType = QuestRewardType.Currency; // 기존 재화 지급 여부
    [Min(0)] public int rewardAmount = 100; // 직접 수령할 재화
    public Sprite rewardIcon; // 현재 보상에 표시할 이미지
    public Sprite popupImage; // 클릭 시 표시할 안내 이미지
    public QuestUnlockType unlockReward; // 수령 후 해금할 기능
}

// 11번 이후 같은 조건 종류가 등장할 때 목표가 증가하는 결정적 규칙입니다.
[Serializable]
public sealed class InfiniteQuestRule
{
    public QuestType type; // 순환할 조건 종류
    public string title; // 이 반복 조건이 생성할 Quest 제목
    [Min(1)] public int baseTarget = 5; // 이 규칙이 처음 등장할 때 목표
    [Min(0)] public int targetIncreasePerCycle = 5; // 순환 한 바퀴마다 증가할 목표
    public CurrencyUpgradeType targetUpgrade; // 재화 강화 규칙의 목표 종류
    public UpgradeStatType targetStat; // 연구 강화 규칙의 목표 종류
    [Min(0)] public int productionUpgradeIndex; // 생산 강화 원본의 목표 항목
    [Range(1, 12)] public int targetRarityTier = 1; // 반복 레어도 획득 조건의 목표 Tier
    [TextArea] public string descriptionFormat = "목표 {0} 달성"; // {0}에 계산된 목표를 넣을 문구
    public Sprite popupImage; // 이 조건 종류가 공유할 안내 이미지
}

// 초반 수동 퀘스트와 이후 무한 순환 밸런스를 보관합니다.
[CreateAssetMenu(fileName = "QuestSettings", menuName = "Raising Zombies/Quest Settings")]
public sealed class QuestSettings : ScriptableObject
{
    public QuestDefinition[] quests = Array.Empty<QuestDefinition>(); // 초반 튜토리얼 및 해금 퀘스트
    public InfiniteQuestRule[] infiniteRules = Array.Empty<InfiniteQuestRule>(); // 초반 이후 반복할 결정적 규칙
    [Min(0)] public int infiniteBaseReward = 50; // 첫 반복 퀘스트의 작은 재화 보상
    [Min(0)] public int rewardIncreasePerCycle = 5; // 순환 한 바퀴마다 증가할 보상
    public Sprite infiniteRewardIcon; // 반복 퀘스트가 공유할 재화 이미지

    // 인덱스 하나로 수동 또는 반복 퀘스트 하나만 재구성합니다.
    public QuestDefinition GetQuest(int questIndex)
    {
        if (questIndex < 0) return null;
        if (quests != null && questIndex < quests.Length) return quests[questIndex];
        if (infiniteRules == null || infiniteRules.Length == 0) return null;
        long generatedIndex = (long)questIndex - (quests == null ? 0 : quests.Length); // 반복 구간 안의 0 기준 인덱스
        int ruleIndex = (int)(generatedIndex % infiniteRules.Length); // 현재 순환에서 사용할 규칙
        long cycle = generatedIndex / infiniteRules.Length; // 완료한 반복 순환 횟수
        InfiniteQuestRule rule = infiniteRules[ruleIndex]; // 이 인덱스에서 항상 같은 규칙
        if (rule == null) return null;
        int target = SaturateToInt((long)rule.baseTarget + cycle * rule.targetIncreasePerCycle); // 오버플로 없이 증가한 목표
        return new QuestDefinition
        {
            id = $"infinite_{(long)questIndex + 1}", type = rule.type, title = rule.title, description = FormatDescription(rule.descriptionFormat, target),
            target = target, targetStage = rule.type == QuestType.StageClear ? target : 1,
            targetUpgrade = rule.targetUpgrade, targetStat = rule.targetStat, productionUpgradeIndex = rule.productionUpgradeIndex, targetRarityTier = rule.targetRarityTier,
            rewardType = QuestRewardType.Currency,
            rewardAmount = SaturateToInt((long)infiniteBaseReward + cycle * rewardIncreasePerCycle),
            rewardIcon = infiniteRewardIcon, popupImage = rule.popupImage, unlockReward = QuestUnlockType.None
        };
    }

    // 높은 인덱스의 곱셈 결과를 현재 재화 및 목표 타입 범위로 제한합니다.
    private static int SaturateToInt(long value) { return value <= 0 ? 0 : value >= int.MaxValue ? int.MaxValue : (int)value; }

    // 잘못된 사용자 Format 문자열도 퀘스트 생성을 중단시키지 않습니다.
    private static string FormatDescription(string format, int target)
    {
        if (string.IsNullOrWhiteSpace(format)) return $"목표 {target} 달성";
        try { return string.Format(format, target); }
        catch (FormatException) { return format; }
    }

    // 누락되거나 중복된 ID 및 유효하지 않은 목표를 검사합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (quests == null || quests.Length == 0) { error = "초반 퀘스트 목록이 비어 있습니다."; return false; }
        if (infiniteRules == null || infiniteRules.Length == 0) { error = "11번 이후 반복 규칙이 비어 있습니다."; return false; }
        HashSet<string> ids = new(StringComparer.Ordinal); // 중복 식별자 검사 집합
        foreach (QuestDefinition quest in quests) // 검사할 수동 정의
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.id) || !ids.Add(quest.id) || quest.target < 1 ||
                quest.targetStage < 1 || quest.targetRarityTier < 1 || quest.targetRarityTier > 12 || quest.rewardAmount < 0 || !IsValidEnum(quest))
            { error = "수동 퀘스트 ID 중복/누락, 목표 또는 보상 설정을 확인하세요."; return false; }
            if (string.IsNullOrWhiteSpace(quest.title)) { error = $"수동 퀘스트 '{quest.id}'의 제목이 비어 있습니다."; return false; }
        }
        foreach (InfiniteQuestRule rule in infiniteRules) // 검사할 반복 규칙
        {
            if (rule == null || rule.baseTarget < 1 || rule.targetIncreasePerCycle < 0 || rule.targetRarityTier < 1 || rule.targetRarityTier > 12 || !Enum.IsDefined(typeof(QuestType), rule.type))
            { error = "반복 규칙의 조건 종류와 목표 증가량을 확인하세요."; return false; }
            if (string.IsNullOrWhiteSpace(rule.title)) { error = $"반복 퀘스트 규칙 '{rule.type}'의 제목이 비어 있습니다."; return false; }
        }
        if (infiniteBaseReward < 0 || rewardIncreasePerCycle < 0) { error = "반복 보상은 음수가 될 수 없습니다."; return false; }
        return true;
    }

    // 수동 정의의 Enum 직렬화 값이 유효한지 확인합니다.
    private static bool IsValidEnum(QuestDefinition quest)
    {
        return Enum.IsDefined(typeof(QuestType), quest.type) && Enum.IsDefined(typeof(QuestRewardType), quest.rewardType) &&
            Enum.IsDefined(typeof(QuestUnlockType), quest.unlockReward) && Enum.IsDefined(typeof(CurrencyUpgradeType), quest.targetUpgrade) &&
            Enum.IsDefined(typeof(UpgradeStatType), quest.targetStat);
    }

    // Inspector에서 데이터 오류를 바로 안내합니다.
    private void OnValidate() { if (!TryValidate(out string error)) Debug.LogWarning($"[QuestSettings] {error}", this); }
}
