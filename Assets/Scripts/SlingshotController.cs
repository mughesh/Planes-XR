using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Pocket Pilot Slingshot Controller
/// Handles elastic pull-back, snap-back, and plane launching
///
/// SETUP:
/// 1. Attach to Slingshot root GameObject
/// 2. Assign leatherPad reference
/// 3. Assign handle reference
/// 4. Assign planeObject (the plane already sitting on the slingshot)
/// 5. Assign flightController (the scene FlightController manager)
/// 6. Assign flightDynamics (the scene FlightDynamics)
/// </summary>
public class SlingshotController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The leather pad GameObject that can be grabbed")]
    public Transform leatherPad;
    [Tooltip("Optional spawn point for plane (if different from leather pad)")]
    public Transform spawnPoint;

    [Tooltip("The handle GameObject (base that should be held)")]
    public Transform handle;

    [Tooltip("The plane GameObject sitting on the slingshot at start")]
    public Transform planeObject;

    [Tooltip("The scene FlightController component")]
    public FlightController flightController;

    [Tooltip("The scene FlightDynamics component")]
    public FlightDynamics flightDynamics;

    [Tooltip("Main camera/XR rig for plane positioning")]
    public Transform playerCamera;

    [Header("Launch Settings")]
    [Tooltip("How far user can pull back (meters)")]
    [Range(0.1f, 0.5f)]
    public float maxPullDistance = 0.3f;

    [Tooltip("Launch force multiplier (higher = faster launch)")]
    [Range(5f, 30f)]
    public float launchPowerMultiplier = 15f;

    [Tooltip("Minimum pull distance to launch (prevents accidental launches)")]
    [Range(0.02f, 0.1f)]
    public float minLaunchDistance = 0.05f;

    [Header("Snap-Back Animation")]
    [Tooltip("How fast pad snaps back after release (higher = faster)")]
    [Range(5f, 50f)]
    public float snapBackSpeed = 40f;

    [Header("Plane Scaling")]
    [Tooltip("Scale of plane when in slingshot (local scale)")]
    public Vector3 planeScaleInSlingshot = new Vector3(0.2f, 0.2f, 0.2f);

    [Tooltip("Scale of plane after launch (world scale)")]
    public Vector3 planeScaleAfterLaunch = Vector3.one;

    [Tooltip("How long it takes to scale up after launch (seconds)")]
    [Range(0.2f, 1.5f)]
    public float scaleUpDuration = 0.6f;

    [Header("Launch Trajectory")]
    [Tooltip("Enable parabolic launch trajectory (engine startup delay)")]
    public bool enableLaunchTrajectory = true;

    [Tooltip("How long the parabolic trajectory lasts (seconds)")]
    [Range(0.3f, 1.5f)]
    public float trajectoryDuration = 0.8f;

    [Tooltip("How much plane dips down during launch (meters)")]
    [Range(0.05f, 0.3f)]
    public float trajectoryDip = 0.15f;

    [Tooltip("Forward speed during trajectory (before engine starts)")]
    [Range(0.5f, 3f)]
    public float trajectorySpeed = 1.5f;

    [Header("Two-Hand Interaction")]
    [Tooltip("Require handle to be held before pad can be grabbed")]
    public bool requireHandleGrip = true;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showDebugLogs = false;

    // Internal state
    private Vector3 restPosition;      // Where pad should be when not pulled
    private Quaternion restRotation;   // Original rotation
    private bool isGrabbed = false;
    private bool isSnappingBack = false;
    private bool hasLaunched = false;  // One-time launch flag

    // Plane components to disable/enable
    private Animator planeAnimator;
    private AudioSource planeAudioSource;

    // Meta SDK grab components
    private HandGrabInteractable padGrabInteractable;
    private HandGrabInteractable handleGrabInteractable;
    private bool isHandleGrabbed = false;

    void Start()
    {
        // Store rest position
        if (leatherPad != null)
        {
            restPosition = leatherPad.localPosition;
            restRotation = leatherPad.localRotation;
        }
        else
        {
            Debug.LogError("[Slingshot] Leather pad reference not assigned!");
            return;
        }

        // Find Meta SDK grab components
        padGrabInteractable = leatherPad.GetComponentInChildren<HandGrabInteractable>();
        if (padGrabInteractable != null)
        {
            // Disable pad grabbing at start
            padGrabInteractable.enabled = false;

            // Subscribe to grab events
            padGrabInteractable.WhenSelectingInteractorAdded.Action += OnPadGrabbed;
            padGrabInteractable.WhenSelectingInteractorRemoved.Action += OnPadReleased;
        }
        else
        {
            Debug.LogWarning("[Slingshot] No HandGrabInteractable found on leather pad. Grab events won't work.");
        }

        // Find handle grab component
        if (handle != null)
        {
            handleGrabInteractable = handle.GetComponentInChildren<HandGrabInteractable>();
            if (handleGrabInteractable != null)
            {
                handleGrabInteractable.WhenSelectingInteractorAdded.Action += OnHandleGrabbed;
                handleGrabInteractable.WhenSelectingInteractorRemoved.Action += OnHandleReleased;
            }
        }

        // Auto-find player camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main?.transform;
        }

        // Setup plane initial state
        if (planeObject != null)
        {
            // Parent plane to spawn point (if exists, otherwise to leather pad)
            Transform parentTarget = leatherPad;
            //Transform spawnPoint = leatherPad.Find("Plane_spawn");
            if (spawnPoint != null)
            {
                parentTarget = spawnPoint;
            }

            planeObject.SetParent(parentTarget);
            planeObject.localPosition = Vector3.zero;
            planeObject.localRotation = Quaternion.identity;

            // Scale down to slingshot size (local scale)
            planeObject.localScale = planeScaleInSlingshot;

            // Get plane components
            planeAnimator = planeObject.GetComponent<Animator>();
            planeAudioSource = planeObject.GetComponent<AudioSource>();

            // Disable animator and audio until launch
            if (planeAnimator != null) planeAnimator.enabled = false;
            if (planeAudioSource != null) planeAudioSource.enabled = false;

            if (showDebugLogs)
            {
                Debug.Log($"[Slingshot] Plane parented and scaled. Local scale: {planeObject.localScale}");
            }
        }
        else
        {
            Debug.LogWarning("[Slingshot] No plane object assigned!");
        }

        // Disable flight systems at start
        if (flightController != null)
        {
            flightController.enabled = false;
        }
        if (flightDynamics != null)
        {
            flightDynamics.enabled = false;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] Initialized. Rest pos: {restPosition}");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (padGrabInteractable != null)
        {
            padGrabInteractable.WhenSelectingInteractorAdded.Action -= OnPadGrabbed;
            padGrabInteractable.WhenSelectingInteractorRemoved.Action -= OnPadReleased;
        }

        if (handleGrabInteractable != null)
        {
            handleGrabInteractable.WhenSelectingInteractorAdded.Action -= OnHandleGrabbed;
            handleGrabInteractable.WhenSelectingInteractorRemoved.Action -= OnHandleReleased;
        }
    }

    void Update()
    {
        if (leatherPad == null || hasLaunched) return;

        // Snap back animation
        if (isSnappingBack)
        {
            UpdateSnapBack();
        }

        // Show pull distance while grabbed
        if (isGrabbed && showDebugLogs && Time.frameCount % 30 == 0)
        {
            float pullDist = GetPullDistance();
            Debug.Log($"[Slingshot] Pull distance: {pullDist:F3}m");
        }
    }

    void OnHandleGrabbed(HandGrabInteractor interactor)
    {
        isHandleGrabbed = true;

        // Enable pad grabbing when handle is held
        if (padGrabInteractable != null && !hasLaunched)
        {
            padGrabInteractable.enabled = true;
        }

        if (showDebugLogs)
        {
            Debug.Log("[Slingshot] Handle grabbed! Pad now grabbable.");
        }
    }

    void OnHandleReleased(HandGrabInteractor interactor)
    {
        isHandleGrabbed = false;

        // Disable pad grabbing when handle is released
        if (padGrabInteractable != null && requireHandleGrip)
        {
            padGrabInteractable.enabled = false;
        }

        if (showDebugLogs)
        {
            Debug.Log("[Slingshot] Handle released! Pad no longer grabbable.");
        }
    }

    void OnPadGrabbed(HandGrabInteractor interactor)
    {
        isGrabbed = true;
        isSnappingBack = false;

        if (showDebugLogs)
        {
            Debug.Log("[Slingshot] Pad grabbed!");
        }
    }

    void OnPadReleased(HandGrabInteractor interactor)
    {
        isGrabbed = false;

        float pullDistance = GetPullDistance();

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] Pad released! Pull: {pullDistance:F3}m");
        }

        // Only launch if pulled far enough and haven't launched yet
        if (pullDistance >= minLaunchDistance && !hasLaunched)
        {
            LaunchPlane();
            // Start snap-back AFTER launching
            isSnappingBack = true;
        }
        else
        {
            // Snap back without launching
            isSnappingBack = true;
        }
    }

    void UpdateSnapBack()
    {
        // Smoothly move pad back to rest position
        leatherPad.localPosition = Vector3.Lerp(
            leatherPad.localPosition,
            restPosition,
            snapBackSpeed * Time.deltaTime
        );

        leatherPad.localRotation = Quaternion.Slerp(
            leatherPad.localRotation,
            restRotation,
            snapBackSpeed * Time.deltaTime
        );

        // Stop snapping when close enough
        if (Vector3.Distance(leatherPad.localPosition, restPosition) < 0.001f)
        {
            leatherPad.localPosition = restPosition;
            leatherPad.localRotation = restRotation;
            isSnappingBack = false;

            if (showDebugLogs)
            {
                Debug.Log("[Slingshot] Snap-back complete");
            }
        }
    }

    float GetPullDistance()
    {
        if (leatherPad == null) return 0f;

        // Distance from current position to rest position (in local space)
        return Vector3.Distance(leatherPad.localPosition, restPosition);
    }

    Vector3 GetPullDirection()
    {
        if (leatherPad == null) return Vector3.zero;

        // Direction FROM rest position TO current position (in world space)
        Vector3 restWorldPos = transform.TransformPoint(restPosition);
        Vector3 currentWorldPos = leatherPad.position;

        return (currentWorldPos - restWorldPos).normalized;
    }

    void LaunchPlane()
    {
        if (planeObject == null)
        {
            Debug.LogWarning("[Slingshot] No plane to launch!");
            return;
        }

        float pullDistance = GetPullDistance();
        Vector3 pullDirection = GetPullDirection();

        // Calculate launch direction (opposite of pull)
        Vector3 launchDirection = -pullDirection;
        float launchSpeed = Mathf.Clamp(pullDistance / maxPullDistance, 0f, 1f) * launchPowerMultiplier;

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] LAUNCH! Speed: {launchSpeed:F1} m/s, Direction: {launchDirection}");
        }

        // Detach plane from slingshot
        planeObject.SetParent(null);

        // Orient plane to launch direction
        planeObject.rotation = Quaternion.LookRotation(launchDirection);

        // Configure flight systems (but keep disabled until trajectory completes)
        if (flightController != null && flightDynamics != null)
        {
            // Set plane transform reference
            flightController.planeTransform = planeObject;
            flightDynamics.planeTransform = planeObject;

            // Keep disabled until trajectory completes
            flightController.enabled = false;
            flightDynamics.enabled = false;

            if (showDebugLogs)
            {
                Debug.Log($"[Slingshot] Configured flight systems for {planeObject.name}");
            }
        }
        else
        {
            Debug.LogWarning("[Slingshot] FlightController or FlightDynamics not assigned!");
        }

        // Enable animator and audio
        if (planeAnimator != null) planeAnimator.enabled = true;
        if (planeAudioSource != null) planeAudioSource.enabled = true;

        // Start launch sequence (trajectory → scale → flight control)
        if (enableLaunchTrajectory)
        {
            StartCoroutine(LaunchTrajectory(launchDirection, launchSpeed));
        }
        else
        {
            // No trajectory - just scale and enable flight
            StartCoroutine(ScalePlaneUp());
        }

        // Disable pad grabbing after launch
        if (padGrabInteractable != null)
        {
            padGrabInteractable.enabled = false;
        }

        // Hide/deactivate slingshot after launch
        StartCoroutine(HideSlingshot());

        hasLaunched = true;
    }

    System.Collections.IEnumerator LaunchTrajectory(Vector3 launchDirection, float launchSpeed)
    {
        if (planeObject == null) yield break;

        Vector3 startPos = planeObject.position;
        float elapsed = 0f;

        // Simultaneously: trajectory motion + scaling up
        StartCoroutine(ScalePlaneUp());

        while (elapsed < trajectoryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / trajectoryDuration;

            // Parabolic curve: dip down then rise
            // y = -4 * dip * t * (t - 1)  -- This creates arc that dips then returns
            float verticalOffset = -4f * trajectoryDip * t * (t - 1f);

            // Move forward with slight downward arc
            Vector3 forward = launchDirection * trajectorySpeed * elapsed;
            Vector3 down = Vector3.down * verticalOffset;

            planeObject.position = startPos + forward + down;

            yield return null;
        }

        // Trajectory complete - enable flight systems
        if (flightController != null)
        {
            flightController.enabled = true;
        }
        if (flightDynamics != null)
        {
            flightDynamics.enableMovement = true;
            flightDynamics.enabled = true;
        }

        if (showDebugLogs)
        {
            Debug.Log("[Slingshot] Launch trajectory complete! Flight systems enabled.");
        }
    }

    System.Collections.IEnumerator ScalePlaneUp()
    {
        if (planeObject == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = planeObject.localScale;
        Vector3 targetScale = planeScaleAfterLaunch;

        // Scale up animation
        while (elapsed < scaleUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleUpDuration;

            // Smooth curve
            t = Mathf.SmoothStep(0f, 1f, t);

            planeObject.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        planeObject.localScale = targetScale;

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] Plane scaled to target size: {targetScale}");
        }
    }

    System.Collections.IEnumerator HideSlingshot()
    {
        // Wait a moment so user sees the launch
        yield return new WaitForSeconds(1f);

        // Fade out or just disable
        // Option 1: Simple disable
        gameObject.SetActive(false);

        // Option 2: If you want to fade, you'd animate materials here

        if (showDebugLogs)
        {
            Debug.Log("[Slingshot] Slingshot hidden");
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos || leatherPad == null) return;

        // Draw rest position
        Vector3 restWorldPos = transform.TransformPoint(restPosition);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(restWorldPos, 0.02f);

        // Draw current pad position
        Gizmos.color = isGrabbed ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(leatherPad.position, 0.02f);

        // Draw pull vector
        if (isGrabbed)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(restWorldPos, leatherPad.position);

            // Draw launch direction (opposite of pull)
            Vector3 launchDir = -GetPullDirection();
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(restWorldPos, launchDir * 0.2f);
        }

        // Draw max pull distance sphere
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(restWorldPos, maxPullDistance);
    }
}
