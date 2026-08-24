using UnityEngine;
using System.Collections.Generic;

public class Alien : Fish
{
    [Header("Floaty Movement")]
    public float floatSpeed = 1.2f;
    public float velocitySmoothing = 2f;

    [Header("Zigzag")]
    public float zigzagFrequency = 1.5f;
    public float zigzagAmplitude = 0.6f;

    [Header("Seeking")]
    public float seekStrength = 0.7f;
    public float clumsyNoise = 0.4f;

    [Header("Eating")]
    public float eatCooldown = 2f;
    [Range(0f, 100f)]
    public float eatChancePercent = 25f;
    public int damagePerAttack = 1;

    [Header("Target Tracking")]
    public float targetCheckInterval = 1f;

    [Header("Bug Spawn on Death")]
    public GameObject bugPrefab;
    public int minBugsToSpawn = 1;
    public int maxBugsToSpawn = 3;
    public float spawnScatterRadius = 0.5f;

    protected Fish targetFish;
    protected float zigzagTimer;
    protected bool isTouchingTarget;
    protected float eatCooldownTimer = 0f;
    protected float targetCheckTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        targetDirection = Random.insideUnitCircle.normalized;
        AcquireRandomTarget();
    }

    protected override void Update()
    {
        if (targetFish == null)
        {
            AcquireRandomTarget();
        }
        else if (targetFish.isUntargetable)
        {
            DevTools.LogTargetLost(gameObject, targetFish.name);
            targetFish = null;
            isTouchingTarget = false;
            AcquireRandomTarget();
        }
        else
        {
            targetCheckTimer += Time.deltaTime;
            if (targetCheckTimer >= targetCheckInterval)
            {
                targetCheckTimer = 0f;

                if (targetFish == null || targetFish.gameObject == null)
                {
                    DevTools.LogTargetLost(gameObject, "target became invalid");
                    AcquireRandomTarget();
                }
            }
        }

        UpdateZigzagDirection();
        FaceMovementDirection();

        HandleEating();
    }

    protected override void FixedUpdate()
    {
        Vector2 desiredVelocity = targetDirection * floatSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, velocitySmoothing * Time.fixedDeltaTime);
    }

    protected virtual void AcquireRandomTarget()
    {
        string previousTargetName = targetFish != null ? targetFish.name : null;

        Fish[] allFish = FindObjectsByType<Fish>(FindObjectsSortMode.None);

        List<Fish> validTargets = new List<Fish>();
        foreach (Fish fish in allFish)
        {
            if (fish == this) continue;
            if (fish.isPredator) continue;
            if (fish.isUntargetable) continue;
            validTargets.Add(fish);
        }

        if (validTargets.Count > 0)
        {
            int randomIndex = Random.Range(0, validTargets.Count);
            targetFish = validTargets[randomIndex];
            DevTools.LogTargetAcquired(gameObject, targetFish.gameObject);
        }
        else
        {
            if (previousTargetName != null)
            {
                DevTools.LogTargetLost(gameObject, previousTargetName);
            }
            targetFish = null;
        }

        isTouchingTarget = false;
        targetCheckTimer = 0f;
    }

    protected virtual void UpdateZigzagDirection()
    {
        zigzagTimer += Time.deltaTime * zigzagFrequency;

        Vector2 perpendicular = new Vector2(-targetDirection.y, targetDirection.x);
        Vector2 zigzagOffset = perpendicular * Mathf.Sin(zigzagTimer) * zigzagAmplitude;

        Vector2 baseDirection = targetDirection;

        if (targetFish != null)
        {
            Vector2 towardTarget = ((Vector2)targetFish.transform.position - (Vector2)transform.position).normalized;

            Vector2 clumsyOffset = Random.insideUnitCircle * clumsyNoise;
            Vector2 noisySeek = (towardTarget + clumsyOffset).normalized;

            baseDirection = Vector2.Lerp(baseDirection, noisySeek, seekStrength).normalized;
        }

        Vector2 combined = (baseDirection + zigzagOffset).normalized;
        targetDirection = ClampToHorizontalTilt(combined);
    }

    protected virtual void HandleEating()
    {
        if (eatCooldownTimer > 0f)
        {
            eatCooldownTimer -= Time.deltaTime;
        }

        if (targetFish == null || !isTouchingTarget)
            return;

        if (eatCooldownTimer <= 0f)
        {
            AttemptEat();
        }
    }

    protected virtual void AttemptEat()
    {
        float roll = Random.Range(0f, 100f);
        bool success = roll <= eatChancePercent;

        DevTools.LogEatAttempt(gameObject, targetFish.gameObject, roll, eatChancePercent, success);

        eatCooldownTimer = eatCooldown;

        if (success)
        {
            EatTarget();
        }
    }

    protected virtual void EatTarget()
    {
        if (targetFish == null) return;

        string eatenName = targetFish.name;
        bool killed = targetFish.TakeDamage(damagePerAttack, gameObject);

        if (killed)
        {
            DevTools.LogTargetLost(gameObject, eatenName);
            targetFish = null;
            isTouchingTarget = false;
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        DevTools.LogCollision(gameObject, collision.gameObject);

        if (targetFish != null && collision.gameObject == targetFish.gameObject)
        {
            isTouchingTarget = true;

            if (eatCooldownTimer <= 0f)
            {
                AttemptEat();
            }
        }
    }

    protected virtual void OnCollisionExit2D(Collision2D collision)
    {
        if (targetFish != null && collision.gameObject == targetFish.gameObject)
        {
            isTouchingTarget = false;
        }
    }

    protected override void Die()
    {
        SpawnBugs();
        base.Die();
    }

    protected virtual void SpawnBugs()
    {
        if (bugPrefab == null)
            return;

        int bugCount = Random.Range(minBugsToSpawn, maxBugsToSpawn + 1);

        for (int i = 0; i < bugCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * spawnScatterRadius;
            Vector3 spawnPosition = transform.position + (Vector3)randomOffset;

            Instantiate(bugPrefab, spawnPosition, Quaternion.identity);
            GameManager.RegisterPredatorSpawn(); // bonus spawn counts toward the night's total too
        }
    }
}