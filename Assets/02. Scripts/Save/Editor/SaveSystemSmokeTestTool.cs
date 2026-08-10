#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 통합 저장의 신규·저장·로드·초기화·백업·누락 Provider를 검증합니다.
public static class SaveSystemSmokeTestTool
{
    // 격리된 임시 디렉터리에서 저장 시나리오를 순서대로 실행합니다.
    [MenuItem("Tools/Save System/Run Smoke Tests")]
    public static void Run()
    {
        string testDirectory = Path.Combine(Application.temporaryCachePath, "RaisingZombiesSaveTest_" + Guid.NewGuid().ToString("N")); // 이번 테스트 전용 디렉터리
        SaveFileService service = new SaveFileService(testDirectory); // 실제 파일 흐름을 검사할 서비스

        try
        {
            Assert(!service.HasSave(), "New Save: 새 경로에 저장 파일이 없어야 합니다.");
            Assert(!service.TryLoad(out _, out _), "New Save: 비어 있는 경로의 로드는 실패로 반환되어야 합니다.");

            GameSaveData firstSave = CreateSave(100); // 백업에서 복구할 첫 번째 저장
            Assert(service.TrySave(firstSave), "Save: 첫 번째 저장에 실패했습니다.");
            Assert(service.HasSave(), "Save: 저장 파일이 생성되지 않았습니다.");

            GameSaveData secondSave = CreateSave(250); // 메인 파일에서 읽을 두 번째 저장
            Assert(service.TrySave(secondSave), "Save: 두 번째 저장에 실패했습니다.");
            Assert(File.Exists(service.BackupPath), "Backup: 기존 메인의 백업이 생성되지 않았습니다.");

            Assert(service.TryLoad(out GameSaveData loadedSave, out bool loadedFromBackup), "Load: 메인 저장 로드에 실패했습니다.");
            Assert(!loadedFromBackup, "Load: 정상 메인을 백업으로 잘못 판정했습니다.");
            Assert(ReadCurrency(loadedSave) == 250, "Load: 두 번째 저장의 재화가 복원되지 않았습니다.");

            File.WriteAllText(service.SavePath, "{corrupted json", System.Text.Encoding.UTF8);
            Assert(service.TryLoad(out GameSaveData recoveredSave, out loadedFromBackup), "Backup: 손상된 메인에서 복구하지 못했습니다.");
            Assert(loadedFromBackup, "Backup: 백업 복구 여부가 표시되지 않았습니다.");
            Assert(ReadCurrency(recoveredSave) == 100, "Backup: 이전 메인의 재화가 복구되지 않았습니다.");
            Assert(!recoveredSave.TryGetSection("relic", out _), "Provider 누락: 존재하지 않는 Provider가 생성되었습니다.");
            Assert(recoveredSave.TryGetSection("upgrade", out _), "Provider 누락: 기존 Provider 데이터가 사라졌습니다.");
            Assert(service.TrySave(recoveredSave, true), "Backup: 정상 백업을 보존한 메인 복구에 실패했습니다.");
            File.WriteAllText(service.SavePath, "{corrupted again", System.Text.Encoding.UTF8);
            Assert(service.TryLoad(out GameSaveData preservedBackup, out loadedFromBackup), "Backup: 보존한 백업을 다시 읽지 못했습니다.");
            Assert(loadedFromBackup && ReadCurrency(preservedBackup) == 100, "Backup: 메인 복구 중 정상 백업이 덮어써졌습니다.");

            GameSaveData legacySave = GameSaveData.CreateNew(); // 버전 진입점을 검사할 과거 형식 저장
            legacySave.saveVersion = 0;
            Assert(SaveMigrationService.TryMigrate(legacySave), "Migration: 과거 저장 버전 변환에 실패했습니다.");
            Assert(legacySave.saveVersion == SaveMigrationService.CurrentSaveVersion, "Migration: 현재 버전으로 올라오지 않았습니다.");

            Assert(service.DeleteAll(), "Reset: 저장 파일 삭제에 실패했습니다.");
            Assert(!service.HasSave(), "Reset: 메인 또는 백업 파일이 남았습니다.");
            Debug.Log("[SaveSmokeTest] 통과: New Save, Save, Load, Reset, Backup, Provider 누락, Migration");
        }
        finally
        {
            service.DeleteAll();
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }
    }

    // 테스트 재화를 가진 전체 저장 DTO를 만듭니다.
    private static GameSaveData CreateSave(int currency)
    {
        GameSaveData save = GameSaveData.CreateNew(); // 구성할 전체 저장 DTO
        UpgradeState upgrade = new UpgradeState { currency = currency }; // 저장할 업그레이드 원본
        save.SetSection("upgrade", JsonUtility.ToJson(upgrade));
        return save;
    }

    // 전체 저장에서 테스트 재화를 읽습니다.
    private static int ReadCurrency(GameSaveData save)
    {
        Assert(save.TryGetSection("upgrade", out SaveDataSection section), "재화 Provider 구역이 없습니다.");
        UpgradeState upgrade = JsonUtility.FromJson<UpgradeState>(section.json); // 역직렬화한 업그레이드 원본
        return upgrade.currency;
    }

    // 조건이 거짓이면 스모크 테스트를 즉시 실패시킵니다.
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
