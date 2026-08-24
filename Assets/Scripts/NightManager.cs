using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NightManager : MonoBehaviour
{
    public static NightManager Instance;

    [Header("Predator Prefabs")]
    public List<GameObject> predatorPrefabs;

    [Header("Night Length")]
    public float baseNightLength = 180f;
    public float nightLengthIncrease = 30f;
    public float maxNightLength = 300f;

    [Header("Points Requirement - Nights 1-5")]
    public int night1GoldRequirement = 500;
    public int night2GoldRequirement = 650;
    public int night3GoldRequirement = 850;
    public int night4GoldRequirement = 1100;
    public int night5GoldRequirement = 1450;
    public float goldRequirementGrowthAfterNight5 = 1.3f;

    [Header("Win/Lose UI")]
    public WinLoseUI winLoseUI;

    protected float nightLength;
    protected float spawnBudget;
    protected List<GameObject> spawnQueue = new List<GameObject>();
    protected List<Spawner> activeSpawners = new List<Spawner>();

    protected bool allPredatorsQueued = false;
    protected bool nightEvaluated = false;

    protected virtual void Start()
    {
        Instance = this;

        int night = GameManager.currentNight;

        nightLength = CalculateNightLength(night);
        spawnBudget = CalculateSpawnTarget(night);

        FindSpawners();
        BuildSpawnQueue();

        Debug.Log($"[NightManager] Night {night} - Length: {nightLength}s, Spawn Target: {spawnBudget}, Queued: {spawnQueue.Count} predators, Points Requirement: {CalculateGoldRequirement(night)}");

        StartCoroutine(SpawnOverTime());
    }

    protected virtual void Update()
    {
        if (allPredatorsQueued && !nightEvaluated && GameManager.totalPredatorsKilled >= GameManager.totalPredatorsSpawned)
        {
            nightEvaluated = true;
            EvaluateWinLose();
        }
    }

    protected virtual float CalculateNightLength(int night)
    {
        float length = baseNightLength + (night - 1) * nightLengthIncrease;
        return Mathf.Min(length, maxNightLength);
    }

    protected virtual float CalculateSpawnTarget(int night)
    {
        float[] earlyNightTargets = { 500f, 600f, 750f, 950f, 1350f };

        if (night <= earlyNightTargets.Length)
        {
            return earlyNightTargets[night - 1];
        }

        float value = earlyNightTargets[earlyNightTargets.Length - 1];
        int extraNights = night - earlyNightTargets.Length;

        for (int i = 0; i < extraNights; i++)
        {
            value *= 1.3f;
        }

        return value;
    }

    protected virtual int CalculateGoldRequirement(int night)
    {
        int[] earlyRequirements = { night1GoldRequirement, night2GoldRequirement, night3GoldRequirement, night4GoldRequirement, night5GoldRequirement };

        if (night <= earlyRequirements.Length)
        {
            return earlyRequirements[night - 1];
        }

        float value = earlyRequirements[earlyRequirements.Length - 1];
        int extraNights = night - earlyRequirements.Length;

        for (int i = 0; i < extraNights; i++)
        {
            value *= goldRequirementGrowthAfterNight5;
        }

        return Mathf.RoundToInt(value);
    }

    // Public accessor so other scripts can get this night's points requirement
    public virtual int GetGoldRequirement()
    {
        return CalculateGoldRequirement(GameManager.currentNight);
    }

    protected virtual void FindSpawners()
    {
        activeSpawners.Clear();
        Spawner[] found = FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        activeSpawners.AddRange(found);

        if (activeSpawners.Count == 0)
        {
            Debug.LogWarning("[NightManager] No Spawner objects found in the scene - nothing will be able to spawn this night.");
        }
    }

    protected virtual void BuildSpawnQueue()
    {
        spawnQueue.Clear();

        if (predatorPrefabs == null || predatorPrefabs.Count == 0)
        {
            Debug.LogWarning("[NightManager] No predator prefabs assigned - nothing will spawn this night.");
            return;
        }

        float remainingBudget = spawnBudget;
        int safetyLimit = 1000;
        int iterations = 0;

        while (remainingBudget > 0f && iterations < safetyLimit)
        {
            GameObject chosenPrefab = predatorPrefabs[Random.Range(0, predatorPrefabs.Count)];
            Fish fishComponent = chosenPrefab.GetComponent<Fish>();

            float cost = fishComponent != null ? fishComponent.spawnValue : 10f;

            spawnQueue.Add(chosenPrefab);
            remainingBudget -= cost;
            iterations++;
        }
    }

    protected virtual IEnumerator SpawnOverTime()
    {
        if (spawnQueue.Count == 0 || activeSpawners.Count == 0)
        {
            allPredatorsQueued = true;
            yield break;
        }

        float interval = nightLength / spawnQueue.Count;

        foreach (GameObject prefab in spawnQueue)
        {
            SpawnPredator(prefab);
            yield return new WaitForSeconds(interval);
        }

        allPredatorsQueued = true;
    }

    protected virtual void SpawnPredator(GameObject prefab)
    {
        Spawner chosenSpawner = activeSpawners[Random.Range(0, activeSpawners.Count)];
        Instantiate(prefab, chosenSpawner.transform.position, Quaternion.identity);
        GameManager.RegisterPredatorSpawn();
    }

    // Win/loss now checks totalPoints (never decreases) instead of totalGold (spendable, can decrease in shop)
    protected virtual void EvaluateWinLose()
    {
        int required = CalculateGoldRequirement(GameManager.currentNight);
        bool success = GameManager.totalPoints >= required;

        Debug.Log($"[NightManager] Night {GameManager.currentNight} complete. Points: {GameManager.totalPoints} / Required: {required} - {(success ? "WIN" : "GAME OVER")}");

        if (winLoseUI == null) return;

        if (success)
        {
            winLoseUI.ShowWin();
        }
        else
        {
            winLoseUI.ShowGameOver();
        }
    }
}