using System;

// 저장 가능한 각 게임 시스템이 구현하는 공통 인터페이스입니다.
public interface ISaveDataProvider
{
    string SaveKey { get; } // 통합 저장에서 사용할 안정적인 Provider 키
    Type SaveDataType { get; } // 역직렬화할 DTO의 실제 형식

    // 현재 런타임 원본 데이터를 직렬화 가능한 DTO로 반환합니다.
    object CaptureSaveData();

    // 역직렬화된 DTO를 런타임 원본 데이터에 적용합니다.
    void RestoreSaveData(object data);

    // 런타임 원본 데이터를 새 게임 기본값으로 되돌립니다.
    void ResetSaveData();
}
