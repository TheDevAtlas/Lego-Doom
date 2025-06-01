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

    [Header("Slicing Configuration")]
    public Vector3 slicePlaneNormal = Vector3.up; // Direction of the slice (up = horizontal cut)
    public float sliceOffset = 0f; // Offset from enemy center for slice plane
    public bool useRandomSliceAngle = false;
    public float minRandomAngle = -30f;
    public float maxRandomAngle = 30f;

    [Header("Auto Fire")]
    public bool autoFire = false;
    public float fireDelay = 3f; // Longer delay for chainsaw

    [Header("Animation")]
    public Animator chainsawAnimator; // Animator for chainsaw
    public string sliceAnimationTrigger = "Slice"; // Animation trigger name
    public AudioSource chainsawAudio; // Audio source for chainsaw sounds

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
            // Try to find common player controller types
            playerController = playerTransform.GetComponent<FirstPersonController>() as MonoBehaviour;
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
        Vector3 sliceNormal = GetSliceNormal(target);
        
        // Position the slice plane at target center with offset
        slicePlaneVisualizer.transform.position = targetCenter + (sliceNormal * sliceOffset);
        
        // Orient the plane perpendicular to the slice normal
        slicePlaneVisualizer.transform.rotation = Quaternion.LookRotation(sliceNormal, Vector3.up);
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
            
            float distance = Vector3.Distance(transform.position, col.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = col.gameObject;
            }
        }
        
        return nearestEnemy;
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
        Vector3 targetDirection = (target.transform.position - playerTransform.position).normalized;
        targetDirection.y = 0; // Keep rotation only on Y axis
        
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        
        while (Quaternion.Angle(playerTransform.rotation, targetRotation) > 1f)
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
        // Get all objects that make up this enemy
        List<GameObject> enemyParts = GetEnemyParts(enemy);
        
        if (enemyParts.Count == 0)
        {
            Debug.LogWarning($"No enemy parts found for {enemy.name}");
            return;
        }
        
        Vector3 sliceNormal = GetSliceNormal(enemy);
        Vector3 enemyCenter = GetEnemyCenter(enemy);
        Vector3 slicePosition = enemyCenter + (sliceNormal * sliceOffset);
        
        Debug.Log($"Slicing enemy {enemy.name} with {enemyParts.Count} parts");
        
        foreach (GameObject part in enemyParts)
        {
            if (part != null)
            {
                SliceObject(part, slicePosition, sliceNormal);
            }
        }
        
        // Optionally destroy the original enemy parent object
        if (enemy != null)
        {
            // Add death effects, sounds, etc. here
            Destroy(enemy, 0.1f); // Small delay to allow slice effects to show
        }
    }

    List<GameObject> GetEnemyParts(GameObject enemy)
    {
        List<GameObject> parts = new List<GameObject>();
        
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
        
        return parts;
    }

    Vector3 GetEnemyCenter(GameObject enemy)
    {
        // Calculate the center of all renderers in the enemy
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return enemy.transform.position;
        
        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }
        
        return combinedBounds.center;
    }

    Vector3 GetSliceNormal(GameObject enemy)
    {
        if (useRandomSliceAngle)
        {
            float randomAngle = Random.Range(minRandomAngle, maxRandomAngle);
            return Quaternion.Euler(0, randomAngle, 0) * slicePlaneNormal;
        }
        return slicePlaneNormal;
    }

    void SliceObject(GameObject obj, Vector3 slicePosition, Vector3 sliceNormal)
    {
        // Create two halves of the object
        GameObject upperHalf = CreateObjectHalf(obj, slicePosition, sliceNormal, true);
        GameObject lowerHalf = CreateObjectHalf(obj, slicePosition, sliceNormal, false);
        
        // Add physics to the halves for dramatic effect
        AddSlicePhysics(upperHalf, sliceNormal);
        AddSlicePhysics(lowerHalf, -sliceNormal);
        
        // Hide original object
        obj.SetActive(false);
    }

    GameObject CreateObjectHalf(GameObject original, Vector3 slicePosition, Vector3 sliceNormal, bool upperHalf)
    {
        // Create a copy of the original object
        GameObject half = Instantiate(original);
        half.name = original.name + (upperHalf ? "_Upper" : "_Lower");
        
        // Scale the half appropriately (simple approach - you might want more sophisticated slicing)
        Vector3 scale = half.transform.localScale;
        if (sliceNormal == Vector3.up || sliceNormal == Vector3.down)
        {
            scale.y *= 0.5f;
            half.transform.localScale = scale;
            
            // Offset position
            Vector3 offset = sliceNormal * (scale.y * 0.5f);
            if (!upperHalf) offset = -offset;
            half.transform.position = slicePosition + offset;
        }
        else
        {
            // Handle other slice directions similarly
            scale.x *= 0.5f;
            half.transform.localScale = scale;
            
            Vector3 offset = sliceNormal * (scale.x * 0.5f);
            if (!upperHalf) offset = -offset;
            half.transform.position = slicePosition + offset;
        }
        
        return half;
    }

    void AddSlicePhysics(GameObject half, Vector3 forceDirection)
    {
        // Add rigidbody if it doesn't exist
        Rigidbody rb = half.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = half.AddComponent<Rigidbody>();
        }
        
        // Add force to make the halves separate dramatically
        Vector3 force = forceDirection * 5f + Vector3.up * 2f; // Upward and outward force
        rb.AddForce(force, ForceMode.Impulse);
        
        // Add some random torque for spinning effect
        Vector3 torque = new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f));
        rb.AddTorque(torque, ForceMode.Impulse);
        
        // Destroy the half after a few seconds
        Destroy(half, 5f);
    }

    void DisablePlayerControl()
    {
        if (playerController != null)
        {
            playerController.enabled = false;
            playerControlDisabled = true;
            Debug.Log("Player control disabled for chainsaw slice");
        }
    }

    void EnablePlayerControl()
    {
        if (playerController != null && playerControlDisabled)
        {
            playerController.enabled = true;
            playerControlDisabled = false;
            Debug.Log("Player control re-enabled after chainsaw slice");
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
                Vector3 sliceNormal = GetSliceNormal(target);
                Vector3 slicePos = targetCenter + (sliceNormal * sliceOffset);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(slicePos - Vector3.right, slicePos + Vector3.right);
                Gizmos.DrawLine(slicePos - Vector3.forward, slicePos + Vector3.forward);
            }
        }
    }
}