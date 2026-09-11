using Darkmatter.Core;
using UnityEngine;

namespace Darkmatter.Gameplay
{
    /// <summary>
    /// A bike, ridden the way a bike is ridden: the bars roll it, the roll is what turns it, and
    /// how tight the corner comes out is settled by the speed it is carrying rather than by any
    /// turn rate of its own.
    /// </summary>
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

        [Header("Handling")]
        [Tooltip("Bank angle in degrees at full lock and top speed. A road bike runs out of tyre " +
                 "somewhere around 45; past that it stops reading as a bike and starts reading " +
                 "as a sprite lying on its side.")]
        [Min(1f)]
        [SerializeField]
        private float leanAngle = 30f;

        [Tooltip("Seconds to roll the whole way over from upright. The one knob that sets how " +
                 "the bike feels, because nothing turns until it is over: short and it flicks " +
                 "between corners like a supermoto, long and it has to be set up for a bend " +
                 "well before the bend.")]
        [Min(0.01f)]
        [SerializeField]
        private float rollTime = 0.18f;

        [Tooltip("The sideways pull the tyres hold at full lean, in units per second squared. " +
                 "This is what sets how tight a corner comes out: the radius the bike carves at " +
                 "full lean is its speed squared divided by this, so at 20 units per second a " +
                 "grip of 30 turns inside a circle about 27 units across. Raise it if the bike " +
                 "will not go round the painted corners; lower it to make it lazier at speed.")]
        [Min(1f)]
        [SerializeField]
        private float corneringGrip = 30f;

        [Tooltip("How far the bike's origin sits above the tyres' contact patch, in units. The " +
                 "lean is rolled about that patch rather than about the middle of the sprite, so " +
                 "the tyres hold the line through a corner and the bike falls in over them. " +
                 "Rolled about the middle instead, the wheels swing out from under the bike.")]
        [Min(0f)]
        [SerializeField]
        private float contactHeight = 0.9f;

        [Header("Speed pads")]
        [Tooltip("How quickly a pad's change of pace takes hold, in units per second squared. " +
                 "Deliberately sharper than the throttle and the brake, or driving over a pad " +
                 "reads as the bike accelerating normally rather than as a shove in the back.")]
        [SerializeField]
        private float padResponse = 30f;

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

        /// <summary>
        /// Below this the bike is not moving enough for a corner to mean anything. Only there to
        /// keep the turn rate's division honest on the frames either side of a standstill.
        /// </summary>
        private const float Crawling = 0.5f;

        /// <summary>
        /// Seconds for the reading of how hard the bike is pulling to settle. Short, or the
        /// camera hung off it judders every time the throttle is touched.
        /// </summary>
        private const float SurgeTime = 0.15f;

        private float heading;
        private float currentSpeed;
        private float lean;
        private float surge;
        private float lastSpeed;
        private float padMultiplier = 1f;
        private float padRemaining;
        private bool scraping;
        private float scrapeIncidence;
        private bool engineRunning;
        private readonly TrackBarrier barrier = new TrackBarrier();
        private Vector3 ride;
        private Vector3 startPosition;
        private float startHeading;
        private float topSpeed;

        public float SpeedNormalized => maxSpeed > 0f ? currentSpeed / maxSpeed : 0f;

        public float Heading => heading;

        public Vector3 Forward => Quaternion.Euler(0f, heading, 0f) * Vector3.forward;

        /// <summary>Lean as a share of full lean, signed. -1 is hard over to the left, 1 hard over to the right.</summary>
        public float LeanNormalized => Mathf.Clamp(lean / leanAngle, -1f, 1f);

        /// <summary>
        /// 1 while the bike is pulling as hard as it can, -1 while it is hauling up as hard as it
        /// can, 0 while it is holding a speed. What the camera wants in order to dip under braking.
        /// </summary>
        public float SurgeNormalized => surge;

        /// <summary>
        /// Where the bike is on the road, free of the sway the lean puts on the sprite. The camera
        /// wants this rather than the transform, or it rocks from side to side with every corner.
        /// </summary>
        public Vector3 Ride => ride;

        public TrackPathSO Track => track;

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

            heading = transform.rotation.eulerAngles.y;

            ride = transform.position;
            startPosition = ride;
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
            lastSpeed = 0f;
            surge = 0f;
            topSpeed = 0f;
            ClearPad();
        }


        public void Release()
        {
            Held = false;
        }


        public void ReturnToLine()
        {
            heading = startHeading;
            currentSpeed = 0f;
            lastSpeed = 0f;
            surge = 0f;
            lean = 0f;
            barrier.Forget();
            scraping = false;
            scrapeIncidence = 0f;
            ClearPad();

            ride = startPosition;
            ApplyPose();
        }

        /// <summary>A pad's temporary ceiling on top speed: above 1 to boost, below it to slow.</summary>
        public void ApplySpeedModifier(float multiplier, float duration)
        {
            if (multiplier <= 0f || duration <= 0f)
            {
                return;
            }

            padMultiplier = multiplier;
            padRemaining = duration;
        }

        private void ClearPad()
        {
            padMultiplier = 1f;
            padRemaining = 0f;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (Held)
            {
                currentSpeed = 0f;
                lean = Mathf.MoveTowards(lean, 0f, RollRate * deltaTime);
                UpdateSurge(deltaTime);
            }
            else
            {
                UpdateSpeed(deltaTime);
                UpdateSteering(deltaTime);
                ride = KeepOnRoad(ride + Forward * (currentSpeed * deltaTime), deltaTime);
            }

            ApplyPose();
            SpinWheels();
            UpdateEngine();
        }

        private void UpdateSpeed(float deltaTime)
        {
            if (padRemaining > 0f)
            {
                padRemaining = Mathf.Max(padRemaining - deltaTime, 0f);
                if (padRemaining == 0f)
                {
                    padMultiplier = 1f;
                }
            }

            bool braking = inputReader != null && inputReader.IsBraking;

            float targetSpeed = braking
                ? Mathf.Min(currentSpeed, brakeFloor * maxSpeed)
                : maxSpeed * padMultiplier;
            float rate = braking ? brakeDeceleration : acceleration;
            if (padRemaining > 0f && !braking)
            {
                rate = Mathf.Max(rate, padResponse);
            }

            if (scraping)
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

            if (currentSpeed > topSpeed)
            {
                topSpeed = currentSpeed;
            }

            UpdateSurge(deltaTime);
        }

        /// <summary>
        /// The bars do not turn the bike, they roll it, and the roll is what turns it. Two things
        /// fall out of that on their own, and they are the whole of why a bike is not a car. It
        /// cannot lean without speed to lean against, so it stands up and steers on the bars at a
        /// crawl. And the pull a lean is worth has more speed to bend the faster it is going, so
        /// the same lean runs a wider corner the harder the bike is pressed.
        /// </summary>
        private void UpdateSteering(float deltaTime)
        {
            float steer = inputReader != null ? Mathf.Clamp(inputReader.Steer, -1f, 1f) : 0f;
            lean = Mathf.MoveTowards(lean, steer * leanAngle * Mathf.Clamp01(SpeedNormalized),
                RollRate * deltaTime);

            // Speed times turn rate is the sideways pull a corner asks of the tyres, so the turn
            // rate a lean is good for is that pull divided by the speed. Carry too much into a
            // bend and the bike runs wide however hard the rider leans on it.
            float bite = Mathf.Tan(lean * Mathf.Deg2Rad) / Mathf.Tan(leanAngle * Mathf.Deg2Rad);
            heading += corneringGrip * bite / Mathf.Max(currentSpeed, Crawling)
                       * Mathf.Rad2Deg * deltaTime;
        }

        /// <summary>Degrees per second the bike rolls, so a full lean takes the time asked for.</summary>
        private float RollRate => leanAngle / rollTime;

        private Vector3 KeepOnRoad(Vector3 position, float deltaTime)
        {
            BarrierHit hit = barrier.Hold(track, position, heading, Forward,
                barrierMargin, scrapeSteer, deltaTime);

            scraping = hit.Scraping;
            scrapeIncidence = hit.Incidence;
            heading = hit.Heading;
            return hit.Position;
        }

        /// <summary>
        /// Lays the bike over about the strip of rubber it is standing on rather than about the
        /// middle of the sprite, so the tyres hold the line through a corner and the bike falls
        /// in over them. Rolled about the middle instead the wheels swing out from under it, and
        /// it reads as a sprite being spun rather than as a bike being leaned.
        /// </summary>
        private void ApplyPose()
        {
            Quaternion roll = Quaternion.Euler(0f, 0f, -lean);
            Quaternion nose = Quaternion.Euler(0f, heading, 0f);

            transform.SetPositionAndRotation(
                ride - Vector3.up * contactHeight + nose * roll * Vector3.up * contactHeight,
                nose * roll);
        }

        /// <summary>Reads how hard the bike is pulling or hauling up, as a share of all it has.</summary>
        private void UpdateSurge(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            float change = (currentSpeed - lastSpeed) / deltaTime;
            lastSpeed = currentSpeed;

            float most = change >= 0f
                ? Mathf.Max(acceleration, 0.01f)
                : Mathf.Max(brakeDeceleration, 0.01f);
            surge = Mathf.MoveTowards(surge, Mathf.Clamp(change / most, -1f, 1f),
                deltaTime / SurgeTime);
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

        private void OnDrawGizmosSelected()
        {
            TrackBarrier.DrawGizmos(track, transform.position.y, barrierMargin);
        }
    }
}
