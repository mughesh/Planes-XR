using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the parabolic launch trajectory.
/// Controlled by FlightManager.
/// </summary>
public class TrajectoryMover : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How long trajectory lasts")]
    public float duration = 1.2f;

    [Tooltip("How high the plane arcs UP")]
    public float arcHeight = 0.5f;

    public void BeginTrajectory(Vector3 startPos, Vector3 direction, float startSpeed, float endSpeed, System.Action onComplete)
    {
        StartCoroutine(TrajectoryRoutine(startPos, direction, startSpeed, endSpeed, onComplete));
    }

    private IEnumerator TrajectoryRoutine(Vector3 startPos, Vector3 direction, float startSpeed, float endSpeed, System.Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 1. Speed Blending (SmoothStep)
            float currentSpeed = Mathf.Lerp(startSpeed, endSpeed, Mathf.SmoothStep(0f, 1f, t));

            // 2. Calculate Distance
            // Approximation: d = v_avg * t
            // For more precision, we could integrate, but this is visual "juice".
            // Let's use the same logic as before: incremental or absolute based on linear accel assumption.
            // Absolute is smoother.
            float accel = (endSpeed - startSpeed) / duration;
            float distTraveled = (startSpeed * elapsed) + (0.5f * accel * elapsed * elapsed);

            Vector3 forwardPos = startPos + (direction * distTraveled);

            // 3. Vertical Arc
            float verticalOffset = 4f * arcHeight * t * (1f - t);

            Vector3 nextPos = forwardPos + (Vector3.up * verticalOffset);
            
            // Rotation
            Vector3 flightDir = (nextPos - transform.position).normalized;
            if (flightDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(flightDir);
            }

            transform.position = nextPos;

            yield return null;
        }

        onComplete?.Invoke();
    }
}
