using UnityEngine;
using System.Collections;

/// <summary>
/// Manages the coordination between the Slingshot and the Plane Launch Sequence.
/// This script should be placed on a persistent GameObject (e.g., "SlingshotManager").
/// </summary>
public class SlingshotManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The SlingshotController script")]
    public SlingshotController slingshotController;

    [Tooltip("The PlaneLaunchSequence script on the plane")]
    public PlaneLaunchSequence planeLauncher;

    [Header("Settings")]
    [Tooltip("Delay before hiding slingshot after launch (seconds)")]
    [Range(0.1f, 2f)]
    public float hideDelay = 0.5f;

    [Tooltip("Wait for snap back to complete before hiding?")]
    public bool waitForSnapBack = true;

    private void OnEnable()
    {
        if (slingshotController != null)
        {
            slingshotController.OnLaunchRequest += HandleLaunchRequest;
        }
        else
        {
            Debug.LogError("[SlingshotManager] SlingshotController reference missing!");
        }
    }

    private void OnDisable()
    {
        if (slingshotController != null)
        {
            slingshotController.OnLaunchRequest -= HandleLaunchRequest;
        }
    }

    private void HandleLaunchRequest(Vector3 launchDirection, float launchSpeed, bool hasPassedTrigger)
    {
        Debug.Log("[SlingshotManager] Launch request received.");

        // 1. Launch the plane
        if (planeLauncher != null)
        {
            // Detach plane from slingshot (if it was parented)
            planeLauncher.transform.SetParent(null);
            
            // Start the plane's sequence
            planeLauncher.StartLaunch(launchDirection, launchSpeed, hasPassedTrigger);
        }
        else
        {
            Debug.LogError("[SlingshotManager] PlaneLaunchSequence reference missing!");
        }

        // 2. Handle Slingshot cleanup
        StartCoroutine(CleanupSlingshotSequence());
    }

    private IEnumerator CleanupSlingshotSequence()
    {
        // Wait for snap back if requested
        if (waitForSnapBack && slingshotController != null)
        {
            // Wait until the controller says it's done snapping back
            // We can check the isSnappingBack property or distance
            while (slingshotController.IsSnappingBack)
            {
                yield return null;
            }
        }

        // Optional extra delay
        if (hideDelay > 0)
        {
            yield return new WaitForSeconds(hideDelay);
        }

        // Disable the slingshot object
        if (slingshotController != null)
        {
            Debug.Log("[SlingshotManager] Disabling slingshot.");
            slingshotController.gameObject.SetActive(false);
        }
    }
}
