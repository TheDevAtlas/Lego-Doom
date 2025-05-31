using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    [Header("Scrollable Weapons")]
    [SerializeField] private List<Weapon> weapons = new List<Weapon>();
    [SerializeField] private int currentWeaponIndex = 0;
    
    [Header("Special Tools")]
    [SerializeField] private List<SpecialTool> specialTools = new List<SpecialTool>();
    
    [Header("Shooting Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float shootRange = 100f;
    [SerializeField] private LayerMask shootableLayer = -1;
    [SerializeField] private Material bulletHoleMaterial;
    
    [System.Serializable]
    public class Weapon
    {
        public string name;
        public GameObject weaponObject;
        [Space(5)]
        [Header("Ammo")]
        public int currentAmmo;
        public int maxAmmo;
        [Space(5)]
        [Header("Shooting")]
        public bool canShoot = true;
        public bool isAutomatic = false;
        public float fireRate = 0.1f; // Time between shots in seconds
    }
    
    [System.Serializable]
    public class SpecialTool
    {
        public string name;
        public KeyCode keyCode;
        public GameObject toolObject;
        public bool toggleMode = false; // If true, tool stays active until pressed again
        [Space(5)]
        [Header("Ammo (if applicable)")]
        public bool usesAmmo = false;
        public int currentAmmo;
        public int maxAmmo;
    }
    
    private Weapon currentActiveWeapon;
    private SpecialTool currentActiveTool;
    private bool isToolActive = false;
    
    // Shooting variables
    private float lastShotTime = 0f;
    
    void Start()
    {
        InitializeWeapons();
        SelectWeapon(currentWeaponIndex);
        
        // Auto-find camera if not assigned
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        // Create bullet hole material if not assigned
        if (bulletHoleMaterial == null)
            CreateDefaultBulletHoleMaterial();
    }
    
    void Update()
    {
        HandleWeaponScrolling();
        HandleNumberKeySelection();
        HandleSpecialTools();
        HandleShooting();
    }
    
    void InitializeWeapons()
    {
        // Disable all weapons at start
        foreach (Weapon weapon in weapons)
        {
            if (weapon.weaponObject != null)
                weapon.weaponObject.SetActive(false);
        }
        
        // Disable all special tools at start
        foreach (SpecialTool tool in specialTools)
        {
            if (tool.toolObject != null)
                tool.toolObject.SetActive(false);
        }
    }
    
    void HandleWeaponScrolling()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (scroll > 0f)
        {
            // Scroll up - next weapon
            NextWeapon();
        }
        else if (scroll < 0f)
        {
            // Scroll down - previous weapon
            PreviousWeapon();
        }
    }
    
    void HandleNumberKeySelection()
    {
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                int weaponIndex = i - 1;
                if (weaponIndex < weapons.Count)
                {
                    SelectWeapon(weaponIndex);
                }
            }
        }
    }
    
    void HandleSpecialTools()
    {
        foreach (SpecialTool tool in specialTools)
        {
            if (Input.GetKeyDown(tool.keyCode))
            {
                if (tool.toggleMode)
                {
                    ToggleTool(tool);
                }
                else
                {
                    ActivateTool(tool);
                }
            }
            else if (!tool.toggleMode && Input.GetKeyUp(tool.keyCode))
            {
                DeactivateTool(tool);
            }
        }
    }
    
    void NextWeapon()
    {
        if (weapons.Count <= 1) return;
        
        currentWeaponIndex = (currentWeaponIndex + 1) % weapons.Count;
        SelectWeapon(currentWeaponIndex);
    }
    
    void PreviousWeapon()
    {
        if (weapons.Count <= 1) return;
        
        currentWeaponIndex--;
        if (currentWeaponIndex < 0)
            currentWeaponIndex = weapons.Count - 1;
        
        SelectWeapon(currentWeaponIndex);
    }
    
    void SelectWeapon(int index)
    {
        if (index < 0 || index >= weapons.Count || weapons[index].weaponObject == null)
            return;
        
        // Deactivate current weapon
        if (currentActiveWeapon != null)
            currentActiveWeapon.weaponObject.SetActive(false);
        
        // Deactivate any active tools when switching weapons
        DeactivateAllTools();
        
        // Activate new weapon
        currentWeaponIndex = index;
        currentActiveWeapon = weapons[index];
        currentActiveWeapon.weaponObject.SetActive(true);
        
        // Optional: Call events or other weapon-specific logic here
        OnWeaponChanged(currentActiveWeapon);
    }
    
    void ActivateTool(SpecialTool tool)
    {
        if (tool.toolObject == null) return;
        
        // Deactivate current weapon
        if (currentActiveWeapon != null)
            currentActiveWeapon.weaponObject.SetActive(false);
        
        // Deactivate other tools
        DeactivateAllTools();
        
        // Activate tool
        tool.toolObject.SetActive(true);
        currentActiveTool = tool;
        isToolActive = true;
        
        OnToolActivated(tool);
    }
    
    void DeactivateTool(SpecialTool tool)
    {
        if (tool.toolObject == null) return;
        
        tool.toolObject.SetActive(false);
        
        if (currentActiveTool == tool)
        {
            currentActiveTool = null;
            isToolActive = false;
            
            // Reactivate current weapon
            if (currentActiveWeapon != null)
                currentActiveWeapon.weaponObject.SetActive(true);
        }
        
        OnToolDeactivated(tool);
    }
    
    void ToggleTool(SpecialTool tool)
    {
        if (tool.toolObject == null) return;
        
        if (currentActiveTool == tool)
        {
            DeactivateTool(tool);
        }
        else
        {
            ActivateTool(tool);
        }
    }
    
    void DeactivateAllTools()
    {
        foreach (SpecialTool tool in specialTools)
        {
            if (tool.toolObject != null && tool.toolObject.activeInHierarchy)
            {
                tool.toolObject.SetActive(false);
            }
        }
        
        currentActiveTool = null;
        isToolActive = false;
    }
    
    void HandleShooting()
    {
        // Only handle shooting if we have an active weapon that can shoot
        if (currentActiveWeapon == null || !currentActiveWeapon.canShoot || isToolActive)
            return;
            
        bool wantToShoot = false;
        
        if (currentActiveWeapon.isAutomatic)
        {
            // For automatic weapons, check if mouse button is held down
            wantToShoot = Input.GetMouseButton(0);
        }
        else
        {
            // For semi-automatic weapons, check for mouse button press
            wantToShoot = Input.GetMouseButtonDown(0);
        }
        
        if (wantToShoot && CanShoot())
        {
            Shoot();
        }
    }
    
    bool CanShoot()
    {
        // Check if enough time has passed since last shot
        if (Time.time - lastShotTime < currentActiveWeapon.fireRate)
            return false;
            
        // Check if we have ammo
        if (currentActiveWeapon.currentAmmo <= 0)
            return false;
            
        return true;
    }
    
    void Shoot()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("No camera assigned for shooting!");
            return;
        }
        
        // Consume ammo
        if (!ConsumeWeaponAmmo(1))
            return;
            
        // Update last shot time
        lastShotTime = Time.time;
        
        // Create ray from camera center
        Ray shootRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        
        // Perform raycast
        if (Physics.Raycast(shootRay, out hit, shootRange, shootableLayer))
        {
            // Hit something - create bullet hole
            CreateBulletHole(hit.point, hit.normal);
            
            // Call hit event for extending functionality
            OnBulletHit(hit);
        }
        
        // Call shoot event for extending functionality (muzzle flash, sound, etc.)
        OnWeaponShoot(currentActiveWeapon);
    }
    
    void CreateBulletHole(Vector3 position, Vector3 normal)
    {
        // Create a small sphere at hit position
        GameObject bulletHole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletHole.name = "BulletHole";
        
        // Position and scale the bullet hole
        bulletHole.transform.position = position + normal * 0.01f; // Slightly offset from surface
        bulletHole.transform.localScale = Vector3.one * 0.1f; // Small sphere
        
        // Orient the bullet hole to the surface normal
        bulletHole.transform.LookAt(position + normal);
        
        // Apply material
        if (bulletHoleMaterial != null)
        {
            Renderer renderer = bulletHole.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = bulletHoleMaterial;
        }
        
        // Remove collider (we don't want bullet holes to be collidable)
        Collider collider = bulletHole.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
            
        // Destroy after 1 second
        Destroy(bulletHole, 1f);
    }
    
    void CreateDefaultBulletHoleMaterial()
    {
        // Create a simple green material for URP
        bulletHoleMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bulletHoleMaterial.color = Color.green;
        bulletHoleMaterial.name = "BulletHole_Green";
    }
    
    // Public methods for external access
    public Weapon GetCurrentWeapon()
    {
        return currentActiveWeapon;
    }
    
    public SpecialTool GetCurrentTool()
    {
        return currentActiveTool;
    }
    
    public bool IsToolActive()
    {
        return isToolActive;
    }
    
    public void ForceSelectWeapon(int index)
    {
        SelectWeapon(index);
    }
    
    // Ammo management methods
    public bool ConsumeWeaponAmmo(int amount = 1)
    {
        if (currentActiveWeapon == null) return false;
        
        if (currentActiveWeapon.currentAmmo >= amount)
        {
            currentActiveWeapon.currentAmmo -= amount;
            return true;
        }
        return false;
    }
    
    public bool ConsumeToolAmmo(int amount = 1)
    {
        if (currentActiveTool == null || !currentActiveTool.usesAmmo) return false;
        
        if (currentActiveTool.currentAmmo >= amount)
        {
            currentActiveTool.currentAmmo -= amount;
            return true;
        }
        return false;
    }
    
    public void AddWeaponAmmo(int amount)
    {
        if (currentActiveWeapon == null) return;
        
        currentActiveWeapon.currentAmmo = Mathf.Min(
            currentActiveWeapon.currentAmmo + amount, 
            currentActiveWeapon.maxAmmo
        );
    }
    
    public void AddToolAmmo(int amount)
    {
        if (currentActiveTool == null || !currentActiveTool.usesAmmo) return;
        
        currentActiveTool.currentAmmo = Mathf.Min(
            currentActiveTool.currentAmmo + amount, 
            currentActiveTool.maxAmmo
        );
    }
    
    public void AddAmmoToWeapon(int weaponIndex, int amount)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Count) return;
        
        weapons[weaponIndex].currentAmmo = Mathf.Min(
            weapons[weaponIndex].currentAmmo + amount,
            weapons[weaponIndex].maxAmmo
        );
    }
    
    public void AddAmmoToTool(string toolName, int amount)
    {
        SpecialTool tool = specialTools.Find(t => t.name == toolName);
        if (tool != null && tool.usesAmmo)
        {
            tool.currentAmmo = Mathf.Min(
                tool.currentAmmo + amount,
                tool.maxAmmo
            );
        }
    }
    
    public int GetCurrentWeaponAmmo()
    {
        return currentActiveWeapon?.currentAmmo ?? 0;
    }
    
    public int GetCurrentWeaponMaxAmmo()
    {
        return currentActiveWeapon?.maxAmmo ?? 0;
    }
    
    public int GetCurrentToolAmmo()
    {
        return currentActiveTool?.currentAmmo ?? 0;
    }
    
    public int GetCurrentToolMaxAmmo()
    {
        return currentActiveTool?.maxAmmo ?? 0;
    }
    
    // Shooting related public methods
    public bool CanCurrentWeaponShoot()
    {
        return currentActiveWeapon != null && currentActiveWeapon.canShoot && !isToolActive && CanShoot();
    }
    
    public void SetShootRange(float range)
    {
        shootRange = range;
    }
    
    public void SetShootableLayer(LayerMask layer)
    {
        shootableLayer = layer;
    }
    
    // Override methods for custom behavior
    protected virtual void OnWeaponChanged(Weapon newWeapon)
    {
        // Override this method to add custom weapon change logic
        Debug.Log($"Weapon changed to: {newWeapon.name} (Ammo: {newWeapon.currentAmmo}/{newWeapon.maxAmmo})");
    }
    
    protected virtual void OnToolActivated(SpecialTool tool)
    {
        // Override this method to add custom tool activation logic
        string ammoInfo = tool.usesAmmo ? $" (Ammo: {tool.currentAmmo}/{tool.maxAmmo})" : "";
        Debug.Log($"Tool activated: {tool.name}{ammoInfo}");
    }
    
    protected virtual void OnToolDeactivated(SpecialTool tool)
    {
        // Override this method to add custom tool deactivation logic
        Debug.Log($"Tool deactivated: {tool.name}");
    }
    
    // Shooting events for extending functionality
    protected virtual void OnWeaponShoot(Weapon weapon)
    {
        // Override this method to add custom shooting logic (muzzle flash, sound, etc.)
        Debug.Log($"{weapon.name} fired! Remaining ammo: {weapon.currentAmmo}");
    }
    
    protected virtual void OnBulletHit(RaycastHit hit)
    {
        // Override this method to add custom hit logic (damage, particle effects, etc.)
        Debug.Log($"Bullet hit: {hit.collider.name} at {hit.point}");
    }
}