using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChainsawWeapon : MonoBehaviour
{
    [Header("Controller & Ammo")]
    public WeaponController weaponController;
    public int ammoIndex = 0;

    [Header("Player Control")]
    public MonoBehaviour playerController; // Reference to player controller to disable
    public Transform playerTransform; // Player transform for rotation
    public Camera playerCamera; // Reference to the player's camera
    
    [Header("Chainsaw Settings")]
    public float chainsawRange = 3f; // How close enemy must be to slice
    public float faceTargetSpeed = 5f; // How fast to rotate player towards target
    public float sliceAnimationDuration = 2f; // How long the slicing animation takes
    public LayerMask enemyLayerMask = -1; // What layers count as enemies
    public LayerMask playerLayerMask = 1; // Player layer to exclude

    [Header("Slice Physics")]
    public float sliceForce = 8f; // How strong the slice force is
    public float upwardForce = 3f; // Upward force applied to pieces
    public float torqueForce = 10f; // Spinning force applied to pieces
    public float pieceLifetime = 5f; // How long pieces stay before being destroyed

    [Header("Auto Fire")]
    public bool autoFire = false;
    public float fireDelay = 3f; // Longer delay for chainsaw

    [Header("Animation")]
    public Animator chainsawAnimator; // Animator for chainsaw
    public string sliceAnimationTrigger = "Slice"; // Animation trigger name
    public AudioSource chainsawAudio; // Audio source for chainsaw sounds

    [Header("Rotation Fix")]
    public float rotationOffset = -90f; // Fixed: rotate 90 degrees in the other direction

    [Header("Visualization")]
    public bool showTargeting = true;
    public KeyCode toggleVisualizationKey = KeyCode.R;
    public Material targetLineMaterial;
    public Material slicePlaneMaterial;

    [Header("Input")]
    public KeyCode fireKey = KeyCode.Mouse0;

    // State variables
    private bool isSlicing = false;
    private GameObject currentTarget = null;
    private bool playerControlDisabled = false;
    
    // Targeting visualization
    private LineRenderer targetLine;
    private GameObject slicePlaneVisualizer;
    
    // Timing
    private float fireTimer = 0f;
    
    void Start()
    {
        // Auto-find components if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = FindObjectOfType<Camera>();
        }
        
        if (playerTransform == null)
            playerTransform = transform.root; // Assume weapon is child of player
        
        if (playerController == null)
        {
            // Try to find the FirstPersonController component
            playerController = playerTransform.GetComponent<FirstPersonController>();
        }

        // Create visualization objects
        CreateVisualizationObjects();
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        // Handle visualization toggle
        if (Input.GetKeyDown(toggleVisualizationKey))
        {
            showTargeting = !showTargeting;
            UpdateVisualizationVisibility();
        }

        // Update targeting visualization if not slicing
        if (!isSlicing && showTargeting)
        {
            UpdateTargetingVisualization();
        }

        // Handle firing input
        if (!isSlicing && ((autoFire && Input.GetKey(fireKey)) || (!autoFire && Input.GetKeyDown(fireKey))))
        {
            if (fireTimer >= fireDelay)
            {
                AttemptSlice();
                fireTimer = 0f;
            }
        }
    }

    void CreateVisualizationObjects()
    {
        // Create targeting line
        GameObject targetLineObj = new GameObject("TargetLine");
        targetLineObj.transform.SetParent(transform);
        targetLine = targetLineObj.AddComponent<LineRenderer>();
        
        targetLine.positionCount = 2;
        targetLine.startWidth = 0.05f;
        targetLine.endWidth = 0.05f;
        targetLine.useWorldSpace = true;
        
        if (targetLineMaterial == null)
            targetLineMaterial = CreateURPMaterial(Color.red);
        targetLine.material = targetLineMaterial;
        
        // Create slice plane visualizer
        slicePlaneVisualizer = GameObject.CreatePrimitive(PrimitiveType.Quad);
        slicePlaneVisualizer.name = "SlicePlaneVisualizer";
        slicePlaneVisualizer.transform.localScale = Vector3.one * 2f;
        
        // Remove collider
        Collider collider = slicePlaneVisualizer.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);
        
        if (slicePlaneMaterial == null)
        {
            slicePlaneMaterial = CreateURPMaterial(Color.yellow);
            slicePlaneMaterial.color = new Color(1f, 1f, 0f, 0.3f); // Semi-transparent yellow
        }
        slicePlaneVisualizer.GetComponent<Renderer>().material = slicePlaneMaterial;
        
        UpdateVisualizationVisibility();
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

    void UpdateVisualizationVisibility()
    {
        bool visible = showTargeting && !isSlicing;
        
        if (targetLine != null)
            targetLine.gameObject.SetActive(visible);
        if (slicePlaneVisualizer != null)
            slicePlaneVisualizer.SetActive(visible);
    }

    void UpdateTargetingVisualization()
    {
        GameObject target = FindNearestEnemy();
        
        if (target != null)
        {
            Vector3 startPoint = transform.position;
            Vector3 targetPoint = target.transform.position;
            
            // Update target line
            targetLine.SetPosition(0, startPoint);
            targetLine.SetPosition(1, targetPoint);
            targetLine.material = targetLineMaterial;
            
            // Update slice plane position and orientation
            UpdateSlicePlaneVisualization(target);
            
            targetLine.gameObject.SetActive(true);
            slicePlaneVisualizer.SetActive(true);
        }
        else
        {
            targetLine.gameObject.SetActive(false);
            slicePlaneVisualizer.SetActive(false);
        }
    }

    void UpdateSlicePlaneVisualization(GameObject target)
    {
        Vector3 targetCenter = GetEnemyCenter(target);
        
        // Position the slice plane at target center (horizontal slice)
        slicePlaneVisualizer.transform.position = targetCenter;
        
        // Orient the plane horizontally (slice from top to bottom)
        slicePlaneVisualizer.transform.rotation = Quaternion.LookRotation(Vector3.up, playerTransform.forward);
    }

    GameObject FindNearestEnemy()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, chainsawRange, enemyLayerMask);
        
        GameObject nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Collider col in nearbyObjects)
        {
            // Skip if it's the player
            if (((1 << col.gameObject.layer) & playerLayerMask) != 0)
                continue;
            
            // Find the root enemy parent (the one with LegoEnemy component)
            GameObject rootEnemy = FindRootEnemy(col.gameObject);
            if (rootEnemy == null) continue;
            
            float distance = Vector3.Distance(transform.position, rootEnemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = rootEnemy;
            }
        }
        
        return nearestEnemy;
    }

    GameObject FindRootEnemy(GameObject obj)
    {
        // First check if this object has LegoEnemy component
        if (obj.GetComponent<LegoEnemy>() != null)
        {
            return obj;
        }
        
        // Check parent objects up the hierarchy
        Transform current = obj.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<LegoEnemy>() != null)
            {
                return current.gameObject;
            }
            current = current.parent;
        }
        
        // If no LegoEnemy found in hierarchy, check if any child has LegoEnemy
        LegoEnemy childEnemy = obj.GetComponentInChildren<LegoEnemy>();
        if (childEnemy != null)
        {
            return childEnemy.gameObject;
        }
        
        // If still no LegoEnemy found, this might not be a valid enemy
        return null;
    }

    void AttemptSlice()
    {
        if (!weaponController.HasAmmo(ammoIndex)) return;
        
        GameObject target = FindNearestEnemy();
        if (target == null)
        {
            Debug.Log("No enemy in range to slice!");
            return;
        }
        
        Debug.Log($"Chainsaw targeting root enemy: {target.name}");
        
        // Start slicing process
        StartCoroutine(SliceSequence(target));
        
        // Use ammo
        weaponController.UseAmmo(ammoIndex, 1);
    }

    IEnumerator SliceSequence(GameObject target)
    {
        isSlicing = true;
        currentTarget = target;
        
        // Disable player control
        DisablePlayerControl();
        
        // Hide targeting visualization
        UpdateVisualizationVisibility();
        
        // Phase 1: Face the target
        yield return StartCoroutine(FaceTarget(target));
        
        // Phase 2: Play chainsaw animation and sound
        TriggerChainsawAnimation();
        
        // Phase 3: Wait for animation duration
        yield return new WaitForSeconds(sliceAnimationDuration);
        
        // Phase 4: Slice the enemy
        SliceEnemy(target);
        
        // Phase 5: Re-enable player control
        EnablePlayerControl();
        
        isSlicing = false;
        currentTarget = null;
        UpdateVisualizationVisibility();
    }

    IEnumerator FaceTarget(GameObject target)
    {
        Vector3 directionToTarget = (target.transform.position - playerTransform.position).normalized;
        directionToTarget.y = 0; // Keep rotation only on Y axis (horizontal plane)
        
        // Apply rotation offset to fix the 90-degree issue
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget) * Quaternion.Euler(0, rotationOffset, 0);
        
        float rotationThreshold = 1f;
        
        while (Quaternion.Angle(playerTransform.rotation, targetRotation) > rotationThreshold)
        {
            playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRotation, faceTargetSpeed * Time.deltaTime);
            yield return null;
        }
        
        playerTransform.rotation = targetRotation;
    }

    void TriggerChainsawAnimation()
    {
        if (chainsawAnimator != null)
        {
            chainsawAnimator.SetTrigger(sliceAnimationTrigger);
        }
        
        if (chainsawAudio != null)
        {
            chainsawAudio.Play();
        }
        
        Debug.Log("Chainsaw slicing animation started!");
    }

    void SliceEnemy(GameObject enemy)
    {
        Debug.Log($"Chainsaw slicing enemy: {enemy.name}");
        
        // INSTANTLY kill the enemy first to prevent interference
        KillEnemyInstantly(enemy);
        
        // Get all enemy parts
        List<GameObject> enemyParts = GetEnemyParts(enemy);
        
        if (enemyParts.Count == 0)
        {
            Debug.LogWarning($"No enemy parts found for {enemy.name}");
            return;
        }
        
        // Calculate the center point of all enemy parts
        Vector3 enemyCenter = GetEnemyCenter(enemy);
        
        // Get player's right direction for slice forces
        Vector3 playerRight = playerTransform.right;
        
        Debug.Log($"Slicing {enemyParts.Count} parts at center: {enemyCenter}");
        
        // Separate parts into top and bottom halves based on their position relative to center
        List<GameObject> topParts = new List<GameObject>();
        List<GameObject> bottomParts = new List<GameObject>();
        
        foreach (GameObject part in enemyParts)
        {
            if (part != null)
            {
                // Determine if this part is above or below the center
                if (part.transform.position.y >= enemyCenter.y)
                {
                    topParts.Add(part);
                }
                else
                {
                    bottomParts.Add(part);
                }
            }
        }
        
        Debug.Log($"Top parts: {topParts.Count}, Bottom parts: {bottomParts.Count}");
        
        // Create grouped objects for top and bottom halves to keep parts together
        GameObject topGroup = CreatePartGroup(topParts, "TopHalf");
        GameObject bottomGroup = CreatePartGroup(bottomParts, "BottomHalf");
        
        // Apply physics to groups (fly to opposite sides)
        if (topGroup != null)
        {
            ApplyGroupPhysics(topGroup, playerRight, true);
        }
        
        if (bottomGroup != null)
        {
            ApplyGroupPhysics(bottomGroup, -playerRight, false);
        }
        
        // Destroy the original enemy immediately
        Destroy(enemy);
    }

    GameObject CreatePartGroup(List<GameObject> parts, string groupName)
    {
        if (parts.Count == 0) return null;
        
        // Create a parent object to hold all parts together
        GameObject group = new GameObject($"ChainsawSlice_{groupName}");
        
        // Calculate group center
        Vector3 groupCenter = Vector3.zero;
        foreach (GameObject part in parts)
        {
            if (part != null)
            {
                groupCenter += part.transform.position;
            }
        }
        groupCenter /= parts.Count;
        group.transform.position = groupCenter;
        
        // Parent all parts to this group
        foreach (GameObject part in parts)
        {
            if (part != null)
            {
                part.transform.SetParent(group.transform);
            }
        }
        
        Debug.Log($"Created {groupName} with {parts.Count} parts at position {groupCenter}");
        return group;
    }

    void ApplyGroupPhysics(GameObject group, Vector3 forceDirection, bool isTopHalf)
    {
        if (group == null) return;
        
        // Add rigidbody to the group object
        Rigidbody rb = group.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        
        // Calculate force: horizontal direction + upward force
        Vector3 totalForce = (forceDirection * sliceForce) + (Vector3.up * upwardForce);
        
        // Add some variation to make it look more natural
        totalForce += new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.5f, 1f),
            Random.Range(-1f, 1f)
        );
        
        rb.AddForce(totalForce, ForceMode.Impulse);
        
        // Add spinning torque
        Vector3 torque = new Vector3(
            Random.Range(-torqueForce, torqueForce),
            Random.Range(-torqueForce, torqueForce),
            Random.Range(-torqueForce, torqueForce)
        );
        rb.AddTorque(torque, ForceMode.Impulse);
        
        Debug.Log($"Applied group physics to {group.name}: force={totalForce}, torque={torque}");
        
        // Destroy the group after some time
        Destroy(group, pieceLifetime);
    }

    void KillEnemyInstantly(GameObject enemy)
    {
        // Try to get the LegoEnemy component and stop all its systems
        LegoEnemy legoEnemy = enemy.GetComponent<LegoEnemy>();
        if (legoEnemy != null)
        {
            Debug.Log($"Instantly killing LegoEnemy {enemy.name} - stopping all systems");
            
            // Stop all weapon group shooting coroutines using reflection
            System.Reflection.FieldInfo weaponGroupsField = typeof(LegoEnemy).GetField("weaponGroups", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (weaponGroupsField != null)
            {
                List<WeaponGroup> weaponGroups = (List<WeaponGroup>)weaponGroupsField.GetValue(legoEnemy);
                if (weaponGroups != null)
                {
                    foreach (WeaponGroup group in weaponGroups)
                    {
                        if (group.shootCoroutine != null)
                        {
                            legoEnemy.StopCoroutine(group.shootCoroutine);
                            group.shootCoroutine = null;
                        }
                    }
                }
            }
            
            // Set health to 0 using reflection
            System.Reflection.FieldInfo healthField = typeof(LegoEnemy).GetField("currentHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (healthField != null)
            {
                healthField.SetValue(legoEnemy, 0);
            }
            
            // Disable the enemy component
            legoEnemy.enabled = false;
        }
        else
        {
            // Try to find LegoEnemy on parent objects
            LegoEnemy parentLegoEnemy = enemy.GetComponentInParent<LegoEnemy>();
            if (parentLegoEnemy != null)
            {
                Debug.Log($"Instantly killing parent LegoEnemy of {enemy.name}");
                KillEnemyInstantly(parentLegoEnemy.gameObject);
            }
        }
    }

    List<GameObject> GetEnemyParts(GameObject enemy)
    {
        List<GameObject> parts = new List<GameObject>();
        
        // Get the LegoEnemy component to access its parts properly
        LegoEnemy legoEnemy = enemy.GetComponent<LegoEnemy>();
        if (legoEnemy != null)
        {
            // Use reflection to access the colliderParts list
            System.Reflection.FieldInfo colliderPartsField = typeof(LegoEnemy).GetField("colliderParts", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (colliderPartsField != null)
            {
                List<GameObject> colliderParts = (List<GameObject>)colliderPartsField.GetValue(legoEnemy);
                if (colliderParts != null)
                {
                    parts.AddRange(colliderParts);
                }
            }
            
            // Also get weapon group parts
            System.Reflection.FieldInfo weaponGroupsField = typeof(LegoEnemy).GetField("weaponGroups", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (weaponGroupsField != null)
            {
                List<WeaponGroup> weaponGroups = (List<WeaponGroup>)weaponGroupsField.GetValue(legoEnemy);
                if (weaponGroups != null)
                {
                    foreach (WeaponGroup group in weaponGroups)
                    {
                        if (group.parts != null)
                        {
                            parts.AddRange(group.parts);
                        }
                    }
                }
            }
        }
        
        // Fallback: if we couldn't get parts from LegoEnemy, get all child renderers
        if (parts.Count == 0)
        {
            // Add the enemy itself if it has a renderer
            if (enemy.GetComponent<Renderer>() != null)
            {
                parts.Add(enemy);
            }
            
            // Add all child objects with renderers
            Renderer[] childRenderers = enemy.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in childRenderers)
            {
                if (renderer.gameObject != enemy && !parts.Contains(renderer.gameObject))
                {
                    parts.Add(renderer.gameObject);
                }
            }
        }
        
        return parts;
    }

    Vector3 GetEnemyCenter(GameObject enemy)
    {
        // Get all parts and calculate their collective center
        List<GameObject> parts = GetEnemyParts(enemy);
        
        if (parts.Count == 0)
            return enemy.transform.position;
        
        Vector3 totalPosition = Vector3.zero;
        int validParts = 0;
        
        foreach (GameObject part in parts)
        {
            if (part != null)
            {
                totalPosition += part.transform.position;
                validParts++;
            }
        }
        
        if (validParts > 0)
            return totalPosition / validParts;
        else
            return enemy.transform.position;
    }

    void ApplySlicePhysics(GameObject part, Vector3 forceDirection, bool isTopHalf)
    {
        // This method is no longer used since we now use groups
        // Keeping it for backward compatibility but it shouldn't be called
        Debug.LogWarning("ApplySlicePhysics called on individual part - this shouldn't happen with the new group system");
    }

    void DisablePlayerControl()
    {
        // DO NOT disable any components to avoid deletion issues
        // Just set the flag for tracking state
        playerControlDisabled = true;
        Debug.Log("Player control disabled for chainsaw slice (no components actually disabled)");
    }

    void EnablePlayerControl()
    {
        // DO NOT re-enable any components to avoid issues
        // Just reset the flag
        if (playerControlDisabled)
        {
            playerControlDisabled = false;
            Debug.Log("Player control re-enabled after chainsaw slice (no components actually changed)");
        }
    }

    void OnDestroy()
    {
        // Make sure to re-enable player control if the weapon is destroyed during slicing
        if (playerControlDisabled)
        {
            EnablePlayerControl();
        }
        
        // Clean up visualization objects
        if (targetLine != null)
            DestroyImmediate(targetLine.gameObject);
        if (slicePlaneVisualizer != null)
            DestroyImmediate(slicePlaneVisualizer);
    }

    void OnDrawGizmosSelected()
    {
        // Draw chainsaw range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chainsawRange);
        
        // Draw slice plane if we have a target
        if (showTargeting && !isSlicing)
        {
            GameObject target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 targetCenter = GetEnemyCenter(target);
                
                Gizmos.color = Color.yellow;
                // Draw horizontal slice line
                Gizmos.DrawLine(targetCenter - transform.right * 2f, targetCenter + transform.right * 2f);
                Gizmos.DrawLine(targetCenter - transform.forward * 2f, targetCenter + transform.forward * 2f);
                
                // Draw a marker for the root enemy
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(target.transform.position, Vector3.one * 0.5f);
            }
        }
    }
}