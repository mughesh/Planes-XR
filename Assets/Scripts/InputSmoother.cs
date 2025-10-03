using UnityEngine;

/// <summary>
/// Utility class for smoothing input values to reduce jitter and improve feel
/// Uses multiple techniques: low-pass filter, dead zones, and sensitivity curves
/// </summary>
public class InputSmoother
{
    private float currentValue;
    private float velocity; // For SmoothDamp

    public float smoothTime = 0.1f;      // How long to reach target (lower = more responsive)
    public float deadZone = 0.05f;       // Input below this is treated as zero
    public bool useExponentialCurve = false; // Makes small movements more precise

    public InputSmoother(float initialSmoothTime = 0.1f, float initialDeadZone = 0.05f)
    {
        smoothTime = initialSmoothTime;
        deadZone = initialDeadZone;
        currentValue = 0f;
        velocity = 0f;
    }

    /// <summary>
    /// Smooth an input value using SmoothDamp
    /// </summary>
    public float Smooth(float targetValue, float deltaTime)
    {
        // Apply dead zone first
        if (Mathf.Abs(targetValue) < deadZone)
        {
            targetValue = 0f;
        }

        // Apply exponential curve if enabled (makes center more precise)
        if (useExponentialCurve)
        {
            float sign = Mathf.Sign(targetValue);
            targetValue = sign * Mathf.Pow(Mathf.Abs(targetValue), 2f);
        }

        // Smooth using Unity's SmoothDamp (critically damped spring)
        currentValue = Mathf.SmoothDamp(currentValue, targetValue, ref velocity, smoothTime, Mathf.Infinity, deltaTime);

        return currentValue;
    }

    /// <summary>
    /// Reset smoother to zero (useful when input disconnects)
    /// </summary>
    public void Reset()
    {
        currentValue = 0f;
        velocity = 0f;
    }

    /// <summary>
    /// Get current smoothed value without updating
    /// </summary>
    public float GetValue()
    {
        return currentValue;
    }
}

/// <summary>
/// Advanced One Euro Filter - industry standard for hand tracking smoothing
/// Better than simple low-pass for varying speed movements
/// Based on: http://cristal.univ-lille.fr/~casiez/1euro/
/// </summary>
public class OneEuroFilter
{
    private float minCutoff;      // Decrease to reduce jitter at slow speeds
    private float beta;           // Increase to reduce lag at fast speeds
    private float derivativeCutoff;

    private float previousValue;
    private float previousDerivative;
    private bool initialized;

    public OneEuroFilter(float minCutoff = 1f, float beta = 0.007f, float derivativeCutoff = 1f)
    {
        this.minCutoff = minCutoff;
        this.beta = beta;
        this.derivativeCutoff = derivativeCutoff;
        initialized = false;
    }

    public float Filter(float value, float deltaTime)
    {
        if (deltaTime <= 0f) deltaTime = 0.016f; // Fallback to ~60fps

        if (!initialized)
        {
            previousValue = value;
            previousDerivative = 0f;
            initialized = true;
            return value;
        }

        // Estimate derivative
        float derivative = (value - previousValue) / deltaTime;
        float smoothedDerivative = LowPassFilter(derivative, previousDerivative, Alpha(deltaTime, derivativeCutoff));

        // Adaptive cutoff based on speed of movement
        float adaptiveCutoff = minCutoff + beta * Mathf.Abs(smoothedDerivative);
        float smoothedValue = LowPassFilter(value, previousValue, Alpha(deltaTime, adaptiveCutoff));

        previousValue = smoothedValue;
        previousDerivative = smoothedDerivative;

        return smoothedValue;
    }

    private float LowPassFilter(float current, float previous, float alpha)
    {
        return alpha * current + (1f - alpha) * previous;
    }

    private float Alpha(float deltaTime, float cutoff)
    {
        float tau = 1f / (2f * Mathf.PI * cutoff);
        return 1f / (1f + tau / deltaTime);
    }

    public void Reset()
    {
        initialized = false;
    }
}
