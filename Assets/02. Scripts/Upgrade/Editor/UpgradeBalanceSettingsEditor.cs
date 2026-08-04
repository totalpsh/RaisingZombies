using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>밸런스 누락과 중복을 UpgradeBalanceSettings Inspector에 표시합니다.</summary>
[CustomEditor(typeof(UpgradeBalanceSettings))]
public sealed class UpgradeBalanceSettingsEditor : Editor
{
    private readonly List<string> validationErrors = new();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var settings = (UpgradeBalanceSettings)target;
        settings.CollectValidationErrors(validationErrors);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("밸런스 검증", EditorStyles.boldLabel);

        if (validationErrors.Count == 0)
        {
            EditorGUILayout.HelpBox("필수 업그레이드 밸런스 정의가 모두 설정되어 있습니다.", MessageType.Info);
        }
        else
        {
            foreach (string error in validationErrors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        if (GUILayout.Button("검증 결과를 콘솔에 출력"))
        {
            LogValidationResults(settings);
        }
    }

    private void LogValidationResults(UpgradeBalanceSettings settings)
    {
        settings.CollectValidationErrors(validationErrors);
        if (validationErrors.Count == 0)
        {
            Debug.Log("[UpgradeBalanceSettings] 밸런스 검증을 통과했습니다.", settings);
            return;
        }

        foreach (string error in validationErrors)
        {
            Debug.LogError($"[UpgradeBalanceSettings] {error}", settings);
        }
    }
}
