using UnityEngine;

/// <summary>
/// Pocket Pilot Flight Controller
/// Handles "Free Flight" input and rotation.
/// Controlled by FlightManager (enabled/disabled).
/// </summary>
public class FlightController : MonoBehaviour
{
    [Header("References")]
    public FlightInputManager inputManager;
    public FlightDynamics flightDynamics;
    public Transform planeTransform;

    [Header("Control Angles")]
    [Range(30f, 70f)] public float maxRollAngle = 45f;
    [Range(15f, 45f)] public float maxPitchAngle = 25f;
    [Range(60f, 180f)] public float rotationSpeed = 120f;
    [Range(50f, 200f)] public float bankingTurnStrength = 100f;

    [Header("Pitch Mode")]
    public PitchMode pitchMode = PitchMode.PositionBased;

    public enum PitchMode
    {
        PositionBased,
        AutoLevel // Not used here anymore, but kept for enum compatibility if needed
    }

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Internal state
    private float currentRoll = 0f;
    private float currentPitch = 0f;
    private float currentYaw = 0f;

    void Start()
    {
        if (inputManager == null) inputManager = FindFirstObjectByType<FlightInputManager>();
        if (flightDynamics == null) flightDynamics = GetComponent<FlightDynamics>();
        if (planeTransform == null) planeTransform = transform;
    }

    /// <summary>
    /// Called by FlightManager when enabling control.
    /// Syncs internal state to current rotation to prevent snapping.
    /// </summary>
    public void SyncRotation()
    {
        Vector3 euler = planeTransform.rotation.eulerAngles;
        
        // Normalize angles to -180 to 180
        currentPitch = (euler.x > 180) ? euler.x - 360 : euler.x;
        currentYaw = euler.y;
        currentRoll = (euler.z > 180) ? euler.z - 360 : euler.z;

        if (showDebugInfo) Debug.Log($"[FlightController] Synced Rotation: P:{currentPitch:F1} Y:{currentYaw:F1} R:{currentRoll:F1}");
    }

    void Update()
    {
        if (!inputManager || !inputManager.IsInputActive()) return;

        UpdateFlightControl();
    }

    void UpdateFlightControl()
    {
        // Input
        float rollInput = inputManager.GetRoll();
        float pitchInput = inputManager.GetPitch();

        // Target Angles
        float targetRoll = rollInput * maxRollAngle;
        float targetPitch = (pitchMode == PitchMode.PositionBased) ? pitchInput * maxPitchAngle : 0f;

        // Smooth Interpolation
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, rotationSpeed * Time.deltaTime);
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, rotationSpeed * Time.deltaTime);

        // Banking Turn (Yaw)
        float bankingYawRate = -Mathf.Sin(currentRoll * Mathf.Deg2Rad) * bankingTurnStrength;
        currentYaw += bankingYawRate * Time.deltaTime;

        // Apply
        planeTransform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);

        // Debug
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Flight] Roll: {currentRoll:F1}° | Pitch: {currentPitch:F1}°");
        }
    }
}
