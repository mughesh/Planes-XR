using UnityEngine;

/// <summary>
/// Manages the high-level Game State and Flow.
/// Persists throughout the level.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Initialization,
        Aiming,
        Launching,
        AutoLeveling,
        Flying,
        Loitering,
        LevelEnd
    }

    [Header("Debug")]
    public bool showDebugLogs = true;
    public GameState CurrentState { get; private set; }

    private void Start()
    {
        // Initialize Game
        SetState(GameState.Initialization);
        
        // Simulate Level Start (in a real game, this might wait for room scan)
        Invoke(nameof(StartLevel), 1.0f);
    }

    private void OnEnable()
    {
        GameEvents.OnLaunch += HandleLaunch;
        GameEvents.OnTrajectoryComplete += HandleTrajectoryComplete;
        GameEvents.OnAutoLevelComplete += HandleAutoLevelComplete;
        GameEvents.OnLoiterToggle += HandleLoiterToggle;
        GameEvents.OnLevelComplete += HandleLevelComplete;
    }

    private void OnDisable()
    {
        GameEvents.OnLaunch -= HandleLaunch;
        GameEvents.OnTrajectoryComplete -= HandleTrajectoryComplete;
        GameEvents.OnAutoLevelComplete -= HandleAutoLevelComplete;
        GameEvents.OnLoiterToggle -= HandleLoiterToggle;
        GameEvents.OnLevelComplete -= HandleLevelComplete;
    }

    private void StartLevel()
    {
        if (showDebugLogs) Debug.Log("[GameManager] Level Started");
        GameEvents.OnLevelStart?.Invoke();
        SetState(GameState.Aiming);
    }

    private void HandleLaunch(Vector3 dir, float speed, bool trigger)
    {
        SetState(GameState.Launching);
    }

    private void HandleTrajectoryComplete()
    {
        SetState(GameState.AutoLeveling);
    }

    private void HandleAutoLevelComplete()
    {
        SetState(GameState.Flying);
        GameEvents.OnFlightControlActive?.Invoke();
    }

    private void HandleLoiterToggle(bool isLoitering)
    {
        if (CurrentState == GameState.Flying || CurrentState == GameState.Loitering)
        {
            SetState(isLoitering ? GameState.Loitering : GameState.Flying);
        }
    }

    private void HandleLevelComplete()
    {
        SetState(GameState.LevelEnd);
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        if (showDebugLogs) Debug.Log($"[GameManager] State: {CurrentState}");
    }
}
