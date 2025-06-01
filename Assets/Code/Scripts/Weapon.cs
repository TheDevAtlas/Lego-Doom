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

    [Header("Bullet Animation")]
    public float bulletTravelTime = 0.1f; // Time for bullet to travel from start to hit point

    [Header("Raycast Visualization")]
    public bool showRaycast = true;
    public KeyCode toggleRaycastKey = KeyCode.R;
    public LayerMask playerLayerMask = 1;
    public Material hitLineMaterial;
    public Material missLineMaterial;    
    public float shootFeedbackDuration = 2f; // How long to show shoot feedback
    public float infiniteVisualizationDistance = 1000f; // Distance for infinite range visualization

    [Header("Input")]
    public KeyCode fireKey = KeyCode.Mouse0;

    // Timing variables
    private float fireTimer = 0f;
    private float flashTimer = 0f;
    private bool flashActive = false;
    
    // Visualization objects
    private LineRenderer aimLine;
    private GameObject aimEndPoint;
    private GameObject[] bulletSpheres;
    private int maxBulletSpheres = 10; // Pool size for bullet spheres
    private int currentBulletIndex = 0;
    
    // Bullet animation data
    private BulletData[] bulletDataArray;
    
    // Bullet animation struct
    [System.Serializable]
    public struct BulletData
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float animationTimer;
        public bool isAnimating;
        public bool isHit; // Whether this bullet hit something
    }
    
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

        CreateVisualizationObjects();
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        // Handle raycast toggle
        if (Input.GetKeyDown(toggleRaycastKey))
        {
            showRaycast = !showRaycast;
            UpdateVisualizationVisibility();
        }

        // Update continuous aim visualization
        if (showRaycast)
        {
            UpdateAimVisualization();
        }

        // Update bullet animation and feedback
        UpdateBulletAnimation();

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

    void CreateVisualizationObjects()
    {
        // Create materials if not assigned
        if (hitLineMaterial == null)
            hitLineMaterial = CreateURPMaterial(Color.green);
        if (missLineMaterial == null)
            missLineMaterial = CreateURPMaterial(Color.red);

        // Create aim line (continuous visualization)
        CreateAimLine();
        
        // Create bullet spheres (temporary feedback when shooting)
        CreateBulletSpheres();
        
        UpdateVisualizationVisibility();
    }

    void CreateAimLine()
    {
        // Aim line for continuous visualization
        GameObject aimLineObj = new GameObject("AimLine");
        aimLineObj.transform.SetParent(transform);
        aimLine = aimLineObj.AddComponent<LineRenderer>();
        
        ConfigureLineRenderer(aimLine, 0.02f);
        
        // Create aim endpoint sphere
        aimEndPoint = CreateSphere("AimEndPoint", 0.1f, Color.white);
    }

    void CreateBulletSpheres()
    {
        bulletSpheres = new GameObject[maxBulletSpheres];
        bulletDataArray = new BulletData[maxBulletSpheres];
        
        for (int i = 0; i < maxBulletSpheres; i++)
        {
            // Create bullet impact sphere
            bulletSpheres[i] = CreateSphere($"BulletSphere_{i}", 0.08f, Color.yellow);
            bulletSpheres[i].SetActive(false);
            
            // Initialize bullet data
            bulletDataArray[i] = new BulletData
            {
                startPoint = Vector3.zero,
                endPoint = Vector3.zero,
                animationTimer = 0f,
                isAnimating = false,
                isHit = false
            };
        }
    }

    GameObject CreateSphere(string name, float scale, Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.localScale = Vector3.one * scale;
        
        // Remove collider
        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);
        
        // Apply material
        sphere.GetComponent<Renderer>().material = CreateURPMaterial(color);
        
        return sphere;
    }

    void ConfigureLineRenderer(LineRenderer lr, float width)
    {
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = true;
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
        bool visible = showRaycast;
        
        if (aimLine != null)
            aimLine.gameObject.SetActive(visible);
        if (aimEndPoint != null)
            aimEndPoint.SetActive(visible);
    }

    void UpdateAimVisualization()
    {
        if (aimLine == null || aimEndPoint == null) return;

        Vector3 startPoint = GetShootStartPoint();
        Vector3 direction = GetShootDirection();
        
        // Use much larger distance for visualization when infinite range is enabled
        float visualizationDistance = infiniteRange ? infiniteVisualizationDistance : maxDistance;
        int layerMask = ~playerLayerMask;

        if (Physics.Raycast(startPoint, direction, out RaycastHit hit, visualizationDistance, layerMask))
        {
            // Hit - green line and visible endpoint
            aimLine.material = hitLineMaterial;
            aimLine.SetPosition(0, startPoint);
            aimLine.SetPosition(1, hit.point);
            
            aimEndPoint.transform.position = hit.point;
            aimEndPoint.GetComponent<Renderer>().material = hitLineMaterial;
            aimEndPoint.SetActive(showRaycast);
        }
        else
        {
            // Miss - red line and no endpoint
            Vector3 endPoint = startPoint + (direction * visualizationDistance);
            aimLine.material = missLineMaterial;
            aimLine.SetPosition(0, startPoint);
            aimLine.SetPosition(1, endPoint);
            
            aimEndPoint.SetActive(false);
        }
    }

    void UpdateBulletAnimation()
    {
        for (int i = 0; i < maxBulletSpheres; i++)
        {
            if (bulletDataArray[i].isAnimating)
            {
                bulletDataArray[i].animationTimer += Time.deltaTime;
                
                if (bulletDataArray[i].animationTimer <= bulletTravelTime)
                {
                    // Animate bullet from start to end point
                    float t = bulletDataArray[i].animationTimer / bulletTravelTime;
                    Vector3 currentPosition = Vector3.Lerp(bulletDataArray[i].startPoint, bulletDataArray[i].endPoint, t);
                    bulletSpheres[i].transform.position = currentPosition;
                }
                else if (bulletDataArray[i].animationTimer <= bulletTravelTime + shootFeedbackDuration)
                {
                    // Keep bullet at end position during feedback duration
                    bulletSpheres[i].transform.position = bulletDataArray[i].endPoint;
                    
                    // Optional fade out effect
                    float fadeTime = bulletDataArray[i].animationTimer - bulletTravelTime;
                    float alpha = 1f - (fadeTime / shootFeedbackDuration);
                    // You could modify material alpha here if needed
                }
                else
                {
                    // Animation and feedback complete - hide bullet
                    bulletSpheres[i].SetActive(false);
                    bulletDataArray[i].isAnimating = false;
                    bulletDataArray[i].animationTimer = 0f;
                }
            }
        }
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
                ShootRay(true);
            }
        }
        else
        {
            ShootRay(false);
        }

        // MODIFIED: Always use only 1 bullet, regardless of shotgun pellets
        weaponController.UseAmmo(ammoIndex, 1);
        AnimateFlash();
    }

    void ShootRay(bool applySpread)
    {
        Vector3 startPoint = GetShootStartPoint();
        Vector3 direction = GetShootDirection();

        if (applySpread)
        {
            // NEW: Use independent X/Y spread if enabled
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

        // For actual shooting, use infinite distance if enabled
        float distance = infiniteRange ? Mathf.Infinity : maxDistance;
        int layerMask = ~playerLayerMask;

        // Get next available bullet sphere
        int sphereIndex = GetNextBulletIndex();
        GameObject bulletSphere = bulletSpheres[sphereIndex];

        if (Physics.Raycast(startPoint, direction, out RaycastHit hit, distance, layerMask))
        {
            // Hit - start bullet animation from shoot point to hit point
            Vector3 endPoint = hit.point - (direction * 0.05f); // Slightly offset from hit surface
            
            bulletDataArray[sphereIndex] = new BulletData
            {
                startPoint = startPoint,
                endPoint = endPoint,
                animationTimer = 0f,
                isAnimating = true,
                isHit = true
            };
            
            bulletSphere.transform.position = startPoint; // Start at shoot point
            bulletSphere.GetComponent<Renderer>().material = CreateURPMaterial(Color.yellow);
            bulletSphere.SetActive(showRaycast);
            
            Debug.Log($"SHOT HIT: {hit.collider.name} at {hit.point}");
        }
        else
        {
            // Miss - animate bullet to max distance (use maxDistance even for infinite weapons for animation purposes)
            Vector3 endPoint = startPoint + (direction * maxDistance);
            
            bulletDataArray[sphereIndex] = new BulletData
            {
                startPoint = startPoint,
                endPoint = endPoint,
                animationTimer = 0f,
                isAnimating = true,
                isHit = false
            };
            
            bulletSphere.transform.position = startPoint; // Start at shoot point
            bulletSphere.GetComponent<Renderer>().material = CreateURPMaterial(Color.red); // Red for miss
            bulletSphere.SetActive(showRaycast);
            
            Debug.Log("SHOT MISS: No target hit");
        }
    }

    int GetNextBulletIndex()
    {
        int index = currentBulletIndex;
        currentBulletIndex = (currentBulletIndex + 1) % maxBulletSpheres;
        return index;
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

    void OnDestroy()
    {
        // Clean up all visualization objects
        if (aimLine != null)
            DestroyImmediate(aimLine.gameObject);
        if (aimEndPoint != null)
            DestroyImmediate(aimEndPoint);
            
        if (bulletSpheres != null)
        {
            foreach (var sphere in bulletSpheres)
                if (sphere != null) DestroyImmediate(sphere);
        }
    }
}