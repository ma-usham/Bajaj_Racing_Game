using System.Collections;
using Darkmatter.Gameplay;
using Darkmatter.UI;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace Darkmatter.Core
{
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

        [Header("Input")]
        [Tooltip("The driving controls. Handed to the rider when the flag drops and taken back the " +
                 "moment the lap is in, so the bike cannot be steered from the menu, through the " +
                 "countdown, or while a name is being typed into the prize form.")]
        [SerializeField]
        private InputReaderSO inputReader;

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

        [Tooltip("Optional. Fired the moment the lap is in, with the screen to itself before " +
                 "the results panel goes up. Its own Play On Awake is overruled, so it does not " +
                 "go off when the world appears.")]
        [SerializeField]
        private ParticleSystem confetti;

        [Tooltip("Seconds the confetti gets before the results appear over it. 0 puts them up " +
                 "straight away.")]
        [Min(0f)]
        [SerializeField]
        private float resultsDelay = 1f;

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

        [Header("Countdown")]
        [Tooltip("Runs the three, two, one. Owns the label, the timing and the beeps; this only " +
                 "tells it when to start and listens for the flag dropping.")]
        [SerializeField]
        private RaceCountdown countdown;

        public RaceState State { get; private set; } = RaceState.Menu;

        private Coroutine celebration;

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

            if (inputReader == null)
            {
                Debug.LogError($"{name}: no InputReaderSO, so nothing will ever hand the rider the " +
                               "controls and the bike will not steer.", this);
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

            SetDrivingEnabled(false);
        }


        public void ShowMenu()
        {
            StopCountdown();
            StopCelebrating();

            State = RaceState.Menu;
            SetDrivingEnabled(false);
            gameplay.SetActive(false);
            mainMenu.SetActive(true);
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
            State = RaceState.Countdown;
            SetDrivingEnabled(false);
            StopCelebrating();

            if (player != null)
            {
                player.HoldOnLine();
                player.StartEngine();
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

            if (countdown != null)
            {
                countdown.Run(DropTheFlag);
            }
            else
            {
                Debug.LogError($"{name}: no RaceCountdown, so the flag would never drop. Letting " +
                               "the rider go straight away instead.", this);
                DropTheFlag();
            }
        }

        /// <summary>
        /// The flag, the throttle and the clock, together. Handed to the countdown rather than
        /// run after it, so the moment the numbers finish is the moment the race starts and the
        /// two cannot drift apart.
        /// </summary>
        private void DropTheFlag()
        {
            SetDrivingEnabled(true);

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
        }

        private void StopCountdown()
        {
            if (countdown != null)
            {
                countdown.Stop();
            }
        }


        private void OnLapCompleted(int lapsCompleted)
        {
            if (State != RaceState.Racing)
            {
                return;
            }

            State = RaceState.LapComplete;
            SetDrivingEnabled(false);


            float lapTime = hud != null ? hud.LapTime : 0f;
            float topSpeed = player != null ? player.TopSpeedNormalized : 0f;

            if (player != null)
            {
                player.HoldOnLine();
                player.StopEngine();
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

            celebration = StartCoroutine(Celebrate());
        }

        /// <summary>
        /// Confetti first, results after. The panel fills the screen, so putting it up on the
        /// same frame would bury the confetti under it before any of it was seen.
        /// </summary>
        private IEnumerator Celebrate()
        {
            if (confetti != null)
            {
                confetti.gameObject.SetActive(true);
                confetti.Clear(true);
                confetti.Play(true);
            }

            if (resultsDelay > 0f)
            {
                yield return new WaitForSeconds(resultsDelay);
            }

            ShowResults(true);
            celebration = null;
        }

        /// <summary>
        /// Drops the celebration on the floor, for the ways out of the results that do not go
        /// through waiting for them.
        /// </summary>
        private void StopCelebrating()
        {
            if (celebration != null)
            {
                StopCoroutine(celebration);
                celebration = null;
            }

            if (confetti != null)
            {
                confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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

        /// <summary>
        /// The controls are live exactly while the race is, and nowhere else. Owning that here
        /// rather than in the bike's OnEnable means it follows the race rather than following
        /// whoever happened to switch the gameplay object on.
        /// </summary>
        private void SetDrivingEnabled(bool driving)
        {
            if (inputReader == null)
            {
                return;
            }

            if (driving)
            {
                inputReader.Enable();
            }
            else
            {
                inputReader.Disable();
            }
        }

    }
}
