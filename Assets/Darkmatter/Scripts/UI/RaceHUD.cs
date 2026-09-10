using System.Text;
using Darkmatter.Gameplay;
using TMPro;
using UnityEngine;

namespace Darkmatter.UI
{
    [DisallowMultipleComponent]
    public class RaceHUD : MonoBehaviour
    {
        [Header("Source")] [SerializeField] private PlayerController player;

        [Header("Speed")] [SerializeField] private TextMeshProUGUI speedLabel;

        [Tooltip("Reading shown at top speed. World units are arbitrary here, so the HUD maps the " +
                 "bike's 0 to 1 speed onto this instead of converting units per second. Retuning " +
                 "maxSpeed on the bike then leaves the headline number alone.")]
        [SerializeField]
        private float displayTopSpeed = 120f;

        [SerializeField] private string speedSuffix = "km/h";

        [Header("Lap time")] [SerializeField] private TextMeshProUGUI lapTimeLabel;

        [Header("Wrong way")]
        [Tooltip("Optional. Switched on while the bike is pointed back down the circuit, and off " +
                 "the rest of the time. Only while the lap is running, so the grid and the " +
                 "finish are left alone whichever way the bike ends up facing.")]
        [SerializeField]
        private TextMeshProUGUI wrongWayLabel;

        private float lapStartTime;
        private bool lapRunning;

        private int segmentHint = -1;
        private bool wrongWay;

        private int shownSpeed = -1;
        private readonly StringBuilder lapTimeBuilder = new StringBuilder(16);


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

            if (wrongWayLabel != null)
            {
                wrongWayLabel.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            UpdateSpeed();
            UpdateLapTime();
            ShowWrongWay(lapRunning && FacingBackwards());
        }

        public void BeginLap()
        {
            lapStartTime = Time.time;
            lapRunning = true;
        }

        public void StopLap()
        {
            lapRunning = false;
            ShowWrongWay(false);
        }

        private void UpdateSpeed()
        {
            if (player == null || speedLabel == null)
            {
                return;
            }


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
        /// The bike has no reverse, so riding the wrong way is a matter of where it is pointed
        /// rather than which way it is moving: past a right angle to the racing direction baked
        /// into the circuit and it is heading back the way it came. Held on the line does not
        /// count, whatever the bike is facing.
        /// </summary>
        private bool FacingBackwards()
        {
            if (player == null || player.Held)
            {
                return false;
            }

            TrackPathSO road = player.Track;
            if (road == null || !road.IsValid)
            {
                return false;
            }

            Vector3 position = player.transform.position;
            segmentHint = road.Sample(new Vector2(position.x, position.z), segmentHint,
                out _, out Vector2 along);

            Vector3 forward = player.Forward;
            return Vector2.Dot(new Vector2(forward.x, forward.z), along) < 0f;
        }

        private void ShowWrongWay(bool shown)
        {
            if (wrongWayLabel == null || shown == wrongWay)
            {
                return;
            }

            wrongWay = shown;
            wrongWayLabel.gameObject.SetActive(shown);
        }


        public string FormatSpeed(float normalized) => Reading(normalized) + speedSuffix;

        private int Reading(float normalized) =>
            Mathf.RoundToInt(Mathf.Max(normalized, 0f) * displayTopSpeed);


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
}
