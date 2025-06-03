using UnityEngine;

public class WeaponAnimationController : MonoBehaviour
{
    [Header("Animation Components")]
    public Animator weaponAnimator; // Animator for the weapon
    public FirstPersonController playerController; // Reference to player controller
    
    [Header("Animation Parameters")]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string isShootingParameter = "IsShooting";
    [SerializeField] private string shootTriggerParameter = "Shoot";
    
    [Header("Shooting Animation Settings")]
    [SerializeField] private float shootAnimationDuration = 0.5f; // How long the shooting animation lasts
    
    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold = 0.1f; // Minimum movement to trigger movement animation
    [SerializeField] private float movementSmoothTime = 0.1f; // Smoothing for movement transitions
    
    // Internal state tracking
    private bool isCurrentlyMoving = false;
    private bool isCurrentlyShooting = false;
    private float shootingTimer = 0f;
    private float lastMovementMagnitude = 0f;
    private Vector3 lastPlayerPosition;
    
    void Start()
    {
        // Auto-find components if not assigned
        if (weaponAnimator == null)
            weaponAnimator = GetComponent<Animator>();
            
        if (playerController == null)
            playerController = FindObjectOfType<FirstPersonController>();
        
        // Initialize last position
        if (playerController != null)
            lastPlayerPosition = playerController.transform.position;
        
        // Validate animator
        if (weaponAnimator == null)
        {
            Debug.LogError($"No Animator found on {gameObject.name}! Weapon animations will not work.");
            return;
        }
        
        // Check if animation parameters exist
        ValidateAnimationParameters();
    }
    
    void Update()
    {
        if (weaponAnimator == null) return;
        
        UpdateMovementDetection();
        UpdateShootingState();
        UpdateAnimatorParameters();
    }
    
    void ValidateAnimationParameters()
    {
        if (weaponAnimator.parameters.Length == 0)
        {
            Debug.LogWarning($"Animator on {gameObject.name} has no parameters. Make sure to set up the Animator Controller with the required parameters.");
            return;
        }
        
        bool hasIsMoving = false;
        bool hasIsShooting = false;
        bool hasShootTrigger = false;
        
        foreach (AnimatorControllerParameter param in weaponAnimator.parameters)
        {
            if (param.name == isMovingParameter && param.type == AnimatorControllerParameterType.Bool)
                hasIsMoving = true;
            else if (param.name == isShootingParameter && param.type == AnimatorControllerParameterType.Bool)
                hasIsShooting = true;
            else if (param.name == shootTriggerParameter && param.type == AnimatorControllerParameterType.Trigger)
                hasShootTrigger = true;
        }
        
        if (!hasIsMoving)
            Debug.LogWarning($"Animator parameter '{isMovingParameter}' (Bool) not found in {gameObject.name}");
        if (!hasIsShooting)
            Debug.LogWarning($"Animator parameter '{isShootingParameter}' (Bool) not found in {gameObject.name}");
        if (!hasShootTrigger)
            Debug.LogWarning($"Animator parameter '{shootTriggerParameter}' (Trigger) not found in {gameObject.name}");
    }
    
    void UpdateMovementDetection()
    {
        if (playerController == null) return;
        
        // Calculate movement based on position change
        Vector3 currentPosition = playerController.transform.position;
        Vector3 movementDelta = currentPosition - lastPlayerPosition;
        movementDelta.y = 0; // Ignore vertical movement for breathing/movement detection
        
        float currentMovementMagnitude = movementDelta.magnitude / Time.deltaTime;
        
        // Smooth the movement detection to avoid jittery transitions
        lastMovementMagnitude = Mathf.Lerp(lastMovementMagnitude, currentMovementMagnitude, Time.deltaTime / movementSmoothTime);
        
        // Update movement state
        isCurrentlyMoving = lastMovementMagnitude > movementThreshold;
        
        lastPlayerPosition = currentPosition;
    }
    
    void UpdateShootingState()
    {
        if (isCurrentlyShooting)
        {
            shootingTimer -= Time.deltaTime;
            if (shootingTimer <= 0f)
            {
                isCurrentlyShooting = false;
            }
        }
    }
    
    void UpdateAnimatorParameters()
    {
        // Set movement parameter
        weaponAnimator.SetBool(isMovingParameter, isCurrentlyMoving);
        
        // Set shooting parameter
        weaponAnimator.SetBool(isShootingParameter, isCurrentlyShooting);
    }
    
    /// <summary>
    /// Call this method when a weapon fires to trigger the shooting animation
    /// </summary>
    public void TriggerShootAnimation()
    {
        if (weaponAnimator == null) return;
        
        // Set shooting state
        isCurrentlyShooting = true;
        shootingTimer = shootAnimationDuration;
        
        // Trigger the shoot animation
        weaponAnimator.SetTrigger(shootTriggerParameter);
        
        Debug.Log($"Weapon shoot animation triggered on {gameObject.name}");
    }
    
    /// <summary>
    /// Call this to manually set the shooting animation duration
    /// </summary>
    public void SetShootAnimationDuration(float duration)
    {
        shootAnimationDuration = duration;
    }
    
    /// <summary>
    /// Get current animation state for debugging
    /// </summary>
    public void GetAnimationState(out bool moving, out bool shooting)
    {
        moving = isCurrentlyMoving;
        shooting = isCurrentlyShooting;
    }
    
    /// <summary>
    /// Force update the animation state (useful when weapon becomes active)
    /// </summary>
    public void RefreshAnimationState()
    {
        if (weaponAnimator == null) return;
        
        UpdateMovementDetection();
        UpdateAnimatorParameters();
    }
    
    // Optional: Method to override movement detection for special cases
    public void SetMovementOverride(bool isMoving, float duration = -1f)
    {
        isCurrentlyMoving = isMoving;
        
        if (duration > 0f)
        {
            StartCoroutine(ResetMovementOverride(duration));
        }
    }
    
    private System.Collections.IEnumerator ResetMovementOverride(float duration)
    {
        yield return new WaitForSeconds(duration);
        // Movement detection will resume normally on next Update()
    }
    
    void OnEnable()
    {
        // Refresh animation state when weapon becomes active
        RefreshAnimationState();
    }
    
    void OnDisable()
    {
        // Reset shooting state when weapon is disabled
        isCurrentlyShooting = false;
        shootingTimer = 0f;
    }
}