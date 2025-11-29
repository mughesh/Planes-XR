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

    [Tooltip("GameObject containing FlightController & FlightDynamics")]
    public GameObject flightSystemObject;

    [Tooltip("Main camera/XR rig for plane positioning")]
    public Transform playerCamera;

    [Tooltip("Optional: Point on handle where plane must pass to start scaling")]
    public Transform scaleStartTrigger;

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
    [Range(0.3f, 2.5f)]
    public float trajectoryDuration = 1.2f;

    [Tooltip("Total distance plane travels during trajectory (meters)")]
    [Range(0.5f, 5f)]
    public float trajectoryDistance = 2.5f;

    [Tooltip("How much plane dips down during launch (meters)")]
    [Range(0.05f, 0.5f)]
    public float trajectoryDip = 0.2f;

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
    private FlightController flightController;
    private FlightDynamics flightDynamics;

    // Scaling state
    private bool hasPassedScaleTrigger = false;

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

        // Get flight system components
        if (flightSystemObject != null)
        {
            flightController = flightSystemObject.GetComponent<FlightController>();
            flightDynamics = flightSystemObject.GetComponent<FlightDynamics>();

            // Disable entire object at start
            flightSystemObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[Slingshot] No flight system object assigned!");
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

        // Check if plane has passed scale trigger point
        if (isGrabbed && !hasPassedScaleTrigger && scaleStartTrigger != null && planeObject != null)
        {
            // Simple distance check: Has plane moved past trigger?
            Vector3 restWorldPos = transform.TransformPoint(restPosition);
            float planeDist = Vector3.Distance(planeObject.position, restWorldPos);
            float triggerDist = Vector3.Distance(scaleStartTrigger.position, restWorldPos);

            if (planeDist > triggerDist)
            {
                hasPassedScaleTrigger = true;
                if (showDebugLogs)
                {
                    Debug.Log($"[Slingshot] Plane passed scale trigger! Plane dist: {planeDist:F3}m, Trigger dist: {triggerDist:F3}m");
                }
            }
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
        hasPassedScaleTrigger = (scaleStartTrigger == null); // If no trigger, always allow scaling

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

        // Configure flight systems (set references but keep object disabled)
        if (flightController != null && flightDynamics != null)
        {
            // Set plane transform reference
            flightController.planeTransform = planeObject;
            flightDynamics.planeTransform = planeObject;
            flightDynamics.enableMovement = true;

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
            if (showDebugLogs)
            {
                Debug.Log($"[Slingshot] Starting launch trajectory. hasPassedScaleTrigger: {hasPassedScaleTrigger}");
            }
            StartCoroutine(LaunchTrajectory(launchDirection, launchSpeed));
        }
        else
        {
            // No trajectory - just scale and enable flight immediately
            if (showDebugLogs)
            {
                Debug.Log("[Slingshot] No trajectory - immediate launch");
            }

            if (hasPassedScaleTrigger)
            {
                StartCoroutine(ScalePlaneUp());
            }

            // Enable flight immediately
            if (flightSystemObject != null)
            {
                flightSystemObject.SetActive(true);
            }
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
        if (planeObject == null)
        {
            Debug.LogError("[Slingshot] Plane object is null in LaunchTrajectory!");
            yield break;
        }

        Vector3 startPos = planeObject.position;
        Vector3 restWorldPos = transform.TransformPoint(restPosition);
        float elapsed = 0f;

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] Trajectory START. Duration: {trajectoryDuration}s, Distance: {trajectoryDistance}m");
        }

        // Simultaneously: trajectory motion + scaling up (if trigger passed or no trigger)
        if (hasPassedScaleTrigger)
        {
            if (showDebugLogs)
            {
                Debug.Log("[Slingshot] Trigger already passed - starting scale immediately");
            }
            StartCoroutine(ScalePlaneUp());
        }

        while (elapsed < trajectoryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / trajectoryDuration;

            // Parabolic curve: dip down then rise
            // y = -4 * dip * t * (t - 1)  -- This creates arc that dips then returns
            float verticalOffset = -4f * trajectoryDip * t * (t - 1f);

            // Move forward along launch direction based on total distance
            Vector3 forward = launchDirection * trajectoryDistance * t;
            Vector3 down = Vector3.down * verticalOffset;

            planeObject.position = startPos + forward + down;

            // Start scaling if we just passed the trigger during trajectory
            if (!hasPassedScaleTrigger && scaleStartTrigger != null)
            {
                float planeDist = Vector3.Distance(planeObject.position, restWorldPos);
                float triggerDist = Vector3.Distance(scaleStartTrigger.position, restWorldPos);

                if (planeDist > triggerDist)
                {
                    hasPassedScaleTrigger = true;
                    StartCoroutine(ScalePlaneUp());
                    if (showDebugLogs)
                    {
                        Debug.Log($"[Slingshot] Passed trigger during flight! Plane: {planeDist:F3}m, Trigger: {triggerDist:F3}m");
                    }
                }
            }

            yield return null;
        }

        // If we STILL haven't scaled (no trigger or very short pull), scale now
        if (!hasPassedScaleTrigger)
        {
            hasPassedScaleTrigger = true;
            if (showDebugLogs)
            {
                Debug.Log("[Slingshot] Forcing scale - trigger never passed");
            }
            StartCoroutine(ScalePlaneUp());
        }

        // Trajectory complete - enable flight systems
        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] Trajectory COMPLETE after {elapsed:F2}s. Enabling flight systems...");
        }

        if (flightSystemObject != null)
        {
            flightSystemObject.SetActive(true);

            if (showDebugLogs)
            {
                Debug.Log($"[Slingshot] Flight system object '{flightSystemObject.name}' activated!");
            }
        }
        else
        {
            Debug.LogError("[Slingshot] FlightSystemObject is NULL! Cannot enable flight.");
        }
    }

    System.Collections.IEnumerator ScalePlaneUp()
    {
        if (planeObject == null)
        {
            Debug.LogError("[Slingshot] Plane object is null in ScalePlaneUp!");
            yield break;
        }

        float elapsed = 0f;
        Vector3 startScale = planeObject.localScale;
        Vector3 targetScale = planeScaleAfterLaunch;

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] SCALE START: {startScale} → {targetScale} over {scaleUpDuration}s");
        }

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
            Debug.Log($"[Slingshot] SCALE COMPLETE: Final scale: {planeObject.localScale}");
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
