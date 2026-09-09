using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takes the game from the menu to the flag.
///
/// Four states, and one way round them. The menu is up and the gameplay object is switched off
/// entirely, so nothing in the world is ticking behind it. Picking a rider and hitting Let's
/// Ride switches the world on and starts the countdown, and the bike is held on the line for
/// it: the world is already there to look at, the rider simply cannot go yet. The flag drops on
/// GO, which is also when the lap timer and the lap counter start, so the clock, the throttle
/// and the lap are released by the same few lines and cannot drift apart. A finished lap parks
/// the bike and puts the time up, and Race Again goes back to the countdown.
///
/// Race Again is a restart, not a reload: the world, the props and the pads all stay exactly
/// where they are, and only the bike, the clock and the lap are put back to the start. That
/// keeps a retry instant, which is what makes a one lap circuit worth retrying.
///
/// Both buttons are wired up here rather than in the inspector. A listener added in code cannot
/// quietly come unstuck when a button is renamed or a scene is merged, and it keeps who-starts-
/// the-race in one file instead of split between a script and a click handler.
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public enum RaceState
    {
        /// <summary>Menu up, world switched off.</summary>
        Menu,

        /// <summary>World up, bike held on the line, numbers counting down.</summary>
        Countdown,

        /// <summary>Flag dropped.</summary>
        Racing,

        /// <summary>Lap in the bag, results up, bike parked.</summary>
        LapComplete,
    }

    [Header("Screens")]
    [Tooltip("The menu, switched on at boot and off the moment the race starts.")]
    [SerializeField] private GameObject mainMenu;

    [Tooltip("Everything that is the race: the world, the bike and the HUD. Switched off at " +
             "boot so none of it ticks behind the menu, and on when Let's Ride is pressed.")]
    [SerializeField] private GameObject gameplay;

    [Header("Menu")]
    [Tooltip("Where the Let's Ride button lives. This is what starts the race, so the button " +
             "needs no click handler of its own.")]
    [SerializeField] private RiderSelectionUI riderSelection;

    [Header("Race")]
    [Tooltip("The bike. Held still through the countdown and let go on GO.")]
    [SerializeField] private PlayerController player;

    [Tooltip("Optional. The lap clock is started on GO rather than when the world appears, so " +
             "the countdown is not on the rider's time.")]
    [SerializeField] private RaceHUD hud;

    [Tooltip("Optional. Counts the bike round the circuit and says when a lap is done. " +
             "Without it the race simply never ends.")]
    [SerializeField] private LapTracker lapTracker;

    [Header("Lap complete")]
    [Tooltip("Shown when a lap is finished. Switched off the rest of the time.")]
    [SerializeField] private GameObject lapCompletePanel;

    [Tooltip("Optional. The lap time is written here, in the same m:ss.mmm the HUD uses.")]
    [SerializeField] private TextMeshProUGUI lapResultLabel;

    [Tooltip("Optional. The fastest the bike went on the lap, in the same units as the speedo.")]
    [SerializeField] private TextMeshProUGUI topSpeedLabel;

    [Tooltip("Puts the bike back on the grid and runs the countdown again. Like Let's Ride, " +
             "this needs no click handler of its own.")]
    [SerializeField] private Button raceAgainButton;

    [Header("Countdown")]
    [Tooltip("Where the numbers are drawn. Switched off between races.")]
    [SerializeField] private TextMeshProUGUI countdownLabel;

    [Tooltip("Counts down from here. 3 gives the usual three, two, one.")]
    [Min(1)]
    [SerializeField] private int countFrom = 3;

    [Tooltip("Seconds each number is held for.")]
    [Min(0.1f)]
    [SerializeField] private float beatSeconds = 1f;

    [Tooltip("Shown in place of zero, when the bike is let go.")]
    [SerializeField] private string goWord = "GO!";

    [Tooltip("Seconds GO stays on screen. The bike is already moving through this, so it wants " +
             "to be short enough not to sit over the road.")]
    [Min(0f)]
    [SerializeField] private float goHold = 0.7f;

    [Tooltip("How much bigger each number starts before it settles. 1 holds it still.")]
    [Min(1f)]
    [SerializeField] private float beatPunch = 1.5f;

    [Header("Sound")]
    [Tooltip("Optional. One per number. Needs an AudioManager in the scene.")]
    [SerializeField] private AudioClip countBeep;

    [Tooltip("Optional. Played on GO.")]
    [SerializeField] private AudioClip goBeep;

    public RaceState State { get; private set; } = RaceState.Menu;

    private Coroutine countdown;

    private void Awake()
    {
        if (mainMenu == null || gameplay == null)
        {
            Debug.LogError($"{name}: the menu and gameplay objects both need assigning, or " +
                           "there is nothing to switch between.", this);
            enabled = false;
            return;
        }

        if (player == null)
        {
            Debug.LogError($"{name}: no PlayerController, so the bike cannot be held on the line.", this);
        }

        if (riderSelection != null && riderSelection.RaceButton != null)
        {
            riderSelection.RaceButton.onClick.AddListener(StartRace);
        }
        else
        {
            Debug.LogWarning($"{name}: no Let's Ride button found, so nothing will start the " +
                             "race. Call StartRace from somewhere, or assign the rider selection.", this);
        }

        if (lapTracker != null)
        {
            lapTracker.LapCompleted += OnLapCompleted;
        }

        if (raceAgainButton != null)
        {
            raceAgainButton.onClick.AddListener(RaceAgain);
        }

        ShowMenu();
    }

    private void OnDestroy()
    {
        if (riderSelection != null && riderSelection.RaceButton != null)
        {
            riderSelection.RaceButton.onClick.RemoveListener(StartRace);
        }

        if (raceAgainButton != null)
        {
            raceAgainButton.onClick.RemoveListener(RaceAgain);
        }

        if (lapTracker != null)
        {
            lapTracker.LapCompleted -= OnLapCompleted;
        }
    }

    /// <summary>
    /// Puts the menu back up and the world away. Also the state the game boots into, so booting
    /// and returning from a race leave things looking exactly the same.
    /// </summary>
    public void ShowMenu()
    {
        if (countdown != null)
        {
            StopCoroutine(countdown);
            countdown = null;
        }

        State = RaceState.Menu;
        gameplay.SetActive(false);
        mainMenu.SetActive(true);
        HideCountdown();
        ShowResults(false);

        if (lapTracker != null)
        {
            lapTracker.ResetLaps();
        }
    }

    /// <summary>
    /// Leaves the menu and starts the countdown. Wired to the Let's Ride button, and safe to
    /// call from anywhere else: a second call while a race is already running does nothing.
    /// </summary>
    public void StartRace()
    {
        if (State != RaceState.Menu)
        {
            return;
        }

        mainMenu.SetActive(false);

        // Switched on before anything is asked of the bike, because this is the frame the
        // bike's own Awake and OnEnable run in. Holding it first would be writing to a
        // component that has not started yet.
        gameplay.SetActive(true);

        BeginCountdown();
    }

    /// <summary>
    /// Back to the grid for another go. Wired to the Race Again button on the results panel.
    /// The world stays up: only the bike, the clock and the lap are put back to the start, so
    /// this is a restart rather than a reload.
    /// </summary>
    public void RaceAgain()
    {
        if (State != RaceState.LapComplete)
        {
            return;
        }

        ShowResults(false);

        if (player != null)
        {
            player.ReturnToLine();
        }

        BeginCountdown();
    }

    /// <summary>Holds the bike, stops the clocks, and runs the numbers down.</summary>
    private void BeginCountdown()
    {
        if (countdown != null)
        {
            StopCoroutine(countdown);
        }

        State = RaceState.Countdown;

        if (player != null)
        {
            player.HoldOnLine();
        }

        if (hud != null)
        {
            hud.StopLap();
        }

        if (lapTracker != null)
        {
            lapTracker.Stop();
        }

        countdown = StartCoroutine(RunCountdown());
    }

    /// <summary>
    /// The lap is in. Everything is stopped before the panel goes up, and the time is read
    /// before the clock is: <see cref="RaceHUD.LapTime"/> reads zero once the lap is stopped.
    /// </summary>
    private void OnLapCompleted(int lapsCompleted)
    {
        if (State != RaceState.Racing)
        {
            return;
        }

        State = RaceState.LapComplete;

        // Both readings are taken before anything is stopped: the clock reads zero once the
        // lap is stopped, and the bike forgets its best speed the moment it is held.
        float lapTime = hud != null ? hud.LapTime : 0f;
        float topSpeed = player != null ? player.TopSpeedNormalized : 0f;

        if (player != null)
        {
            player.HoldOnLine();
        }

        if (hud != null)
        {
            hud.StopLap();
        }

        if (lapTracker != null)
        {
            lapTracker.Stop();
        }

        if (lapResultLabel != null)
        {
            lapResultLabel.text = RaceHUD.FormatTime(lapTime);
        }

        if (topSpeedLabel != null && hud != null)
        {
            topSpeedLabel.text = hud.FormatSpeed(topSpeed);
        }

        ShowResults(true);
    }

    private void ShowResults(bool shown)
    {
        if (lapCompletePanel != null)
        {
            lapCompletePanel.SetActive(shown);
        }
    }

    private IEnumerator RunCountdown()
    {
        for (int count = countFrom; count > 0; count--)
        {
            yield return Beat(count.ToString(), countBeep, beatSeconds);
        }

        // The flag and the clock, together. Anything that waits until after GO is shown is a
        // countdown the rider can beat.
        if (player != null)
        {
            player.Release();
        }

        if (hud != null)
        {
            hud.BeginLap();
        }

        if (lapTracker != null)
        {
            lapTracker.Begin();
        }

        State = RaceState.Racing;

        yield return Beat(goWord, goBeep, goHold);

        HideCountdown();
        countdown = null;
    }

    /// <summary>One number, punched up and settling over the time it is held for.</summary>
    private IEnumerator Beat(string word, AudioClip sound, float seconds)
    {
        if (sound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(sound);
        }

        if (countdownLabel == null)
        {
            yield return new WaitForSeconds(seconds);
            yield break;
        }

        countdownLabel.gameObject.SetActive(true);
        countdownLabel.text = word;

        Transform label = countdownLabel.transform;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            float t = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
            label.localScale = Vector3.one * Mathf.Lerp(beatPunch, 1f, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        label.localScale = Vector3.one;
    }

    private void HideCountdown()
    {
        if (countdownLabel != null)
        {
            countdownLabel.transform.localScale = Vector3.one;
            countdownLabel.gameObject.SetActive(false);
        }
    }
}
