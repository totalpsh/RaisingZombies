using System.Collections.Generic;
using UnityEngine;

// 저장 버전을 순서대로 올리는 단일 마이그레이션 진입점입니다.
public static class SaveMigrationService
{
    public const int CurrentSaveVersion = 1; // 현재 게임이 기록하는 전체 저장 버전

    // 과거 저장 데이터를 현재 버전까지 순차 변환합니다.
    public static bool TryMigrate(GameSaveData data)
    {
        if (data == null) return false;
        if (data.saveVersion > CurrentSaveVersion)
        {
            Debug.LogError($"[Save] 지원하지 않는 미래 저장 버전입니다: {data.saveVersion}");
            return false;
        }

        if (data.sections == null) data.sections = new List<SaveDataSection>();
        if (data.saveVersion <= 0) data.saveVersion = 1;
        if (string.IsNullOrWhiteSpace(data.saveId)) data.saveId = System.Guid.NewGuid().ToString("N");
        return data.saveVersion == CurrentSaveVersion;
    }
}
