using UnityEngine;
using System.Collections;

/// <summary>
/// Pocket Pilot Flight Controller
/// Hybrid Position-Based Control: Hand angle controls plane orientation
/// Banking creates natural coordinated turns
/// Constant cruise speed (no throttle control)
/// </summary>
public class FlightController : MonoBehaviour
{
    [Header("References")]
    public FlightInputManager inputManager;
    public FlightDynamics flightDynamics;
    public Transform planeTransform;

    [Header("Visuals")]
    [Tooltip("The mini plane on the hand (visual signifier)")]
    public GameObject handVisualPlane;

    [Header("Control Angles")]
    [Tooltip("Maximum roll angle (degrees)")]
    [Range(30f, 70f)]
    public float maxRollAngle = 45f;

    [Tooltip("Maximum pitch angle (degrees)")]
    [Range(15f, 45f)]
    public float maxPitchAngle = 25f;

    [Tooltip("How fast plane responds to input (degrees/second)")]
    [Range(60f, 180f)]
    public float rotationSpeed = 120f;

    [Header("Banking Turn")]
    [Tooltip("How much banking creates yaw (higher = tighter turns)")]
    [Range(50f, 200f)]
    public float bankingTurnStrength = 100f;

    [Header("Auto-Leveling")]
    [Tooltip("Duration of auto-leveling phase after launch (seconds)")]
    public float autoLevelDuration = 2.0f;

    [Header("Pitch Mode")]
    public PitchMode pitchMode = PitchMode.PositionBased;

    public enum PitchMode
    {
        PositionBased,  // Hand tilt controls pitch
        AutoLevel       // Pitch stays level automatically
    }

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Internal state
    private float currentRoll = 0f;
    private float currentPitch = 0f;
    private float currentYaw = 0f;
    private bool isAutoLeveling = false;

    void Start()
    {
        if (inputManager == null)
        {
            inputManager = FindFirstObjectByType<FlightInputManager>();
            if (inputManager == null)
            {
                GameObject go = new GameObject("FlightInputManager");
                inputManager = go.AddComponent<FlightInputManager>();
            }
        }

        if (flightDynamics == null)
        {
            flightDynamics = GetComponent<FlightDynamics>();
            if (flightDynamics == null)
            {
                flightDynamics = gameObject.AddComponent<FlightDynamics>();
            }
        }

        if (planeTransform == null)
        {
            CreateTestPlane();
        }

        if (flightDynamics != null)
        {
            flightDynamics.planeTransform = planeTransform;
        }

        // Ensure visual is hidden at start
        if (handVisualPlane != null)
        {
            handVisualPlane.SetActive(false);
        }
    }

    void Update()
    {
        if (!inputManager.IsInputActive())
        {
            return;
        }

        UpdateFlightControl();
    }

    /// <summary>
    /// Starts the auto-leveling sequence.
    /// Called by PlaneLaunchSequence when trajectory ends.
    /// </summary>
    public void StartAutoLevelSequence()
    {
        StartCoroutine(AutoLevelRoutine());
    }

    private IEnumerator AutoLevelRoutine()
    {
        isAutoLeveling = true;
        
        // Hide visual during auto-level
        if (handVisualPlane != null) handVisualPlane.SetActive(false);

        if (showDebugInfo) Debug.Log("[FlightController] Starting Auto-Level Phase");

        yield return new WaitForSeconds(autoLevelDuration);

        isAutoLeveling = false;
        
        // Show visual when control is returned
        if (handVisualPlane != null) handVisualPlane.SetActive(true);

        if (showDebugInfo) Debug.Log("[FlightController] Auto-Level Complete. Hand Control Active.");
    }

    void UpdateFlightControl()
    {
        float targetRoll = 0f;
        float targetPitch = 0f;

        // If NOT auto-leveling, get input from hand
        if (!isAutoLeveling)
        {
            // Get input from hand tracking (-1 to 1)
            float rollInput = inputManager.GetRoll();
            float pitchInput = inputManager.GetPitch();

            // === ROLL ===
            targetRoll = rollInput * maxRollAngle;

            // === PITCH ===
            if (pitchMode == PitchMode.PositionBased)
            {
                targetPitch = pitchInput * maxPitchAngle;
            }
        }
        else
        {
            // Auto-leveling: Target is zero (flat)
            targetRoll = 0f;
            targetPitch = 0f;
        }

        // === SMOOTH INTERPOLATION ===
        // Use rotationSpeed to smoothly move towards target (whether input or level)
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, rotationSpeed * Time.deltaTime);
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, rotationSpeed * Time.deltaTime);

        // === BANKING TURN (Coordinated Yaw) ===
        // Roll creates yaw: banking left makes the plane turn left
        // Negate to match expected behavior: positive roll (right bank) = positive yaw (turn right)
        float bankingYawRate = -Mathf.Sin(currentRoll * Mathf.Deg2Rad) * bankingTurnStrength;
        currentYaw += bankingYawRate * Time.deltaTime;

        // Keep yaw in reasonable range
        if (currentYaw > 360f) currentYaw -= 360f;
        if (currentYaw < 0f) currentYaw += 360f;

        // === APPLY ROTATION ===
        planeTransform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);

        // === CONSTANT FORWARD MOVEMENT ===
        if (flightDynamics != null)
        {
            flightDynamics.SetThrottle(1f);  // Always full cruise
        }

        // === DEBUG ===
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            float yawRate = -Mathf.Sin(currentRoll * Mathf.Deg2Rad) * bankingTurnStrength;
            string mode = isAutoLeveling ? "AUTO-LEVEL" : "MANUAL";
            Debug.Log($"[Flight] {mode} | Roll: {currentRoll:F1}° | Pitch: {currentPitch:F1}°");
        }
    }

    void CreateTestPlane()
    {
        GameObject planeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        planeGO.name = "TestPlane";
        planeGO.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;
        planeGO.transform.localScale = new Vector3(0.15f, 0.05f, 0.1f);

        Renderer renderer = planeGO.GetComponent<Renderer>();
        renderer.material.color = new Color(0.2f, 0.6f, 1f);

        planeTransform = planeGO.transform;
        Debug.Log("Created test plane");
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.white;

        string pitchModeStr = pitchMode == PitchMode.PositionBased ? "PosBased" : "AutoLevel";
        string status = isAutoLeveling ? "AUTO-LEVELING" : "MANUAL CONTROL";
        
        GUI.Label(new Rect(10, 10, 500, 25), $"[POCKET PILOT] {status}", style);
        GUI.Label(new Rect(10, 35, 500, 25), $"Roll: {currentRoll:F1}° | Pitch: {currentPitch:F1}° | Yaw: {currentYaw:F1}°", style);

        float speed = flightDynamics != null ? flightDynamics.GetSpeed() : 0f;
        GUI.Label(new Rect(10, 60, 500, 25), $"Speed: {speed:F1} m/s", style);
    }
}
