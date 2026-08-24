using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class GameManager
{
    public static int totalGold = 0;
    public static int totalPoints = 0; // never decreases - tracks total gold ever collected, used for win/loss (spending gold doesn't affect this)
    public static int currentNight = 1;

    public static Dictionary<string, int> ownedUpgrades = new Dictionary<string, int>();

    public static int doubleClickSourceCount = 0;

    public static int totalPredatorsSpawned = 0;
    public static int totalPredatorsKilled = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        totalGold = 0;
        totalPoints = 0;
        currentNight = 1;
        ownedUpgrades.Clear();
        doubleClickSourceCount = 0;
        totalPredatorsSpawned = 0;
        totalPredatorsKilled = 0;

        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == "Aquarium")
        {
            currentNight += 1;
            Debug.Log($"[GameManager] Left Aquarium - now Night {currentNight}");
        }
    }

    public static bool TrySpendGold(int amount)
{
    if (amount <= 0 || totalGold < amount)
    {
        return false;
    }

    totalGold -= amount;
    return true;
}

    public static void AddGold(int amount)
    {
        totalGold += amount;
        totalPoints += amount; // points track total collected, unaffected by future spending
        Debug.Log($"[GameManager] Gold added: +{amount}. Total gold: {totalGold}, Total points: {totalPoints}");
    }

    public static void AddUpgrade(string upgradeId, int amount = 1)
    {
        if (ownedUpgrades.ContainsKey(upgradeId))
        {
            ownedUpgrades[upgradeId] += amount;
        }
        else
        {
            ownedUpgrades[upgradeId] = amount;
        }

        Debug.Log($"[GameManager] Added upgrade '{upgradeId}'. Now own: {ownedUpgrades[upgradeId]}");
    }

    public static bool RemoveUpgrade(string upgradeId, int amount = 1)
    {
        if (ownedUpgrades.ContainsKey(upgradeId) && ownedUpgrades[upgradeId] >= amount)
        {
            ownedUpgrades[upgradeId] -= amount;

            if (ownedUpgrades[upgradeId] <= 0)
            {
                ownedUpgrades.Remove(upgradeId);
            }

            Debug.Log($"[GameManager] Removed upgrade '{upgradeId}' (eaten by fish).");
            return true;
        }

        Debug.LogWarning($"[GameManager] Tried to remove upgrade '{upgradeId}' but it wasn't in inventory.");
        return false;
    }

    public static void RegisterDoubleClickSource()
    {
        doubleClickSourceCount++;
        Debug.Log($"[GameManager] Double Click source registered. Active sources: {doubleClickSourceCount}");
    }

    public static void UnregisterDoubleClickSource()
    {
        doubleClickSourceCount = Mathf.Max(0, doubleClickSourceCount - 1);
        Debug.Log($"[GameManager] Double Click source unregistered. Active sources: {doubleClickSourceCount}");
    }

    public static bool IsDoubleClickActive => doubleClickSourceCount > 0;

    public static void RegisterPredatorSpawn()
    {
        totalPredatorsSpawned++;
    }

    public static void RegisterPredatorDeath()
    {
        totalPredatorsKilled++;
    }
}