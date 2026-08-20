using System;

[Serializable]
// Stage 영구 진행 상태를 저장하는 순수 DTO입니다.
public sealed class StageProgressState
{
    public int version = 1; // Stage Provider 내부 저장 형식 버전
    public int currentStageNumber = 1; // 현재 플레이어가 진행할 Stage 번호
    public bool allStagesCompleted; // 현재 등록된 모든 Stage를 완료했는지 여부
}
