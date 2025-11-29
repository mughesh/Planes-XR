using UnityEngine;

/// <summary>
/// Hand tracking input provider for Pocket Pilot
/// Free-flight system: Uses hand rotation for roll/pitch control
/// No cockpit sphere - control works anywhere hand is tracked
/// </summary>
public class HandTrackingInput : MonoBehaviour, IFlightInputProvider
{
    [Header("Hand Tracking Reference")]
    [Tooltip("Assign the OpenXR Right Hand transform here")]
    public Transform rightHandTransform;

    [Header("Control Ranges")]
    [Tooltip("Maximum hand tilt angle (degrees) for full roll input")]
    [Range(20f, 60f)]
    public float rollAngleRange = 45f;

    [Tooltip("Maximum hand pitch angle (degrees) for full pitch input")]
    [Range(20f, 60f)]
    public float pitchAngleRange = 45f;

    [Header("Smoothing")]
    [Tooltip("Enable input smoothing to reduce jitter")]
    public bool enableSmoothing = true;

    [Tooltip("Smoothing method: Simple (fast) or OneEuro (better for varying speeds)")]
    public SmoothingMethod smoothingMethod = SmoothingMethod.OneEuro;

    [Tooltip("How fast input responds (0.05 = very responsive, 0.2 = very smooth)")]
    [Range(0.01f, 0.5f)]
    public float smoothTime = 0.08f;

    [Tooltip("Input below this threshold is ignored (reduces drift)")]
    [Range(0f, 0.15f)]
    public float deadZone = 0.05f;

    public enum SmoothingMethod
    {
        Simple,      // Fast, good for most cases
        OneEuro      // Advanced, adapts to movement speed
    }

    [Header("Debug")]
    public bool showDebugLogs = false;

    // Current input values (-1 to 1)
    private float currentRoll;
    private float currentPitch;
    private bool isTracking;

    // Smoothing filters
    private InputSmoother rollSmoother;
    private InputSmoother pitchSmoother;
    private OneEuroFilter rollEuroFilter;
    private OneEuroFilter pitchEuroFilter;

    void Start()
    {
        // Auto-find hand if not assigned
        if (rightHandTransform == null)
        {
            AutoFindRightHand();
        }

        // Initialize smoothers
        InitializeSmoothing();
    }

    void InitializeSmoothing()
    {
        // Simple smoothers
        rollSmoother = new InputSmoother(smoothTime, deadZone);
        pitchSmoother = new InputSmoother(smoothTime, deadZone);

        // One Euro filters (better for hand tracking)
        rollEuroFilter = new OneEuroFilter(minCutoff: 1.5f, beta: 0.01f);
        pitchEuroFilter = new OneEuroFilter(minCutoff: 1.5f, beta: 0.01f);
    }

    void Update()
    {
        UpdateTrackingStatus();
    }

    void UpdateTrackingStatus()
    {
        if (rightHandTransform == null)
        {
            isTracking = false;
            return;
        }

        // For Meta XR: Check if hand object is active
        isTracking = rightHandTransform.gameObject.activeInHierarchy;
    }

    public void UpdateInput()
    {
        if (rightHandTransform == null || !isTracking)
        {
            ResetInputs();
            return;
        }

        CalculateRotationInputs();

        if (showDebugLogs && Time.frameCount % 30 == 0)
        {
            string smoothStatus = enableSmoothing ? $"[{smoothingMethod}]" : "[RAW]";
           // Debug.Log($"[HandTracking] {smoothStatus} Roll: {currentRoll:F2} | Pitch: {currentPitch:F2}");
        }
    }

    void CalculateRotationInputs()
    {
        Vector3 handEuler = rightHandTransform.rotation.eulerAngles;

        // === ROLL CALCULATION ===
        // Roll is rotation around the Z-axis (palm tilts left/right)
        float rollAngle = handEuler.z;
        if (rollAngle > 180f)
            rollAngle -= 360f;

        float rawRoll = Mathf.Clamp(rollAngle / rollAngleRange, -1f, 1f);

        // === PITCH CALCULATION ===
        // Pitch is rotation around the X-axis (fingers point up/down)
        float pitchAngle = handEuler.x;
        if (pitchAngle > 180f)
            pitchAngle -= 360f;

        float rawPitch = Mathf.Clamp(pitchAngle / pitchAngleRange, -1f, 1f);

        // === APPLY SMOOTHING ===
        if (enableSmoothing)
        {
            rollSmoother.smoothTime = smoothTime;
            rollSmoother.deadZone = deadZone;
            pitchSmoother.smoothTime = smoothTime;
            pitchSmoother.deadZone = deadZone;

            if (smoothingMethod == SmoothingMethod.Simple)
            {
                currentRoll = rollSmoother.Smooth(rawRoll, Time.deltaTime);
                currentPitch = pitchSmoother.Smooth(rawPitch, Time.deltaTime);
            }
            else // OneEuro
            {
                if (Mathf.Abs(rawRoll) < deadZone) rawRoll = 0f;
                if (Mathf.Abs(rawPitch) < deadZone) rawPitch = 0f;

                currentRoll = rollEuroFilter.Filter(rawRoll, Time.deltaTime);
                currentPitch = pitchEuroFilter.Filter(rawPitch, Time.deltaTime);
            }
        }
        else
        {
            currentRoll = rawRoll;
            currentPitch = rawPitch;
        }
    }

    void ResetInputs()
    {
        currentRoll = 0f;
        currentPitch = 0f;
    }

    // === IFlightInputProvider Implementation ===
    public float GetRoll() => currentRoll;
    public float GetPitch() => currentPitch;
    public float GetYaw() => 0f;  // No yaw input - comes from banking
    public float GetThrottle() => 1f;  // Constant cruise speed
    public bool IsActive() => isTracking;

    public void UpdateInput(float deltaTime)
    {
        UpdateInput();
    }

    // === Helper Methods ===
    [ContextMenu("Auto-Find Right Hand")]
    void AutoFindRightHand()
    {
        GameObject rightHandGO = GameObject.Find("OpenXRRightHand");
        if (rightHandGO == null)
            rightHandGO = GameObject.Find("RightHand");
        if (rightHandGO == null)
            rightHandGO = GameObject.Find("Right Hand");

        if (rightHandGO != null)
        {
            rightHandTransform = rightHandGO.transform;
            Debug.Log($"Auto-found right hand: {rightHandGO.name}");
        }
        else
        {
            Debug.LogWarning("Could not auto-find right hand. Please assign manually.");
        }
    }

    string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (rightHandTransform == null) return;

        // Draw hand position (green when tracking, red when not)
        Gizmos.color = isTracking ? Color.green : Color.red;
        Gizmos.DrawWireSphere(rightHandTransform.position, 0.02f);

        // Draw hand forward direction
        if (isTracking)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(rightHandTransform.position, rightHandTransform.forward * 0.1f);
        }
    }
}
