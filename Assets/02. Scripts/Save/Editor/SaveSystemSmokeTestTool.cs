#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 통합 저장의 생성·로드·백업·검증·부분 Provider 손상을 검증합니다.
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
            Assert(!recoveredSave.TryGetSection("stage_progress", out _), "Provider 누락: 기존 Upgrade 전용 Save에 Stage Section이 임의 생성됐습니다.");
            Assert(recoveredSave.TryGetSection("upgrade", out _), "Provider 누락: 기존 Provider 데이터가 사라졌습니다.");
            Assert(service.TrySave(recoveredSave, true), "Backup: 정상 백업을 보존한 메인 복구에 실패했습니다.");
            File.WriteAllText(service.SavePath, "{corrupted again", System.Text.Encoding.UTF8);
            Assert(service.TryLoad(out GameSaveData preservedBackup, out loadedFromBackup), "Backup: 보존한 백업을 다시 읽지 못했습니다.");
            Assert(loadedFromBackup && ReadCurrency(preservedBackup) == 100, "Backup: 메인 복구 중 정상 백업이 덮어써졌습니다.");

            GameSaveData legacySave = GameSaveData.CreateNew(); // 버전 진입점을 검사할 과거 형식 저장
            legacySave.saveVersion = 0;
            Assert(SaveMigrationService.TryMigrate(legacySave), "Migration: 과거 저장 버전 변환에 실패했습니다.");
            Assert(legacySave.saveVersion == SaveMigrationService.CurrentSaveVersion, "Migration: 현재 버전으로 올라오지 않았습니다.");

            Assert(service.DeleteAll(), "Validation: 검증 테스트 전 저장 파일 정리에 실패했습니다.");
            GameSaveData duplicateSave = CreateSave(10); // 동일 Provider 키를 두 번 가진 잘못된 저장
            duplicateSave.sections.Add(new SaveDataSection { key = "upgrade", json = JsonUtility.ToJson(new UpgradeState()) });
            Assert(!service.TrySave(duplicateSave), "Duplicate Section: 중복 Provider 키 저장을 허용했습니다.");

            GameSaveData futureSave = CreateSave(20); // 현재 게임보다 높은 Container 버전 저장
            futureSave.saveVersion = SaveMigrationService.CurrentSaveVersion + 1;
            Assert(!service.TrySave(futureSave), "Future Version: 지원하지 않는 미래 버전을 저장으로 허용했습니다.");

            GameSaveData corruptProviderSave = CreateSave(123); // 한 Provider JSON만 손상된 전체 저장
            corruptProviderSave.SetSection("broken_provider", "{invalid provider json");
            Assert(service.TrySave(corruptProviderSave), "Provider Corruption: 부분 손상 Container 저장에 실패했습니다.");
            GameSaveData partialSave; // 부분 손상 Container에서 다시 읽을 전체 저장 데이터
            Assert(service.TryLoad(out partialSave, out loadedFromBackup), "Provider Corruption: 부분 손상 Container 로드에 실패했습니다.");
            Assert(!loadedFromBackup && ReadCurrency(partialSave) == 123, "Provider Corruption: 정상 Provider 데이터가 보존되지 않았습니다.");
            Assert(partialSave.TryGetSection("broken_provider", out _), "Provider Corruption: 알 수 없는 Section이 유실되었습니다.");

            GameSaveData emptyProviderSave = CreateSave(456); // 한 Provider JSON만 비어 있는 전체 저장
            emptyProviderSave.SetSection("broken_provider", string.Empty);
            Assert(service.TrySave(emptyProviderSave), "Provider Empty JSON: 빈 Provider Section 때문에 Container 저장이 실패했습니다.");
            GameSaveData emptySectionLoadedSave; // 빈 Provider Section과 함께 다시 읽을 전체 저장 데이터
            Assert(service.TryLoad(out emptySectionLoadedSave, out loadedFromBackup), "Provider Empty JSON: Container 로드에 실패했습니다.");
            Assert(!loadedFromBackup && ReadCurrency(emptySectionLoadedSave) == 456, "Provider Empty JSON: 정상 Provider 데이터가 보존되지 않았습니다.");
            SaveDataSection emptySection; // 다시 읽은 빈 Provider 저장 구역
            Assert(emptySectionLoadedSave.TryGetSection("broken_provider", out emptySection) && string.IsNullOrEmpty(emptySection.json),
                "Provider Empty JSON: 빈 Provider Section이 유실되거나 변경됐습니다.");

            GameSaveData stageProviderSave = CreateSave(654); // Upgrade와 Stage Section을 함께 검증할 전체 저장
            StageProgressState stageProgress = new StageProgressState { currentStageNumber = 2, allStagesCompleted = true }; // 저장할 Stage 영구 진행 원본
            stageProviderSave.SetSection("stage_progress", JsonUtility.ToJson(stageProgress));
            Assert(service.TrySave(stageProviderSave), "Stage Provider: Stage Section 저장에 실패했습니다.");
            GameSaveData loadedStageProviderSave; // Stage Section과 함께 다시 읽을 전체 저장
            Assert(service.TryLoad(out loadedStageProviderSave, out loadedFromBackup), "Stage Provider: Stage Section 로드에 실패했습니다.");
            Assert(!loadedFromBackup && ReadCurrency(loadedStageProviderSave) == 654, "Stage Provider: 기존 Upgrade Section이 보존되지 않았습니다.");
            StageProgressState loadedStageProgress = ReadStageProgress(loadedStageProviderSave); // 다시 읽은 Stage 영구 진행 원본
            Assert(loadedStageProgress.currentStageNumber == 2 && loadedStageProgress.allStagesCompleted,
                "Stage Provider: Stage 진행 원본이 동일하게 복원되지 않았습니다.");

            File.WriteAllText(service.TemporaryPath, "stale tmp", System.Text.Encoding.UTF8);
            Assert(service.TrySave(CreateSave(321)), "Temporary File: 남은 tmp를 덮어쓰는 저장에 실패했습니다.");
            Assert(!File.Exists(service.TemporaryPath), "Temporary File: 정상 저장 뒤 tmp 파일이 남았습니다.");

            Assert(service.DeleteAll(), "Reset: 저장 파일 삭제에 실패했습니다.");
            Assert(!service.HasSave(), "Reset: 메인 또는 백업 파일이 남았습니다.");
            Debug.Log("[SaveSmokeTest] 통과: New Save, Save, Load, Reset, Backup, Backup Preservation, Provider Missing, Provider Corruption, Provider Empty JSON, Stage Provider, Duplicate Section, Migration, Future Version, Temporary File");
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

    // 전체 저장에서 테스트 Stage 진행 원본을 읽습니다.
    private static StageProgressState ReadStageProgress(GameSaveData save)
    {
        SaveDataSection section; // 역직렬화할 Stage Provider 저장 구역
        Assert(save.TryGetSection("stage_progress", out section), "Stage Provider 구역이 없습니다.");
        return JsonUtility.FromJson<StageProgressState>(section.json);
    }

    // 조건이 거짓이면 스모크 테스트를 즉시 실패시킵니다.
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
