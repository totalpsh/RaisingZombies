using System;
using System.Collections.Generic;

// 신체 장비가 장착되는 확장 가능한 슬롯 종류입니다.
public enum BodyEquipmentSlot
{
    Head,
    Torso,
    Arms,
    Legs,
    Eyes,
    Jaw,
    Heart,
    Spine
}

// 신체 장비가 제공할 수 있는 전투 스탯 종류입니다.
public enum EquipmentStatType
{
    Attack,
    Health,
    Defense,
    AttackSpeed,
    MoveSpeed,
    LifeSteal,
    HealthRegen,
    DamagePercent,
    HealthPercent,
    DefensePercent,
    CriticalChance,
    CriticalDamage,
    DamageReduction
}

// 장비 스탯 값이 고정 수치인지 비율인지 구분합니다.
public enum EquipmentStatValueKind
{
    Flat,
    Percent
}

// 한 장비에 실제로 뽑힌 스탯 종류와 수치를 저장합니다.
[Serializable]
public sealed class EquipmentStatRoll
{
    public EquipmentStatType statType; // 실제로 결정된 장비 스탯 종류
    public float value; // Stat Scale과 Rarity 배율까지 반영된 최종 Roll 값

    // 저장과 UI에 안전하게 전달할 독립 복제본을 만듭니다.
    public EquipmentStatRoll Clone()
    {
        return new EquipmentStatRoll { statType = statType, value = value };
    }
}

// 유저가 실제로 소유하는 랜덤 장비 한 개의 저장 데이터입니다.
[Serializable]
public sealed class BodyEquipmentInstance
{
    public string uniqueId = string.Empty; // 장비 인스턴스를 구분하는 고유 ID
    public string definitionId = string.Empty; // 원형 장비 ScriptableObject의 안정적인 ID
    public int rarityTier = 1; // 뽑힌 장비의 1 기준 레어도 단계
    public EquipmentStatRoll mainStat = new(); // 뽑힌 주 스탯
    public List<EquipmentStatRoll> subStats = new(); // 중복 없이 뽑힌 보조 스탯 목록
    public bool isLocked; // 실수로 파기되지 않도록 보호하는 상태

    // 외부 코드가 내부 저장 목록을 변경하지 못하도록 깊은 복제본을 만듭니다.
    public BodyEquipmentInstance Clone()
    {
        BodyEquipmentInstance copy = new() // 복제할 장비 기본 데이터
        {
            uniqueId = uniqueId,
            definitionId = definitionId,
            rarityTier = rarityTier,
            mainStat = mainStat == null ? new EquipmentStatRoll() : mainStat.Clone(),
            isLocked = isLocked
        };
        if (subStats != null)
        {
            foreach (EquipmentStatRoll roll in subStats) // 원본과 분리할 보조 스탯
                if (roll != null) copy.subStats.Add(roll.Clone());
        }
        return copy;
    }
}

// 신체 슬롯과 현재 장착 장비 ID의 저장 연결입니다.
[Serializable]
public sealed class EquippedBodySlotEntry
{
    public BodyEquipmentSlot slot; // 장비가 장착된 신체 슬롯
    public string instanceId = string.Empty; // 해당 슬롯에 장착된 장비 인스턴스 ID
}

// 인벤토리와 뽑기 연구의 영구 원본을 통합 저장하는 DTO입니다.
[Serializable]
public sealed class BodyEquipmentState
{
    public int version = 1; // Body Equipment Provider 내부 저장 형식 버전
    public List<BodyEquipmentInstance> inventory = new(); // 획득 후 파기하지 않은 모든 장비
    public List<EquippedBodySlotEntry> equippedSlots = new(); // 슬롯별 장착 장비 연결
    public int researchLevel = 1; // 현재 적용 중인 뽑기 연구 레벨
    public int researchPoints; // 장비 파기로 모은 뽑기 연구 포인트
    public bool isResearching; // 실제 시간 연구가 진행 중인지 여부
    public long researchEndUtc; // 연구가 자동 완료될 Unix UTC 초
    public long totalBodyDrawCount; // 성공한 신체 장비 뽑기 누적 횟수
    public long totalEquipCount; // 성공한 장착 누적 횟수
    public long totalDismantleCount; // 성공한 파기 누적 횟수
    public int highestRarityTier; // 지금까지 한 번이라도 획득한 최고 레어도
}

// 향후 확장할 게임 재화의 안정적인 종류입니다.
public enum GameCurrencyType
{
    BodyDrawTicket,
    ProductionUpgradeCurrency,
    CurrencyUpgradeCurrency,
    PremiumCurrency
}

// 타입형 지갑에 저장할 재화 한 종류의 값입니다.
[Serializable]
public sealed class GameCurrencyEntry
{
    public GameCurrencyType type; // 저장할 재화 종류
    public long amount; // 음수가 될 수 없는 현재 보유량
}

// 타입형 지갑의 독립 저장 DTO입니다.
[Serializable]
public sealed class CurrencyWalletState
{
    public int version = 1; // Currency Wallet Provider 내부 저장 형식 버전
    public List<GameCurrencyEntry> currencies = new(); // 종류별 재화 값 목록
}

// UI와 Quest가 사용하는 한 번의 신체 장비 뽑기 결과입니다.
public readonly struct BodyDrawResult
{
    public readonly BodyEquipmentInstance Equipment; // 인벤토리에 저장된 새 장비의 복제본

    // 새 장비 한 개를 변경 불가능한 뽑기 결과로 묶습니다.
    public BodyDrawResult(BodyEquipmentInstance equipment)
    {
        Equipment = equipment;
    }
}

// 확률 UI가 실제 Weight 원본에서 읽는 레어도 확률 결과입니다.
public readonly struct BodyRarityProbability
{
    public readonly int Tier; // 조회한 레어도 단계
    public readonly string DisplayName; // 레어도 데이터의 표시 이름
    public readonly float Probability; // 0부터 1 사이로 정규화된 실제 등장 확률

    // 레어도 단계와 정규화 확률을 표시용 결과로 묶습니다.
    public BodyRarityProbability(int tier, string displayName, float probability)
    {
        Tier = tier;
        DisplayName = displayName;
        Probability = probability;
    }
}
