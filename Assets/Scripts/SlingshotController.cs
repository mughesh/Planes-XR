using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Pocket Pilot Slingshot Controller (SIMPLIFIED)
/// Handles ONLY slingshot mechanics: grab, pull, release, snap-back, hide
/// Plane trajectory/scaling is handled by PlaneLaunchSequence script on the plane
///
/// SETUP:
/// 1. Attach to Slingshot root GameObject
/// 2. Assign leatherPad and handle references
/// 3. Assign planeObject (must have PlaneLaunchSequence component)
/// </summary>
public class SlingshotController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The leather pad GameObject that can be grabbed")]
    public Transform leatherPad;

    [Tooltip("The handle GameObject (base that should be held)")]
    public Transform handle;

    [Tooltip("The plane GameObject (must have PlaneLaunchSequence component)")]
    public Transform planeObject;

    [Header("Slingshot Settings")]
    [Tooltip("How far user can pull back (meters)")]
    [Range(0.1f, 0.5f)]
    public float maxPullDistance = 0.3f;

    [Tooltip("Launch force multiplier")]
    [Range(5f, 30f)]
    public float launchPowerMultiplier = 15f;

    [Tooltip("Minimum pull distance to launch")]
    [Range(0.02f, 0.1f)]
    public float minLaunchDistance = 0.05f;

    [Tooltip("How fast pad snaps back after release")]
    [Range(5f, 50f)]
    public float snapBackSpeed = 40f;

    [Tooltip("Delay before hiding slingshot after launch (seconds)")]
    [Range(0.1f, 2f)]
    public float hideDelay = 0.5f;

    [Header("Two-Hand Interaction")]
    [Tooltip("Require handle to be held before pad can be grabbed")]
    public bool requireHandleGrip = true;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showDebugLogs = false;

    // Internal state
    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool isGrabbed = false;
    private bool isSnappingBack = false;
    private bool hasLaunched = false;

    // Meta SDK grab components
    private HandGrabInteractable padGrabInteractable;
    private HandGrabInteractable handleGrabInteractable;
    private bool isHandleGrabbed = false;

    // Plane launch component
    private PlaneLaunchSequence planeLauncher;

    // Scale trigger tracking
    private Transform scaleStartTrigger;
    private bool hasPassedScaleTrigger = false;

    void Start()
    {
        // Store rest position of pad
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

        // Find grab components
        SetupGrabComponents();

        // Get plane launcher component
        if (planeObject != null)
        {
            planeLauncher = planeObject.GetComponent<PlaneLaunchSequence>();
            if (planeLauncher == null)
            {
                Debug.LogError("[Slingshot] Plane is missing PlaneLaunchSequence component!");
            }

            // Parent plane to pad at start
            Transform parentTarget = leatherPad;
            Transform spawnPoint = leatherPad.Find("Plane_spawn");
            if (spawnPoint != null)
            {
                parentTarget = spawnPoint;
            }

            planeObject.SetParent(parentTarget);
            planeObject.localPosition = Vector3.zero;
            planeObject.localRotation = Quaternion.identity;

            // Find scale trigger from plane's launch sequence
            if (planeLauncher != null && planeLauncher.scaleStartTrigger != null)
            {
                scaleStartTrigger = planeLauncher.scaleStartTrigger;
                if (showDebugLogs)
                {
                    Debug.Log($"[Slingshot] Scale trigger found: {scaleStartTrigger.name}");
                }
            }

            if (showDebugLogs)
            {
                Debug.Log($"[Slingshot] Plane parented to {parentTarget.name}");
            }
        }
        else
        {
            Debug.LogWarning("[Slingshot] No plane object assigned!");
        }

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] Initialized. Rest pos: {restPosition}");
        }
    }

    void SetupGrabComponents()
    {
        // Pad grab component
        padGrabInteractable = leatherPad.GetComponentInChildren<HandGrabInteractable>();
        if (padGrabInteractable != null)
        {
            padGrabInteractable.enabled = false; // Disabled until handle grabbed
            padGrabInteractable.WhenSelectingInteractorAdded.Action += OnPadGrabbed;
            padGrabInteractable.WhenSelectingInteractorRemoved.Action += OnPadReleased;
        }
        else
        {
            Debug.LogWarning("[Slingshot] No HandGrabInteractable found on leather pad!");
        }

        // Handle grab component
        if (handle != null)
        {
            handleGrabInteractable = handle.GetComponentInChildren<HandGrabInteractable>();
            if (handleGrabInteractable != null)
            {
                handleGrabInteractable.WhenSelectingInteractorAdded.Action += OnHandleGrabbed;
                handleGrabInteractable.WhenSelectingInteractorRemoved.Action += OnHandleReleased;
            }
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
        // DEBUG: Always show state
        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Slingshot] UPDATE - hasLaunched:{hasLaunched}, isSnappingBack:{isSnappingBack}, isGrabbed:{isGrabbed}");
        }

        if (hasLaunched) return;

        // Snap back animation
        if (isSnappingBack)
        {
            UpdateSnapBack();
        }

        // Check if plane passed scale trigger while being pulled
        if (isGrabbed && !hasPassedScaleTrigger && scaleStartTrigger != null && planeObject != null)
        {
            Vector3 restWorldPos = transform.TransformPoint(restPosition);
            float planeDist = Vector3.Distance(planeObject.position, restWorldPos);
            float triggerDist = Vector3.Distance(scaleStartTrigger.position, restWorldPos);

            if (planeDist > triggerDist)
            {
                hasPassedScaleTrigger = true;
                if (showDebugLogs)
                {
                    Debug.Log($"[Slingshot] Plane passed trigger during pull! {planeDist:F3}m > {triggerDist:F3}m");
                }
            }
        }

        // Debug pull distance
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
        hasPassedScaleTrigger = (scaleStartTrigger == null); // If no trigger, always true

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

        // Launch if pulled far enough
        if (pullDistance >= minLaunchDistance && !hasLaunched)
        {
            LaunchPlane();
        }

        // Always snap back
        isSnappingBack = true;
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
        return Vector3.Distance(leatherPad.localPosition, restPosition);
    }

    Vector3 GetPullDirection()
    {
        if (leatherPad == null) return Vector3.zero;

        Vector3 restWorldPos = transform.TransformPoint(restPosition);
        Vector3 currentWorldPos = leatherPad.position;

        return (currentWorldPos - restWorldPos).normalized;
    }

    void LaunchPlane()
    {
        if (planeLauncher == null)
        {
            Debug.LogError("[Slingshot] No PlaneLaunchSequence component on plane!");
            return;
        }

        float pullDistance = GetPullDistance();
        Vector3 pullDirection = GetPullDirection();

        // Calculate launch parameters
        Vector3 launchDirection = -pullDirection; // Opposite of pull
        float launchSpeed = Mathf.Clamp(pullDistance / maxPullDistance, 0f, 1f) * launchPowerMultiplier;

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] LAUNCH! Speed: {launchSpeed:F1} m/s, Direction: {launchDirection}");
        }

        // Detach plane from slingshot
        planeObject.SetParent(null);

        // Orient plane to launch direction
        planeObject.rotation = Quaternion.LookRotation(launchDirection);

        // Tell plane to start its launch sequence (with trigger state)
        planeLauncher.StartLaunch(launchDirection, launchSpeed, hasPassedScaleTrigger);

        // Disable pad grabbing
        if (padGrabInteractable != null)
        {
            padGrabInteractable.enabled = false;
        }

        // Hide slingshot after delay
        StartCoroutine(HideSlingshot());

        hasLaunched = true;
    }

    System.Collections.IEnumerator HideSlingshot()
    {
        yield return new WaitForSeconds(hideDelay);

        // Disable slingshot GameObject
        gameObject.SetActive(false);

        if (showDebugLogs)
        {
            Debug.Log("[Slingshot] Hidden");
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

            // Draw launch direction
            Vector3 launchDir = -GetPullDirection();
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(restWorldPos, launchDir * 0.2f);
        }

        // Draw max pull distance sphere
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(restWorldPos, maxPullDistance);
    }
}
