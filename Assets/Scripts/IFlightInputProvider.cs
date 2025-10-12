using UnityEngine;

/// <summary>
/// Interface for flight input providers (hand tracking, controllers, etc.)
/// Returns normalized values ready for flight physics
/// </summary>
public interface IFlightInputProvider
{
    /// <summary>
    /// Roll input: -1 (left) to +1 (right)
    /// Represents banking/tilting the plane on its longitudinal axis
    /// </summary>
    float GetRoll();

    /// <summary>
    /// Pitch input: -1 (down) to +1 (up)
    /// Represents nose up/down rotation
    /// </summary>
    float GetPitch();

    /// <summary>
    /// Yaw input: -1 (left) to +1 (right)
    /// Represents left/right turn without banking (rudder control)
    /// </summary>
    float GetYaw();

    /// <summary>
    /// Throttle input: 0 (idle) to 1 (full power)
    /// Represents forward speed control
    /// </summary>
    float GetThrottle();

    /// <summary>
    /// Is this input source currently active and tracking?
    /// </summary>
    bool IsActive();

    /// <summary>
    /// Called every frame to update internal state
    /// </summary>
    void UpdateInput();
}
