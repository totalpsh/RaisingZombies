using System;
using System.Collections.Generic;

// 저장 파일 전체와 메타데이터를 표현하는 최상위 DTO입니다.
[Serializable]
public sealed class GameSaveData
{
    public int saveVersion = SaveMigrationService.CurrentSaveVersion; // 전체 저장 형식 버전
    public string saveId = string.Empty; // 저장 파일을 구분하는 고유 ID
    public long savedAt; // 마지막 저장 시각의 Unix 초
    public List<SaveDataSection> sections = new(); // Provider별 JSON 데이터 목록

    // 메타데이터가 채워진 새 저장 데이터를 만듭니다.
    public static GameSaveData CreateNew()
    {
        return new GameSaveData
        {
            saveId = Guid.NewGuid().ToString("N"),
            savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    // 지정한 Provider 키의 저장 구역을 찾습니다.
    public bool TryGetSection(string key, out SaveDataSection section)
    {
        section = null;
        if (sections == null || string.IsNullOrWhiteSpace(key)) return false;

        foreach (SaveDataSection item in sections) // 키와 비교할 저장 구역
        {
            if (item != null && string.Equals(item.key, key, StringComparison.Ordinal))
            {
                section = item;
                return true;
            }
        }

        return false;
    }

    // 지정한 Provider의 JSON을 추가하거나 교체합니다.
    public void SetSection(string key, string json)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("저장 키가 비어 있습니다.", nameof(key));
        if (sections == null) sections = new List<SaveDataSection>();

        if (TryGetSection(key, out SaveDataSection section)) // 갱신할 기존 저장 구역
        {
            section.json = json ?? string.Empty;
            return;
        }

        sections.Add(new SaveDataSection { key = key, json = json ?? string.Empty });
    }

    // 전체 Save Container의 필수 메타데이터와 Section 식별 구조를 검증합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (saveVersion <= 0)
        {
            error = "Save Version이 유효하지 않습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(saveId))
        {
            error = "Save ID가 비어 있습니다.";
            return false;
        }

        if (sections == null)
        {
            error = "Save Section 목록이 null입니다.";
            return false;
        }

        HashSet<string> keys = new(StringComparer.Ordinal); // 중복 Provider 키를 검사할 집합
        foreach (SaveDataSection section in sections) // 구조를 검증할 Provider 저장 구역
        {
            if (section == null)
            {
                error = "null Save Section이 존재합니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(section.key))
            {
                error = "Save Section 키가 비어 있습니다.";
                return false;
            }

            if (!keys.Add(section.key))
            {
                error = $"중복 Save Section 키입니다: {section.key}";
                return false;
            }

        }

        return true;
    }
}

// 한 Save Provider의 키와 직렬화 결과를 보관하는 DTO입니다.
[Serializable]
public sealed class SaveDataSection
{
    public string key = string.Empty; // Provider의 안정적인 저장 키
    public string json = string.Empty; // Provider DTO의 JSON 문자열
}
