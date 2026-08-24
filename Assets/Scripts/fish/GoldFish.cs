using UnityEngine;

// Inherits from Fish - behaves exactly like a normal fish (pulse movement, tilt, wall bounce, HP),
// but periodically drops a Gold prefab on a timer, scaled by this fish's permanent gold multiplier.
public class GoldFish : Fish
{
    [Header("Gold Drop")]
    public GameObject goldPrefab;      // assign your Gold prefab here
    public float dropInterval = 5f;    // seconds between each gold drop
    public int goldMultiplier = 1;     // permanent multiplier applied to each dropped Gold's value - doubled by the Double Gold upgrade

    protected float dropTimer = 0f;

    protected override void Update()
    {
        base.Update(); // keeps all normal Fish pulse/movement/rotation behavior

        dropTimer += Time.deltaTime;
        if (dropTimer >= dropInterval)
        {
            dropTimer = 0f;
            DropGold();
        }
    }

    // Spawns a Gold object at this fish's current position, applying this fish's gold multiplier
    protected virtual void DropGold()
    {
        if (goldPrefab == null)
            return; // no prefab assigned - skip silently rather than erroring

        GameObject spawned = Instantiate(goldPrefab, transform.position, Quaternion.identity);

        Gold goldScript = spawned.GetComponent<Gold>();
        if (goldScript != null)
        {
            goldScript.goldValue *= goldMultiplier;
        }
    }
}