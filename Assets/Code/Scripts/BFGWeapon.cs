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

    [Header("Visualization")]
    public bool showRaycast = true;
    public KeyCode toggleRaycastKey = KeyCode.R;
    public Material enemyRayMaterial;
    public LayerMask playerLayerMask = 1;

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

        // Create default enemy ray material if not assigned
        if (enemyRayMaterial == null)
            enemyRayMaterial = CreateURPMaterial(Color.cyan);
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        // Handle raycast toggle
        if (Input.GetKeyDown(toggleRaycastKey))
        {
            showRaycast = !showRaycast;
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

    Material CreateURPMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.0f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.5f);

        return mat;
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
            // Skip if it's the player
            if (((1 << enemyCollider.gameObject.layer) & playerLayerMask) != 0)
                continue;
                
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
                
                // Create/update visualization
                UpdateEnemyRayVisualization(projectile, enemyId, projectilePos, enemyCollider.transform.position);
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

    void UpdateEnemyRayVisualization(BFGProjectile projectile, string enemyId, Vector3 projectilePos, Vector3 enemyPos)
    {
        if (!showRaycast) return;
        
        if (!projectile.enemyRayRenderers.ContainsKey(enemyId))
        {
            // Create new ray renderer for this enemy
            GameObject rayObj = new GameObject($"EnemyRay_{enemyId}");
            LineRenderer lr = rayObj.AddComponent<LineRenderer>();
            
            lr.positionCount = 2;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.useWorldSpace = true;
            lr.material = enemyRayMaterial;
            
            projectile.enemyRayRenderers[enemyId] = lr;
        }
        
        // Update ray position
        LineRenderer rayRenderer = projectile.enemyRayRenderers[enemyId];
        rayRenderer.SetPosition(0, projectilePos);
        rayRenderer.SetPosition(1, enemyPos);
        rayRenderer.gameObject.SetActive(showRaycast);
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
        
        // Remove and destroy ray visualization
        if (projectile.enemyRayRenderers.ContainsKey(enemyId))
        {
            if (projectile.enemyRayRenderers[enemyId] != null)
                Destroy(projectile.enemyRayRenderers[enemyId].gameObject);
            projectile.enemyRayRenderers.Remove(enemyId);
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
            projectileObj.GetComponent<Renderer>().material = CreateURPMaterial(Color.green);
        }
        
        // Create projectile data
        BFGProjectile newProjectile = new BFGProjectile
        {
            projectileObject = projectileObj,
            direction = direction,
            targetedEnemies = new Dictionary<string, EnemyTarget>(),
            enemyRayRenderers = new Dictionary<string, LineRenderer>()
        };
        
        activeProjectiles.Add(newProjectile);
        
        Debug.Log("BFG projectile fired!");
    }

    void DestroyProjectile(int index)
    {
        BFGProjectile projectile = activeProjectiles[index];
        
        // Clean up all enemy ray renderers
        foreach (var kvp in projectile.enemyRayRenderers)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
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

    void OnDestroy()
    {
        // Clean up all active projectiles
        foreach (var projectile in activeProjectiles)
        {
            if (projectile.projectileObject != null)
                Destroy(projectile.projectileObject);
                
            foreach (var kvp in projectile.enemyRayRenderers)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
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
    public Dictionary<string, LineRenderer> enemyRayRenderers;
}

[System.Serializable]
public class EnemyTarget
{
    public GameObject enemyObject;
    public Vector3 enemyPosition;
    public float firstTargetedTime;
    public float lastSeenTime;
}