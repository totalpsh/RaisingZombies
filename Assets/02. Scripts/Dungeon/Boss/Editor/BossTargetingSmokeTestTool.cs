#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BossTargetingSmokeTestTool
{
    [MenuItem("Tools/Raising Zombies/Boss/Run Targeting Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running this test.");

        Scene scene = EditorSceneManager.NewPreviewScene();
        UnitData zombieData = ScriptableObject.CreateInstance<UnitData>();
        UnitData humanData = ScriptableObject.CreateInstance<UnitData>();
        try
        {
            Set(humanData, "team", UnitTeam.Human);
            BattleArea area = Create<BattleArea>(scene);
            UnitController owner = Create<UnitController>(scene);
            Set(owner, "data", zombieData);
            UnitTargeting targeting = owner.gameObject.AddComponent<UnitTargeting>();
            targeting.Initialize(owner, area);

            StructureController structure = Create<StructureController>(scene);
            Set(structure, "team", UnitTeam.Human);
            Set(structure, "structureType", StructureType.HumanFortress);
            structure.transform.position = Vector3.right;
            structure.Initialize(area);
            Check(ReferenceEquals(targeting.FindTarget(), structure), "Structure fallback");

            BossController boss = Create<BossController>(scene);
            Set(boss, "bossCollider", boss.gameObject.AddComponent<BoxCollider2D>());
            boss.transform.position = Vector3.right * 3f;
            UnitStats stats = new(100f, 0f, 0f, 1f, 0f, 0f);
            Check(boss.Initialize(stats, area), "Boss initialization");
            area.RegisterAdditionalTarget(boss);
            Check(area.GetAdditionalEnemyTargets(UnitTeam.Zombie).Count == 1, "No duplicate registration");
            Check(area.GetAdditionalEnemyTargets(UnitTeam.Human).Count == 0, "Team filtering");
            Check(ReferenceEquals(targeting.FindTarget(), boss), "Additional target precedes closer structure");

            UnitController human = Create<UnitController>(scene);
            Set(human, "data", humanData);
            Set(human, "model", new UnitModel(stats));
            Set(human, "_isInitialized", true);
            human.transform.position = Vector3.right * 10f;
            area.RegisterUnit(human);
            Check(ReferenceEquals(targeting.FindTarget(), human), "Ordinary unit precedes closer boss");
            area.UnregisterUnit(human);

            boss.transform.position = Vector3.left;
            Check(ReferenceEquals(targeting.FindTarget(), structure), "Behind-owner target is excluded");
            boss.transform.position = Vector3.right * 3f;
            boss.TakeDamage(25f);
            Check(Mathf.Approximately(boss.CurrentHealth, 75f), "Damage application");

            // Avoid using the open scene's pool while testing death in isolation.
            int deaths = 0;
            boss.Died += deadBoss => { deaths++; deadBoss.gameObject.SetActive(false); };
            boss.TakeDamage(100f);
            boss.TakeDamage(100f);
            Check(deaths == 1 && boss.IsDead, "Death occurs once");
            Check(area.GetAdditionalEnemyTargets(UnitTeam.Zombie).Count == 0, "Death unregisters target");

            boss.gameObject.SetActive(true);
            Check(boss.Initialize(stats, area) && boss.CurrentHealth == 100f, "Reinitialization resets health");
            boss.gameObject.SetActive(false);
            Check(area.GetAdditionalEnemyTargets(UnitTeam.Zombie).Count == 0, "Disable unregisters target");
            Check(ReferenceEquals(targeting.FindTarget(), structure), "Structure fallback after cleanup");
            Debug.Log("[BossTargetingSmokeTest] All checks passed.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(zombieData);
            UnityEngine.Object.DestroyImmediate(humanData);
        }
    }

    private static T Create<T>(Scene scene) where T : Component
    {
        GameObject instance = new(typeof(T).Name);
        SceneManager.MoveGameObjectToScene(instance, scene);
        return instance.AddComponent<T>();
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("Missing field: " + name);
        field.SetValue(target, value);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[BossTargetingSmokeTest] " + message);
    }
}
#endif
