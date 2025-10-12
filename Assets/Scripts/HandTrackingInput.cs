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

    [Header("Smoothing & Feel (Phase 1.3)")]
    [Tooltip("Enable input smoothing to reduce jitter")]
    public bool enableSmoothing = true;

    [Tooltip("Smoothing method: Simple (fast) or OneEuro (better for varying speeds)")]
    public SmoothingMethod smoothingMethod = SmoothingMethod.Simple;

    [Tooltip("How fast input responds (0.05 = very responsive, 0.2 = very smooth)")]
    [Range(0.01f, 0.5f)]
    public float smoothTime = 0.1f;

    [Tooltip("Input below this threshold is ignored (reduces drift)")]
    [Range(0f, 0.2f)]
    public float deadZone = 0.08f;

    [Tooltip("Exponential curve makes small movements more precise")]
    public bool useExponentialCurve = false;

    public enum SmoothingMethod
    {
        Simple,      // Fast, good for most cases
        OneEuro      // Advanced, adapts to movement speed
    }

    [Header("Cockpit Sphere (Phase 2)")]
    [Tooltip("Reference to the cockpit sphere zone - hand must be inside to control")]
    public CockpitSphere cockpitSphere;

    [Tooltip("Use cockpit sphere for position-based controls (yaw/throttle)")]
    public bool useCockpitSphere = true;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // Cached values
    private float currentRoll;
    private float currentPitch;
    private float currentYaw;
    private float currentThrottle;
    private bool isTracking;
    private bool isInsideSphere;
    private float sphereTransitionAlpha = 0f; // 0 = outside, 1 = fully inside

    // Smoothing
    private InputSmoother rollSmoother;
    private InputSmoother pitchSmoother;
    private InputSmoother yawSmoother;
    private InputSmoother throttleSmoother;
    private OneEuroFilter rollEuroFilter;
    private OneEuroFilter pitchEuroFilter;
    private OneEuroFilter yawEuroFilter;
    private OneEuroFilter throttleEuroFilter;

    void Start()
    {
        // Auto-find hand if not assigned
        if (rightHandTransform == null)
        {
            AutoFindRightHand();
        }

        // Auto-find cockpit sphere if not assigned
        if (cockpitSphere == null && useCockpitSphere)
        {
            cockpitSphere = FindObjectOfType<CockpitSphere>();
            if (cockpitSphere != null)
            {
                Debug.Log($"✅ Auto-found CockpitSphere: {cockpitSphere.name}");
            }
            else
            {
                Debug.LogWarning("⚠ CockpitSphere not found. Create one or disable 'useCockpitSphere'");
            }
        }

        // Initialize smoothers
        InitializeSmoothing();
    }

    void InitializeSmoothing()
    {
        // Simple smoothers
        rollSmoother = new InputSmoother(smoothTime, deadZone);
        pitchSmoother = new InputSmoother(smoothTime, deadZone);
        yawSmoother = new InputSmoother(smoothTime, deadZone);
        throttleSmoother = new InputSmoother(smoothTime, deadZone);

        rollSmoother.useExponentialCurve = useExponentialCurve;
        pitchSmoother.useExponentialCurve = useExponentialCurve;
        yawSmoother.useExponentialCurve = useExponentialCurve;
        throttleSmoother.useExponentialCurve = useExponentialCurve;

        // One Euro filters
        rollEuroFilter = new OneEuroFilter(minCutoff: 1f, beta: 0.007f);
        pitchEuroFilter = new OneEuroFilter(minCutoff: 1f, beta: 0.007f);
        yawEuroFilter = new OneEuroFilter(minCutoff: 1f, beta: 0.007f);
        throttleEuroFilter = new OneEuroFilter(minCutoff: 1f, beta: 0.007f);
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

        // Check if hand is inside cockpit sphere (if enabled)
        UpdateSphereStatus();

        // Calculate roll and pitch from hand rotation (always active)
        CalculateRotationInputs();

        // Calculate yaw and throttle from hand position
        CalculatePositionInputs();

        // Apply sphere transition fade
        ApplySphereTransition();

        if (showDebugLogs && Time.frameCount % 30 == 0)
        {
            string smoothStatus = enableSmoothing ? $"[{smoothingMethod}]" : "[RAW]";
            string sphereStatus = useCockpitSphere ? (isInsideSphere ? "[IN SPHERE]" : "[OUTSIDE]") : "";
            Debug.Log($"[HandTracking] {smoothStatus}{sphereStatus} Roll: {currentRoll:F2} | Pitch: {currentPitch:F2} | Yaw: {currentYaw:F2} | Throttle: {currentThrottle:F2}");
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
        float rawRoll = Mathf.Clamp(rollAngle / rollAngleRange, -1f, 1f);

        // === PITCH CALCULATION ===
        // Pitch is rotation around the X-axis (fingers point up/down)
        // Convert from 0-360 to -180 to +180 range
        float pitchAngle = handEuler.x;
        if (pitchAngle > 180f)
            pitchAngle -= 360f;

        // Normalize to -1 to +1 based on pitchAngleRange
        // Fingers down = nose down, fingers up = nose up
        float rawPitch = Mathf.Clamp(pitchAngle / pitchAngleRange, -1f, 1f);

        // === APPLY SMOOTHING ===
        if (enableSmoothing)
        {
            // Update smoother parameters in case they changed at runtime
            rollSmoother.smoothTime = smoothTime;
            rollSmoother.deadZone = deadZone;
            rollSmoother.useExponentialCurve = useExponentialCurve;

            pitchSmoother.smoothTime = smoothTime;
            pitchSmoother.deadZone = deadZone;
            pitchSmoother.useExponentialCurve = useExponentialCurve;

            if (smoothingMethod == SmoothingMethod.Simple)
            {
                currentRoll = rollSmoother.Smooth(rawRoll, Time.deltaTime);
                currentPitch = pitchSmoother.Smooth(rawPitch, Time.deltaTime);
            }
            else // OneEuro
            {
                // Apply dead zone manually for OneEuro
                if (Mathf.Abs(rawRoll) < deadZone) rawRoll = 0f;
                if (Mathf.Abs(rawPitch) < deadZone) rawPitch = 0f;

                currentRoll = rollEuroFilter.Filter(rawRoll, Time.deltaTime);
                currentPitch = pitchEuroFilter.Filter(rawPitch, Time.deltaTime);
            }
        }
        else
        {
            // No smoothing, use raw values
            currentRoll = rawRoll;
            currentPitch = rawPitch;
        }
    }

    void CalculatePositionInputs()
    {
        Vector3 handPos = rightHandTransform.position;
        float rawYaw = 0f;
        float rawThrottle = 0.5f; // Default to 50% throttle when not using sphere

        // === USE COCKPIT SPHERE (Phase 2) ===
        if (useCockpitSphere && cockpitSphere != null)
        {
            // Get hand position normalized within sphere (-1 to +1 per axis)
            Vector3 sphereLocalPos = cockpitSphere.GetNormalizedPosition(handPos);

            // Yaw from X-axis (left/right within sphere)
            rawYaw = sphereLocalPos.x;

            // Throttle from Z-axis (forward/back within sphere)
            // Map from -1/+1 to 0/1 range (center = 50%)
            rawThrottle = Mathf.Clamp01((sphereLocalPos.z + 1f) * 0.5f);
        }

        // === APPLY SMOOTHING ===
        if (enableSmoothing)
        {
            // Update smoother parameters
            yawSmoother.smoothTime = smoothTime;
            yawSmoother.deadZone = deadZone;
            yawSmoother.useExponentialCurve = useExponentialCurve;

            throttleSmoother.smoothTime = smoothTime;
            throttleSmoother.deadZone = deadZone * 0.5f; // Less dead zone for throttle (need finer control)
            throttleSmoother.useExponentialCurve = useExponentialCurve;

            if (smoothingMethod == SmoothingMethod.Simple)
            {
                currentYaw = yawSmoother.Smooth(rawYaw, Time.deltaTime);
                currentThrottle = throttleSmoother.Smooth(rawThrottle, Time.deltaTime);
            }
            else // OneEuro
            {
                // Apply dead zone manually
                if (Mathf.Abs(rawYaw) < deadZone) rawYaw = 0f;
                if (Mathf.Abs(rawThrottle - 0.5f) < deadZone * 0.5f) rawThrottle = 0.5f; // Dead zone around neutral

                currentYaw = yawEuroFilter.Filter(rawYaw, Time.deltaTime);
                currentThrottle = throttleEuroFilter.Filter(rawThrottle, Time.deltaTime);
            }
        }
        else
        {
            // No smoothing, use raw values
            currentYaw = rawYaw;
            currentThrottle = rawThrottle;
        }
    }

    void UpdateSphereStatus()
    {
        if (!useCockpitSphere || cockpitSphere == null)
        {
            isInsideSphere = true; // Always active if not using sphere
            sphereTransitionAlpha = 1f;
            return;
        }

        Vector3 handPos = rightHandTransform.position;
        bool wasInside = isInsideSphere;
        isInsideSphere = cockpitSphere.IsPointInside(handPos);

        // Smooth transition when entering/exiting
        float targetAlpha = isInsideSphere ? 1f : 0f;
        float transitionSpeed = cockpitSphere.transitionTime > 0 ? (1f / cockpitSphere.transitionTime) : 10f;
        sphereTransitionAlpha = Mathf.MoveTowards(sphereTransitionAlpha, targetAlpha, transitionSpeed * Time.deltaTime);

        // Debug logging
        if (showDebugLogs && wasInside != isInsideSphere)
        {
            Debug.Log(isInsideSphere ? "✅ Hand entered cockpit sphere" : "❌ Hand exited cockpit sphere");
        }
    }

    void ApplySphereTransition()
    {
        // Fade inputs based on sphere transition (0 = outside, 1 = fully inside)
        if (useCockpitSphere && sphereTransitionAlpha < 1f)
        {
            currentRoll *= sphereTransitionAlpha;
            currentPitch *= sphereTransitionAlpha;
            currentYaw *= sphereTransitionAlpha;

            // Throttle fades to 50% (neutral) when exiting
            currentThrottle = Mathf.Lerp(0.5f, currentThrottle, sphereTransitionAlpha);
        }
    }

    void ResetInputs()
    {
        currentRoll = 0f;
        currentPitch = 0f;
        currentYaw = 0f;
        currentThrottle = 0.5f;
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
        if (rightHandTransform == null) return;

        Vector3 handPos = rightHandTransform.position;

        // Draw hand position (green when inside sphere, yellow outside, red when not tracking)
        if (!isTracking)
            Gizmos.color = Color.red;
        else if (useCockpitSphere && !isInsideSphere)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(handPos, 0.02f);

        // If using cockpit sphere, draw connection to sphere center
        if (useCockpitSphere && cockpitSphere != null)
        {
            Vector3 sphereCenter = cockpitSphere.Center;

            // Draw line from hand to sphere center
            Gizmos.color = isInsideSphere ? Color.green : new Color(1f, 0.5f, 0f); // Green inside, orange outside
            Gizmos.DrawLine(handPos, sphereCenter);

            // Show distance ratio as text (Scene view only)
            float distRatio = cockpitSphere.GetDistanceRatio(handPos);
            // Optional: Draw distance indicator spheres
            if (distRatio > 0.8f && distRatio < 1.2f)
            {
                // Near edge - draw warning
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(handPos, 0.025f);
            }
        }
    }
}
