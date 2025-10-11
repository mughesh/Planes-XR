using UnityEngine;

/// <summary>
/// Main flight controller that applies input to the plane
/// Phase 1.1: Stationary plane with roll/pitch visualization only
/// </summary>
public class FlightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Input manager handling hand tracking / controller switching")]
    public FlightInputManager inputManager;

    [Tooltip("The plane GameObject to control")]
    public Transform planeTransform;

    [Header("Phase 1: Rotation Visualization")]
    [Tooltip("How fast the plane rotates to match input (degrees/second)")]
    public float rotationSpeed = 90f;

    [Tooltip("Maximum roll angle in degrees")]
    public float maxRollAngle = 60f;

    [Tooltip("Maximum pitch angle in degrees")]
    public float maxPitchAngle = 45f;

    [Tooltip("Maximum yaw angle in degrees (for visualization only)")]
    public float maxYawAngle = 30f;

    [Header("Phase 1.2+: Movement (Disabled for now)")]
    public bool enableMovement = false;
    public float maxSpeed = 2f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Current plane state
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

        // Create test plane if none assigned
        if (planeTransform == null)
        {
            CreateTestPlane();
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

        UpdatePlaneRotation();

        if (enableMovement)
        {
            UpdatePlaneMovement();
        }
    }

    void UpdatePlaneRotation()
    {
        // Get normalized input values (-1 to 1)
        float rollInput = inputManager.GetRoll();
        float pitchInput = inputManager.GetPitch();
        float yawInput = inputManager.GetYaw();

        // Convert to target angles
        float targetRoll = rollInput * maxRollAngle;
        float targetPitch = pitchInput * maxPitchAngle;
        float targetYaw = yawInput * maxYawAngle;

        // Smoothly interpolate current angles toward target
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, rotationSpeed * Time.deltaTime);
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, rotationSpeed * Time.deltaTime);
        currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, rotationSpeed * Time.deltaTime);

        // Apply rotation to plane
        // Aircraft convention: Roll = Z-axis, Pitch = X-axis, Yaw = Y-axis
        planeTransform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);

        // Debug output
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            float throttle = inputManager.GetThrottle();
            Debug.Log($"Flight Control → Roll: {rollInput:F2} ({currentRoll:F1}°) | Pitch: {pitchInput:F2} ({currentPitch:F1}°) | Yaw: {yawInput:F2} ({currentYaw:F1}°) | Throttle: {throttle:F2}");
        }
    }

    void UpdatePlaneMovement()
    {
        // TODO Phase 1.3: Implement velocity-based movement
        float throttle = inputManager.GetThrottle();
        Vector3 forward = planeTransform.forward * throttle * maxSpeed * Time.deltaTime;
        planeTransform.position += forward;
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

        GUI.Label(new Rect(10, 10, 500, 30), $"Input Mode: {inputManager.GetCurrentMode()}{smoothingInfo}", style);
        GUI.Label(new Rect(10, 40, 400, 30), $"Roll: {inputManager.GetRoll():F2} ({currentRoll:F1}°)", style);
        GUI.Label(new Rect(10, 70, 400, 30), $"Pitch: {inputManager.GetPitch():F2} ({currentPitch:F1}°)", style);
        GUI.Label(new Rect(10, 100, 400, 30), $"Yaw: {inputManager.GetYaw():F2} ({currentYaw:F1}°)", style);
        GUI.Label(new Rect(10, 130, 400, 30), $"Throttle: {inputManager.GetThrottle():F2} ({inputManager.GetThrottle() * 100:F0}%)", style);

        // Helper text
        style.fontSize = 14;
        style.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(10, 170, 600, 30), "Tilt hand = Roll/Pitch | Move hand left/right = Yaw | Move forward/back = Throttle", style);
    }
}
