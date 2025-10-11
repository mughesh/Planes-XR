using UnityEngine;

/// <summary>
/// Defines a spherical control zone for hand-based flight controls
/// Hand must be inside sphere for inputs to register
/// Sphere center = neutral position for all controls
/// </summary>
public class CockpitSphere : MonoBehaviour
{
    [Header("Sphere Properties")]
    [Tooltip("Radius of the control sphere in meters (default 0.15m = 15cm)")]
    [Range(0.1f, 0.5f)]
    public float radius = 0.15f;

    [Header("Behavior")]
    [Tooltip("Time to fade inputs when entering/exiting sphere")]
    [Range(0f, 1f)]
    public float transitionTime = 0.3f;

    [Header("Debug")]
    public bool showDebug = false;

    // Public accessors
    public Vector3 Center => transform.position;
    public float Radius => radius;

    /// <summary>
    /// Check if a point is inside the control sphere
    /// </summary>
    public bool IsPointInside(Vector3 point)
    {
        float distance = Vector3.Distance(point, Center);
        return distance <= radius;
    }

    /// <summary>
    /// Get normalized position of point within sphere (-1 to +1 range per axis)
    /// Returns Vector3.zero if point is outside sphere
    /// </summary>
    public Vector3 GetNormalizedPosition(Vector3 point)
    {
        Vector3 offset = point - Center;
        float distance = offset.magnitude;

        if (distance > radius)
        {
            // Outside sphere
            return Vector3.zero;
        }

        // Normalize to -1 to +1 range based on radius
        return offset / radius;
    }

    /// <summary>
    /// Get how far the point is from center as a 0-1 value
    /// 0 = at center, 1 = at edge, >1 = outside
    /// </summary>
    public float GetDistanceRatio(Vector3 point)
    {
        float distance = Vector3.Distance(point, Center);
        return distance / radius;
    }

    void OnDrawGizmos()
    {
        // Draw sphere boundary
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);

        // Draw center point
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 0.01f);

        // Draw coordinate axes from center
        Gizmos.color = Color.red; // X-axis (yaw left/right)
        Gizmos.DrawLine(transform.position - transform.right * radius, transform.position + transform.right * radius);

        Gizmos.color = Color.green; // Y-axis (up/down - not used)
        Gizmos.DrawLine(transform.position - transform.up * radius * 0.5f, transform.position + transform.up * radius * 0.5f);

        Gizmos.color = Color.blue; // Z-axis (throttle forward/back)
        Gizmos.DrawLine(transform.position - transform.forward * radius, transform.position + transform.forward * radius);
    }

    void OnDrawGizmosSelected()
    {
        // When selected, draw more detailed info
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Draw octants to show control regions
        float r = radius;
        Vector3 c = transform.position;

        // Draw control zone markers
        Gizmos.color = Color.yellow;
        // Yaw markers
        Gizmos.DrawWireSphere(c + transform.right * r, 0.02f); // Right yaw
        Gizmos.DrawWireSphere(c - transform.right * r, 0.02f); // Left yaw

        // Throttle markers
        Gizmos.DrawWireSphere(c + transform.forward * r, 0.02f); // Forward throttle
        Gizmos.DrawWireSphere(c - transform.forward * r, 0.02f); // Back throttle
    }
}
