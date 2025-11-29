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

    [Tooltip("Total distance plane travels during trajectory")]
    [Range(0.5f, 5f)]
    public float trajectoryDistance = 2.5f;

    [Tooltip("How much plane dips down")]
    [Range(0.05f, 0.5f)]
    public float trajectoryDip = 0.2f;

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
    /// Called by SlingshotController when plane is launched
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

        if (showDebugLogs)
        {
            Debug.Log($"[PlaneSequence] Trajectory START. Duration: {trajectoryDuration}s, Distance: {trajectoryDistance}m");
        }

        // Start scaling simultaneously
        StartCoroutine(ScalePlane());

        while (elapsed < trajectoryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / trajectoryDuration;

            // Parabolic curve: dips down then rises
            float verticalOffset = -4f * trajectoryDip * t * (t - 1f);

            // Move forward along launch direction
            Vector3 forward = launchDirection * trajectoryDistance * t;
            Vector3 down = Vector3.down * verticalOffset;

            transform.position = startPos + forward + down;

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

            if (showDebugLogs)
            {
                Debug.Log($"[PlaneSequence] Flight systems ENABLED! Player can now control plane.");
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
