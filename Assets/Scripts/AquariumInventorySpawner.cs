using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges FishInventory (built in the Fishing scene) to the Aquarium scene.
/// On load, spawns one real fish object per fish actually caught, choosing the
/// correct prefab and configuring stats based on each species' FishData.
/// Assumes any placeholder fish have already been deleted manually from the scene.
/// </summary>
public class AquariumInventorySpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Used for fish with abilityType = MoneyGeneration.")]
    public GameObject goldFishPrefab;
    [Tooltip("Used for fish with any other abilityType (None, DamageBonus). Does not drop gold.")]
    public GameObject standardFishPrefab;

    [Header("Spawning")]
    [Tooltip("Roughly where fish should appear - fish will be spawned at random points within this box.")]
    public BoxCollider2D spawnArea;

    private void Start()
    {
        SpawnCaughtFish();
    }

    private void SpawnCaughtFish()
    {
        if (FishInventory.Instance == null)
        {
            Debug.LogWarning("[AquariumInventorySpawner] No FishInventory found. Did the Fishing scene run first?");
            return;
        }

        List<OwnedFish> caughtFish = FishInventory.Instance.GetAllFish();
        Debug.Log($"[AquariumInventorySpawner] Spawning {caughtFish.Count} caught fish.");

        foreach (OwnedFish owned in caughtFish)
        {
            SpawnFish(owned);
        }
    }

    private void SpawnFish(OwnedFish owned)
    {
        FishData species = owned.species;
        bool isMoneyFish = species.abilityType == FishAbilityType.MoneyGeneration;

        GameObject prefabToUse = isMoneyFish ? goldFishPrefab : standardFishPrefab;
        if (prefabToUse == null)
        {
            Debug.LogWarning($"[AquariumInventorySpawner] No prefab assigned for {species.speciesName} (abilityType: {species.abilityType}). Skipping.");
            return;
        }

        Vector3 spawnPosition = GetRandomSpawnPosition();
        GameObject fishObject = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);
        fishObject.name = species.speciesName;

        SpriteRenderer spriteRenderer = fishObject.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && species.sprite != null)
        {
            spriteRenderer.sprite = species.sprite;
        }
        else if (spriteRenderer == null)
        {
            Debug.LogWarning($"[AquariumInventorySpawner] No SpriteRenderer found on {species.speciesName}'s prefab (checked children too).");
        }
        else if (species.sprite == null)
        {
            Debug.LogWarning($"[AquariumInventorySpawner] {species.speciesName}'s FishData has no sprite assigned.");
        }

        Fish fishComponent = fishObject.GetComponent<Fish>();
        if (fishComponent != null)
        {
            fishComponent.isPredator = false;
            fishComponent.isUntargetable = false;

            if (species.abilityType == FishAbilityType.DamageBonus)
            {
                // abilityValue represents the bonus this fish contributes to click damage.
                fishComponent.clickDamageAmount += Mathf.RoundToInt(species.abilityValue);
            }
        }

        if (isMoneyFish)
        {
            GoldFish goldFishComponent = fishObject.GetComponent<GoldFish>();
            if (goldFishComponent != null)
            {
                // abilityValue represents this species' money-generation strength.
                goldFishComponent.goldMultiplier = Mathf.Max(1, Mathf.RoundToInt(species.abilityValue));
            }
        }

        // NOTE: baseHealth from FishData is intentionally not wired to Fish.maxHP yet.
        // Fish.currentHP is set from maxHP in Awake(), which runs during Instantiate()
        // itself - by the time we can access the spawned object, Awake has already run,
        // so setting maxHP here wouldn't correctly update currentHP too. Fixing this
        // properly needs a small change to Fish.cs (e.g. a public ResetHealth() method),
        // which is worth a follow-up conversation rather than a rushed workaround right now.
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (spawnArea == null)
        {
            return transform.position;
        }

        Bounds bounds = spawnArea.bounds;
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float y = Random.Range(bounds.min.y, bounds.max.y);
        return new Vector3(x, y, 0f);
    }
}
