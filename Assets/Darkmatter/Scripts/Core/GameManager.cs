using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public enum RaceState
    {
        Menu,
        Countdown,
        Racing,
        LapComplete,
        ClaimingPrize,
    }

    [Header("Screens")] [Tooltip("The menu, switched on at boot and off the moment the race starts.")] [SerializeField]
    private GameObject mainMenu;

    [Tooltip("Everything that is the race: the world, the bike and the HUD. Switched off at " +
             "boot so none of it ticks behind the menu, and on when Let's Ride is pressed.")]
    [SerializeField]
    private GameObject gameplay;

    [Header("Menu")]
    [Tooltip("Where the Let's Ride button lives. This is what starts the race, so the button " +
             "needs no click handler of its own.")]
    [SerializeField]
    private RiderSelectionUI riderSelection;

    [Header("Race")] [Tooltip("The bike. Held still through the countdown and let go on GO.")] [SerializeField]
    private PlayerController player;

    [Tooltip("Optional. Dropped into place behind the bike at the top of every countdown. " +
             "Without it the camera swings into frame while the numbers run, because the bike " +
             "was put on the grid rather than ridden there.")]
    [SerializeField]
    private CameraFollow chaseCamera;

    [Tooltip("Optional. The lap clock is started on GO rather than when the world appears, so " +
             "the countdown is not on the rider's time.")]
    [SerializeField]
    private RaceHUD hud;

    [Tooltip("Optional. Counts the bike round the circuit and says when a lap is done. " +
             "Without it the race simply never ends.")]
    [SerializeField]
    private LapTracker lapTracker;

    [Header("Lap complete")]
    [Tooltip("Shown when a lap is finished. Switched off the rest of the time.")]
    [SerializeField]
    private GameObject lapCompletePanel;

    [Tooltip("Optional. The lap time is written here, in the same m:ss.mmm the HUD uses.")] [SerializeField]
    private TextMeshProUGUI lapResultLabel;

    [Tooltip("Optional. The fastest the bike went on the lap, in the same units as the speedo.")] [SerializeField]
    private TextMeshProUGUI topSpeedLabel;

    [Tooltip("Puts the bike back on the grid and runs the countdown again. Like Let's Ride, " +
             "this needs no click handler of its own.")]
    [SerializeField]
    private Button raceAgainButton;

    [Header("Prize")]
    [Tooltip("Takes the results away and puts the prize form up. Like the other two, this " +
             "needs no click handler of its own.")]
    [SerializeField]
    private Button claimPrizeButton;

    [Tooltip("The name, phone number and reward code form. Switched off until the prize is " +
             "claimed, and off again whenever the game goes back to the menu or the grid.")]
    [SerializeField]
    private GameObject claimPrizePanel;

    [Header("Countdown")] [Tooltip("Where the numbers are drawn. Switched off between races.")] [SerializeField]
    private TextMeshProUGUI countdownLabel;

    [Tooltip("Counts down from here. 3 gives the usual three, two, one.")] [Min(1)] [SerializeField]
    private int countFrom = 3;

    [Tooltip("Seconds each number is held for.")] [Min(0.1f)] [SerializeField]
    private float beatSeconds = 1f;

    [Tooltip("Shown in place of zero, when the bike is let go.")] [SerializeField]
    private string goWord = "GO!";

    [Tooltip("Seconds GO stays on screen. The bike is already moving through this, so it wants " +
             "to be short enough not to sit over the road.")]
    [Min(0f)]
    [SerializeField]
    private float goHold = 0.7f;

    [Tooltip("How much bigger each number starts before it settles. 1 holds it still.")] [Min(1f)] [SerializeField]
    private float beatPunch = 1.5f;

    [Header("Sound")] [Tooltip("Optional. One per number. Needs an AudioManager in the scene.")] [SerializeField]
    private AudioClip countBeep;

    [Tooltip("Optional. Played on GO.")] [SerializeField]
    private AudioClip goBeep;

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


        if (chaseCamera == null)
        {
            chaseCamera = FindAnyObjectByType<CameraFollow>();
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

        if (claimPrizeButton != null)
        {
            claimPrizeButton.onClick.AddListener(ClaimPrize);
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

        if (claimPrizeButton != null)
        {
            claimPrizeButton.onClick.RemoveListener(ClaimPrize);
        }

        if (lapTracker != null)
        {
            lapTracker.LapCompleted -= OnLapCompleted;
        }
    }


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
        ShowPrizeClaim(false);

        if (lapTracker != null)
        {
            lapTracker.ResetLaps();
        }
    }


    public void StartRace()
    {
        if (State != RaceState.Menu)
        {
            return;
        }

        mainMenu.SetActive(false);


        gameplay.SetActive(true);

        BeginCountdown();
    }


    public void RaceAgain()
    {
        if (State != RaceState.LapComplete)
        {
            return;
        }

        ShowResults(false);
        ShowPrizeClaim(false);

        if (player != null)
        {
            player.ReturnToLine();
        }

        BeginCountdown();
    }


    public void ClaimPrize()
    {
        if (State != RaceState.LapComplete)
        {
            return;
        }

        State = RaceState.ClaimingPrize;

        ShowResults(false);
        ShowPrizeClaim(true);
    }


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


        if (chaseCamera != null)
        {
            chaseCamera.SnapToTarget();
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


    private void OnLapCompleted(int lapsCompleted)
    {
        if (State != RaceState.Racing)
        {
            return;
        }

        State = RaceState.LapComplete;


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

    private void ShowPrizeClaim(bool shown)
    {
        if (claimPrizePanel != null)
        {
            claimPrizePanel.SetActive(shown);
        }
    }

    private IEnumerator RunCountdown()
    {
        for (int count = countFrom; count > 0; count--)
        {
            yield return Beat(count.ToString(), countBeep, beatSeconds);
        }


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