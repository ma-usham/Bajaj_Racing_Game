using System.Text;
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
    
    private int shownSpeed = -1;
    private readonly StringBuilder lapTimeBuilder = new StringBuilder(16);

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
    
    public void BeginLap()
    {
        lapStartTime = Time.time;
        lapRunning = true;
    }
    
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

        // Not clamped to the top of the dial: a boost pad takes the bike past its own top
        // speed, and a speedo that sticks at the headline number while it happens is the one
        // moment the reading would be lying.
        int reading = Reading(player.SpeedNormalized);
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

        Format(lapTimeBuilder, LapTime);
        lapTimeLabel.SetText(lapTimeBuilder);
    }

    /// <summary>
    /// A 0 to 1 speed written the way the speedo writes it, units and all. Public so the
    /// results panel reads off the same dial instead of inventing its own conversion.
    /// </summary>
    public string FormatSpeed(float normalized) => Reading(normalized) + speedSuffix;

    private int Reading(float normalized) =>
        Mathf.RoundToInt(Mathf.Max(normalized, 0f) * displayTopSpeed);

    /// <summary>
    /// A time as m:ss.mmm. Public and static because the results panel shows the same clock
    /// the HUD does, and two spellings of the same time is how they end up disagreeing.
    /// </summary>
    public static string FormatTime(float seconds)
    {
        StringBuilder builder = new StringBuilder(16);
        Format(builder, seconds);
        return builder.ToString();
    }

    private static void Format(StringBuilder builder, float seconds)
    {
        int elapsed = Mathf.Max(0, Mathf.FloorToInt(seconds * 1000f));
        int minutes = elapsed / 60000;
        int wholeSeconds = elapsed / 1000 % 60;
        int milliseconds = elapsed % 1000;

        builder.Clear();
        builder.Append(minutes).Append(':');
        if (wholeSeconds < 10)
        {
            builder.Append('0');
        }

        builder.Append(wholeSeconds).Append('.');
        if (milliseconds < 100)
        {
            builder.Append('0');
        }

        if (milliseconds < 10)
        {
            builder.Append('0');
        }

        builder.Append(milliseconds);
    }
}
