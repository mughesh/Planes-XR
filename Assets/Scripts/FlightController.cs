using UnityEngine;

/// <summary>
/// Main flight controller - converts normalized input to flight dynamics
/// HYBRID POSITION-BASED SYSTEM: Hand angle determines plane orientation (clamped)
/// Banking creates natural turning via FlightDynamics coordinatedTurns
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

    [Header("Hybrid Position-Based Control")]
    [Tooltip("Maximum roll angle (degrees) - clamped")]
    [Range(30f, 80f)]
    public float maxRollAngle = 45f;

    [Tooltip("Maximum pitch angle (degrees) - clamped")]
    [Range(10f, 45f)]
    public float maxPitchAngle = 25f;

    [Tooltip("How fast plane interpolates to target angle (degrees/second)")]
    [Range(30f, 180f)]
    public float rotationSpeed = 90f;

    [Header("Pitch Behavior")]
    [Tooltip("Choose how pitch input is handled")]
    public PitchMode pitchMode = PitchMode.PositionBased;

    public enum PitchMode
    {
        PositionBased,  // Hand tilt up/down = plane pitch up/down (clamped)
        AutoLevel       // Plane stays level, ignores pitch input
    }

    [Header("Turn Radius")]
    [Tooltip("Multiplier for banking-based yaw rate (higher = tighter/faster turns)")]
    [Range(30f, 180f)]
    public float bankingTurnStrength = 100f;  // Controls how much banking creates yaw

    [Header("Stabilization")]
    [Tooltip("Auto-level roll and pitch when hand leaves sphere")]
    public bool enableAutoLevel = true;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Control state
    private float currentRoll = 0f;
    private float currentPitch = 0f;

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

        UpdatePlaneRotation_Hybrid();
    }

    // State for cumulative yaw from banking
    private float currentYaw = 0f;

    void UpdatePlaneRotation_Hybrid()
    {
        // HYBRID POSITION-BASED CONTROL SYSTEM
        // Hand angle directly controls plane orientation (with clamping)
        // Banking creates natural turning (yaw accumulation based on roll angle)

        float rollInput = inputManager.GetRoll();      // -1 to 1
        float pitchInput = inputManager.GetPitch();    // -1 to 1
        float throttleInput = inputManager.GetThrottle();  // 0 to 1

        // === ROLL CONTROL ===
        // Hand tilt left/right = plane bank left/right (clamped to maxRollAngle)
        // NEGATE rollInput: Z-axis rotation in Euler is inverted relative to hand tilt intuition
        float targetRoll = -rollInput * maxRollAngle;

        // === PITCH CONTROL ===
        // Depends on pitchMode setting
        float targetPitch = 0f;
        if (pitchMode == PitchMode.PositionBased)
        {
            // Hand tilt up/down = plane pitch up/down (clamped to maxPitchAngle)
            targetPitch = pitchInput * maxPitchAngle;
        }
        // else AutoLevel: pitch stays at 0, targetPitch = 0

        // === STABILIZATION ===
        // When hand leaves sphere, gradually return to level (0° roll and pitch)
        if (enableAutoLevel && !inputManager.handInput.IsHandInSphere())
        {
            targetRoll = 0f;
            targetPitch = 0f;
        }

        // === SMOOTH INTERPOLATION ===
        // Plane smoothly moves toward target angles (not instant)
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, rotationSpeed * Time.deltaTime);
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, rotationSpeed * Time.deltaTime);

        // === BANKING-BASED YAW (Coordinated Turn) ===
        // Banking (roll) naturally creates turning (yaw change)
        // More bank = tighter turn radius
        // Formula: yawRate = sin(rollAngle) * bankingTurnStrength
        float bankingYawRate = Mathf.Sin(currentRoll * Mathf.Deg2Rad) * bankingTurnStrength;
        currentYaw += bankingYawRate * Time.deltaTime;
        // Keep yaw in 0-360 range
        if (currentYaw > 360f) currentYaw -= 360f;
        if (currentYaw < 0f) currentYaw += 360f;

        // === APPLY ROTATION ===
        // Yaw from banking creates natural turning without direct yaw input
        planeTransform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);

        // === MOVEMENT ===
        // Send throttle to dynamics system for forward motion
        if (flightDynamics != null)
        {
            flightDynamics.SetThrottle(throttleInput);
        }

        // === DEBUG OUTPUT ===
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            string pitchModeStr = pitchMode == PitchMode.PositionBased ? "PosBased" : "AutoLevel";
            float bankingYawRateDebug = Mathf.Sin(currentRoll * Mathf.Deg2Rad) * bankingTurnStrength;
            Debug.Log($"[FlightController HYBRID] Roll: {rollInput:F2}→{currentRoll:F1}° | Pitch: {pitchInput:F2}→{currentPitch:F1}° | Yaw: {currentYaw:F1}° (rate: {bankingYawRateDebug:F1}°/s) [{pitchModeStr}] | Throttle: {throttleInput:F2}");
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
        string pitchModeStr = pitchMode == PitchMode.PositionBased ? "PosBased" : "AutoLevel";
        GUI.Label(new Rect(10, 10, 600, 30), $"Mode: [HYBRID] Pitch: [{pitchModeStr}] {smoothingInfo}", style);

        // Hybrid mode display
        Vector3 euler = planeTransform.eulerAngles;
        GUI.Label(new Rect(10, 40, 500, 30), $"Roll: {inputManager.GetRoll():F2} → {currentRoll:F1}° (target ±{maxRollAngle:F0}°)", style);

        if (pitchMode == PitchMode.PositionBased)
        {
            GUI.Label(new Rect(10, 70, 500, 30), $"Pitch: {inputManager.GetPitch():F2} → {currentPitch:F1}° (target ±{maxPitchAngle:F0}°)", style);
        }
        else
        {
            GUI.Label(new Rect(10, 70, 500, 30), $"Pitch: AUTO-LEVEL (staying level)", style);
        }

        GUI.Label(new Rect(10, 100, 500, 30), $"Yaw: From Banking (coordinatedTurns enabled)", style);
        GUI.Label(new Rect(10, 130, 400, 30), $"Throttle: {inputManager.GetThrottle():F2} ({inputManager.GetThrottle() * 100:F0}%)", style);

        // Helper text
        style.fontSize = 14;
        style.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        string helpText = "HYBRID: Hand angle = plane bank (clamped). Banking creates turning. Auto-levels when hand leaves sphere.";
        GUI.Label(new Rect(10, 170, 900, 30), helpText, style);
    }
}
