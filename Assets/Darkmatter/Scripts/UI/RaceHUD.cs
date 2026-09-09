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
        int reading = Mathf.RoundToInt(Mathf.Max(player.SpeedNormalized, 0f) * displayTopSpeed);
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
        
        int elapsed = Mathf.Max(0, Mathf.FloorToInt(LapTime * 1000f));
        int minutes = elapsed / 60000;
        int seconds = elapsed / 1000 % 60;
        int milliseconds = elapsed % 1000;

        lapTimeBuilder.Clear();
        lapTimeBuilder.Append(minutes).Append(':');
        if (seconds < 10)
        {
            lapTimeBuilder.Append('0');
        }

        lapTimeBuilder.Append(seconds).Append('.');
        if (milliseconds < 100)
        {
            lapTimeBuilder.Append('0');
        }

        if (milliseconds < 10)
        {
            lapTimeBuilder.Append('0');
        }

        lapTimeBuilder.Append(milliseconds);
        lapTimeLabel.SetText(lapTimeBuilder);
    }
}
