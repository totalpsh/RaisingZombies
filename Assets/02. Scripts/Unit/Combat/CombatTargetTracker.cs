using System.Collections.Generic;
using UnityEngine;

public static class CombatTargetTracker
{
    private static readonly Dictionary<ICombatTarget, int> AttackerCounts = new();

    public static int GetAttackerCount(ICombatTarget target)
    {
        if (target == null)
            return 0;

        return AttackerCounts.TryGetValue(target, out int count) ? count : 0;
    }

    public static void Register(ICombatTarget target)
    {
        if (target == null)
            return;

        AttackerCounts.TryGetValue(target, out int count);
        AttackerCounts[target] = count + 1;
    }

    public static void Unregister(ICombatTarget target)
    {
        if (target == null)
            return;

        if (!AttackerCounts.TryGetValue(target, out int count))
        
            return;

        count--;

        if (count <= 0)
            AttackerCounts.Remove(target);
        else
            AttackerCounts[target] = count;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        AttackerCounts.Clear();
    }
}
