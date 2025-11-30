using UnityEngine;

/// <summary>
/// Pocket Pilot Flight Dynamics
/// Handles forward movement at constant cruise speed
/// Rotation is handled by FlightController
/// </summary>
public class FlightDynamics : MonoBehaviour
{
    [Header("References")]
    public Transform planeTransform;

    [Header("Movement")]
    [Tooltip("Enable forward movement")]
    public bool enableMovement = true;

    [Tooltip("Cruise speed (m/s)")]
    [Range(0.5f, 5f)]
    public float maxSpeed = 1.5f;

    [Tooltip("How fast to reach cruise speed")]
    [Range(0.5f, 3f)]
    public float acceleration = 1f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // State
    private float currentSpeed;
    private float targetThrottle;
    private Vector3 currentVelocity;

    void Start()
    {
        if (planeTransform == null)
        {
            planeTransform = transform;
        }
        currentSpeed = 0f;
    }

    void FixedUpdate()
    {
        if (!enableMovement || planeTransform == null) return;

        // Calculate target speed
        float targetSpeed = targetThrottle * maxSpeed;

        // Smoothly approach target speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        // Move forward
        currentVelocity = planeTransform.forward * currentSpeed;
        planeTransform.position += currentVelocity * Time.fixedDeltaTime;

        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[FlightDynamics] Speed: {currentSpeed:F1} m/s | Throttle: {targetThrottle:F2}");
        }
    }

    // === Public API ===
    public void SetThrottle(float throttle)
    {
        targetThrottle = Mathf.Clamp01(throttle);
    }

    public void SetCurrentSpeed(float speed)
    {
        currentSpeed = speed;
    }

    public float GetSpeed() => currentSpeed;
    public Vector3 GetVelocity() => currentVelocity;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || planeTransform == null) return;

        if (currentVelocity.magnitude > 0.01f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(planeTransform.position, planeTransform.position + currentVelocity * 0.3f);
        }
    }
}
