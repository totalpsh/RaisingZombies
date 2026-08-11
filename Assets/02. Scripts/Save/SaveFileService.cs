using System;
using System.IO;
using System.Text;
using UnityEngine;

// JSON 직렬화와 메인·임시·백업 파일 I/O를 전담합니다.
public sealed class SaveFileService
{
    private const string SaveFileName = "save.json"; // 메인 저장 파일명
    private const string BackupFileName = "save.backup.json"; // 백업 저장 파일명
    private const string TemporaryFileName = "save.tmp"; // 쓰기 중 사용할 임시 파일명
    private readonly string _directoryPath; // 저장 파일을 둘 디렉터리

    public string SavePath => Path.Combine(_directoryPath, SaveFileName); // 메인 저장 파일의 전체 경로
    public string BackupPath => Path.Combine(_directoryPath, BackupFileName); // 백업 저장 파일의 전체 경로
    public string TemporaryPath => Path.Combine(_directoryPath, TemporaryFileName); // 임시 저장 파일의 전체 경로

    // 기본 또는 테스트용 저장 디렉터리를 설정합니다.
    public SaveFileService(string directoryPath = null)
    {
        _directoryPath = string.IsNullOrWhiteSpace(directoryPath) ? Application.persistentDataPath : directoryPath;
    }

    // 메인 저장이 없거나 손상되면 백업 저장을 차례로 읽습니다.
    public bool TryLoad(out GameSaveData data, out bool loadedFromBackup)
    {
        loadedFromBackup = false;
        if (TryLoadPath(SavePath, out data)) return true;

        if (File.Exists(SavePath)) Debug.LogWarning("[Save] 메인 저장을 읽지 못했습니다. 백업을 확인합니다.");
        if (!TryLoadPath(BackupPath, out data)) return false;

        loadedFromBackup = true;
        Debug.LogWarning("[Save] 메인 저장이 손상되어 백업 저장을 불러왔습니다.");
        return true;
    }

    // 임시 파일을 검증한 뒤 메인 파일과 교체하고 기존 메인을 백업합니다.
    public bool TrySave(GameSaveData data, bool preserveBackup = false)
    {
        if (data == null || !SaveMigrationService.TryMigrate(data)) return false;
        string validationError; // 저장 컨테이너 검증 실패 이유
        if (!data.TryValidate(out validationError))
        {
            Debug.LogError($"[Save] 저장 데이터 검증 실패: {validationError}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(_directoryPath);
            string json = JsonUtility.ToJson(data, true); // 디스크에 기록할 전체 JSON
            File.WriteAllText(TemporaryPath, json, new UTF8Encoding(false));
            if (!TryLoadPath(TemporaryPath, out _)) throw new InvalidDataException("임시 저장 파일 검증에 실패했습니다.");

            if (preserveBackup) RepairMainFile();
            else ReplaceMainFile();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] 저장 실패: {exception.Message}");
            TryDeleteTemporaryFile();
            return false;
        }
    }

    // 메인 또는 백업 파일이 하나라도 있는지 확인합니다.
    public bool HasSave()
    {
        return File.Exists(SavePath) || File.Exists(BackupPath);
    }

    // 메인·백업·임시 저장 파일을 모두 삭제합니다.
    public bool DeleteAll()
    {
        try
        {
            DeleteIfExists(SavePath);
            DeleteIfExists(BackupPath);
            DeleteIfExists(TemporaryPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] 저장 파일 삭제 실패: {exception.Message}");
            return false;
        }
    }

    // 지정한 파일을 역직렬화하고 버전을 검증합니다.
    private static bool TryLoadPath(string path, out GameSaveData data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8); // 읽어 온 전체 저장 JSON
            if (string.IsNullOrWhiteSpace(json)) return false;
            data = JsonUtility.FromJson<GameSaveData>(json);
            if (!SaveMigrationService.TryMigrate(data)) return false;
            string validationError; // 읽은 컨테이너 검증 실패 이유
            if (data.TryValidate(out validationError)) return true;
            Debug.LogWarning($"[Save] 파일 구조 검증 실패 ({Path.GetFileName(path)}): {validationError}");
            data = null;
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Save] 파일 읽기 실패 ({Path.GetFileName(path)}): {exception.Message}");
            data = null;
            return false;
        }
    }

    // 플랫폼이 지원하면 원자적 교체를 사용하고 아니면 안전한 복사 방식으로 대체합니다.
    private void ReplaceMainFile()
    {
        if (!File.Exists(SavePath))
        {
            File.Move(TemporaryPath, SavePath);
            return;
        }

        try
        {
            File.Replace(TemporaryPath, SavePath, BackupPath);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceMainFileByCopy();
        }
        catch (IOException)
        {
            ReplaceMainFileByCopy();
        }
    }

    // 백업 복구 직후에는 정상 백업을 덮지 않고 메인 파일만 복구합니다.
    private void RepairMainFile()
    {
        File.Copy(TemporaryPath, SavePath, true);
        DeleteIfExists(TemporaryPath);
    }

    // 원자적 교체를 지원하지 않는 플랫폼에서 백업 후 메인을 덮어씁니다.
    private void ReplaceMainFileByCopy()
    {
        File.Copy(SavePath, BackupPath, true);
        File.Copy(TemporaryPath, SavePath, true);
        DeleteIfExists(TemporaryPath);
    }

    // 실패 후 남은 임시 파일을 예외 없이 정리합니다.
    private void TryDeleteTemporaryFile()
    {
        try
        {
            DeleteIfExists(TemporaryPath);
        }
        catch (Exception)
        {
        }
    }

    // 파일이 존재할 때만 삭제합니다.
    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
