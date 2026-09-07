using System;
using System.Collections.Generic;
using UnityEngine;

// 실제 게임 원본으로 판정할 순차 퀘스트 조건입니다.
public enum QuestType { StatGachaCount, StageClear, EnemyKillCount, CurrencyUpgradeLevel, StatResearchLevel }

// 현재 게임에 존재하는 재화만 보상으로 사용합니다.
public enum QuestRewardType { None, Currency }

// 퀘스트 완료 시 접근을 허용할 강화 화면입니다.
public enum QuestUnlockType { None, CurrencyUpgrade, ProductionUpgrade }

// Inspector에서 목표와 보상 및 안내 이미지를 편집할 퀘스트 한 개입니다.
[Serializable]
public sealed class QuestDefinition
{
    public string id; // 순서를 변경해도 유지해야 할 고유 식별자
    public QuestType type; // 실제 원본에서 읽을 조건 종류
    [TextArea] public string description; // 현재 목표 설명
    [Min(1)] public int target = 1; // 누적 횟수 또는 목표 강화 레벨
    [Min(1)] public int targetStage = 1; // 클리어해야 할 스테이지 번호
    public CurrencyUpgradeType targetUpgrade; // 목표 재화 강화 종류
    public UpgradeStatType targetStat; // 목표 스탯 연구 종류
    public QuestRewardType rewardType = QuestRewardType.Currency; // 기존 재화 지급 여부
    [Min(0)] public int rewardAmount = 100; // 완료 즉시 지급할 재화
    public Sprite rewardIcon; // 현재 보상에 표시할 이미지
    public Sprite popupImage; // 클릭 시 표시할 안내 이미지
    public QuestUnlockType unlockReward; // 완료 후 해금할 기능
}

// 목록 순서대로 하나씩 진행하며 끝에 데이터를 추가해 확장합니다.
[CreateAssetMenu(fileName = "QuestSettings", menuName = "Raising Zombies/Quest Settings")]
public sealed class QuestSettings : ScriptableObject
{
    public QuestDefinition[] quests = Array.Empty<QuestDefinition>(); // 초반 진행 순서와 목표 데이터

    // 누락되거나 중복된 ID 및 유효하지 않은 목표를 검사합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (quests == null || quests.Length == 0) { error = "퀘스트 목록이 비어 있습니다."; return false; }
        HashSet<string> ids = new(StringComparer.Ordinal); // 중복 식별자 검사 집합
        foreach (QuestDefinition quest in quests) // 검사할 정의
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.id) || !ids.Add(quest.id) ||
                quest.target < 1 || quest.targetStage < 1 || quest.rewardAmount < 0 ||
                !Enum.IsDefined(typeof(QuestType), quest.type) || !Enum.IsDefined(typeof(QuestRewardType), quest.rewardType) ||
                !Enum.IsDefined(typeof(QuestUnlockType), quest.unlockReward) ||
                !Enum.IsDefined(typeof(CurrencyUpgradeType), quest.targetUpgrade) || !Enum.IsDefined(typeof(UpgradeStatType), quest.targetStat))
            { error = "퀘스트 ID 중복/누락, 목표 또는 보상 설정을 확인하세요."; return false; }
        }
        return true;
    }

    // Inspector에서 데이터 오류를 바로 안내합니다.
    private void OnValidate()
    {
        if (!TryValidate(out string error)) Debug.LogWarning($"[QuestSettings] {error}", this); // 편집 중 잘못된 설정 안내
    }
}
