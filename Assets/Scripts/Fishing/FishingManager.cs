using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the Fishing scene's core loop: Shop -> (Cast -> Delay -> Minigame -> Resolve) x5 -> Aquarium.
/// This is the central state machine other Fishing scripts (UI, minigame, upgrades) will hook into.
/// </summary>
public class FishingManager : MonoBehaviour
{
    private enum FishingState
    {
        Shop,
        WaitingForCast,
        Delay,
        Minigame,
        Resolve,
        Complete
    }

    [Header("Fish Pool")]
    [Tooltip("All fish species that can be caught. Drag in your FishData assets here.")]
    public List<FishData> fishPool;

    [Header("Timing")]
    [Tooltip("Seconds between casting and the catch minigame starting.")]
    public float delayDuration = 1.5f;

    [Header("Scene Transition")]
    [Tooltip("Name of the scene to load once all casts are used. Must be added to Build Settings.")]
    public string aquariumSceneName = "Aquarium";

    [Header("Minigame")]
    [Tooltip("Drag the GameObject holding FishingMinigameController here.")]
    public FishingMinigameController minigameController;

    [Header("UI Timing")]
    [Tooltip("How long the catch result stays visible before the next cast can begin.")]
    public float catchResultDisplayDuration = 1.5f;

    private float baseDelayDuration;

    // Fired whenever castsRemaining changes, including the initial value at Start().
    public event Action<int> OnCastsRemainingChanged;
    // Fired the moment a fish is caught, passing which species.
    public event Action<FishData> OnFishCaught;
    // Fired when the Shop state begins - UI should show the shop panel.
    public event Action OnShopEntered;

    private FishingState currentState;
    private int castsRemaining = 5;
    private const int MaxCasts = 5;
    private bool lastMinigameSuccess;

    private void Start()
    {
        baseDelayDuration = delayDuration;
        ApplyPurchasedUpgrades();
        castsRemaining = MaxCasts;
        OnCastsRemainingChanged?.Invoke(castsRemaining);
        ChangeState(FishingState.Shop);
    }

    /// <summary>
    /// Re-reads current upgrade levels and applies their effects. Safe to call multiple
    /// times (e.g. right after a purchase in the shop, not just once at scene start) -
    /// always recalculates from baseDelayDuration rather than mutating delayDuration
    /// cumulatively, so repeated calls at the same level don't stack incorrectly.
    /// </summary>
    public void ApplyPurchasedUpgrades()
    {
        if (FishingUpgradeManager.Instance == null) return;

        foreach (FishingUpgradeData upgrade in FishingUpgradeManager.Instance.upgrades)
        {
            int level = FishingUpgradeManager.Instance.GetLevel(upgrade);

            switch (upgrade.upgradeType)
            {
                case FishingUpgradeType.CastRange:
                    delayDuration = Mathf.Max(0.1f, baseDelayDuration - (upgrade.effectPerLevel * level));
                    break;
                case FishingUpgradeType.LineStrength:
                    if (minigameController != null)
                        minigameController.lineStrengthLevel = level;
                    break;
                    // BaitQuality is read directly by GetRandomWeightedFish() below rather than
                    // modifying a field here, since it affects per-catch weighting math.
            }
        }
    }

    private void Update()
    {
        // Only listen for cast input while actually waiting for one.
        if (currentState == FishingState.WaitingForCast
            && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ChangeState(FishingState.Delay);
        }
    }

    private void ChangeState(FishingState newState)
    {
        currentState = newState;
        Debug.Log($"[FishingManager] State changed to: {newState}");

        switch (newState)
        {
            case FishingState.Shop:
                HandleShop();
                break;
            case FishingState.WaitingForCast:
                // Nothing to do here yet, just waiting for Update() to catch the click.
                break;
            case FishingState.Delay:
                StartCoroutine(HandleDelay());
                break;
            case FishingState.Minigame:
                HandleMinigame();
                break;
            case FishingState.Resolve:
                StartCoroutine(HandleResolveRoutine());
                break;
            case FishingState.Complete:
                HandleComplete();
                break;
        }
    }

    private void HandleShop()
    {
        // Wait here until FishingUIController calls ConfirmShopAndStartFishing()
        // (hooked up to the "Start Fishing" button's onClick).
        OnShopEntered?.Invoke();
    }

    /// <summary>
    /// Called by the UI's "Start Fishing" button to close the shop and begin casting.
    /// </summary>
    public void ConfirmShopAndStartFishing()
    {
        if (currentState == FishingState.Shop)
        {
            ChangeState(FishingState.WaitingForCast);
        }
    }

    private IEnumerator HandleDelay()
    {
        yield return new WaitForSeconds(delayDuration);
        ChangeState(FishingState.Minigame);
    }

    private void HandleMinigame()
    {
        if (minigameController == null)
        {
            Debug.LogWarning("[FishingManager] No FishingMinigameController assigned. Skipping straight to Resolve.");
            lastMinigameSuccess = false;
            ChangeState(FishingState.Resolve);
            return;
        }

        minigameController.StartMinigame(success =>
        {
            lastMinigameSuccess = success;
            ChangeState(FishingState.Resolve);
        });
    }

    private IEnumerator HandleResolveRoutine()
    {
        FishData caughtFish = GetRandomWeightedFish(lastMinigameSuccess);

        if (caughtFish != null && FishInventory.Instance != null)
        {
            FishInventory.Instance.AddFish(caughtFish);
        }

        OnFishCaught?.Invoke(caughtFish);

        castsRemaining--;
        OnCastsRemainingChanged?.Invoke(castsRemaining);
        Debug.Log($"[FishingManager] Casts remaining: {castsRemaining}");

        yield return new WaitForSeconds(catchResultDisplayDuration);

        if (castsRemaining > 0)
        {
            ChangeState(FishingState.WaitingForCast);
        }
        else
        {
            ChangeState(FishingState.Complete);
        }
    }

    private void HandleComplete()
    {
        Debug.Log("[FishingManager] All casts used. Loading Aquarium scene...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(aquariumSceneName);
    }

    /// <summary>
    /// Returns how strongly Bait Quality should nudge odds toward rarer fish on a normal
    /// (non-minigame-success) catch, as a 0-1 blend factor. Capped so it never fully
    /// guarantees rare fish outright - that's still reserved for succeeding the minigame.
    /// </summary>
    private float GetBaitQualityBias()
    {
        if (FishingUpgradeManager.Instance == null) return 0f;

        foreach (FishingUpgradeData upgrade in FishingUpgradeManager.Instance.upgrades)
        {
            if (upgrade.upgradeType == FishingUpgradeType.BaitQuality)
            {
                int level = FishingUpgradeManager.Instance.GetLevel(upgrade);
                return Mathf.Clamp01(level * upgrade.effectPerLevel);
            }
        }

        return 0f;
    }

    /// <summary>
    /// Picks a random fish from the pool, weighted by each fish's catchWeight.
    /// Higher catchWeight = more likely to be selected under normal odds.
    /// If biasTowardRare is true (minigame succeeded), weighting is fully inverted so
    /// rarer fish (lower catchWeight) become more likely. Bait Quality upgrade levels
    /// also nudge odds toward rare fish even without a minigame success, by a smaller amount.
    /// </summary>
    private FishData GetRandomWeightedFish(bool biasTowardRare)
    {
        if (fishPool == null || fishPool.Count == 0)
        {
            Debug.LogWarning("[FishingManager] Fish pool is empty. Assign FishData assets in the Inspector.");
            return null;
        }

        float blend = biasTowardRare ? 1f : GetBaitQualityBias();

        float totalWeight = 0f;
        foreach (FishData fish in fishPool)
        {
            totalWeight += GetBlendedWeight(fish, blend);
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (FishData fish in fishPool)
        {
            cumulative += GetBlendedWeight(fish, blend);
            if (roll <= cumulative)
            {
                return fish;
            }
        }

        // Fallback in case of floating point rounding at the very edge.
        return fishPool[fishPool.Count - 1];
    }

    private float GetBlendedWeight(FishData fish, float blend)
    {
        float normalWeight = fish.catchWeight;
        float rarityWeight = 1f / Mathf.Max(fish.catchWeight, 0.01f);
        return Mathf.Lerp(normalWeight, rarityWeight, blend);
    }
}
