using UnityEngine;

/// <summary>
/// Hand tracking input provider for flight controls
/// Uses right hand rotation and position for natural airplane control
/// </summary>
public class HandTrackingInput : MonoBehaviour, IFlightInputProvider
{
    [Header("Hand Tracking Reference")]
    [Tooltip("Assign the OpenXR Right Hand transform here")]
    public Transform rightHandTransform;

    [Header("Roll & Pitch Settings (Phase 1.1)")]
    [Tooltip("Maximum hand tilt angle (degrees) for full roll input")]
    public float rollAngleRange = 45f;

    [Tooltip("Maximum hand pitch angle (degrees) for full pitch input")]
    public float pitchAngleRange = 45f;

    [Header("Position-Based Controls (Phase 1.2 - Disabled for now)")]
    [Tooltip("Enable yaw control via hand left/right position")]
    public bool enableYawControl = false;

    [Tooltip("Enable throttle control via hand forward/back position")]
    public bool enableThrottleControl = false;

    public float yawPositionRange = 0.2f;      // meters left/right for full yaw
    public float throttlePositionRange = 0.3f; // meters forward/back for full throttle

    [Header("Neutral Position")]
    [Tooltip("Reference point for hand position. If null, uses controller transform")]
    public Transform neutralPosition;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // Cached values
    private float currentRoll;
    private float currentPitch;
    private float currentYaw;
    private float currentThrottle;
    private bool isTracking;
    private Vector3 neutralPos;

    void Start()
    {
        // Set up neutral position
        if (neutralPosition == null)
        {
            neutralPosition = transform;
        }
        neutralPos = neutralPosition.position;

        // Auto-find hand if not assigned
        if (rightHandTransform == null)
        {
            AutoFindRightHand();
        }
    }

    void Update()
    {
        // Update tracking status every frame so IsActive() works correctly
        UpdateTrackingStatus();
    }

    void UpdateTrackingStatus()
    {
        // Check if hand transform is assigned and valid
        if (rightHandTransform == null)
        {
            isTracking = false;
            return;
        }

        // For Meta XR: Hand object is always in hierarchy, so just check if transform exists
        isTracking = true;
    }

    public void UpdateInput()
    {
        // Check if hand transform is assigned
        if (rightHandTransform == null)
        {
            ResetInputs();
            if (showDebugLogs && Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("❌ Hand transform not assigned!");
            }
            return;
        }

        // Calculate roll and pitch from hand rotation
        CalculateRotationInputs();

        // Calculate yaw and throttle from hand position (if enabled)
        if (enableYawControl || enableThrottleControl)
        {
            CalculatePositionInputs();
        }
        else
        {
            currentYaw = 0f;
            currentThrottle = 0f;
        }

        if (showDebugLogs && Time.frameCount % 30 == 0)
        {
            Vector3 handEuler = rightHandTransform.eulerAngles;
            Debug.Log($"[HandTracking] Raw Hand Euler: ({handEuler.x:F1}, {handEuler.y:F1}, {handEuler.z:F1}) → Roll: {currentRoll:F2} | Pitch: {currentPitch:F2} | IsActive: {isTracking}");
        }
    }

    void CalculateRotationInputs()
    {
        // Get hand's local rotation relative to neutral orientation
        Quaternion handRotation = rightHandTransform.rotation;
        Vector3 handEuler = handRotation.eulerAngles;

        // === ROLL CALCULATION ===
        // Roll is rotation around the Z-axis (palm tilts left/right)
        // Convert from 0-360 to -180 to +180 range
        float rollAngle = handEuler.z;
        if (rollAngle > 180f)
            rollAngle -= 360f;

        // Normalize to -1 to +1 based on rollAngleRange
        currentRoll = Mathf.Clamp(rollAngle / rollAngleRange, -1f, 1f);

        // === PITCH CALCULATION ===
        // Pitch is rotation around the X-axis (fingers point up/down)
        // Convert from 0-360 to -180 to +180 range
        float pitchAngle = handEuler.x;
        if (pitchAngle > 180f)
            pitchAngle -= 360f;

        // Normalize to -1 to +1 based on pitchAngleRange
        // Fingers down = nose down, fingers up = nose up
        currentPitch = Mathf.Clamp(pitchAngle / pitchAngleRange, -1f, 1f);
    }

    void CalculatePositionInputs()
    {
        Vector3 handPos = rightHandTransform.position;
        Vector3 relativePos = handPos - neutralPos;

        // Convert to headset's local space for consistent controls
        // (So moving hand "left" from user's perspective always means left)
        Transform headTransform = Camera.main.transform;
        Vector3 localPos = headTransform.InverseTransformPoint(handPos) - headTransform.InverseTransformPoint(neutralPos);

        if (enableYawControl)
        {
            // Yaw from hand left/right position (X-axis in local space)
            currentYaw = Mathf.Clamp(localPos.x / yawPositionRange, -1f, 1f);
        }

        if (enableThrottleControl)
        {
            // Throttle from hand forward/back position (Z-axis in local space)
            // Normalize to 0-1 range (not -1 to 1, since throttle is always positive)
            float throttleInput = (localPos.z / throttlePositionRange);
            currentThrottle = Mathf.Clamp01(throttleInput + 0.5f); // Offset so neutral = 0.5
        }
    }

    void ResetInputs()
    {
        currentRoll = 0f;
        currentPitch = 0f;
        currentYaw = 0f;
        currentThrottle = 0f;
    }

    // === IFlightInputProvider Implementation ===
    public float GetRoll() => currentRoll;
    public float GetPitch() => currentPitch;
    public float GetYaw() => currentYaw;
    public float GetThrottle() => currentThrottle;
    public bool IsActive() => isTracking;

    // === Helper Methods ===
    [ContextMenu("Auto-Find Right Hand")]
    void AutoFindRightHand()
    {
        // Primary: Look for OpenXRRightHand (correct Meta XR SDK structure)
        GameObject rightHandGO = GameObject.Find("OpenXRRightHand");

        // Fallback: Try other common names
        if (rightHandGO == null)
            rightHandGO = GameObject.Find("RightHand");
        if (rightHandGO == null)
            rightHandGO = GameObject.Find("Right Hand");

        if (rightHandGO != null)
        {
            rightHandTransform = rightHandGO.transform;
            Debug.Log($"✅ Auto-found right hand: {rightHandGO.name} at path: {GetGameObjectPath(rightHandGO)}");
        }
        else
        {
            Debug.LogWarning("⚠ Could not auto-find right hand. Please assign 'OpenXRRightHand' manually.");
        }
    }

    // Helper to show full hierarchy path
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

    // Visualize control zones in Scene view
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!isTracking) return;

        Vector3 handPos = rightHandTransform.position;

        // Draw hand position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(handPos, 0.02f);

        // Draw neutral position
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(neutralPos, 0.03f);

        // Draw connection line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(neutralPos, handPos);

        // If position controls enabled, draw control volume
        if (enableYawControl || enableThrottleControl)
        {
            Gizmos.color = new Color(0, 1, 1, 0.2f);
            Gizmos.DrawWireCube(neutralPos, new Vector3(yawPositionRange * 2, 0.1f, throttlePositionRange * 2));
        }
    }
}
