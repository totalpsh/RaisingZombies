using System;
using UnityEngine;

public class BattleFocusPoint : MonoBehaviour
{
    [SerializeField] private ZombieSpawner zombieSpawner;
    [SerializeField] private HumanSpawner humanSpawner;
    [SerializeField] private Transform defaultFocus;
    
    [SerializeField, Min(0f)] private float combatDetectionDistance = 3f;
    // [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;
    // private float _velocityX;

    private void LateUpdate()
    {
        Vector3 position = transform.position;
        position.x = CalculateTargetX();
        transform.position = position;
    }
    
    private float CalculateTargetX()
    {
        UnitController frontZombie = FindFrontZombie();

        if (frontZombie == null)
            return defaultFocus != null ? defaultFocus.position.x : transform.position.x;

        UnitController frontHuman = FindFrontHuman();

        if (frontHuman == null)
            return frontZombie.transform.position.x;

        float distance = frontHuman.transform.position.x - frontZombie.transform.position.x;
        
        if (distance > combatDetectionDistance)
            return frontZombie.transform.position.x;

        return (frontZombie.transform.position.x + frontHuman.transform.position.x) * 0.5f;
    }
    
    private UnitController FindFrontZombie()
    {
        if (zombieSpawner == null)
            return null;

        UnitController front = null;
        float frontX = float.MinValue;

        foreach (UnitController zombie in zombieSpawner.SpawnedZombies)
        {
            if (!IsValidUnit(zombie))
                continue;

            if (zombie.transform.position.x <= frontX)
                continue;

            front = zombie;
            frontX = zombie.transform.position.x;
        }

        return front;
    }

    private UnitController FindFrontHuman()
    {
        if (humanSpawner == null)
            return null;

        UnitController front = null;
        float frontX = float.MaxValue;

        foreach (UnitController human in humanSpawner.SpawnedHumans)
        {
            if (!IsValidUnit(human))
                continue;

            if (human.transform.position.x >= frontX)
                continue;

            front = human;
            frontX = human.transform.position.x;
        }

        return front;
    }

    private static bool IsValidUnit(UnitController unit)
    {
        return unit != null && unit.gameObject.activeInHierarchy && !unit.IsDead;
    }
}
