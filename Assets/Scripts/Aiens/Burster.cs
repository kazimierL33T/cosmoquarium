using UnityEngine;

// Inherits everything from Alien - movement, seeking, eating, wall pass-through, health.
// Only difference: on death, explodes outward into a burst of projectiles instead of
// (or alongside) any other death behavior.
public class Burster : Alien
{
    [Header("Burst on Death")]
    public GameObject burstProjectilePrefab;   // assign your projectile prefab here
    public int minProjectiles = 4;             // minimum projectiles spawned on death
    public int maxProjectiles = 8;             // maximum projectiles spawned on death (inclusive)
    public float projectileSpeed = 5f;         // how fast each projectile travels outward

    [Header("Burst Projectile Behavior")]
    [Range(0f, 100f)]
    public float burstEatChancePercent = 25f;  // chance each projectile successfully "eats" the fish it hits
    public int burstDamagePerAttack = 1;       // damage dealt to a fish if the eat chance succeeds

    // Overrides Alien's Die() to spawn the projectile burst before this object is destroyed
    protected override void Die()
    {
        SpawnBurst();
        base.Die(); // still runs Alien's Die() logic (which may itself call Fish's - handles DevTools log + Destroy)
    }

    // Spawns projectiles in random directions, each flying outward from this position
    protected virtual void SpawnBurst()
    {
        if (burstProjectilePrefab == null)
            return; // no prefab assigned - skip silently rather than erroring

        int projectileCount = Random.Range(minProjectiles, maxProjectiles + 1); // +1 because max is exclusive in Random.Range for ints

        for (int i = 0; i < projectileCount; i++)
        {
            // Fully random direction instead of evenly-spaced around a circle
            Vector2 direction = Random.insideUnitCircle.normalized;

            GameObject projectileObj = Instantiate(burstProjectilePrefab, transform.position, Quaternion.identity);

            BurstProjectile projectileScript = projectileObj.GetComponent<BurstProjectile>();
            if (projectileScript != null)
            {
                projectileScript.Initialize(direction, projectileSpeed, burstDamagePerAttack, burstEatChancePercent, gameObject);
            }
        }
    }
}
