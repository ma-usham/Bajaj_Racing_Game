using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Takes the game from the menu to the flag.
///
/// Three states, and only one way through them. The menu is up and the gameplay object is
/// switched off entirely, so nothing in the world is ticking behind it. Picking a rider and
/// hitting Let's Ride switches the world on and starts the countdown, and the bike is held on
/// the line for it: the world is already there to look at, the rider simply cannot go yet.
/// The flag drops on GO, which is also when the lap timer starts, so the clock and the throttle
/// are released by the same line of code and cannot drift apart.
///
/// The Let's Ride button is wired up here rather than in the inspector. A listener added in
/// code cannot quietly come unstuck when the button is renamed or the scene is merged, and it
/// keeps who-starts-the-race in one file instead of split between a script and a click handler.
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

        ShowMenu();
    }

    private void OnDestroy()
    {
        if (riderSelection != null && riderSelection.RaceButton != null)
        {
            riderSelection.RaceButton.onClick.RemoveListener(StartRace);
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

        State = RaceState.Countdown;
        mainMenu.SetActive(false);

        // Switched on before the bike is held, because this is the frame the bike's own Awake
        // and OnEnable run in. Holding it first would be writing to a component that has not
        // started yet.
        gameplay.SetActive(true);

        if (player != null)
        {
            player.HoldOnLine();
        }

        if (hud != null)
        {
            hud.StopLap();
        }

        countdown = StartCoroutine(RunCountdown());
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
