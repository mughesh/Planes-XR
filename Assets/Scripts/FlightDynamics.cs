using UnityEngine;

/// <summary>
/// Rate-based flight dynamics system
/// Converts input rates (degrees/sec) into orientation and velocity changes
/// Handles angular velocity integration, coordinated turns, and auto-stabilization
/// </summary>
public class FlightDynamics : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The plane transform to control")]
    public Transform planeTransform;

    [Header("Phase 1: Rate-Based Control")]
    [Tooltip("Use FixedUpdate for physics calculations (more stable)")]
    public bool useFixedUpdate = true;

    [Header("Phase 2: Coordinated Turns")]
    [Tooltip("Banking creates automatic yaw (realistic turns) - ENABLED for hybrid mode")]
    public bool enableCoordinatedTurns = true;

    [Tooltip("How much roll angle creates turn (0 = none, 1 = realistic, >1 = arcade)")]
    [Range(0f, 2f)]
    public float bankingFactor = 0.8f;  // Passenger plane - moderate turning radius

    [Header("Phase 3: Auto-Stabilization (Disabled for Phase 1)")]
    [Tooltip("Automatically level out when no input detected")]
    public bool enableAutoLevel = false;

    [Tooltip("How fast plane returns to level flight (degrees/sec)")]
    public float rollStabilizationRate = 30f;

    [Tooltip("How fast plane returns to horizontal pitch (degrees/sec)")]
    public float pitchStabilizationRate = 20f;

    [Tooltip("Delay before stabilization engages (seconds)")]
    public float stabilizationDelay = 0.5f;

    [Header("Phase 5: Movement (Disabled for Phase 1)")]
    [Tooltip("Enable forward movement based on throttle")]
    public bool enableMovement = true;

    public float maxSpeed = 1.5f;  // Slow, cinematic speed for video
    public float acceleration = 0.5f;  // Gentle acceleration
    public float deceleration = 0.5f;  // Gentle deceleration

    [Header("Debug")]
    public bool showDebugInfo = false;

    // === STATE ===
    private Vector3 currentAngularVelocity; // degrees/sec for pitch, yaw, roll
    private Vector3 currentLinearVelocity;  // m/s
    private float currentSpeed;
    private float noInputTimer;
    private bool hasInput;

    // Target rates set by controller
    private float targetPitchRate;  // degrees/sec
    private float targetYawRate;    // degrees/sec
    private float targetRollRate;   // degrees/sec
    private float targetThrottle;   // 0-1

    void Start()
    {
        if (planeTransform == null)
        {
            planeTransform = transform;
        }

        // Initialize state
        currentAngularVelocity = Vector3.zero;
        currentLinearVelocity = Vector3.zero;
        currentSpeed = 0f;
    }

    void Update()
    {
        if (!useFixedUpdate)
        {
            UpdateDynamics(Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        if (useFixedUpdate)
        {
            UpdateDynamics(Time.fixedDeltaTime);
        }
    }

    void UpdateDynamics(float deltaTime)
    {
        // NOTE: In hybrid mode, FlightController handles rotation directly.
        // FlightDynamics only handles movement via throttle input.
        // Rotation integration is skipped to avoid conflicts.

        // === PHASE 1: ANGULAR VELOCITY INTEGRATION ===
        // DISABLED in hybrid mode - FlightController handles rotation
        // UpdateAngularVelocity(deltaTime);
        // IntegrateOrientation(deltaTime);

        // === PHASE 5: LINEAR VELOCITY & MOVEMENT ===
        if (enableMovement)
        {
            UpdateLinearVelocity(deltaTime);
            IntegratePosition(deltaTime);
        }

        // Debug output
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            Vector3 euler = planeTransform.eulerAngles;
            Debug.Log($"[FlightDynamics] Orientation: ({euler.x:F1}, {euler.y:F1}, {euler.z:F1}) | AngularVel: ({currentAngularVelocity.x:F1}, {currentAngularVelocity.y:F1}, {currentAngularVelocity.z:F1}) °/s");
        }
    }

    void UpdateAngularVelocity(float deltaTime)
    {
        // Start with target rates from controller input
        Vector3 targetRates = new Vector3(targetPitchRate, targetYawRate, targetRollRate);

        // Detect if we have active input
        hasInput = targetRates.sqrMagnitude > 0.01f;

        // === PHASE 2: COORDINATED TURNS ===
        if (enableCoordinatedTurns)
        {
            // Banking creates automatic yaw (slip/skid physics)
            float currentRollAngle = planeTransform.eulerAngles.z;
            if (currentRollAngle > 180f) currentRollAngle -= 360f;

            float inducedYawRate = Mathf.Sin(currentRollAngle * Mathf.Deg2Rad) * bankingFactor * 60f;
            targetRates.y += inducedYawRate;
        }

        // === PHASE 3: AUTO-STABILIZATION ===
        if (enableAutoLevel && !hasInput)
        {
            noInputTimer += deltaTime;

            if (noInputTimer > stabilizationDelay)
            {
                // Apply stabilization forces
                Vector3 stabilizationForce = CalculateStabilizationForce();
                targetRates += stabilizationForce;
            }
        }
        else
        {
            noInputTimer = 0f;
        }

        // Update angular velocity (instant for now, could add damping)
        currentAngularVelocity = targetRates;
    }

    void IntegrateOrientation(float deltaTime)
    {
        // Convert angular velocity (degrees/sec) to rotation delta
        Vector3 rotationDelta = currentAngularVelocity * deltaTime;

        // Apply rotation using quaternions (correct way to accumulate rotations)
        // Order: Pitch (X) -> Yaw (Y) -> Roll (Z)
        Quaternion deltaRotation = Quaternion.Euler(rotationDelta.x, rotationDelta.y, rotationDelta.z);
        planeTransform.rotation = planeTransform.rotation * deltaRotation;
    }

    Vector3 CalculateStabilizationForce()
    {
        // Get current orientation
        Vector3 euler = planeTransform.eulerAngles;

        // Convert to -180 to +180 range
        float pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;

        float roll = euler.z;
        if (roll > 180f) roll -= 360f;

        // Calculate forces to return to level (0, 0, 0)
        Vector3 force = Vector3.zero;

        // Roll stabilization (strongest - wings level is most important)
        force.z = -roll * (rollStabilizationRate / 90f); // Normalize by 90 degrees

        // Pitch stabilization (weaker - allow some pitch freedom)
        force.x = -pitch * (pitchStabilizationRate / 90f);

        return force;
    }

    void UpdateLinearVelocity(float deltaTime)
    {
        // === PHASE 5: THROTTLE → SPEED ===
        float targetSpeed = targetThrottle * maxSpeed;

        // Accelerate or decelerate toward target
        if (currentSpeed < targetSpeed)
        {
            currentSpeed += acceleration * deltaTime;
            currentSpeed = Mathf.Min(currentSpeed, targetSpeed);
        }
        else if (currentSpeed > targetSpeed)
        {
            currentSpeed -= deceleration * deltaTime;
            currentSpeed = Mathf.Max(currentSpeed, targetSpeed);
        }

        // Convert speed to velocity vector (forward direction)
        currentLinearVelocity = planeTransform.forward * currentSpeed;
    }

    void IntegratePosition(float deltaTime)
    {
        // Move plane based on velocity
        planeTransform.position += currentLinearVelocity * deltaTime;
    }

    // === PUBLIC API FOR FLIGHT CONTROLLER ===

    /// <summary>
    /// Set target angular rates in degrees/second
    /// </summary>
    public void SetTargetRates(float pitchRate, float yawRate, float rollRate)
    {
        targetPitchRate = pitchRate;
        targetYawRate = yawRate;
        targetRollRate = rollRate;
    }

    /// <summary>
    /// Set throttle (0 to 1)
    /// </summary>
    public void SetThrottle(float throttle)
    {
        targetThrottle = Mathf.Clamp01(throttle);
    }

    /// <summary>
    /// Get current orientation
    /// </summary>
    public Quaternion GetOrientation()
    {
        return planeTransform.rotation;
    }

    /// <summary>
    /// Get current angular velocity (degrees/sec)
    /// </summary>
    public Vector3 GetAngularVelocity()
    {
        return currentAngularVelocity;
    }

    /// <summary>
    /// Get current linear velocity (m/s)
    /// </summary>
    public Vector3 GetLinearVelocity()
    {
        return currentLinearVelocity;
    }

    /// <summary>
    /// Get current speed (scalar)
    /// </summary>
    public float GetSpeed()
    {
        return currentSpeed;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || planeTransform == null) return;

        // Draw velocity vector
        if (enableMovement && currentLinearVelocity.magnitude > 0.01f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(planeTransform.position, planeTransform.position + currentLinearVelocity * 0.5f);
        }

        // Draw angular velocity representation
        if (currentAngularVelocity.magnitude > 1f)
        {
            Gizmos.color = Color.yellow;
            Vector3 angVelDir = planeTransform.rotation * currentAngularVelocity.normalized;
            Gizmos.DrawLine(planeTransform.position, planeTransform.position + angVelDir * 0.2f);
        }
    }
}
