# XR Planes Flight Control - Rate-Based System Implementation Plan

## Overview
Upgrading from **position-based** to **rate-based** flight controls for natural, unlimited-rotation flight mechanics in Mixed Reality.

**Core Concept Change:**
- **Old:** Hand at 30° → Plane at 30° (limited range, mirrors hand)
- **New:** Hand at 30° → Plane rotates at 120°/sec (unlimited range, hand controls speed)

---

## Architecture

### File Structure
```
Assets/Scripts/
├── FlightController.cs          [MODIFIED] - Main controller, converts input → rates
├── FlightDynamics.cs            [NEW]      - Physics integration, angular velocity
├── FlightInputManager.cs        [UNCHANGED] - Input source management
├── HandTrackingInput.cs         [UNCHANGED] - Hand tracking processing
├── CockpitSphere.cs            [UNCHANGED] - Control zone detection
└── InputSmoother.cs            [UNCHANGED] - Smoothing utilities
```

### Data Flow
```
Hand Rotation → HandTrackingInput → FlightInputManager → FlightController
                                                              ↓
                                              Convert to Angular Rates (°/s)
                                                              ↓
                                                      FlightDynamics
                                                              ↓
                                        Integrate Velocity → Update Orientation
                                                              ↓
                                                      Plane Transform
```

---

## Phase 1: Core Rate-Based Control ✅ IMPLEMENTED

### Goal
Replace direct angle mapping with angular velocity integration.

### Implementation

**FlightDynamics.cs** - New component
- Manages angular velocity state (pitch rate, yaw rate, roll rate)
- Integrates velocity → orientation using quaternions
- Uses FixedUpdate for physics-stable updates
- Public API: `SetTargetRates(pitch, yaw, roll)`, `GetAngularVelocity()`

**FlightController.cs** - Modified
- Removed: `planeTransform.rotation = Quaternion.Euler(angles)` (direct assignment)
- Added: Rate conversion `float rollRate = rollInput * maxRollRate`
- Added: FlightDynamics reference and calls
- Added: Legacy mode toggle for comparison

**New Parameters (Inspector)**
```
Max Roll Rate:  120°/sec (default)
Max Pitch Rate:  90°/sec (default)
Max Yaw Rate:    45°/sec (default)
```

### Testing Checklist

**Test 1: Continuous Rotation**
- [ ] Tilt hand 45° right
- [ ] Plane continuously rolls right (multiple barrel rolls possible)
- [ ] Rotation speed proportional to hand tilt angle
- [ ] No hand rotation limits

**Test 2: Neutral Position Behavior**
- [ ] Tilt hand, then return to neutral
- [ ] Plane stops rotating but maintains current bank angle
- [ ] Plane does NOT snap back to level
- [ ] Angular velocity reads ~0°/s when hand neutral

**Test 3: Rate Control Validation**
- [ ] Max tilt → max rate (e.g., 120°/s for roll)
- [ ] Half tilt → half rate (e.g., 60°/s)
- [ ] Adjust `maxRollRate` in inspector → rotation speed changes

**Test 4: Legacy Comparison**
- [ ] Toggle `Use Legacy Position Control` ON
- [ ] Plane now mirrors hand angle (old behavior)
- [ ] Toggle OFF → back to rate-based control
- [ ] Clear visual difference in HUD

**Expected HUD Output (Rate Mode):**
```
Mode: [RATE-BASED] [Simple]
Roll: 0.75 → 90.0°/s (at 234.5°)
Pitch: 0.00 → 0.0°/s (at 0.0°)
Yaw: 0.00 → 0.0°/s (at 0.0°)
Throttle: 0.00 (0%)
```

**How to Test:**
1. Play scene in Unity
2. Hold hand at 45° tilt
3. Watch plane continuously rotate (doesn't stop)
4. Return hand to neutral
5. Plane stops rotating at current angle
6. Verify HUD shows angular velocity values

---

## Phase 2: Coordinated Turns (Banking Physics)

### Goal
Banking (rolling) automatically creates yaw, simulating realistic airplane physics.

### Implementation

**FlightDynamics.cs** - Add coordinated turn logic
```csharp
if (enableCoordinatedTurns) {
    float inducedYawRate = sin(rollAngle) * bankingFactor * 60;
    targetYawRate += inducedYawRate;
}
```

**New Parameters**
```
Enable Coordinated Turns: true/false
Banking Factor: 1.0 (realistic) to 2.0 (arcade)
```

### Testing Checklist

**Test 1: Banking Creates Turn**
- [ ] Roll plane left (no yaw input)
- [ ] Plane gradually turns left while maintaining bank
- [ ] Steeper bank → tighter turn radius
- [ ] Level wings → straight flight

**Test 2: Banking Factor Tuning**
- [ ] Banking Factor = 0 → no induced yaw (pure roll)
- [ ] Banking Factor = 1 → realistic turn
- [ ] Banking Factor = 2 → tight arcade turn

**Test 3: Coordinated Flight**
- [ ] Bank 45° left
- [ ] Plane flies in circular path
- [ ] No need to manually add yaw input
- [ ] Feels like real flight sim

**Expected Behavior:**
- Bank left → heading changes left
- Bank right → heading changes right
- Wings level → straight flight (no heading change)

---

## Phase 3: Auto-Stabilization (Optional Assist)

### Goal
Plane gradually levels out when no input detected, reducing pilot workload.

### Implementation

**FlightDynamics.cs** - Add stabilization logic
```csharp
if (enableAutoLevel && noInputDetected) {
    Vector3 stabilizationForce = CalculateStabilizationForce();
    targetRates += stabilizationForce;
}
```

**New Parameters**
```
Enable Auto-Level: true/false
Roll Stabilization Rate: 30°/sec
Pitch Stabilization Rate: 20°/sec
Stabilization Delay: 0.5 seconds
```

### Testing Checklist

**Test 1: Auto-Level Engagement**
- [ ] Bank plane 45°
- [ ] Remove hand from control zone
- [ ] Wait 0.5 seconds
- [ ] Plane gradually returns to wings level over ~2-3 seconds

**Test 2: Stabilization Doesn't Fight Input**
- [ ] Enable auto-level
- [ ] Actively roll plane
- [ ] No resistance or fighting from stabilization
- [ ] Stabilization only engages when idle

**Test 3: Difficulty Modes**
- [ ] Auto-level ON → easier, forgiving (arcade mode)
- [ ] Auto-level OFF → harder, requires manual control (sim mode)
- [ ] Useful for accessibility options

**Expected Behavior:**
- Idle 0.5s → gentle leveling motion
- Active input → stabilization disabled
- Configurable strength and delay

---

## Phase 4: Response Curves & Feel

### Goal
Small hand movements = precision, large movements = aggressive maneuvers.

### Implementation

**Option A:** Extend HandTrackingInput.cs
```csharp
float ApplyResponseCurve(float input, float angle) {
    if (angle < deadZoneAngle) return 0;
    // Apply exponential or S-curve
}
```

**Option B:** New InputResponseCurve.cs component
- Dead zone: 0-10° → no output
- Gentle zone: 10-25° → 0-50% rate
- Aggressive zone: 25-45° → 50-100% rate

**New Parameters**
```
Dead Zone Angle: 10°
Curve Type: Linear / Exponential / S-Curve
Sensitivity Multiplier: 1.0
```

### Testing Checklist

**Test 1: Dead Zone**
- [ ] Tilt hand <10° → no rotation
- [ ] Prevents unintentional drift
- [ ] Clear threshold feel

**Test 2: Precision Control**
- [ ] Small tilts (10-25°) → slow, precise rotation
- [ ] Can aim at distant target
- [ ] Fine adjustments possible

**Test 3: Aggressive Maneuvers**
- [ ] Large tilts (35-45°) → fast spins
- [ ] Combat-style barrel rolls
- [ ] Rapid direction changes

**Test 4: Curve Comparison**
- [ ] Linear → constant scaling
- [ ] Exponential → more sensitive at high end
- [ ] S-Curve → gentle center, aggressive ends

---

## Phase 5: Movement Integration (Complete Flight)

### Goal
Wire up throttle to forward velocity, apply orientation to flight path.

### Implementation

**FlightDynamics.cs** - Enable movement system
```csharp
void UpdateLinearVelocity(deltaTime) {
    float targetSpeed = throttle * maxSpeed;
    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * deltaTime);
    linearVelocity = planeTransform.forward * currentSpeed;
}

void IntegratePosition(deltaTime) {
    planeTransform.position += linearVelocity * deltaTime;
}
```

**New Parameters**
```
Enable Movement: true
Max Speed: 5 m/s
Acceleration Rate: 2 m/s²
Deceleration Rate: 1 m/s²
```

### Testing Checklist

**Test 1: Basic Flight**
- [ ] Throttle up → plane accelerates forward
- [ ] Throttle down → plane slows smoothly
- [ ] Max throttle → reaches max speed, no overshoot

**Test 2: Curved Flight**
- [ ] Bank left while moving → flies curved path
- [ ] Coordinated turns work with movement
- [ ] Can fly circles, figure-8s

**Test 3: Complex Maneuvers**
- [ ] Full loop (pitch up continuously)
- [ ] Barrel roll while moving forward
- [ ] Immelmann turn (half loop + half roll)
- [ ] All maneuvers feel smooth and controllable

**Test 4: Speed Effects** (Optional)
- [ ] Banking at high speed → wider turns
- [ ] Banking at low speed → tighter turns
- [ ] Speed affects handling feel

---

## Current Status

### ✅ Completed
- Phase 1: Core rate-based control system
- FlightDynamics component created
- FlightController refactored
- Legacy mode for comparison
- Inspector parameters exposed

### 🔄 Ready for Testing
- Continuous rotation capability
- Hand neutral behavior
- Rate parameter tuning
- Legacy vs rate-based comparison

### ⏳ Pending
- Phase 2: Coordinated turns (code ready, disabled)
- Phase 3: Auto-stabilization (code ready, disabled)
- Phase 4: Response curves
- Phase 5: Movement integration (partial)

---

## Key Implementation Details

### Angular Velocity Integration
```csharp
// Core physics loop (FixedUpdate)
Vector3 targetRates = new Vector3(pitchRate, yawRate, rollRate);
currentAngularVelocity = targetRates;

// Integrate to orientation
Vector3 rotationDelta = currentAngularVelocity * Time.fixedDeltaTime;
Quaternion deltaRotation = Quaternion.Euler(rotationDelta);
planeTransform.rotation *= deltaRotation; // Accumulate rotation
```

### Coordinated Turn Formula
```csharp
float rollAngleRad = currentRoll * Mathf.Deg2Rad;
float inducedYaw = Mathf.Sin(rollAngleRad) * bankingFactor * turnStrength;
```

### Auto-Stabilization
```csharp
Vector3 stabilizationForce = Vector3.zero;
stabilizationForce.z = -currentRoll * (rollStabilizationRate / 90f);
stabilizationForce.x = -currentPitch * (pitchStabilizationRate / 90f);
```

---

## Parameter Tuning Guide

### For Arcade Feel
```
Max Roll Rate: 180°/sec (fast spins)
Max Pitch Rate: 120°/sec
Banking Factor: 1.5 (tight turns)
Enable Auto-Level: true
```

### For Simulation Feel
```
Max Roll Rate: 90°/sec (realistic)
Max Pitch Rate: 60°/sec
Banking Factor: 1.0 (realistic physics)
Enable Auto-Level: false
```

### For Accessibility
```
Max Roll Rate: 60°/sec (gentle)
Max Pitch Rate: 45°/sec
Enable Auto-Level: true
Stabilization Delay: 0.2s (quick)
Dead Zone: 15° (large, forgiving)
```

---

## Next Steps

1. **Test Phase 1** (Current)
   - Verify continuous rotation works
   - Validate neutral position behavior
   - Tune max rate parameters

2. **Enable Phase 2** (Once Phase 1 validated)
   - Set `enableCoordinatedTurns = true`
   - Test banking creates turns
   - Tune banking factor

3. **Enable Phase 3** (After Phase 2)
   - Set `enableAutoLevel = true`
   - Test stabilization behavior
   - Tune stabilization rates

4. **Implement Phase 4** (Polish)
   - Add response curve system
   - Test precision vs aggressive control
   - Tune dead zones

5. **Complete Phase 5** (Full Flight)
   - Set `enableMovement = true`
   - Test full 3D flight
   - Tune speed and acceleration

---

## Debugging Tools

### Console Logs
```
[FlightController] Input: R:0.75 P:0.00 Y:0.00 | Rates: (0.0, 0.0, 90.0) °/s
[FlightDynamics] Orientation: (0.0, 0.0, 234.5) | AngularVel: (0.0, 0.0, 90.0) °/s
```

### On-Screen HUD
- Shows current mode (Rate-Based vs Legacy)
- Displays input values and resulting rates
- Shows current orientation angles
- Displays angular velocity in real-time

### Scene View Gizmos
- Yellow line: Angular velocity direction
- Green line: Linear velocity vector (when moving)
- Cyan sphere: Cockpit control zone

---

## Known Limitations & Future Work

### Current Limitations
- No acceleration damping (instant rate changes)
- No speed-dependent handling
- No aerodynamic drag simulation
- Fixed rate limits (no envelope protection)

### Potential Enhancements
- **Rate Damping:** Smooth acceleration to target rate
- **Dynamic Limits:** Max rate decreases at high speed
- **Airspeed Effects:** Lift, drag, stall simulation
- **G-Force Feedback:** Haptic feedback for high-G maneuvers
- **Control Surfaces:** Visual ailerons, elevator, rudder
- **Spatial Audio:** Doppler effect on engine sound

---

## File History

### Version 1.0 (Phase 1) - Current
- Created FlightDynamics.cs
- Refactored FlightController.cs
- Rate-based system functional
- Legacy mode preserved

### Upcoming (Phase 2-5)
- Coordinated turns enabled
- Auto-stabilization enabled
- Response curves implemented
- Full movement system active

---

**Last Updated:** 2025-01-XX
**Status:** Phase 1 Complete, Ready for Testing
**Next Milestone:** Phase 2 (Coordinated Turns)
