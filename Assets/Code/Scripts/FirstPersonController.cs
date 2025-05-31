using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    private CharacterController characterController;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float decelerationGrounded = 10f;
    [SerializeField] private float decelerationAirborne = 5f;
    [SerializeField] private float airborneControlFactor = 0.6f;
    
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private int maxDashes = 2;
    [SerializeField] private float dashRechargeTime = 1f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float jumpCooldown = 0.1f;
    
    [Header("Camera Settings")]
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private float upperLookLimit = 80f;
    [SerializeField] private float lowerLookLimit = 80f;
    [SerializeField] private float cameraSmoothTime = 0.03f;
    
    // Internal variables
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetVelocity = Vector3.zero;
    private float verticalRotation = 0f;
    private float jumpTimeStamp = 0f;
    private bool jumping = false;
    private Vector2 currentLookDelta;
    private Vector2 targetLookDelta;
    private Vector2 lookVelocity;
    
    // Dash variables
    private int currentDashes;
    private bool isDashing = false;
    private float dashTimeStamp = 0f;
    private float lastDashRechargeTime = 0f;
    private Vector3 dashDirection;
    
    // Jump variables
    private int currentJumps;
    private bool wasGroundedLastFrame = true;
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        
        // Initialize dash and jump counts
        currentDashes = maxDashes;
        currentJumps = maxJumps;
        
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleInput();
        HandleDash();
        HandleMovement();
        HandleLook();
        ApplyGravity();
        UpdateGroundedState();
        RechargeDashes();
    }

    private void HandleInput()
    {
        // Get movement input using legacy input system
        moveInput.x = Input.GetAxis("Horizontal");
        moveInput.y = Input.GetAxis("Vertical");
        
        // Get look input using legacy input system
        lookInput.x = Input.GetAxis("Mouse X");
        lookInput.y = Input.GetAxis("Mouse Y");
        
        // Handle jump input
        if (Input.GetButtonDown("Jump"))
        {
            OnJump();
        }
        
        // Handle dash input
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            OnDash();
        }
    }

    private void HandleDash()
    {
        // Check if dash duration has ended
        if (isDashing && Time.time >= dashTimeStamp + dashDuration)
        {
            isDashing = false;
        }
    }

    private void HandleMovement()
    {
        // Calculate move direction based on camera orientation (forward is where camera looks)
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        moveDirection = transform.TransformDirection(moveDirection);
        
        // If dashing, use dash movement
        if (isDashing)
        {
            // Use dash direction and speed, cancel horizontal movement
            targetVelocity = dashDirection * dashSpeed;
        }
        else
        {
            // Normal movement
            targetVelocity = moveDirection * walkSpeed;
        }
        
        // Only maintain y velocity from current velocity
        float yVelocity = currentVelocity.y;
        currentVelocity.y = 0;
        
        // Apply acceleration - different values when grounded vs airborne
        float currentAcceleration = characterController.isGrounded ? acceleration : acceleration * airborneControlFactor;
        float currentDeceleration = characterController.isGrounded ? decelerationGrounded : decelerationAirborne;
        
        // If dashing, apply movement immediately
        if (isDashing)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, currentAcceleration * Time.deltaTime * 3f); // Faster acceleration for dash
        }
        else if (moveInput.magnitude > 0.1f)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, currentAcceleration * Time.deltaTime);
        }
        else
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, currentDeceleration * Time.deltaTime);
        }
        
        // Restore y velocity
        currentVelocity.y = yVelocity;
        
        // Apply movement
        characterController.Move(currentVelocity * Time.deltaTime);
    }

    private void HandleLook()
    {
        // Apply smoothing to prevent jittering
        targetLookDelta = lookInput * lookSensitivity;
        currentLookDelta = Vector2.SmoothDamp(currentLookDelta, targetLookDelta, ref lookVelocity, cameraSmoothTime);
        
        // Apply horizontal rotation to the player
        transform.Rotate(Vector3.up * currentLookDelta.x);
        
        // Apply vertical rotation to the camera
        verticalRotation -= currentLookDelta.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -upperLookLimit, lowerLookLimit);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && currentVelocity.y < 0)
        {
            // Small negative value when grounded to keep player on the ground
            currentVelocity.y = -2f;
        }
        else
        {
            // Apply gravity
            currentVelocity.y += gravity * Time.deltaTime;
        }
    }

    private void OnJump()
    {
        if (currentJumps > 0 && Time.time >= jumpTimeStamp)
        {
            // Calculate jump velocity from height using physics formula v = sqrt(2 * g * h)
            currentVelocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            jumpTimeStamp = Time.time + jumpCooldown;
            jumping = true;
            
            // Consume a jump
            currentJumps--;
        }
    }
    
    private void OnDash()
    {
        if (currentDashes > 0 && !isDashing)
        {
            // Set dash direction to forward (camera's forward direction)
            dashDirection = cameraTransform.forward;
            dashDirection.y = 0; // Remove vertical component
            dashDirection.Normalize();
            
            // Start dash
            isDashing = true;
            dashTimeStamp = Time.time;
            
            // Consume a dash
            currentDashes--;
        }
    }
    
    private void UpdateGroundedState()
    {
        // Reset jumps when landing
        if (characterController.isGrounded && !wasGroundedLastFrame)
        {
            currentJumps = maxJumps;
        }
        
        wasGroundedLastFrame = characterController.isGrounded;
    }
    
    private void RechargeDashes()
    {
        // Recharge dashes over time when not at max
        if (currentDashes < maxDashes && Time.time >= lastDashRechargeTime + dashRechargeTime)
        {
            currentDashes++;
            lastDashRechargeTime = Time.time;
        }
    }
    
    // Optional: Add these methods for UI or debugging
    public int GetCurrentDashes() => currentDashes;
    public int GetMaxDashes() => maxDashes;
    public int GetCurrentJumps() => currentJumps;
    public int GetMaxJumps() => maxJumps;
    public bool IsDashing() => isDashing;
}