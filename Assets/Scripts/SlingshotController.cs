using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using System;

/// <summary>
/// Pocket Pilot Slingshot Controller
/// Handles slingshot mechanics and fires GameEvents.OnLaunch.
/// </summary>
public class SlingshotController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The leather pad GameObject that can be grabbed")]
    public Transform leatherPad;

    [Tooltip("The handle GameObject (base that should be held)")]
    public Transform handle;

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

    [Header("Two-Hand Interaction")]
    [Tooltip("Require handle to be held before pad can be grabbed")]
    public bool requireHandleGrip = true;

    [Header("Optional Trigger Reference")]
    [Tooltip("Reference to the scale start trigger to check if we passed it during pull")]
    public Transform scaleStartTrigger;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showDebugLogs = false;

    // Internal state
    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool isGrabbed = false;
    private bool isSnappingBack = false;
    private bool hasLaunched = false;
    private bool hasPassedScaleTrigger = false;

    // Meta SDK grab components
    private HandGrabInteractable padGrabInteractable;
    private HandGrabInteractable handleGrabInteractable;
    private bool isHandleGrabbed = false;

    void Start()
    {
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

        SetupGrabComponents();
        
        // Subscribe to Level Start to reset?
        // GameEvents.OnLevelStart += ResetSlingshot; 
    }

    void SetupGrabComponents()
    {
        padGrabInteractable = leatherPad.GetComponentInChildren<HandGrabInteractable>();
        if (padGrabInteractable != null)
        {
            padGrabInteractable.enabled = false;
            padGrabInteractable.WhenSelectingInteractorAdded.Action += OnPadGrabbed;
            padGrabInteractable.WhenSelectingInteractorRemoved.Action += OnPadReleased;
        }

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
        if (isSnappingBack)
        {
            UpdateSnapBack();
        }

        if (isGrabbed)
        {
            UpdatePullLogic();
        }
    }

    void UpdatePullLogic()
    {
        // 1. Check Trigger
        if (!hasPassedScaleTrigger && scaleStartTrigger != null)
        {
            Vector3 restWorldPos = transform.TransformPoint(restPosition);
            float pullDist = Vector3.Distance(leatherPad.position, restWorldPos);
            float triggerDist = Vector3.Distance(scaleStartTrigger.position, restWorldPos);

            if (pullDist > triggerDist)
            {
                hasPassedScaleTrigger = true;
            }
        }

        // 2. Fire Tension Event (for Audio/Haptics)
        float currentDist = GetPullDistance();
        float tension = Mathf.Clamp01(currentDist / maxPullDistance);
        GameEvents.OnSlingshotPull?.Invoke(tension);
    }

    void OnHandleGrabbed(HandGrabInteractor interactor)
    {
        isHandleGrabbed = true;
        if (padGrabInteractable != null && !hasLaunched)
        {
            padGrabInteractable.enabled = true;
        }
    }

    void OnHandleReleased(HandGrabInteractor interactor)
    {
        isHandleGrabbed = false;
        if (padGrabInteractable != null && requireHandleGrip)
        {
            padGrabInteractable.enabled = false;
        }
    }

    void OnPadGrabbed(HandGrabInteractor interactor)
    {
        isGrabbed = true;
        isSnappingBack = false;
        hasPassedScaleTrigger = (scaleStartTrigger == null);
    }

    void OnPadReleased(HandGrabInteractor interactor)
    {
        isGrabbed = false;
        float pullDistance = GetPullDistance();

        if (pullDistance >= minLaunchDistance && !hasLaunched)
        {
            FireLaunchEvent();
            hasLaunched = true;
            if (padGrabInteractable != null) padGrabInteractable.enabled = false;
        }

        isSnappingBack = true;
    }

    void UpdateSnapBack()
    {
        leatherPad.localPosition = Vector3.Lerp(leatherPad.localPosition, restPosition, snapBackSpeed * Time.deltaTime);
        leatherPad.localRotation = Quaternion.Slerp(leatherPad.localRotation, restRotation, snapBackSpeed * Time.deltaTime);

        if (Vector3.Distance(leatherPad.localPosition, restPosition) < 0.001f)
        {
            leatherPad.localPosition = restPosition;
            leatherPad.localRotation = restRotation;
            isSnappingBack = false;
            
            // Hide slingshot if launched?
            if (hasLaunched)
            {
                gameObject.SetActive(false);
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
        return (leatherPad.position - restWorldPos).normalized;
    }

    void FireLaunchEvent()
    {
        float pullDistance = GetPullDistance();
        Vector3 pullDirection = GetPullDirection();
        Vector3 launchDirection = -pullDirection;
        float launchSpeed = Mathf.Clamp(pullDistance / maxPullDistance, 0f, 1f) * launchPowerMultiplier;

        if (showDebugLogs) Debug.Log($"[Slingshot] Launch! Speed: {launchSpeed}");

        GameEvents.OnLaunch?.Invoke(launchDirection, launchSpeed, hasPassedScaleTrigger);
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos || leatherPad == null) return;
        Vector3 restWorldPos = transform.TransformPoint(restPosition);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(restWorldPos, 0.02f);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(restWorldPos, maxPullDistance);
    }
}
