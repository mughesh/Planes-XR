using UnityEngine;
using System.Collections;

/// <summary>
/// Pocket Pilot - Plane Launch Sequence
/// Handles trajectory, scaling, and flight system activation after slingshot release
///
/// SETUP:
/// 1. Attach to the plane GameObject
/// 2. Assign scaleStartTrigger (the point where trajectory/scaling should start)
/// 3. Assign flightSystemObject (GameObject with FlightController + FlightDynamics)
/// 4. Set scale values in Inspector
/// </summary>
public class PlaneLaunchSequence : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Point where trajectory and scaling should start (V point of slingshot)")]
    public Transform scaleStartTrigger;

    [Tooltip("GameObject containing FlightController & FlightDynamics")]
    public GameObject flightSystemObject;

    [Header("Scaling")]
    [Tooltip("Scale when sitting in slingshot")]
    public Vector3 scaleInSlingshot = new Vector3(0.2f, 0.2f, 0.2f);

    [Tooltip("Scale after launch completes")]
    public Vector3 scaleAfterLaunch = Vector3.one;

    [Tooltip("How long scaling animation takes")]
    [Range(0.2f, 1.5f)]
    public float scaleUpDuration = 0.6f;

    [Header("Trajectory")]
    [Tooltip("Enable parabolic launch trajectory")]
    public bool enableTrajectory = true;

    [Tooltip("How long trajectory lasts")]
    [Range(0.3f, 2.5f)]
    public float trajectoryDuration = 1.2f;

    [Tooltip("How high the plane arcs UP during launch")]
    [Range(0.1f, 2.0f)]
    public float trajectoryHeight = 0.5f;

    [Tooltip("Delay before trajectory starts (wait for scale trigger)")]
    [Range(0f, 0.5f)]
    public float startDelay = 0.1f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // Components
    private Animator planeAnimator;
    private AudioSource planeAudioSource;
    private FlightController flightController;
    private FlightDynamics flightDynamics;

    // State
    private bool isLaunched = false;
    private Vector3 launchDirection;
    private Vector3 originalScale;
    private float initialLaunchSpeed;

    void Awake()
    {
        // Cache components
        planeAnimator = GetComponent<Animator>();
        planeAudioSource = GetComponent<AudioSource>();

        // Store original scale
        originalScale = transform.localScale;

        // Get flight system components
        if (flightSystemObject != null)
        {
            flightController = flightSystemObject.GetComponent<FlightController>();
            flightDynamics = flightSystemObject.GetComponent<FlightDynamics>();
        }

        // Disable flight systems at start
        if (flightSystemObject != null)
        {
            flightSystemObject.SetActive(false);
        }

        // Disable animator and audio until launch
        if (planeAnimator != null) planeAnimator.enabled = false;
        if (planeAudioSource != null) planeAudioSource.enabled = false;
    }

    /// <summary>
    /// Called by SlingshotManager when plane is launched
    /// </summary>
    public void StartLaunch(Vector3 direction, float speed, bool hasPassedTrigger)
    {
        if (isLaunched)
        {
            Debug.LogWarning("[PlaneSequence] Already launched!");
            return;
        }

        isLaunched = true;
        launchDirection = direction;
        initialLaunchSpeed = speed;

        if (showDebugLogs)
        {
            Debug.Log($"[PlaneSequence] Launch initiated. Direction: {direction}, Speed: {speed}, PassedTrigger: {hasPassedTrigger}");
        }

        // Orient plane to launch direction
        transform.rotation = Quaternion.LookRotation(direction);

        // Enable animator and audio immediately
        if (planeAnimator != null) planeAnimator.enabled = true;
        if (planeAudioSource != null) planeAudioSource.enabled = true;

        // Configure flight systems (but keep disabled until trajectory ends)
        ConfigureFlightSystems();

        // Start launch sequence
        if (hasPassedTrigger)
        {
            // Trigger already passed during pull - start trajectory immediately
            if (showDebugLogs)
            {
                Debug.Log("[PlaneSequence] Trigger already passed - starting trajectory NOW");
            }
            StartCoroutine(ExecuteTrajectoryAndScale());
        }
        else
        {
            // Trigger not passed - wait for it
            if (showDebugLogs)
            {
                Debug.Log("[PlaneSequence] Waiting for trigger...");
            }
            StartCoroutine(WaitForTriggerThenLaunch());
        }
    }

    void ConfigureFlightSystems()
    {
        if (flightController != null && flightDynamics != null)
        {
            // Set plane transform references
            flightController.planeTransform = transform;
            flightDynamics.planeTransform = transform;
            flightDynamics.enableMovement = true;

            if (showDebugLogs)
            {
                Debug.Log($"[PlaneSequence] Flight systems configured");
            }
        }
        else
        {
            Debug.LogWarning("[PlaneSequence] FlightController or FlightDynamics not found!");
        }
    }

    System.Collections.IEnumerator WaitForTriggerThenLaunch()
    {
        // This should NOT happen with proper setup, but just in case
        if (scaleStartTrigger == null)
        {
            Debug.LogWarning("[PlaneSequence] No trigger assigned - launching immediately");
            yield return StartCoroutine(ExecuteTrajectoryAndScale());
            yield break;
        }

        // Give plane a tiny initial push toward launch direction
        Vector3 startPos = transform.position;
        float moveSpeed = 0.5f; // Very slow crawl
        float elapsed = 0f;
        float maxWait = 1f;

        while (elapsed < maxWait)
        {
            // Move plane slowly forward
            transform.position += launchDirection * moveSpeed * Time.deltaTime;

            // Check if passed trigger
            float distFromStart = Vector3.Distance(transform.position, startPos);
            float triggerDist = Vector3.Distance(scaleStartTrigger.position, startPos);

            if (distFromStart > triggerDist)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[PlaneSequence] Passed trigger during wait! {distFromStart:F3}m > {triggerDist:F3}m");
                }
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Small pause
        yield return new WaitForSeconds(startDelay);

        // Start trajectory
        yield return StartCoroutine(ExecuteTrajectoryAndScale());
    }

    System.Collections.IEnumerator ExecuteTrajectoryAndScale()
    {
        // Trajectory + Scaling (simultaneously)
        if (enableTrajectory)
        {
            yield return StartCoroutine(ExecuteTrajectory());
        }
        else
        {
            // No trajectory - just scale
            yield return StartCoroutine(ScalePlane());
        }

        // Enable flight systems
        EnableFlightSystems();
    }

    System.Collections.IEnumerator ExecuteTrajectory()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        
        // Target speed is the cruise speed from FlightDynamics
        float targetSpeed = (flightDynamics != null) ? flightDynamics.maxSpeed : 2.0f;

        if (showDebugLogs)
        {
            Debug.Log($"[PlaneSequence] Trajectory START. Duration: {trajectoryDuration}s, Speed: {initialLaunchSpeed} -> {targetSpeed}");
        }

        // Start scaling simultaneously
        StartCoroutine(ScalePlane());

        while (elapsed < trajectoryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / trajectoryDuration;

            // 1. Calculate current speed (blend from launch to cruise)
            // Use smooth step for nicer deceleration
            float currentSpeed = Mathf.Lerp(initialLaunchSpeed, targetSpeed, Mathf.SmoothStep(0f, 1f, t));

            // 2. Calculate forward movement
            // We integrate speed over time roughly by just moving forward by currentSpeed * dt
            // But since we are in a loop, we can calculate total distance traveled if we assumed linear decel,
            // or just move incrementally. Moving incrementally is safer for collision but here we are kinematic.
            // Let's stick to the position formula for simplicity and consistency.
            // Distance = (v0 * t) + 0.5 * a * t^2 ? No, that's physics.
            // Let's just move the transform incrementally to match the speed exactly.
            
            Vector3 moveStep = launchDirection * currentSpeed * Time.deltaTime;
            
            // 3. Calculate vertical arc (Parabola)
            // Parabola formula: y = 4 * height * x * (1 - x) where x is 0..1
            // We need to ADD this offset to the linear path.
            // Since we are moving incrementally, we need to calculate the TOTAL vertical offset for time T,
            // and apply the difference from the previous frame?
            // Or just calculate absolute position based on startPos?
            
            // Let's calculate absolute position to be robust.
            // We need the total distance traveled "horizontally" (along launch dir) at time t.
            // If speed goes from v0 to v1 linearly: dist = v0*t + 0.5*(v1-v0)/duration * t^2
            float accel = (targetSpeed - initialLaunchSpeed) / trajectoryDuration;
            float distTraveled = (initialLaunchSpeed * elapsed) + (0.5f * accel * elapsed * elapsed);
            
            Vector3 forwardPos = startPos + (launchDirection * distTraveled);
            
            // Vertical offset (Upward arc)
            // 4 * h * t * (1-t) gives a hump from 0 to 1
            float verticalOffset = 4f * trajectoryHeight * t * (1f - t);
            
            transform.position = forwardPos + (Vector3.up * verticalOffset);
            
            // Update rotation to match the arc?
            // The plane should pitch up then down.
            // We can look at the next position to determine rotation.
            float nextT = (elapsed + Time.deltaTime) / trajectoryDuration;
            float nextDist = (initialLaunchSpeed * (elapsed + Time.deltaTime)) + (0.5f * accel * (elapsed + Time.deltaTime) * (elapsed + Time.deltaTime));
            float nextVert = 4f * trajectoryHeight * nextT * (1f - nextT);
            Vector3 nextPos = startPos + (launchDirection * nextDist) + (Vector3.up * nextVert);
            
            Vector3 flightDir = (nextPos - transform.position).normalized;
            if (flightDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(flightDir);
            }

            yield return null;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[PlaneSequence] Trajectory COMPLETE after {elapsed:F2}s");
        }
    }

    System.Collections.IEnumerator ScalePlane()
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = scaleAfterLaunch;

        if (showDebugLogs)
        {
            Debug.Log($"[PlaneSequence] SCALE START: {startScale} → {targetScale} over {scaleUpDuration}s");
        }

        while (elapsed < scaleUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleUpDuration;

            // Smooth easing
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        transform.localScale = targetScale;

        if (showDebugLogs)
        {
            Debug.Log($"[PlaneSequence] SCALE COMPLETE: {transform.localScale}");
        }
    }

    void EnableFlightSystems()
    {
        if (flightSystemObject != null)
        {
            flightSystemObject.SetActive(true);

            // 1. Sync Speed
            // Get the final speed from the trajectory (or use current interpolated speed)
            // Since we just finished trajectory, our speed should be targetSpeed (cruise speed)
            // But to be safe, let's explicitly set it.
            if (flightDynamics != null)
            {
                // We want to ensure FlightDynamics starts at the speed we ended at.
                // In ExecuteTrajectory, we lerped to flightDynamics.maxSpeed.
                // So we can just set it to that, or pass the calculated value if we had it.
                // Ideally, we should pass the exact speed we ended with.
                // But since we lerped to maxSpeed, setting it to maxSpeed is correct.
                flightDynamics.SetCurrentSpeed(flightDynamics.maxSpeed);
            }

            // 2. Start Auto-Leveling
            if (flightController != null)
            {
                flightController.StartAutoLevelSequence();
            }

            if (showDebugLogs)
            {
                Debug.Log($"[PlaneSequence] Flight systems ENABLED! Starting auto-level.");
            }
        }
        else
        {
            Debug.LogError("[PlaneSequence] FlightSystemObject is NULL!");
        }
    }

    /// <summary>
    /// Reset plane to slingshot state (for restarting level)
    /// </summary>
    public void ResetToSlingshot()
    {
        isLaunched = false;
        transform.localScale = scaleInSlingshot;

        if (planeAnimator != null) planeAnimator.enabled = false;
        if (planeAudioSource != null) planeAudioSource.enabled = false;

        if (flightSystemObject != null)
        {
            flightSystemObject.SetActive(false);
        }

        if (showDebugLogs)
        {
            Debug.Log("[PlaneSequence] Reset to slingshot state");
        }
    }

    void OnDrawGizmos()
    {
        if (scaleStartTrigger == null) return;

        // Draw line from plane to trigger
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, scaleStartTrigger.position);

        // Draw trigger point
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(scaleStartTrigger.position, 0.05f);
    }
}
