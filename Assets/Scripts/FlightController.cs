using UnityEngine;

/// <summary>
/// Main flight controller - converts normalized input to flight dynamics
/// RATE-BASED SYSTEM: Input controls rotation speed, not absolute angles
/// Phase 1: Core rate-based control with FlightDynamics integration
/// </summary>
public class FlightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Input manager handling hand tracking / controller switching")]
    public FlightInputManager inputManager;

    [Tooltip("Flight dynamics component handling physics integration")]
    public FlightDynamics flightDynamics;

    [Tooltip("The plane GameObject to control")]
    public Transform planeTransform;

    [Header("Phase 1: Rate-Based Control")]
    [Tooltip("Maximum roll rotation speed (degrees/second)")]
    [Range(30f, 240f)]
    public float maxRollRate = 120f;

    [Tooltip("Maximum pitch rotation speed (degrees/second)")]
    [Range(30f, 180f)]
    public float maxPitchRate = 90f;

    [Tooltip("Maximum yaw rotation speed (degrees/second)")]
    [Range(15f, 90f)]
    public float maxYawRate = 45f;

    [Header("Legacy Position Control (For Comparison)")]
    [Tooltip("Use old position-based system instead of rate-based")]
    public bool useLegacyPositionControl = true;

    [Tooltip("Maximum angles for legacy mode")]
    public float maxRollAngle = 60f;
    public float maxPitchAngle = 45f;
    public float maxYawAngle = 30f;
    public float rotationSpeed = 90f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Legacy state (only used if useLegacyPositionControl = true)
    private float currentRoll = 0f;
    private float currentPitch = 0f;
    private float currentYaw = 0f;

    void Start()
    {
        // Auto-create input manager if not assigned
        if (inputManager == null)
        {
            GameObject inputGO = new GameObject("FlightInputManager");
            inputManager = inputGO.AddComponent<FlightInputManager>();
            Debug.Log("Auto-created FlightInputManager");
        }

        // Auto-create or find FlightDynamics
        if (flightDynamics == null)
        {
            flightDynamics = GetComponent<FlightDynamics>();
            if (flightDynamics == null)
            {
                flightDynamics = gameObject.AddComponent<FlightDynamics>();
                Debug.Log("Auto-created FlightDynamics component");
            }
        }

        // Create test plane if none assigned
        if (planeTransform == null)
        {
            CreateTestPlane();
        }

        // Assign plane to dynamics
        if (flightDynamics != null)
        {
            flightDynamics.planeTransform = planeTransform;
        }
    }

    void Update()
    {
        if (!inputManager.IsInputActive())
        {
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.LogWarning("⚠ No input active. Make sure hand is visible or controller is connected.");
            }
            return;
        }

        if (useLegacyPositionControl)
        {
            UpdatePlaneRotation_Legacy();
        }
        else
        {
            UpdatePlaneRotation_RateBased();
        }
    }

    void UpdatePlaneRotation_RateBased()
    {
        // Get normalized input values (-1 to 1)
        float rollInput = inputManager.GetRoll();
        float pitchInput = inputManager.GetPitch();
        float yawInput = inputManager.GetYaw();
        float throttleInput = inputManager.GetThrottle();

        // Convert to target rates (degrees/second)
        float targetRollRate = rollInput * maxRollRate;
        float targetPitchRate = pitchInput * maxPitchRate;
        float targetYawRate = yawInput * maxYawRate;

        // Send rates to dynamics system
        if (flightDynamics != null)
        {
            flightDynamics.SetTargetRates(targetPitchRate, targetYawRate, targetRollRate);
            flightDynamics.SetThrottle(throttleInput);
        }

        // Debug output
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            Vector3 angVel = flightDynamics != null ? flightDynamics.GetAngularVelocity() : Vector3.zero;
            Debug.Log($"[FlightController] Input: R:{rollInput:F2} P:{pitchInput:F2} Y:{yawInput:F2} | Rates: ({angVel.x:F1}, {angVel.y:F1}, {angVel.z:F1}) °/s");
        }
    }

    void UpdatePlaneRotation_Legacy()
    {
        // OLD POSITION-BASED SYSTEM (for comparison)
        float rollInput = inputManager.GetRoll();
        float pitchInput = inputManager.GetPitch();
        float yawInput = inputManager.GetYaw();
        float throttleInput = inputManager.GetThrottle();

        // Convert to target angles
        float targetRoll = rollInput * maxRollAngle;
        float targetPitch = pitchInput * maxPitchAngle;
        float targetYaw = yawInput * maxYawAngle;

        // Smoothly interpolate current angles toward target
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, rotationSpeed * Time.deltaTime);
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, rotationSpeed * Time.deltaTime);
        currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, rotationSpeed * Time.deltaTime);

        // Apply rotation to plane (direct assignment)
        planeTransform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);

        // Send throttle to dynamics for movement
        if (flightDynamics != null)
        {
            flightDynamics.SetThrottle(throttleInput);
        }

        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[FlightController LEGACY] Roll: {rollInput:F2} ({currentRoll:F1}°) | Pitch: {pitchInput:F2} ({currentPitch:F1}°) | Throttle: {throttleInput:F2}");
        }
    }

    void CreateTestPlane()
    {
        GameObject planeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        planeGO.name = "TestPlane";
        planeGO.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;
        planeGO.transform.localScale = new Vector3(0.15f, 0.05f, 0.1f); // Elongated to look more plane-like

        // Color it distinctly
        Renderer renderer = planeGO.GetComponent<Renderer>();
        renderer.material.color = new Color(0.2f, 0.6f, 1f); // Nice blue

        planeTransform = planeGO.transform;
        Debug.Log("✈ Created test plane in front of camera");
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // On-screen HUD showing input values
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        // Get smoothing info from hand input if available
        string smoothingInfo = "";
        if (inputManager.handInput != null)
        {
            if (inputManager.handInput.enableSmoothing)
            {
                smoothingInfo = $" [{inputManager.handInput.smoothingMethod}]";
            }
            else
            {
                smoothingInfo = " [RAW]";
            }
        }

        // Show control mode
        string controlMode = useLegacyPositionControl ? "[LEGACY POSITION]" : "[RATE-BASED]";
        GUI.Label(new Rect(10, 10, 600, 30), $"Mode: {controlMode} {smoothingInfo}", style);

        if (useLegacyPositionControl)
        {
            // Legacy mode display
            GUI.Label(new Rect(10, 40, 400, 30), $"Roll: {inputManager.GetRoll():F2} ({currentRoll:F1}°)", style);
            GUI.Label(new Rect(10, 70, 400, 30), $"Pitch: {inputManager.GetPitch():F2} ({currentPitch:F1}°)", style);
            GUI.Label(new Rect(10, 100, 400, 30), $"Yaw: {inputManager.GetYaw():F2} ({currentYaw:F1}°)", style);
        }
        else
        {
            // Rate-based mode display
            Vector3 euler = planeTransform.eulerAngles;
            Vector3 angVel = flightDynamics != null ? flightDynamics.GetAngularVelocity() : Vector3.zero;

            GUI.Label(new Rect(10, 40, 500, 30), $"Roll: {inputManager.GetRoll():F2} → {angVel.z:F1}°/s (at {euler.z:F1}°)", style);
            GUI.Label(new Rect(10, 70, 500, 30), $"Pitch: {inputManager.GetPitch():F2} → {angVel.x:F1}°/s (at {euler.x:F1}°)", style);
            GUI.Label(new Rect(10, 100, 500, 30), $"Yaw: {inputManager.GetYaw():F2} → {angVel.y:F1}°/s (at {euler.y:F1}°)", style);
        }

        GUI.Label(new Rect(10, 130, 400, 30), $"Throttle: {inputManager.GetThrottle():F2} ({inputManager.GetThrottle() * 100:F0}%)", style);

        // Helper text
        style.fontSize = 14;
        style.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        string helpText = useLegacyPositionControl ?
            "LEGACY: Plane mirrors hand angle" :
            "RATE-BASED: Hand controls rotation speed - can rotate continuously!";
        GUI.Label(new Rect(10, 170, 700, 30), helpText, style);
    }
}
