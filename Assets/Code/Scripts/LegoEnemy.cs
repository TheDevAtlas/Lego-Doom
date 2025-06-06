using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WeaponGroup
{
    [Header("Weapon Group Settings")]
    public string groupName = "Weapon Group";
    public List<GameObject> parts = new List<GameObject>();
    public int hitsToDetach = 3;
    public bool canShoot = true;
    
    [Header("Shooting Settings")]
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float shootInterval = 2f;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;
    public float shootRange = 20f;
    
    [Header("Projectile Visual Settings")]
    public Vector3 projectileScale = new Vector3(4f, 4f, 4f);
    
    [Header("Detachment Settings")]
    public float detachForce = 300f;
    public float detachRadius = 3f;
    public float detachUpward = 2f;
    
    // Runtime variables
    [System.NonSerialized]
    public int currentHits = 0;
    [System.NonSerialized]
    public bool isDetached = false;
    [System.NonSerialized]
    public Coroutine shootCoroutine;
    [System.NonSerialized]
    public Transform player;
}

[System.Serializable]
public class MovementSettings
{
    [Header("Jump Points")]
    public List<Transform> jumpPoints = new List<Transform>();
    public float jumpSpeed = 5f;
    public float jumpHeight = 3f;
    public float jumpCooldown = 2f;
    
    [Header("Walk Points")]
    public List<Transform> walkPoints = new List<Transform>();
    public float walkSpeed = 2f;
    public float walkStoppingDistance = 0.5f;
    
    [Header("Movement Behavior")]
    public float idleTime = 1f;
    public bool randomizeMovement = true;
    public float movementDecisionInterval = 3f;
}

public class LegoEnemy : MonoBehaviour
{
    [Header("Enemy Parts")]
    public List<GameObject> colliderParts = new List<GameObject>();
    
    [Header("Weapon Groups")]
    public List<WeaponGroup> weaponGroups = new List<WeaponGroup>();
    
    [Header("Flash Settings")]
    public Color flashColor = Color.red;
    public float flashDuration = 0.5f;
    public float flashIntensity = 2f;
    
    [Header("Health")]
    public int maxHealth = 100;
    public int damagePerHit = 25;
    public int weaponGroupDamage = 12;
    
    [Header("Death Explosion")]
    public float explosionForce = 500f;
    public float explosionRadius = 5f;
    public float upwardModifier = 3f;
    public float destructionTime = 5f;
    
    [Header("Animation Settings")]
    public Animator enemyAnimator;
    public float punchRange = 3f;
    public float punchInterval = 1.5f;
    public float damageReactionDuration = 0.5f;
    public bool isFlying = false;
    
    [Header("Movement")]
    public MovementSettings movement = new MovementSettings();
    
    [Header("Player Facing")]
    public bool facePlayer = true;
    public float facingSpeed = 1.2f;
    
    private int currentHealth;
    private Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();
    private Dictionary<GameObject, Coroutine> flashCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<GameObject, WeaponGroup> partToWeaponGroup = new Dictionary<GameObject, WeaponGroup>();
    private Transform playerTransform;
    
    // Animation variables
    private float lastPunchTime;
    private bool isReacting = false;
    private float lastReactionTime;
    
    // Movement variables
    private Vector3 currentTarget;
    private bool isMoving = false;
    private bool isJumping = false;
    private float lastJumpTime;
    private float lastMovementDecision;
    private int currentWalkPointIndex = -1;
    
    void Start()
    {
        currentHealth = maxHealth;
        FindPlayer();
        InitializeParts();
        InitializeWeaponGroups();
        InitializeAnimator();
        StartMovementBehavior();
    }
    
    void Update()
    {
        UpdateAnimations();
        UpdateMovement();
        UpdatePlayerFacing();
        CheckPunchAttack();
    }
    
    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }
    
    void InitializeParts()
    {
        foreach (GameObject part in colliderParts)
        {
            if (part != null)
            {
                SetupPart(part);
            }
        }
        
        foreach (WeaponGroup group in weaponGroups)
        {
            foreach (GameObject part in group.parts)
            {
                if (part != null)
                {
                    SetupPart(part);
                    partToWeaponGroup[part] = group;
                }
            }
        }
    }
    
    void SetupPart(GameObject part)
    {
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            originalMaterials[part] = renderer.material;
        }
        
        LegoEnemyPart partScript = part.GetComponent<LegoEnemyPart>();
        if (partScript == null)
        {
            partScript = part.AddComponent<LegoEnemyPart>();
        }
        partScript.parentEnemy = this;
        
        if (part.GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"Part {part.name} doesn't have a collider!");
        }
    }
    
    void InitializeWeaponGroups()
    {
        foreach (WeaponGroup group in weaponGroups)
        {
            group.player = playerTransform;
            
            if (group.canShoot && !group.isDetached)
            {
                group.shootCoroutine = StartCoroutine(WeaponGroupShootCoroutine(group));
            }
        }
    }
    
    void InitializeAnimator()
    {
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponent<Animator>();
        }
        
        if (enemyAnimator != null)
        {
            // Set initial floating state
            enemyAnimator.SetBool("IsFloating", isFlying);
        }
    }
    
    void StartMovementBehavior()
    {
        if (movement.jumpPoints.Count > 0 || movement.walkPoints.Count > 0)
        {
            StartCoroutine(MovementDecisionCoroutine());
        }
    }
    
    void UpdateAnimations()
    {
        if (enemyAnimator == null) return;
        
        // Update floating animation
        enemyAnimator.SetBool("IsFloating", isFlying);
        
        // Reset reaction animation after duration
        if (isReacting && Time.time - lastReactionTime > damageReactionDuration)
        {
            isReacting = false;
            enemyAnimator.SetBool("IsReacting", false);
        }
    }
    
    void UpdateMovement()
    {
        if (isMoving && !isJumping)
        {
            // Handle walking movement
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
            
            if (distanceToTarget > movement.walkStoppingDistance)
            {
                Vector3 direction = (currentTarget - transform.position).normalized;
                transform.position += direction * movement.walkSpeed * Time.deltaTime;
                
                // Face movement direction only when moving and not facing player
                if (direction != Vector3.zero && !facePlayer)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            else
            {
                isMoving = false;
            }
        }
    }
    
    void UpdatePlayerFacing()
    {
        if (!facePlayer || playerTransform == null) return;
        
        // Don't face player during jumping as it looks weird
        if (isJumping) return;
        
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        
        // Only rotate on Y axis to prevent tilting
        directionToPlayer.y = 0;
        
        if (directionToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            float rotationSpeed = 1f / facingSpeed; // Convert lag to speed
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    void CheckPunchAttack()
    {
        if (playerTransform == null || enemyAnimator == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distanceToPlayer <= punchRange && Time.time - lastPunchTime >= punchInterval)
        {
            PerformPunchAttack();
        }
    }
    
    void PerformPunchAttack()
    {
        lastPunchTime = Time.time;
        
        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("IsPunching", true);
            Debug.Log("Setting IsPunching to true");
            StartCoroutine(ResetPunchAnimation());
        }
        
        Debug.Log($"{gameObject.name} performed punch attack!");
        
        // Add punch damage logic here if needed
        // You could raycast or use a trigger collider to detect if punch hits player
    }
    
    IEnumerator ResetPunchAnimation()
    {
        yield return new WaitForSeconds(0.5f); // Adjust based on your animation length
        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("IsPunching", false);
            Debug.Log("Setting IsPunching to false");
        }
    }
    
    IEnumerator MovementDecisionCoroutine()
    {
        while (currentHealth > 0)
        {
            yield return new WaitForSeconds(movement.movementDecisionInterval);
            
            if (!isMoving && !isJumping)
            {
                DecideMovement();
            }
        }
    }
    
    void DecideMovement()
    {
        if (movement.randomizeMovement)
        {
            // Randomly choose between jumping and walking
            bool shouldJump = Random.Range(0f, 1f) > 0.5f && movement.jumpPoints.Count > 0;
            
            if (shouldJump && Time.time - lastJumpTime >= movement.jumpCooldown)
            {
                JumpToRandomPoint();
            }
            else if (movement.walkPoints.Count > 0)
            {
                WalkToRandomPoint();
            }
        }
        else
        {
            // Sequential movement through points
            if (currentWalkPointIndex == -1 && movement.walkPoints.Count > 0)
            {
                currentWalkPointIndex = 0;
            }
            
            if (movement.walkPoints.Count > 0)
            {
                WalkToPoint(movement.walkPoints[currentWalkPointIndex]);
                currentWalkPointIndex = (currentWalkPointIndex + 1) % movement.walkPoints.Count;
            }
        }
    }
    
    void JumpToRandomPoint()
    {
        if (movement.jumpPoints.Count == 0) return;
        
        Transform targetPoint = movement.jumpPoints[Random.Range(0, movement.jumpPoints.Count)];
        StartCoroutine(JumpToPoint(targetPoint));
    }
    
    void WalkToRandomPoint()
    {
        if (movement.walkPoints.Count == 0) return;
        
        Transform targetPoint = movement.walkPoints[Random.Range(0, movement.walkPoints.Count)];
        WalkToPoint(targetPoint);
    }
    
    void WalkToPoint(Transform targetPoint)
    {
        currentTarget = targetPoint.position;
        isMoving = true;
    }
    
    IEnumerator JumpToPoint(Transform targetPoint)
    {
        isJumping = true;
        lastJumpTime = Time.time;
        
        Vector3 startPos = transform.position;
        Vector3 endPos = targetPoint.position;
        float jumpDuration = Vector3.Distance(startPos, endPos) / movement.jumpSpeed;
        
        float elapsed = 0f;
        
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpDuration;
            
            // Parabolic jump trajectory
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * movement.jumpHeight;
            
            transform.position = currentPos;
            
            // Face jump direction only if not facing player
            if (!facePlayer)
            {
                Vector3 direction = (endPos - startPos).normalized;
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            
            yield return null;
        }
        
        transform.position = endPos;
        isJumping = false;
        
        // Wait for idle time after landing
        yield return new WaitForSeconds(movement.idleTime);
    }
    
    public void OnPartHit(GameObject hitPart, Collision collision = null)
    {
        // Trigger damage reaction animation
        if (enemyAnimator != null)
        {
            isReacting = true;
            lastReactionTime = Time.time;
            enemyAnimator.SetBool("IsReacting", true);
            Debug.Log("Setting IsReacting to true");
        }
        
        if (partToWeaponGroup.ContainsKey(hitPart))
        {
            WeaponGroup weaponGroup = partToWeaponGroup[hitPart];
            OnWeaponGroupHit(weaponGroup, hitPart);
        }
        else
        {
            TakeDamage(damagePerHit);
            FlashPart(hitPart);
            Debug.Log($"{hitPart.name} was hit! Health: {currentHealth}/{maxHealth}");
        }
    }
    
    void OnWeaponGroupHit(WeaponGroup weaponGroup, GameObject hitPart)
    {
        if (weaponGroup.isDetached) return;
        
        weaponGroup.currentHits++;
        FlashPart(hitPart);
        TakeDamage(weaponGroupDamage);
        
        Debug.Log($"Weapon group '{weaponGroup.groupName}' hit! Hits: {weaponGroup.currentHits}/{weaponGroup.hitsToDetach}");
        
        if (weaponGroup.currentHits >= weaponGroup.hitsToDetach)
        {
            DetachWeaponGroup(weaponGroup);
        }
    }
    
    void DetachWeaponGroup(WeaponGroup weaponGroup)
    {
        if (weaponGroup.isDetached) return;
        
        weaponGroup.isDetached = true;
        
        if (weaponGroup.shootCoroutine != null)
        {
            StopCoroutine(weaponGroup.shootCoroutine);
            weaponGroup.shootCoroutine = null;
        }
        
        Debug.Log($"Weapon group '{weaponGroup.groupName}' detached!");
        
        Vector3 detachCenter = CalculateGroupCenter(weaponGroup);
        
        foreach (GameObject part in weaponGroup.parts)
        {
            if (part != null)
            {
                DetachPart(part, detachCenter, weaponGroup);
            }
        }
    }
    
    Vector3 CalculateGroupCenter(WeaponGroup weaponGroup)
    {
        Vector3 center = Vector3.zero;
        int validParts = 0;
        
        foreach (GameObject part in weaponGroup.parts)
        {
            if (part != null)
            {
                center += part.transform.position;
                validParts++;
            }
        }
        
        if (validParts > 0)
            center /= validParts;
        else
            center = transform.position;
            
        return center;
    }
    
    void DetachPart(GameObject part, Vector3 detachCenter, WeaponGroup weaponGroup)
    {
        part.transform.SetParent(null);
        
        Rigidbody rb = part.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = part.AddComponent<Rigidbody>();
        }
        
        rb.isKinematic = false;
        rb.AddExplosionForce(weaponGroup.detachForce, detachCenter, weaponGroup.detachRadius, weaponGroup.detachUpward, ForceMode.Impulse);
        
        Vector3 randomTorque = new Vector3(
            Random.Range(-5f, 5f),
            Random.Range(-5f, 5f),
            Random.Range(-5f, 5f)
        );
        rb.AddTorque(randomTorque, ForceMode.Impulse);
        
        LegoPartExplosion explosionHandler = part.AddComponent<LegoPartExplosion>();
        explosionHandler.Initialize(destructionTime);
        
        if (partToWeaponGroup.ContainsKey(part))
        {
            partToWeaponGroup.Remove(part);
        }
    }
    
    IEnumerator WeaponGroupShootCoroutine(WeaponGroup weaponGroup)
    {
        while (!weaponGroup.isDetached && currentHealth > 0)
        {
            yield return new WaitForSeconds(weaponGroup.shootInterval);
            
            if (!weaponGroup.isDetached && currentHealth > 0)
            {
                ShootProjectile(weaponGroup);
            }
        }
    }
    
    void ShootProjectile(WeaponGroup weaponGroup)
    {
        if (weaponGroup.projectilePrefab == null || weaponGroup.shootPoint == null)
        {
            Debug.LogWarning("Missing projectile prefab or shoot point!");
            return;
        }
            
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(weaponGroup.shootPoint.position, playerTransform.position);
            if (distanceToPlayer > weaponGroup.shootRange)
            {
                return;
            }
        }
        
        GameObject projectile = Instantiate(weaponGroup.projectilePrefab, weaponGroup.shootPoint.position, weaponGroup.shootPoint.rotation);
        
        // Apply custom scale
        projectile.transform.localScale = weaponGroup.projectileScale;
        
        LegoProjectile projectileScript = projectile.GetComponent<LegoProjectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<LegoProjectile>();
        }
        
        Vector3 shootDirection = weaponGroup.shootPoint.forward;
        if (playerTransform != null)
        {
            shootDirection = (playerTransform.position - weaponGroup.shootPoint.position).normalized;
        }
        
        projectileScript.Initialize(shootDirection, weaponGroup.projectileSpeed, weaponGroup.projectileLifetime);
        
        Debug.Log($"Weapon group '{weaponGroup.groupName}' fired projectile!");
    }
    
    void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    void FlashPart(GameObject part)
    {
        if (part == null || !originalMaterials.ContainsKey(part))
            return;
            
        if (flashCoroutines.ContainsKey(part) && flashCoroutines[part] != null)
        {
            StopCoroutine(flashCoroutines[part]);
        }
        
        flashCoroutines[part] = StartCoroutine(FlashCoroutine(part));
    }
    
    IEnumerator FlashCoroutine(GameObject part)
    {
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null) yield break;
        
        Material originalMat = originalMaterials[part];
        Material flashMat = new Material(originalMat);
        
        flashMat.EnableKeyword("_EMISSION");
        flashMat.SetColor("_EmissionColor", flashColor * flashIntensity);
        
        renderer.material = flashMat;
        
        float elapsed = 0f;
        
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            
            Color currentEmission = Color.Lerp(flashColor * flashIntensity, Color.black, t);
            flashMat.SetColor("_EmissionColor", currentEmission);
            
            yield return null;
        }
        
        renderer.material = originalMat;
        Destroy(flashMat);
        flashCoroutines[part] = null;
    }
    
    public void Die()
    {
        Debug.Log($"{gameObject.name} has been destroyed!");
        
        foreach (WeaponGroup group in weaponGroups)
        {
            if (group.shootCoroutine != null)
            {
                StopCoroutine(group.shootCoroutine);
            }
        }
        
        foreach (var coroutine in flashCoroutines.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }
        
        ExplodeParts();
        Destroy(gameObject);
    }
    
    void ExplodeParts()
    {
        Vector3 explosionCenter = transform.position;
        
        foreach (GameObject part in colliderParts)
        {
            if (part != null)
            {
                ExplodePart(part, explosionCenter);
            }
        }
        
        foreach (WeaponGroup group in weaponGroups)
        {
            if (!group.isDetached)
            {
                foreach (GameObject part in group.parts)
                {
                    if (part != null)
                    {
                        ExplodePart(part, explosionCenter);
                    }
                }
            }
        }
    }
    
    void ExplodePart(GameObject part, Vector3 explosionCenter)
    {
        part.transform.SetParent(null);
        
        Rigidbody rb = part.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = part.AddComponent<Rigidbody>();
        }
        
        rb.isKinematic = false;
        rb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius, upwardModifier, ForceMode.Impulse);
        
        Vector3 randomTorque = new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(-10f, 10f),
            Random.Range(-10f, 10f)
        );
        rb.AddTorque(randomTorque, ForceMode.Impulse);
        
        LegoPartExplosion explosionHandler = part.AddComponent<LegoPartExplosion>();
        explosionHandler.Initialize(destructionTime);
    }
    
    void OnValidate()
    {
        colliderParts.RemoveAll(item => item == null);
        
        foreach (WeaponGroup group in weaponGroups)
        {
            group.parts.RemoveAll(item => item == null);
        }
        
        movement.jumpPoints.RemoveAll(item => item == null);
        movement.walkPoints.RemoveAll(item => item == null);
    }
}

public class LegoEnemyPart : MonoBehaviour
{
    [HideInInspector]
    public LegoEnemy parentEnemy;

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("PlayerProjectile") ||
            collision.gameObject.CompareTag("Player"))
        {
            if (parentEnemy != null)
            {
                parentEnemy.OnPartHit(gameObject, collision);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerProjectile") ||
            other.CompareTag("Player"))
        {
            if (parentEnemy != null)
            {
                parentEnemy.OnPartHit(gameObject);
            }

            Destroy(other.gameObject);
        }
    }
}

public class LegoProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private bool isInitialized = false;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        if (GetComponent<Collider>() == null)
        {
            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.1f;
        }
        
        gameObject.tag = "EnemyProjectile";
    }
    
    public void Initialize(Vector3 shootDirection, float projectileSpeed, float projectileLifetime)
    {
        if (isInitialized) return;
        
        isInitialized = true;
        
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = false;
            }
        }
        
        Vector3 normalizedDirection = shootDirection.normalized;
        if (normalizedDirection == Vector3.zero)
        {
            normalizedDirection = transform.forward;
        }
        
        StartCoroutine(SetVelocityNextFrame(normalizedDirection, projectileSpeed));
        
        if (normalizedDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(normalizedDirection);
        }
        
        Destroy(gameObject, projectileLifetime);
    }
    
    IEnumerator SetVelocityNextFrame(Vector3 direction, float speed)
    {
        yield return new WaitForFixedUpdate();
        
        if (rb != null)
        {
            try
            {
                rb.linearVelocity = direction * speed;
            }
            catch
            {
                var velocityProperty = rb.GetType().GetProperty("linearVelocity");
                if (velocityProperty != null)
                {
                    velocityProperty.SetValue(rb, direction * speed);
                }
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player hit by enemy projectile!");
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy") && 
                 !other.CompareTag("EnemyProjectile") && 
                 !other.CompareTag("PlayerProjectile") &&
                 !other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
    
    void Update()
    {
        if (isInitialized && rb != null)
        {
            float currentSpeed;
            #if UNITY_2023_3_OR_NEWER
                currentSpeed = rb.linearVelocity.magnitude;
            #else
                currentSpeed = rb.velocity.magnitude;
            #endif
            
            if (currentSpeed > 100f)
            {
                Debug.LogWarning($"Projectile speed too high ({currentSpeed}), destroying!");
                Destroy(gameObject);
            }
        }
    }
}

public class LegoPartExplosion : MonoBehaviour
{
    private float destructionTime;
    private Renderer partRenderer;

    public void Initialize(float timeToDestroy)
    {
        destructionTime = timeToDestroy;
        partRenderer = GetComponent<Renderer>();

        StartCoroutine(BlinkAndDestroy());
    }

    IEnumerator BlinkAndDestroy()
    {
        if (partRenderer == null)
        {
            Destroy(gameObject, destructionTime);
            yield break;
        }

        float elapsed = 0f;
        bool isVisible = true;

        while (elapsed < destructionTime)
        {
            float progress = elapsed / destructionTime;
            float blinkFrequency = Mathf.Lerp(2f, 20f, progress * progress);
            float blinkInterval = 1f / blinkFrequency;

            partRenderer.enabled = isVisible;
            isVisible = !isVisible;

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        Destroy(gameObject);
    }
}