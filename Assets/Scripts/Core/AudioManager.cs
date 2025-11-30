using UnityEngine;

/// <summary>
/// Central Audio Manager.
/// Listens to GameEvents and plays appropriate sounds.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Slingshot Audio")]
    public AudioSource slingshotSource;
    public AudioClip stretchClip;
    public AudioClip releaseClip;
    [Range(0.5f, 2.0f)] public float minPitch = 0.8f;
    [Range(0.5f, 2.0f)] public float maxPitch = 1.5f;

    [Header("Plane Audio")]
    public AudioSource planeSource;
    public AudioClip planeStartClip;
    public AudioClip planeLoopClip;
    [Range(0f, 1f)] public float loopVolume = 0.6f;

    private bool isStretching = false;

    private void Awake()
    {
        // Ensure sources exist
        if (slingshotSource == null) slingshotSource = gameObject.AddComponent<AudioSource>();
        if (planeSource == null) planeSource = gameObject.AddComponent<AudioSource>();

        // Configure Sources
        slingshotSource.playOnAwake = false;
        planeSource.playOnAwake = false;
        planeSource.loop = true; // For the engine loop
    }

    private void OnEnable()
    {
        GameEvents.OnSlingshotPull += HandleSlingshotPull;
        GameEvents.OnLaunch += HandleLaunch;
        GameEvents.OnFlightControlActive += HandleFlightActive;
        GameEvents.OnLevelComplete += HandleLevelEnd;
        GameEvents.OnCrash += HandleLevelEnd;
    }

    private void OnDisable()
    {
        GameEvents.OnSlingshotPull -= HandleSlingshotPull;
        GameEvents.OnLaunch -= HandleLaunch;
        GameEvents.OnFlightControlActive -= HandleFlightActive;
        GameEvents.OnLevelComplete -= HandleLevelEnd;
        GameEvents.OnCrash -= HandleLevelEnd;
    }

    private void HandleSlingshotPull(float tension)
    {
        // If tension is effectively zero, stop sound
        if (tension < 0.01f)
        {
            if (isStretching)
            {
                slingshotSource.Stop();
                isStretching = false;
            }
            return;
        }

        // Start playing stretch if not already
        if (!isStretching)
        {
            slingshotSource.clip = stretchClip;
            slingshotSource.loop = false;
            slingshotSource.Play();
            isStretching = true;
        }

        // Modulate Pitch based on tension
        slingshotSource.pitch = Mathf.Lerp(minPitch, maxPitch, tension);
    }

    private void HandleLaunch(Vector3 dir, float speed, bool trigger)
    {
        // Stop Stretch
        if (isStretching)
        {
            slingshotSource.Stop();
            isStretching = false;
        }

        // Play Release (OneShot)
        if (releaseClip) slingshotSource.PlayOneShot(releaseClip);

        // Play Plane Start (OneShot)
        if (planeStartClip) planeSource.PlayOneShot(planeStartClip);
    }

    private void HandleFlightActive()
    {
        // Start Engine Loop
        if (planeLoopClip)
        {
            planeSource.clip = planeLoopClip;
            planeSource.loop = true;
            planeSource.volume = loopVolume;
            planeSource.Play();
        }
    }

    private void HandleLevelEnd()
    {
        // Stop Engine Loop
        planeSource.Stop();
    }
}
