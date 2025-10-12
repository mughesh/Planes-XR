using UnityEngine;

/// <summary>
/// Manages flight input sources and handles automatic switching
/// For Phase 1.1: Only hand tracking, controller support will be added in Phase 1.2
/// </summary>
public class FlightInputManager : MonoBehaviour
{
    [Header("Input Providers")]
    public HandTrackingInput handInput;
    // public ControllerInput controllerInput; // TODO: Phase 1.2

    [Header("Settings")]
    [Tooltip("Which input method to prioritize when both are active")]
    public InputPriority priority = InputPriority.HandsFirst;

    [Header("Status (Read-only)")]
    [SerializeField] private InputMode currentMode = InputMode.None;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private IFlightInputProvider activeProvider;

    public enum InputPriority
    {
        HandsFirst,     // Prefer hands when available
        ControllerFirst // Prefer controller when available
    }

    public enum InputMode
    {
        None,
        HandTracking,
        Controller
    }

    void Start()
    {
        // Auto-create hand input if not assigned
        if (handInput == null)
        {
            handInput = gameObject.AddComponent<HandTrackingInput>();
            Debug.Log("Auto-created HandTrackingInput component");
        }
    }

    void Update()
    {
        DetermineActiveInput();

        if (activeProvider != null)
        {
            activeProvider.UpdateInput();
        }
        else if (showDebugLogs && Time.frameCount % 120 == 0)
        {
            Debug.LogWarning("[InputManager] No active provider! Check if HandTrackingInput is assigned and hand transform is set.");
        }
    }

    void DetermineActiveInput()
    {
        // For Phase 1.1: Only hand tracking available
        if (handInput != null && handInput.IsActive())
        {
            activeProvider = handInput;
            currentMode = InputMode.HandTracking;
        }
        else
        {
            activeProvider = null;
            currentMode = InputMode.None;

            if (showDebugLogs && Time.frameCount % 120 == 0)
            {
                if (handInput == null)
                    Debug.LogWarning("[InputManager] handInput is NULL!");
                else
                    Debug.LogWarning($"[InputManager] handInput.IsActive() = {handInput.IsActive()}");
            }
        }

        // TODO Phase 1.2: Add controller fallback logic here
        // if (priority == InputPriority.HandsFirst)
        // {
        //     if (handInput.IsActive()) use hands
        //     else if (controllerInput.IsActive()) use controller
        // }
    }

    // === Public API for FlightController ===
    public float GetRoll() => activeProvider?.GetRoll() ?? 0f;
    public float GetPitch() => activeProvider?.GetPitch() ?? 0f;
    public float GetYaw() => activeProvider?.GetYaw() ?? 0f;
    public float GetThrottle() => activeProvider?.GetThrottle() ?? 0f;
    public bool IsInputActive() => activeProvider != null && activeProvider.IsActive();
    public InputMode GetCurrentMode() => currentMode;
}
