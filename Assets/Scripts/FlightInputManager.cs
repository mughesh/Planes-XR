using UnityEngine;

/// <summary>
/// Pocket Pilot Input Manager
/// Manages hand tracking input for flight controls
/// </summary>
public class FlightInputManager : MonoBehaviour
{
    [Header("Input Provider")]
    public HandTrackingInput handInput;

    [Header("Debug")]
    public bool showDebugLogs = false;

    void Start()
    {
        if (handInput == null)
        {
            handInput = GetComponent<HandTrackingInput>();
            if (handInput == null)
            {
                handInput = gameObject.AddComponent<HandTrackingInput>();
                Debug.Log("Auto-created HandTrackingInput");
            }
        }
    }

    void Update()
    {
        if (handInput != null && handInput.IsActive())
        {
            handInput.UpdateInput();
        }
    }

    // === Public API ===
    public float GetRoll() => handInput?.GetRoll() ?? 0f;
    public float GetPitch() => handInput?.GetPitch() ?? 0f;
    public float GetYaw() => 0f;  // Yaw comes from banking, not input
    public float GetThrottle() => 1f;  // Constant cruise speed
    public bool IsInputActive() => handInput != null && handInput.IsActive();
}
