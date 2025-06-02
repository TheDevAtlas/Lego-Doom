using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Controller & Ammo")]
    public WeaponController weaponController;
    public int ammoIndex = 0;

    [Header("Shooting Configuration")]
    public Transform shootPoint; // The point where bullets originate from
    public Camera playerCamera; // Reference to the player's camera
    
    [Header("Firing Settings")]
    public bool isShotgun = false;
    public int pellets = 5;
    public bool useIndependentSpread = false; // New toggle for X/Y spread control
    public float spreadAngle = 5f; // Used when useIndependentSpread is false
    public float spreadAngleX = 5f; // Used when useIndependentSpread is true
    public float spreadAngleY = 5f; // Used when useIndependentSpread is true
    public bool infiniteRange = false;
    public float maxDistance = 100f;

    [Header("Auto Fire")]
    public bool autoFire = false;
    public float fireDelay = 0.2f;

    [Header("Muzzle Flash")]
    public Transform[] muzzleFlashes; // Changed to array for multiple flashes
    public float flashDuration = 0.1f;
    public Vector3 randomRotationMin = Vector3.zero;
    public Vector3 randomRotationMax = new Vector3(360f, 360f, 360f);

    [Header("Bullet Prefab System")]
    public GameObject bulletPrefab; // Prefab for the bullet visual effect
    public GameObject hitEffectPrefab; // Prefab for hit animation/effect
    public float bulletSpeed = 50f; // Speed of bullet travel
    public float hitEffectDuration = 2f; // Duration before hit effect is destroyed

    [Header("Input")]
    public KeyCode fireKey = KeyCode.Mouse0;

    // Timing variables
    private float fireTimer = 0f;
    private float flashTimer = 0f;
    private bool flashActive = false;
    
    void Start()
    {
        // Auto-find camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Camera>();
            }
        }
        
        // Auto-find shoot point if not assigned (use weapon transform as fallback)
        if (shootPoint == null)
        {
            Debug.LogWarning($"No shoot point assigned for weapon {gameObject.name}. Using weapon transform as shoot point.");
            shootPoint = transform;
        }

        // Initialize all muzzle flashes
        if (muzzleFlashes != null)
        {
            foreach (var flash in muzzleFlashes)
            {
                if (flash != null)
                    flash.gameObject.SetActive(false);
            }
        }

        // Validate prefab assignments
        if (bulletPrefab == null)
        {
            Debug.LogError($"Bullet prefab not assigned for weapon {gameObject.name}!");
        }
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning($"Hit effect prefab not assigned for weapon {gameObject.name}. No hit effects will be spawned.");
        }
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        // Handle muzzle flash timing
        UpdateMuzzleFlash();

        // Handle firing input
        if ((autoFire && Input.GetKey(fireKey)) || (!autoFire && Input.GetKeyDown(fireKey)))
        {
            if (fireTimer >= fireDelay)
            {
                Fire();
                fireTimer = 0f;
            }
        }
    }

    Vector3 GetShootDirection()
    {
        if (playerCamera != null)
        {
            return playerCamera.transform.forward;
        }
        else
        {
            Debug.LogWarning("No camera assigned! Using weapon forward direction as fallback.");
            return transform.forward;
        }
    }

    Vector3 GetShootStartPoint()
    {
        return shootPoint != null ? shootPoint.position : transform.position;
    }

    void UpdateMuzzleFlash()
    {
        if (flashActive)
        {
            flashTimer += Time.deltaTime;
            if (flashTimer >= flashDuration)
            {
                // Deactivate all muzzle flashes
                if (muzzleFlashes != null)
                {
                    foreach (var flash in muzzleFlashes)
                    {
                        if (flash != null)
                            flash.gameObject.SetActive(false);
                    }
                }
                flashActive = false;
            }
        }
    }

    void Fire()
    {
        if (!weaponController.HasAmmo(ammoIndex)) return;

        if (isShotgun)
        {
            for (int i = 0; i < pellets; i++)
            {
                SpawnBullet(true);
            }
        }
        else
        {
            SpawnBullet(false);
        }

        // Always use only 1 bullet, regardless of shotgun pellets
        weaponController.UseAmmo(ammoIndex, 1);
        AnimateFlash();
    }

    void SpawnBullet(bool applySpread)
    {
        if (bulletPrefab == null) return;

        Vector3 startPoint = GetShootStartPoint();
        Vector3 direction = GetShootDirection();

        if (applySpread)
        {
            // Use independent X/Y spread if enabled
            if (useIndependentSpread)
            {
                direction = Quaternion.Euler(
                    Random.Range(-spreadAngleX, spreadAngleX), // X-axis spread
                    Random.Range(-spreadAngleY, spreadAngleY), // Y-axis spread
                    0f
                ) * direction;
            }
            else
            {
                // Original spread behavior
                direction = Quaternion.Euler(
                    Random.Range(-spreadAngle, spreadAngle),
                    Random.Range(-spreadAngle, spreadAngle),
                    0f
                ) * direction;
            }
        }

        // Spawn bullet prefab
        GameObject bullet = Instantiate(bulletPrefab, startPoint, Quaternion.LookRotation(direction));
        
        // Add and configure the bullet behavior component
        BulletBehavior bulletBehavior = bullet.GetComponent<BulletBehavior>();
        if (bulletBehavior == null)
        {
            bulletBehavior = bullet.AddComponent<BulletBehavior>();
        }

        // Configure bullet behavior
        bulletBehavior.Initialize(direction, bulletSpeed, infiniteRange ? Mathf.Infinity : maxDistance, hitEffectPrefab, hitEffectDuration);
    }

    void AnimateFlash()
    {
        if (muzzleFlashes != null && muzzleFlashes.Length > 0)
        {
            // Activate all muzzle flash objects
            foreach (var flash in muzzleFlashes)
            {
                if (flash != null)
                {
                    Vector3 randomRotation = new Vector3(
                        Random.Range(randomRotationMin.x, randomRotationMax.x),
                        Random.Range(randomRotationMin.y, randomRotationMax.y),
                        Random.Range(randomRotationMin.z, randomRotationMax.z)
                    );

                    flash.localEulerAngles = randomRotation;
                    flash.gameObject.SetActive(true);
                }
            }
            
            flashActive = true;
            flashTimer = 0f;
        }
    }
}

// Separate component to handle individual bullet behavior
public class BulletBehavior : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private GameObject hitEffectPrefab;
    private float hitEffectDuration;
    private Vector3 startPosition;
    private float traveledDistance = 0f;

    public void Initialize(Vector3 dir, float spd, float maxDist, GameObject hitPrefab, float hitDuration)
    {
        direction = dir.normalized;
        speed = spd;
        maxDistance = maxDist;
        hitEffectPrefab = hitPrefab;
        hitEffectDuration = hitDuration;
        startPosition = transform.position;
    }

    void Update()
    {
        // Move bullet forward
        float moveDistance = speed * Time.deltaTime;
        transform.position += direction * moveDistance;
        traveledDistance += moveDistance;

        // Check for collision
        RaycastHit hit;
        if (Physics.Raycast(transform.position - direction * moveDistance, direction, out hit, moveDistance))
        {
            // Hit something - spawn hit effect and destroy bullet
            if (hitEffectPrefab != null)
            {
                GameObject hitEffect = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(hitEffect, hitEffectDuration);
            }
            
            Debug.Log($"SHOT HIT: {hit.collider.name} at {hit.point}");
            Destroy(gameObject);
            return;
        }

        // Check if bullet has traveled max distance
        if (traveledDistance >= maxDistance)
        {
            Debug.Log("SHOT MISS: Maximum distance reached");
            Destroy(gameObject);
        }
    }
}