using UnityEngine;

/// <summary>
/// The Brain of the Plane.
/// Orchestrates Movement, Trajectory, AutoLeveling, and Input.
/// </summary>
public class FlightManager : MonoBehaviour
{
    [Header("Components")]
    public TrajectoryMover trajectoryMover;
    public AutoLeveler autoLeveler;
    public FlightController flightController;
    public FlightDynamics flightDynamics;

    [Header("Visuals")]
    public GameObject handVisualPlane; // The mini plane

    [Header("Settings")]
    public float scaleUpDuration = 0.6f;
    public Vector3 scaleInSlingshot = new Vector3(0.2f, 0.2f, 0.2f);
    public Vector3 scaleAfterLaunch = Vector3.one;

    private void Awake()
    {
        // Ensure components are assigned
        if (!trajectoryMover) trajectoryMover = GetComponent<TrajectoryMover>();
        if (!autoLeveler) autoLeveler = GetComponent<AutoLeveler>();
        if (!flightController) flightController = GetComponent<FlightController>();
        if (!flightDynamics) flightDynamics = GetComponent<FlightDynamics>();

        // Initial State
        DisableAllControl();
        transform.localScale = scaleInSlingshot;
        
        if (handVisualPlane) handVisualPlane.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.OnLaunch += HandleLaunch;
    }

    private void OnDisable()
    {
        GameEvents.OnLaunch -= HandleLaunch;
    }

    private void HandleLaunch(Vector3 dir, float speed, bool trigger)
    {
        // 1. Unparent
        transform.SetParent(null);

        // 2. Disable Dynamics (Movement) - Prevent conflict with TrajectoryMover
        if (flightDynamics)
        {
            flightDynamics.enableMovement = false;
        }

        // 3. Start Trajectory
        if (trajectoryMover)
        {
            float targetSpeed = flightDynamics ? flightDynamics.maxSpeed : 2f;
            trajectoryMover.BeginTrajectory(transform.position, dir, speed, targetSpeed, OnTrajectoryDone);
        }
        else
        {
            // Fallback if no trajectory script
            OnTrajectoryDone();
        }

        // 4. Scale Up
        StartCoroutine(ScaleRoutine());
    }

    private void OnTrajectoryDone()
    {
        GameEvents.OnTrajectoryComplete?.Invoke();

        // 5. Enable Dynamics (Movement) for Auto-Level Phase
        if (flightDynamics)
        {
            flightDynamics.enableMovement = true;
            // Ensure we continue at the speed the trajectory ended with
            flightDynamics.SetCurrentSpeed(flightDynamics.maxSpeed);
            // Set throttle to 1 so it maintains speed (otherwise it decelerates to 0)
            flightDynamics.SetThrottle(1f);
        }

        // Start Auto Level
        if (autoLeveler)
        {
            autoLeveler.BeginAutoLevel(OnAutoLevelDone);
        }
        else
        {
            OnAutoLevelDone();
        }
    }

    private void OnAutoLevelDone()
    {
        GameEvents.OnAutoLevelComplete?.Invoke();

        // Enable Hand Control
        if (flightController)
        {
            flightController.enabled = true;
            flightController.SyncRotation(); // FIX: Prevent snap
        }

        // Show Visual
        if (handVisualPlane) handVisualPlane.SetActive(true);
    }

    private void DisableAllControl()
    {
        if (flightController) flightController.enabled = false;
        if (flightDynamics) flightDynamics.enableMovement = false;
        // Trajectory and AutoLeveler are passive (coroutines), so just ensure they aren't running logic
    }

    private System.Collections.IEnumerator ScaleRoutine()
    {
        float elapsed = 0f;
        while (elapsed < scaleUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / scaleUpDuration);
            transform.localScale = Vector3.Lerp(scaleInSlingshot, scaleAfterLaunch, t);
            yield return null;
        }
        transform.localScale = scaleAfterLaunch;
    }
}
