using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the smooth leveling of the plane.
/// Controlled by FlightManager.
/// </summary>
public class AutoLeveler : MonoBehaviour
{
    [Header("Settings")]
    public float duration = 2.0f;
    public float rotationSpeed = 2.0f; // Slerp speed

    public void BeginAutoLevel(System.Action onComplete)
    {
        StartCoroutine(AutoLevelRoutine(onComplete));
    }

    private IEnumerator AutoLevelRoutine(System.Action onComplete)
    {
        float elapsed = 0f;
        
        // We want to keep the Yaw (heading) but level Pitch and Roll to 0.
        // But we also want to allow banking turns if we are "loitering"? 
        // No, auto-level is strictly "fly straight and level".
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Current Rotation
            Vector3 currentEuler = transform.rotation.eulerAngles;
            
            // Target: Keep Yaw, Zero Pitch/Roll
            Quaternion targetRot = Quaternion.Euler(0f, currentEuler.y, 0f);
            
            // Smoothly rotate
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            
            yield return null;
        }

        onComplete?.Invoke();
    }
}
