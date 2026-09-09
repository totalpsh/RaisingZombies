using System;

// 순차 규칙으로 재구성할 수 없는 최소 퀘스트 원본만 저장합니다.
[Serializable]
public sealed class QuestState
{
    public int version = 2; // 퀘스트 Provider 내부 형식 버전
    public int currentQuestIndex; // 아직 수령하지 않은 현재 퀘스트 인덱스
    public bool currencyUpgradeUnlocked; // 재화 강화 해금 여부
    public bool productionUpgradeUnlocked; // 생산 강화 해금 여부
    public long enemyKillCount; // 실제 인간 사망 이벤트로만 증가하는 영구 처치 원본
}

// 실제 생산 강화 시스템이 추가될 때 퀘스트에 레벨 원본을 제공하는 최소 계약입니다.
public interface IProductionUpgradeProgressSource
{
    int GetProductionUpgradeLevel(int upgradeIndex); // 지정 생산 강화 항목의 현재 실제 레벨 반환
}
