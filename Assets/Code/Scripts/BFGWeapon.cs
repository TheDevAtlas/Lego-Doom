using UnityEngine;
using System.Collections.Generic;

public class BFGWeapon : MonoBehaviour
{
    [Header("Controller & Ammo")]
    public WeaponController weaponController;
    public int ammoIndex = 0;

    [Header("Shooting Configuration")]
    public Transform shootPoint; // The point where projectiles originate from
    public Camera playerCamera; // Reference to the player's camera
    
    [Header("Animation")]
    public WeaponAnimationController animationController; // Reference to animation controller
    
    [Header("BFG Projectile Settings")]
    public GameObject bfgProjectilePrefab; // Prefab for the BFG projectile
    public float projectileSpeed = 20f; // Speed at which projectile moves
    public float maxProjectileDistance = 100f; // Max distance from player before projectile is destroyed
    public float raycastDistance = 15f; // Distance to raycast to enemies from projectile
    public float enemyDestructionTime = 3f; // Time in seconds before enemy is destroyed
    public LayerMask enemyLayerMask = -1; // What layers count as enemies
    public LayerMask obstacleLayerMask = -1; // What layers block raycasts to enemies

    [Header("Auto Fire")]
    public bool autoFire = false;
    public float fireDelay = 2f; // Longer delay for BFG

    [Header("Muzzle Flash")]
    public Transform[] muzzleFlashes;
    public float flashDuration = 0.2f; // Longer flash for BFG
    public Vector3 randomRotationMin = Vector3.zero;
    public Vector3 randomRotationMax = new Vector3(360f, 360f, 360f);

    [Header("Enemy Connection Lightning")]
    public GameObject lightningQuadPrefab; // Lightning quad prefab (2 units long)
    public float quadLength = 2f; // Length of each lightning quad
    public Material[] lightningMaterials = new Material[4]; // 4 different lightning materials
    public float materialChangeInterval = 0.3f; // Time in seconds between material changes
    public bool showConnections = true; // Toggle to show/hide connection lightning
    public KeyCode toggleConnectionsKey = KeyCode.R; // Key to toggle connection visibility

    [Header("Input")]
    public KeyCode fireKey = KeyCode.Mouse0;

    // Timing variables
    private float fireTimer = 0f;
    private float flashTimer = 0f;
    private bool flashActive = false;
    
    // Active projectiles
    private List<BFGProjectile> activeProjectiles = new List<BFGProjectile>();
    
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
        
        // Auto-find animation controller if not assigned
        if (animationController == null)
        {
            animationController = GetComponent<WeaponAnimationController>();
            if (animationController == null)
            {
                Debug.LogWarning($"No WeaponAnimationController found on {gameObject.name}. BFG animations will not work.");
            }
        }
        
        // Auto-find shoot point if not assigned
        if (shootPoint == null)
        {
            Debug.LogWarning($"No shoot point assigned for BFG {gameObject.name}. Using weapon transform as shoot point.");
            shootPoint = transform;
        }

        // Initialize muzzle flashes
        if (muzzleFlashes != null)
        {
            foreach (var flash in muzzleFlashes)
            {
                if (flash != null)
                    flash.gameObject.SetActive(false);
            }
        }

        // Validate lightning quad prefab assignment
        if (lightningQuadPrefab == null)
        {
            Debug.LogWarning($"Lightning quad prefab not assigned for weapon {gameObject.name}.");
        }

        // Validate lightning materials
        if (lightningMaterials == null || lightningMaterials.Length == 0)
        {
            Debug.LogWarning($"No lightning materials assigned for weapon {gameObject.name}.");
        }

        // Validate BFG prefab assignment
        if (bfgProjectilePrefab == null)
        {
            Debug.LogWarning($"BFG projectile prefab not assigned for weapon {gameObject.name}. Will use default sphere.");
        }
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        // Handle connection lightning toggle
        if (Input.GetKeyDown(toggleConnectionsKey))
        {
            showConnections = !showConnections;
            UpdateAllConnectionVisibility();
        }

        // Update all active projectiles
        UpdateProjectiles();

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

    void UpdateProjectiles()
    {
        Vector3 playerPosition = transform.position;
        
        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            BFGProjectile projectile = activeProjectiles[i];
            
            if (projectile == null || projectile.projectileObject == null)
            {
                activeProjectiles.RemoveAt(i);
                continue;
            }

            // Move projectile
            projectile.projectileObject.transform.position += projectile.direction * projectileSpeed * Time.deltaTime;
            
            // Check if projectile is too far from player
            float distanceFromPlayer = Vector3.Distance(projectile.projectileObject.transform.position, playerPosition);
            if (distanceFromPlayer > maxProjectileDistance)
            {
                DestroyProjectile(i);
                continue;
            }

            // Update enemy targeting
            UpdateEnemyTargeting(projectile);
        }
    }

    void UpdateEnemyTargeting(BFGProjectile projectile)
    {
        Vector3 projectilePos = projectile.projectileObject.transform.position;
        
        // Find all potential enemies within range
        Collider[] nearbyObjects = Physics.OverlapSphere(projectilePos, raycastDistance, enemyLayerMask);
        
        // Update existing targets and add new ones
        List<string> currentTargetIds = new List<string>();
        
        foreach (Collider enemyCollider in nearbyObjects)
        {
            string enemyId = enemyCollider.GetInstanceID().ToString();
            currentTargetIds.Add(enemyId);
            
            // Check if we can see the enemy (no obstacles)
            Vector3 directionToEnemy = (enemyCollider.transform.position - projectilePos).normalized;
            float distanceToEnemy = Vector3.Distance(projectilePos, enemyCollider.transform.position);
            
            bool canSeeEnemy = !Physics.Raycast(projectilePos, directionToEnemy, distanceToEnemy, obstacleLayerMask);
            
            if (canSeeEnemy)
            {
                // Update or create target data
                if (projectile.targetedEnemies.ContainsKey(enemyId))
                {
                    EnemyTarget target = projectile.targetedEnemies[enemyId];
                    target.lastSeenTime = Time.time;
                    target.enemyPosition = enemyCollider.transform.position;
                    
                    // Check if enemy should be destroyed
                    if (Time.time - target.firstTargetedTime >= enemyDestructionTime)
                    {
                        DestroyEnemy(enemyCollider.gameObject, enemyId, projectile);
                        continue;
                    }
                    
                    projectile.targetedEnemies[enemyId] = target;
                }
                else
                {
                    // New enemy target
                    projectile.targetedEnemies[enemyId] = new EnemyTarget
                    {
                        enemyObject = enemyCollider.gameObject,
                        enemyPosition = enemyCollider.transform.position,
                        firstTargetedTime = Time.time,
                        lastSeenTime = Time.time
                    };
                }
                
                // Create/update connection lightning quads
                UpdateEnemyConnectionLightning(projectile, enemyId, projectilePos, enemyCollider.transform.position);
            }
        }
        
        // Remove enemies that are no longer in range or visible
        List<string> enemiesToRemove = new List<string>();
        foreach (var kvp in projectile.targetedEnemies)
        {
            if (!currentTargetIds.Contains(kvp.Key) || Time.time - kvp.Value.lastSeenTime > 0.1f)
            {
                enemiesToRemove.Add(kvp.Key);
            }
        }
        
        foreach (string enemyId in enemiesToRemove)
        {
            RemoveEnemyTarget(projectile, enemyId);
        }
    }

    void UpdateEnemyConnectionLightning(BFGProjectile projectile, string enemyId, Vector3 projectilePos, Vector3 enemyPos)
    {
        if (lightningQuadPrefab == null)
            return;

        // Calculate connection parameters
        Vector3 connectionVector = enemyPos - projectilePos;
        float totalDistance = connectionVector.magnitude;
        Vector3 direction = connectionVector.normalized;
        
        // Calculate how many quads we need
        int requiredQuads = Mathf.CeilToInt(totalDistance / quadLength);
        
        // Get or create quad list for this enemy
        if (!projectile.enemyConnectionQuads.ContainsKey(enemyId))
        {
            projectile.enemyConnectionQuads[enemyId] = new List<LightningQuad>();
        }
        
        List<LightningQuad> lightningQuads = projectile.enemyConnectionQuads[enemyId];
        
        // Add more quads if needed
        while (lightningQuads.Count < requiredQuads)
        {
            GameObject newQuad = Instantiate(lightningQuadPrefab);
            LightningQuad lightningQuad = new LightningQuad
            {
                quadObject = newQuad,
                renderer = newQuad.GetComponent<Renderer>(),
                lastMaterialChangeTime = Time.time
            };
            
            // Set initial random material
            if (lightningQuad.renderer != null && lightningMaterials != null && lightningMaterials.Length > 0)
            {
                int randomMaterialIndex = Random.Range(0, lightningMaterials.Length);
                lightningQuad.renderer.material = lightningMaterials[randomMaterialIndex];
            }
            
            lightningQuads.Add(lightningQuad);
        }
        
        // Remove excess quads if needed
        while (lightningQuads.Count > requiredQuads)
        {
            int lastIndex = lightningQuads.Count - 1;
            LightningQuad lastQuad = lightningQuads[lastIndex];
            if (lastQuad.quadObject != null)
                Destroy(lastQuad.quadObject);
            lightningQuads.RemoveAt(lastIndex);
        }
        
        // Get player camera for billboard rotation
        Vector3 cameraPosition = playerCamera != null ? playerCamera.transform.position : transform.position;
        
        // Position and orient the quads
        for (int i = 0; i < requiredQuads; i++)
        {
            LightningQuad lightningQuad = lightningQuads[i];
            if (lightningQuad.quadObject == null) continue;
            
            float segmentProgress = (float)i / requiredQuads;
            float nextSegmentProgress = (float)(i + 1) / requiredQuads;
            
            // Calculate position for this segment
            Vector3 segmentStart = projectilePos + connectionVector * segmentProgress;
            Vector3 segmentEnd = projectilePos + connectionVector * nextSegmentProgress;
            Vector3 segmentCenter = (segmentStart + segmentEnd) * 0.5f;
            
            // Position the quad at the segment center
            lightningQuad.quadObject.transform.position = segmentCenter;
            
            // Calculate proper rotation: vertical axis along connection line, facing camera
            Vector3 toCameraDirection = (cameraPosition - segmentCenter).normalized;
            
            // The connection direction becomes our "up" vector (vertical axis of the quad)
            Vector3 upVector = direction;
            
            // Calculate the right vector by crossing up with the to-camera direction
            Vector3 rightVector = Vector3.Cross(upVector, toCameraDirection).normalized;
            
            // Recalculate the forward vector to ensure it faces the camera properly
            Vector3 forwardVector = Vector3.Cross(rightVector, upVector).normalized;
            
            // Handle edge case where camera is directly aligned with the connection line
            if (rightVector.magnitude < 0.001f)
            {
                // Fallback: use world up as reference
                rightVector = Vector3.Cross(upVector, Vector3.up).normalized;
                if (rightVector.magnitude < 0.001f)
                {
                    rightVector = Vector3.Cross(upVector, Vector3.right).normalized;
                }
                forwardVector = Vector3.Cross(rightVector, upVector).normalized;
            }
            
            // Create rotation using LookRotation with our calculated vectors
            Quaternion quadRotation = Quaternion.LookRotation(forwardVector, upVector);
            lightningQuad.quadObject.transform.rotation = quadRotation;
            
            // Handle the last segment which might be shorter than quadLength
            if (i == requiredQuads - 1)
            {
                float remainingDistance = totalDistance - (i * quadLength);
                float scaleRatio = remainingDistance / quadLength;
                
                // Scale the quad if it's the last segment and shorter
                if (scaleRatio < 1.0f)
                {
                    Vector3 scale = new Vector3(1f, 6f, scaleRatio); // Ensure Y is always 6
                    lightningQuad.quadObject.transform.localScale = scale;
                }
                else
                {
                    Vector3 scale = new Vector3(1f, 6f, 1f); // Ensure Y is always 6
                    lightningQuad.quadObject.transform.localScale = scale;
                }
            }
            else
            {
                Vector3 scale = new Vector3(1f, 6f, 1f); // Ensure Y is always 6
                lightningQuad.quadObject.transform.localScale = scale;
            }
            
            // Randomize material every materialChangeInterval seconds
            if (Time.time - lightningQuad.lastMaterialChangeTime >= materialChangeInterval)
            {
                if (lightningQuad.renderer != null && lightningMaterials != null && lightningMaterials.Length > 0)
                {
                    int randomMaterialIndex = Random.Range(0, lightningMaterials.Length);
                    lightningQuad.renderer.material = lightningMaterials[randomMaterialIndex];
                    lightningQuad.lastMaterialChangeTime = Time.time;
                }
            }
            
            // Set visibility
            lightningQuad.quadObject.SetActive(showConnections);
        }
    }

    void UpdateAllConnectionVisibility()
    {
        foreach (var projectile in activeProjectiles)
        {
            foreach (var kvp in projectile.enemyConnectionQuads)
            {
                foreach (var lightningQuad in kvp.Value)
                {
                    if (lightningQuad.quadObject != null)
                        lightningQuad.quadObject.SetActive(showConnections);
                }
            }
        }
    }

    void DestroyEnemy(GameObject enemy, string enemyId, BFGProjectile projectile)
    {
        Debug.Log($"BFG destroyed enemy: {enemy.name}");
        
        // Remove from targeting
        RemoveEnemyTarget(projectile, enemyId);
        
        // Destroy the enemy
        Destroy(enemy);
    }

    void RemoveEnemyTarget(BFGProjectile projectile, string enemyId)
    {
        // Remove targeting data
        if (projectile.targetedEnemies.ContainsKey(enemyId))
            projectile.targetedEnemies.Remove(enemyId);
        
        // Remove and destroy connection lightning quads
        if (projectile.enemyConnectionQuads.ContainsKey(enemyId))
        {
            foreach (var lightningQuad in projectile.enemyConnectionQuads[enemyId])
            {
                if (lightningQuad.quadObject != null)
                    Destroy(lightningQuad.quadObject);
            }
            projectile.enemyConnectionQuads.Remove(enemyId);
        }
    }

    void UpdateMuzzleFlash()
    {
        if (flashActive)
        {
            flashTimer += Time.deltaTime;
            if (flashTimer >= flashDuration)
            {
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

        // Trigger shooting animation BEFORE firing
        if (animationController != null)
        {
            animationController.TriggerShootAnimation();
        }

        // Create BFG projectile
        CreateBFGProjectile();
        
        // Use ammo
        weaponController.UseAmmo(ammoIndex, 1);
        AnimateFlash();
    }

    void CreateBFGProjectile()
    {
        Vector3 startPoint = GetShootStartPoint();
        Vector3 direction = GetShootDirection();
        
        GameObject projectileObj;
        
        if (bfgProjectilePrefab != null)
        {
            projectileObj = Instantiate(bfgProjectilePrefab, startPoint, Quaternion.LookRotation(direction));
        }
        else
        {
            // Create default projectile if no prefab assigned
            projectileObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObj.name = "BFG_Projectile";
            projectileObj.transform.position = startPoint;
            projectileObj.transform.localScale = Vector3.one * 0.5f;
            
            // Remove collider to prevent interference
            Collider collider = projectileObj.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            
            // Apply glowing material
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material defaultMaterial = new Material(shader);
            defaultMaterial.color = Color.green;
            if (defaultMaterial.HasProperty("_BaseColor"))
                defaultMaterial.SetColor("_BaseColor", Color.green);
            projectileObj.GetComponent<Renderer>().material = defaultMaterial;
        }
        
        // Create projectile data
        BFGProjectile newProjectile = new BFGProjectile
        {
            projectileObject = projectileObj,
            direction = direction,
            targetedEnemies = new Dictionary<string, EnemyTarget>(),
            enemyConnectionQuads = new Dictionary<string, List<LightningQuad>>()
        };
        
        activeProjectiles.Add(newProjectile);
        
        Debug.Log("BFG projectile fired!");
    }

    void DestroyProjectile(int index)
    {
        BFGProjectile projectile = activeProjectiles[index];
        
        // Clean up all enemy connection lightning quads
        foreach (var kvp in projectile.enemyConnectionQuads)
        {
            foreach (var lightningQuad in kvp.Value)
            {
                if (lightningQuad.quadObject != null)
                    Destroy(lightningQuad.quadObject);
            }
        }
        
        // Destroy projectile object
        if (projectile.projectileObject != null)
            Destroy(projectile.projectileObject);
        
        activeProjectiles.RemoveAt(index);
    }

    void AnimateFlash()
    {
        if (muzzleFlashes != null && muzzleFlashes.Length > 0)
        {
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

    // Called when this weapon becomes active
    void OnEnable()
    {
        if (animationController != null)
        {
            animationController.RefreshAnimationState();
        }
    }

    // Called when this weapon becomes inactive
    void OnDisable()
    {
        // Animation controller's OnDisable will handle resetting animation state
    }

    void OnDestroy()
    {
        // Clean up all active projectiles
        foreach (var projectile in activeProjectiles)
        {
            if (projectile.projectileObject != null)
                Destroy(projectile.projectileObject);
                
            foreach (var kvp in projectile.enemyConnectionQuads)
            {
                foreach (var lightningQuad in kvp.Value)
                {
                    if (lightningQuad.quadObject != null)
                        Destroy(lightningQuad.quadObject);
                }
            }
        }
        activeProjectiles.Clear();
    }
}

// Data structures for BFG projectile system
[System.Serializable]
public class BFGProjectile
{
    public GameObject projectileObject;
    public Vector3 direction;
    public Dictionary<string, EnemyTarget> targetedEnemies;
    public Dictionary<string, List<LightningQuad>> enemyConnectionQuads; // Changed to lightning quads
}

[System.Serializable]
public class EnemyTarget
{
    public GameObject enemyObject;
    public Vector3 enemyPosition;
    public float firstTargetedTime;
    public float lastSeenTime;
}

[System.Serializable]
public class LightningQuad
{
    public GameObject quadObject;
    public Renderer renderer;
    public float lastMaterialChangeTime;
}