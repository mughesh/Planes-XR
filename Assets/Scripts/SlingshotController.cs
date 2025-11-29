using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using System;

/// <summary>
/// Pocket Pilot Slingshot Controller (SIMPLIFIED)
/// Handles ONLY slingshot mechanics: grab, pull, release, snap-back.
/// Emits event OnLaunchRequest when user releases.
/// </summary>
public class SlingshotController : MonoBehaviour
{
    // Event for Manager to listen to
    public event Action<Vector3, float, bool> OnLaunchRequest;

    [Header("References")]
    [Tooltip("The leather pad GameObject that can be grabbed")]
    public Transform leatherPad;

    [Tooltip("The handle GameObject (base that should be held)")]
    public Transform handle;

    // REMOVED: Plane reference (moved to Manager)

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

    // REMOVED: Hide delay (moved to Manager)

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

    // Public property for Manager to check
    public bool IsSnappingBack => isSnappingBack;

    // Meta SDK grab components
    private HandGrabInteractable padGrabInteractable;
    private HandGrabInteractable handleGrabInteractable;
    private bool isHandleGrabbed = false;

    // Scale trigger tracking (still need to know if we passed it during pull)
    // We will find this via tag or name, or just let Manager handle it?
    // Better: Keep a reference to the trigger solely for the "passed trigger" check, 
    // OR, simpler: Just pass the pull percentage and let the plane decide?
    // The user's original code checked distance to a trigger. 
    // Let's keep a reference to the trigger for calculation purposes if possible, 
    // but strictly speaking, the controller shouldn't know about the plane's trigger.
    // However, to preserve the exact logic "passed trigger during pull", we need it.
    // Let's add a specific field for the trigger transform.
    [Header("Optional Trigger Reference")]
    [Tooltip("Reference to the scale start trigger to check if we passed it during pull")]
    public Transform scaleStartTrigger;
    private bool hasPassedScaleTrigger = false;

    // Helper to track the plane if it's parented to us (for distance check)
    // We can just check distance from rest position to current hand position vs trigger position
    // We don't strictly need the plane transform itself if we assume the "pull" is the plane position.

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
        // FIX: Do NOT return early if hasLaunched. We still need to animate snap back.
        // if (hasLaunched) return; <--- REMOVED

        // Snap back animation
        if (isSnappingBack)
        {
            UpdateSnapBack();
        }

        // Check if passed scale trigger while being pulled
        if (isGrabbed && !hasPassedScaleTrigger && scaleStartTrigger != null)
        {
            Vector3 restWorldPos = transform.TransformPoint(restPosition);
            // Use leatherPad position as proxy for plane position
            float pullDist = Vector3.Distance(leatherPad.position, restWorldPos);
            float triggerDist = Vector3.Distance(scaleStartTrigger.position, restWorldPos);

            if (pullDist > triggerDist)
            {
                hasPassedScaleTrigger = true;
                if (showDebugLogs)
                {
                    Debug.Log($"[Slingshot] Passed trigger during pull! {pullDist:F3}m > {triggerDist:F3}m");
                }
            }
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
    }

    void OnHandleReleased(HandGrabInteractor interactor)
    {
        isHandleGrabbed = false;

        // Disable pad grabbing when handle is released
        if (padGrabInteractable != null && requireHandleGrip)
        {
            padGrabInteractable.enabled = false;
        }
    }

    void OnPadGrabbed(HandGrabInteractor interactor)
    {
        isGrabbed = true;
        isSnappingBack = false;
        hasPassedScaleTrigger = (scaleStartTrigger == null); // If no trigger, always true
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
            FireLaunchEvent();
            hasLaunched = true;
            
            // Disable pad grabbing permanently for this session
            if (padGrabInteractable != null)
            {
                padGrabInteractable.enabled = false;
            }
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

    void FireLaunchEvent()
    {
        float pullDistance = GetPullDistance();
        Vector3 pullDirection = GetPullDirection();

        // Calculate launch parameters
        Vector3 launchDirection = -pullDirection; // Opposite of pull
        float launchSpeed = Mathf.Clamp(pullDistance / maxPullDistance, 0f, 1f) * launchPowerMultiplier;

        if (showDebugLogs)
        {
            Debug.Log($"[Slingshot] EVENT: Launch! Speed: {launchSpeed:F1}");
        }

        OnLaunchRequest?.Invoke(launchDirection, launchSpeed, hasPassedScaleTrigger);
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
        }

        // Draw max pull distance sphere
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(restWorldPos, maxPullDistance);
    }
}
