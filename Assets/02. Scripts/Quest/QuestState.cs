using System;
using System.Collections.Generic;

// 기존 원본에 없는 퀘스트 순서·해금·영구 처치 횟수만 저장합니다.
[Serializable]
public sealed class QuestState
{
    public int version = 1; // 퀘스트 Provider 내부 형식 버전
    public int currentQuestIndex; // 다음에 진행할 목록 인덱스
    public bool currencyUpgradeUnlocked; // 재화 강화 해금 여부
    public bool productionUpgradeUnlocked; // 생산 강화 해금 여부
    public long enemyKillCount; // 실제 인간 사망 이벤트로만 증가하는 영구 처치 원본
    public List<string> rewardedQuestIds = new(); // 데이터 재배치 후에도 같은 보상을 다시 주지 않을 ID 목록
}
