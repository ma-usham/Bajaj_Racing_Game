using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class RaceHUD : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerController player;

    [Header("Speed")]
    [SerializeField] private TextMeshProUGUI speedLabel;

    [Tooltip("Reading shown at top speed. World units are arbitrary here, so the HUD maps the " +
             "bike's 0 to 1 speed onto this instead of converting units per second. Retuning " +
             "maxSpeed on the bike then leaves the headline number alone.")]
    [SerializeField] private float displayTopSpeed = 120f;

    [SerializeField] private string speedSuffix = "km/h";

    [Header("Lap time")]
    [SerializeField] private TextMeshProUGUI lapTimeLabel;

    private float lapStartTime;
    private bool lapRunning;

    // Last values pushed to the labels, so the strings are only rebuilt when the reading
    // actually changes rather than every frame.
    private int shownSpeed = -1;
    private int shownSeconds = -1;

    /// Seconds since the current lap started.
    public float LapTime => lapRunning ? Time.time - lapStartTime : 0f;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError($"{name}: no PlayerController assigned, the HUD will not update.", this);
        }

        if (speedLabel == null)
        {
            Debug.LogError($"{name}: no speed label assigned.", this);
        }

        if (lapTimeLabel == null)
        {
            Debug.LogError($"{name}: no lap time label assigned.", this);
        }
    }

    private void OnEnable()
    {
        BeginLap();
    }

    private void Update()
    {
        UpdateSpeed();
        UpdateLapTime();
    }

    /// Restarts the clock from zero. Call this from the race start, and again on each lap
    /// once there is something on the track that can tell when a lap is done.
    public void BeginLap()
    {
        lapStartTime = Time.time;
        lapRunning = true;
        shownSeconds = -1;
    }

    /// Freezes the clock where it stands, for the finish line or a pause.
    public void StopLap()
    {
        lapRunning = false;
    }

    private void UpdateSpeed()
    {
        if (player == null || speedLabel == null)
        {
            return;
        }

        int reading = Mathf.RoundToInt(Mathf.Clamp01(player.SpeedNormalized) * displayTopSpeed);
        if (reading == shownSpeed)
        {
            return;
        }

        shownSpeed = reading;
        speedLabel.text = reading + speedSuffix;
    }

    private void UpdateLapTime()
    {
        if (lapTimeLabel == null || !lapRunning)
        {
            return;
        }

        int elapsed = Mathf.FloorToInt(LapTime);
        if (elapsed == shownSeconds)
        {
            return;
        }

        shownSeconds = elapsed;
        lapTimeLabel.text = $"{elapsed / 60}:{elapsed % 60:00}";
    }
}
