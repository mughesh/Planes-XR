using UnityEngine;
using System;

/// <summary>
/// Central Event Bus for Pocket Pilot.
/// Handles communication between decoupled systems.
/// </summary>
public static class GameEvents
{
    // === Game Flow ===
    public static Action OnLevelStart;      // Called when scene loads/setup complete
    public static Action OnLevelComplete;   // Called when portal entered

    // === Slingshot ===
    public static Action<float> OnSlingshotPull; // float = tension (0-1)
    
    /// <summary>
    /// Fired when the plane is released.
    /// Vector3: Launch Direction
    /// float: Launch Speed
    /// bool: Has passed scale trigger?
    /// </summary>
    public static Action<Vector3, float, bool> OnLaunch;

    // === Flight Phases ===
    public static Action OnTrajectoryComplete;      // Trajectory arc finished
    public static Action OnAutoLevelComplete;       // Auto-leveling finished
    public static Action OnFlightControlActive;     // Full hand control active
    
    // === Gameplay ===
    public static Action<bool> OnLoiterToggle;      // true = loiter (fist), false = fly
    public static Action OnCrash;                   // Plane hit something
    public static Action<int> OnCoinCollected;      // int = coin value
}
