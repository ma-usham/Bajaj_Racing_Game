using Darkmatter.Core;
using UnityEngine;

namespace Darkmatter.Gameplay
{
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")] [SerializeField] private InputReaderSO inputReader;

        [Header("Speed")]
        [Tooltip("Top speed in units per second. The circuit is only 76.8 units across, so this wants to stay low.")]
        [SerializeField]
        private float maxSpeed = 9f;

        [Tooltip("How quickly the bike builds up to top speed, in units per second squared.")] [SerializeField]
        private float acceleration = 6f;

        [Tooltip("How quickly the brake sheds speed, in units per second squared.")] [SerializeField]
        private float brakeDeceleration = 25f;

        [Tooltip("Share of top speed the brake bleeds down to instead of stopping the bike dead. " +
                 "0.35 leaves enough speed to still steer through a corner; 0 brings it to a halt.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float brakeFloor = 0.35f;

        [Header("Speed pads")]
        [Tooltip("How quickly a pad's change of pace takes hold, in units per second squared. " +
                 "Deliberately sharper than the throttle and the brake, or driving over a pad " +
                 "reads as the bike accelerating normally rather than as a shove in the back.")]
        [SerializeField]
        private float padResponse = 30f;

        [Header("Steering")]
        [Tooltip("Degrees per second the bike turns at full lock when it is barely moving. A bike " +
                 "leans round a corner rather than being driven round it, so unlike a car it can " +
                 "still turn when it is crawling, and the rider is never left with dead bars.")]
        [Min(0f)]
        [SerializeField]
        private float turnRateStopped = 65f;

        [Tooltip("Degrees per second at full lock and top speed. Top speed divided by this in " +
                 "radians is the tightest circle the bike can carve, so 120 at 20 units per " +
                 "second turns inside a circle roughly 9.5 units across.")]
        [Min(0f)]
        [SerializeField]
        private float turnRateAtTopSpeed = 120f;

        [Tooltip("Seconds from straight to full lock, and back again. This is the whole of how " +
                 "immediate the bars feel: under about a tenth of a second the bike goes where " +
                 "it is pointed the moment the key is down, and much over it the steering swims.")]
        [Min(0.01f)]
        [SerializeField]
        private float steerTime = 0.08f;

        [Tooltip("Bank angle in degrees at full lock.")] [SerializeField]
        private float leanAngle = 30f;

        [Tooltip("Seconds to roll the whole way into a lean. Wants to stay near the steering " +
                 "time, or the bike banks after it has already turned and the two read as two " +
                 "separate things happening to the same sprite.")]
        [Min(0.01f)]
        [SerializeField]
        private float leanTime = 0.09f;

        [Header("Wheels")]
        [Tooltip("Optional. The wheel animation, taken from this object if left empty.")]
        [SerializeField]
        private Animator wheels;

        [Tooltip("How fast the wheel clip plays at the bike's own top speed. Slower speeds are a " +
                 "straight fraction of it, and a boost pad goes past it. Multiplies with the Speed " +
                 "on the animation state itself, so leave that at 1 and tune it here.")]
        [Min(0f)]
        [SerializeField]
        private float wheelPlaybackAtTopSpeed = 5f;

        [Header("Engine")]
        [Tooltip("Optional. Looping engine note. Played through the AudioManager's engine " +
                 "source, so the sfx volume governs it along with everything else.")]
        [SerializeField]
        private AudioClip engineLoop;

        [Tooltip("Pitch at a standstill, and at the bike's own top speed. A boost pad revs past " +
                 "the top one, the same way it drives the wheels past their top speed.")]
        [Min(0.05f)]
        [SerializeField]
        private float enginePitchIdle = 0.7f;

        [Min(0.05f)]
        [SerializeField]
        private float enginePitchAtTopSpeed = 2f;

        [Tooltip("Volume at a standstill, and at top speed. Unlike the pitch, a boost pad does " +
                 "not push this past the top one.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float engineVolumeIdle = 0.4f;

        [Range(0f, 1f)]
        [SerializeField]
        private float engineVolumeAtTopSpeed = 1f;

        [Header("Barrier")]
        [Tooltip("Baked centreline of the circuit. The road is only pixels on the Track sprite, " +
                 "so without this the bike has nothing to tell it where the asphalt ends.")]
        [SerializeField]
        private TrackPathSO track;

        [Tooltip("How far inside the edge of the asphalt the barrier sits, in units. Roughly half " +
                 "the bike's width, so the sprite does not hang out over the kerb. The baked half " +
                 "width is already the narrowest the road gets, so this does not want to be large.")]
        [SerializeField]
        private float barrierMargin = 0.7f;

        [Tooltip("Speed the barrier scrapes off per second when the bike hits it square on, in " +
                 "units per second squared. A glancing hit costs proportionally less. Wants to " +
                 "rise with maxSpeed, or a scrape stops reading as one.")]
        [SerializeField]
        private float scrapeDeceleration = 25f;

        [Tooltip("Share of top speed a scrape bleeds down to. The bike keeps moving along the " +
                 "barrier rather than sticking to it.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float scrapeSpeedFloor = 0.45f;

        [Tooltip("Degrees per second the barrier turns the bike back in line with itself, at a " +
                 "square-on hit. 0 leaves the bike pointing into the wall while it slides along.")]
        [SerializeField]
        private float scrapeSteer = 150f;

        private float pitch;
        private float heading;
        private float currentSpeed;
        private float distanceTravelled;
        private float steer;
        private float lean;
        private float scrapeIncidence;
        private bool engineRunning;
        private readonly TrackBarrier barrier = new TrackBarrier();
        private readonly SpeedModifier pad = new SpeedModifier();
        private Vector3 startPosition;
        private float startHeading;
        private float topSpeed;

        public bool Scraping { get; private set; }

        public float CurrentSpeed => currentSpeed;


        public float SpeedNormalized => maxSpeed > 0f ? currentSpeed / maxSpeed : 0f;
        public float DistanceTravelled => distanceTravelled;
        public float Heading => heading;
        public float Lean => lean;
        public Vector3 Forward => Quaternion.Euler(0f, heading, 0f) * Vector3.forward;


        public TrackPathSO Track => track;


        public float SpeedMultiplier => pad.Multiplier;


        public float PadTimeRemaining => pad.Remaining;

        public bool Boosting => pad.Running && pad.Multiplier > 1f;

        public bool Slowed => pad.Running && pad.Multiplier < 1f;


        public float TopSpeedNormalized => maxSpeed > 0f ? topSpeed / maxSpeed : 0f;


        public bool Held { get; private set; }

        private void Awake()
        {
            if (inputReader == null)
            {
                Debug.LogError($"{name}: no InputReaderSO assigned, the bike will not steer or brake.", this);
            }

            if (track == null || !track.IsValid)
            {
                Debug.LogWarning($"{name}: no baked TrackPathSO assigned, the bike will ride off the road.", this);
            }

            Vector3 angles = transform.rotation.eulerAngles;
            pitch = angles.x;
            heading = angles.y;


            startPosition = transform.position;
            startHeading = heading;

            if (wheels == null)
            {
                wheels = GetComponent<Animator>();
            }

            if (wheels != null)
            {
                wheels.speed = 0f;
            }
        }

        private void OnDisable()
        {
            StopEngine();
        }

        /// <summary>Starts the engine note. The race owns when, this owns how.</summary>
        public void StartEngine()
        {
            if (engineLoop == null || AudioManager.Instance == null)
            {
                return;
            }

            engineRunning = true;
            UpdateEngine();
            AudioManager.Instance.PlayEngine(engineLoop);
        }

        /// <summary>Cuts the engine dead. Wanted the moment a lap is in, and on the way out.</summary>
        public void StopEngine()
        {
            engineRunning = false;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopEngine();
            }
        }

        public void HoldOnLine()
        {
            Held = true;
            currentSpeed = 0f;
            steer = 0f;
            topSpeed = 0f;
            ClearSpeedModifier();
        }


        public void Release()
        {
            Held = false;
        }


        public void ReturnToLine()
        {
            heading = startHeading;
            currentSpeed = 0f;
            steer = 0f;
            lean = 0f;
            distanceTravelled = 0f;
            barrier.Forget();
            Scraping = false;
            scrapeIncidence = 0f;
            ClearSpeedModifier();

            transform.SetPositionAndRotation(startPosition, Quaternion.Euler(pitch, heading, 0f));
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (Held)
            {
                currentSpeed = 0f;
                lean = Mathf.MoveTowards(lean, 0f, LeanRate * deltaTime);
                transform.rotation = Quaternion.Euler(pitch, heading, 0f)
                                     * Quaternion.Euler(0f, 0f, -lean);
                SpinWheels();
                UpdateEngine();
                return;
            }

            UpdateSpeed(deltaTime);
            UpdateSteering(deltaTime);
            transform.position = KeepOnRoad(transform.position + Forward * (currentSpeed * deltaTime),
                deltaTime);
            transform.rotation = Quaternion.Euler(pitch, heading, 0f)
                                 * Quaternion.Euler(0f, 0f, -lean);
            SpinWheels();
            UpdateEngine();
        }

        private void SpinWheels()
        {
            if (wheels == null)
            {
                return;
            }

            wheels.speed = wheelPlaybackAtTopSpeed * SpeedNormalized;
        }

        /// <summary>
        /// Pitch is unclamped so a boost pad revs past the top note; volume is not, or a boost
        /// would clip.
        /// </summary>
        private void UpdateEngine()
        {
            if (!engineRunning || AudioManager.Instance == null)
            {
                return;
            }

            float revs = Mathf.Max(SpeedNormalized, 0f);
            AudioManager.Instance.SetEngine(
                Mathf.LerpUnclamped(enginePitchIdle, enginePitchAtTopSpeed, revs),
                Mathf.Lerp(engineVolumeIdle, engineVolumeAtTopSpeed, revs));
        }

        private Vector3 KeepOnRoad(Vector3 position, float deltaTime)
        {
            BarrierHit hit = barrier.Hold(track, position, heading, Forward,
                barrierMargin, scrapeSteer, deltaTime);

            Scraping = hit.Scraping;
            scrapeIncidence = hit.Incidence;
            heading = hit.Heading;
            return hit.Position;
        }


        public void ApplySpeedModifier(float multiplier, float duration)
        {
            pad.Apply(multiplier, duration);
        }


        public void ClearSpeedModifier()
        {
            pad.Clear();
        }

        private void UpdateSpeed(float deltaTime)
        {
            pad.Tick(deltaTime);

            bool padRunning = pad.Running;
            bool braking = inputReader != null && inputReader.IsBraking;


            float targetSpeed = braking
                ? Mathf.Min(currentSpeed, brakeFloor * maxSpeed)
                : maxSpeed * pad.Multiplier;
            float rate = braking ? brakeDeceleration : acceleration;
            if (padRunning && !braking)
            {
                rate = Mathf.Max(rate, padResponse);
            }

            if (Scraping)
            {
                float scraped = scrapeSpeedFloor * maxSpeed;
                if (scraped < targetSpeed)
                {
                    float scrapeRate = scrapeDeceleration * scrapeIncidence;
                    targetSpeed = scraped;
                    rate = braking ? Mathf.Max(brakeDeceleration, scrapeRate) : scrapeRate;
                }
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
            distanceTravelled += currentSpeed * deltaTime;

            if (currentSpeed > topSpeed)
            {
                topSpeed = currentSpeed;
            }
        }

        /// <summary>
        /// The bike turns at a rate of its own rather than being swung round by how fast it is
        /// going, which is what stops the steering going dead at the bottom of the throttle and
        /// vague at the top of it. Speed only widens the turn, and the lean is driven off the
        /// same eased steering as the turn so the bank and the corner are one movement.
        /// </summary>
        private void UpdateSteering(float deltaTime)
        {
            float steerInput = inputReader != null ? Mathf.Clamp(inputReader.Steer, -1f, 1f) : 0f;
            steer = Mathf.MoveTowards(steer, steerInput, deltaTime / steerTime);

            float turnRate = Mathf.Lerp(turnRateStopped, turnRateAtTopSpeed,
                Mathf.Clamp01(SpeedNormalized));
            heading += steer * turnRate * deltaTime;

            lean = Mathf.MoveTowards(lean, steer * leanAngle, LeanRate * deltaTime);
        }

        /// <summary>Degrees per second the bike rolls, so a full lean takes the time asked for.</summary>
        private float LeanRate => leanAngle / leanTime;

        private void OnDrawGizmosSelected()
        {
            TrackBarrier.DrawGizmos(track, transform.position.y, barrierMargin);
        }

        /// <summary>
        /// A temporary ceiling on top speed: above 1 for a boost pad, below it for a slow one.
        /// Driving over a second pad replaces the first outright rather than stacking with it,
        /// so a slowdown always cancels a boost, and two boosts in a row are one boost held for
        /// longer rather than a bike that keeps getting faster.
        /// </summary>
        private class SpeedModifier
        {
            public float Multiplier { get; private set; } = 1f;
            public float Remaining { get; private set; }

            public bool Running => Remaining > 0f;

            public void Apply(float multiplier, float duration)
            {
                if (multiplier <= 0f || duration <= 0f)
                {
                    return;
                }

                Multiplier = multiplier;
                Remaining = duration;
            }

            public void Clear()
            {
                Multiplier = 1f;
                Remaining = 0f;
            }

            public void Tick(float deltaTime)
            {
                if (Remaining <= 0f)
                {
                    return;
                }

                Remaining = Mathf.Max(Remaining - deltaTime, 0f);
                if (Remaining == 0f)
                {
                    Multiplier = 1f;
                }
            }
        }
    }
}
