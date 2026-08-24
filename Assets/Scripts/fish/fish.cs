using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Fish : MonoBehaviour
{
    protected enum PulseState { Pulsing, Decelerating, Resting }

    [Header("Faction")]
    public bool isPredator = false;
    public bool hasUpgrade = false;
    public bool isGoldProducer = false;
    public bool isUntargetable = false;

    [Header("Spawn Economy")]
    public float spawnValue = 10f;

    [Header("Click Damage")]
    public int clickDamageAmount = 1;
    public int clickDamageHits = 1;

    [Header("Pulse Movement")]
    public float pulseSpeed = 4f;
    public float pulseDuration = 0.5f;
    public float decelDuration = 0.5f;
    public float restDuration = 0.3f;

    [Header("Rotation")]
    public float rotationSpeed = 720f;
    public float maxTiltAngle = 55f;

    [Header("Turn Flip")]
    public float flipDuration = 0.15f;
    public float squashAmount = 0.4f;

    [Header("Health")]
    public int maxHP = 2;
    protected int currentHP;

    protected Rigidbody2D rb;
    protected Vector2 targetDirection;
    protected PulseState currentState;
    protected float stateTimer;

    protected float baseScaleX;
    protected float baseScaleY;

    protected bool isFacingRight;
    protected bool isFlipping;
    protected bool pendingFacingRight;
    protected float flipTimer;
    protected bool hasSwappedThisFlip;

    protected bool isDoubleClickSource = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        baseScaleX = Mathf.Abs(transform.localScale.x);
        baseScaleY = Mathf.Abs(transform.localScale.y);

        isFacingRight = transform.localScale.x >= 0f;

        currentHP = maxHP;
    }

    protected virtual void Start()
    {
        PickNewDirection();
        EnterState(PulseState.Pulsing);
    }

    protected virtual void Update()
    {
        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case PulseState.Pulsing:
                if (stateTimer >= pulseDuration)
                    EnterState(PulseState.Decelerating);
                break;

            case PulseState.Decelerating:
                if (stateTimer >= decelDuration)
                    EnterState(PulseState.Resting);
                break;

            case PulseState.Resting:
                if (stateTimer >= restDuration)
                {
                    PickNewDirection();
                    EnterState(PulseState.Pulsing);
                }
                break;
        }

        FaceMovementDirection();
    }

    protected virtual void FixedUpdate()
    {
        switch (currentState)
        {
            case PulseState.Pulsing:
                {
                    float t = stateTimer / pulseDuration;
                    float easedT = 1f - Mathf.Pow(1f - t, 2f);
                    rb.linearVelocity = targetDirection * (pulseSpeed * easedT);
                    break;
                }

            case PulseState.Decelerating:
                {
                    float t = stateTimer / decelDuration;
                    float currentSpeed = Mathf.Lerp(pulseSpeed, 0f, t);
                    rb.linearVelocity = targetDirection * currentSpeed;
                    break;
                }

            case PulseState.Resting:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    protected virtual void EnterState(PulseState newState)
    {
        currentState = newState;
        stateTimer = 0f;
    }

    protected virtual void PickNewDirection()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        targetDirection = ClampToHorizontalTilt(randomDirection);
    }

    protected virtual Vector2 ClampToHorizontalTilt(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        bool facingRight = direction.x >= 0f;
        float reference = facingRight ? 0f : 180f;

        float deviation = Mathf.DeltaAngle(reference, angle);
        deviation = Mathf.Clamp(deviation, -maxTiltAngle, maxTiltAngle);

        float clampedAngle = reference + deviation;
        float rad = clampedAngle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    protected virtual void FaceMovementDirection()
    {
        if (targetDirection.sqrMagnitude < 0.0001f)
            return;

        bool desiredFacingRight = targetDirection.x >= 0f;

        if (desiredFacingRight != isFacingRight && !isFlipping)
        {
            isFlipping = true;
            flipTimer = 0f;
            pendingFacingRight = desiredFacingRight;
            hasSwappedThisFlip = false;
        }

        if (isFlipping)
        {
            UpdateTurnFlip();
        }

        float rawAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
        float reference = desiredFacingRight ? 0f : 180f;
        float deviation = Mathf.Clamp(Mathf.DeltaAngle(reference, rawAngle), -maxTiltAngle, maxTiltAngle);

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, deviation);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    protected virtual void UpdateTurnFlip()
    {
        flipTimer += Time.deltaTime;
        float t = Mathf.Clamp01(flipTimer / flipDuration);

        float squashT = 1f - Mathf.Abs((t * 2f) - 1f);
        float widthMultiplier = Mathf.Lerp(1f, squashAmount, squashT);

        Vector3 scale = transform.localScale;

        float unsignedX = baseScaleX * widthMultiplier;

        bool sideToUse = hasSwappedThisFlip ? pendingFacingRight : isFacingRight;
        scale.x = sideToUse ? unsignedX : -unsignedX;
        scale.y = baseScaleY;

        if (!hasSwappedThisFlip && t >= 0.5f)
        {
            hasSwappedThisFlip = true;
        }

        transform.localScale = scale;

        if (t >= 1f)
        {
            isFlipping = false;
            isFacingRight = pendingFacingRight;

            Vector3 finalScale = transform.localScale;
            finalScale.y = baseScaleY;
            finalScale.x = isFacingRight ? baseScaleX : -baseScaleX;
            transform.localScale = finalScale;
        }
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        DevTools.LogCollision(gameObject, collision.gameObject);

        if (collision.gameObject.name.Contains("Wall"))
        {
            Vector2 wallNormal = collision.GetContact(0).normal;
            Vector2 reflectedDirection = Vector2.Reflect(targetDirection, wallNormal).normalized;

            targetDirection = ClampToHorizontalTilt(reflectedDirection);

            EnterState(PulseState.Pulsing);
        }
    }

    public virtual bool TakeDamage(int amount, GameObject attacker = null)
    {
        currentHP -= amount;
        DevTools.LogDamage(attacker != null ? attacker : gameObject, gameObject, amount, currentHP);

        if (currentHP <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    protected virtual void Die()
    {
        if (isPredator)
        {
            GameManager.RegisterPredatorDeath();
        }

        if (isDoubleClickSource)
        {
            GameManager.UnregisterDoubleClickSource();
        }

        DevTools.LogDeath(gameObject);
        Destroy(gameObject);
    }

    public virtual void SetSpriteColor(Color color)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = color;
        }
    }

    public virtual void DoubleMaxHP()
    {
        maxHP *= 2;
        currentHP *= 2;
    }

    public virtual void DealClickDamageTo(Fish target, GameObject attacker = null)
    {
        if (target == null) return;

        for (int i = 0; i < clickDamageHits; i++)
        {
            target.TakeDamage(clickDamageAmount, attacker);
        }
    }

    public virtual void ActivateDoubleClickSource()
    {
        if (!isDoubleClickSource)
        {
            isDoubleClickSource = true;
            GameManager.RegisterDoubleClickSource();
        }
    }

    public virtual void TakeClickHits(int hits, GameObject attacker = null)
    {
        for (int i = 0; i < hits; i++)
        {
            TakeDamage(clickDamageAmount, attacker);
        }
    }
}