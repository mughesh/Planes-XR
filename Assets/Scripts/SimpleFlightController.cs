using UnityEngine;

public class SimpleFlightController : MonoBehaviour
{
    [Header("Hand Tracking - OpenXR")]
    public Transform rightHandTransform; // Assign OpenXRRightHand transform
    public Transform planeObject;

    [Header("Flight Controls")]
    public float rollSensitivity = 1f;
    public float pitchSensitivity = 0f; // Disabled for now
    public float yawSensitivity = 0f;   // Disabled for now
    public float throttleSensitivity = 0f; // Disabled for now

    [Header("Control Limits")]
    public float maxRoll = 45f;
    public float maxPitch = 30f;
    public float maxYaw = 90f;
    public float maxSpeed = 0f; // Disabled movement

    [Header("Neutral Position")]
    public Transform neutralPosition;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private Vector3 neutralPos;
    private Quaternion neutralRot;
    private bool isHandTracked = false;
    private float previousRoll = 0f;

    void Start()
    {
        // Set neutral position (where hand should be for level flight)
        if (neutralPosition == null)
        {
            neutralPosition = transform;
        }

        neutralPos = neutralPosition.position;
        neutralRot = neutralPosition.rotation;

        // Create test plane if none assigned
        if (planeObject == null)
        {
            planeObject = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            planeObject.name = "TestPlane";
            planeObject.position = neutralPos + Vector3.forward * 0.5f;
            planeObject.localScale = Vector3.one * 0.1f;

            // Make it look more like a plane
            planeObject.GetComponent<Renderer>().material.color = Color.cyan;
        }

        Debug.Log("SimpleFlightController initialized. Assign 'OpenXRRightHand' to rightHandTransform field.");
    }

    void Update()
    {
        if (rightHandTransform == null)
        {
            if (showDebugLogs && Time.frameCount % 60 == 0) // Log once per second
            {
                Debug.LogWarning("Right hand transform not assigned! Please assign OpenXRRightHand transform.");
            }
            return;
        }

        // Check if hand is active (moving)
        isHandTracked = rightHandTransform.gameObject.activeInHierarchy;

        if (isHandTracked)
        {
            ControlPlane();
        }
        else if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log("Right hand not tracked or inactive");
        }
    }

    void ControlPlane()
    {
        // Get hand rotation directly from OpenXR hand transform
        Vector3 handEuler = rightHandTransform.eulerAngles;
        Vector3 handPos = rightHandTransform.position;
        Vector3 relativePos = handPos - neutralPos;

        // ROLL CONTROL: Fix the snapping issue by handling angle wrapping properly
        float rawRoll = handEuler.z;

        // Convert 0-360 range to -180 to +180 range
        if (rawRoll > 180f)
        {
            rawRoll -= 360f;
        }

        // Apply sensitivity and clamp
        float roll = Mathf.Clamp(rawRoll * rollSensitivity, -maxRoll, maxRoll);

        // For now, only apply roll - other controls disabled
        float pitch = 0f; // relativePos.y * pitchSensitivity * 100f;
        float yaw = 0f;   // relativePos.x * yawSensitivity * 100f;
        float throttle = 0f; // Mathf.Clamp01((relativePos.z + 0.2f) * throttleSensitivity);

        // Apply only roll rotation to plane (keeping it stationary)
        Vector3 currentEuler = planeObject.eulerAngles;
        Vector3 targetEuler = new Vector3(currentEuler.x, currentEuler.y, roll);
        planeObject.rotation = Quaternion.Euler(targetEuler);

        // No movement for now (maxSpeed = 0)
        if (maxSpeed > 0)
        {
            Vector3 forward = planeObject.forward * throttle * maxSpeed * Time.deltaTime;
            planeObject.position += forward;
        }

        // Debug output
        if (showDebugLogs)
        {
            LogFlightData(rawRoll, roll, pitch, yaw, throttle);
        }

        previousRoll = roll;
    }

    void LogFlightData(float rawRoll, float roll, float pitch, float yaw, float throttle)
    {
        // Only log every 30 frames to avoid spam
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"Hand Z-Euler: {rawRoll:F1}° → Plane Roll: {roll:F1}° | Pitch: {pitch:F1}° | Yaw: {yaw:F1}° | Throttle: {throttle:F2}");
        }
    }

    // Visual debugging in Scene view
    void OnDrawGizmos()
    {
        if (neutralPosition != null)
        {
            // Draw neutral position
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(neutralPosition.position, 0.05f);

            // Draw hand position and connection line when tracked
            if (isHandTracked && rightHandTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(rightHandTransform.position, 0.03f);
                Gizmos.DrawLine(neutralPosition.position, rightHandTransform.position);
            }
        }
    }

    // Helper method to find OpenXR hand automatically
    [ContextMenu("Auto-Find OpenXR Right Hand")]
    void AutoFindOpenXRHand()
    {
        GameObject rightHandGO = GameObject.Find("OpenXRRightHand");
        if (rightHandGO != null)
        {
            rightHandTransform = rightHandGO.transform;
            Debug.Log("✅ Found and assigned OpenXRRightHand!");
        }
        else
        {
            Debug.LogWarning("❌ Could not find 'OpenXRRightHand' in scene. Please assign manually.");
        }
    }
}