using UnityEngine;

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
    [Tooltip("Tightest circle the bike can carve, in units, at full lock. Smaller turns harder. " +
             "A hairpin on a 76.8 unit circuit wants roughly 6 to 10.")]
    [SerializeField]
    private float minTurnRadius = 8f;

    [Tooltip("How quickly steering input eases in and out. Lower is floatier.")] [SerializeField]
    private float steerResponse = 5f;

    [Tooltip("Bank angle in degrees at full lean.")] [SerializeField]
    private float maxLeanAngle = 20f;

    [Tooltip("How quickly the bike rolls into and out of a lean. Lower is heavier.")] [SerializeField]
    private float leanResponse = 6f;

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
    private int trackSegment = -1;
    private float scrapeIncidence;
    private float padMultiplier = 1f;
    private float padRemaining;
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


    public float SpeedMultiplier => padMultiplier;


    public float PadTimeRemaining => padRemaining;

    public bool Boosting => padRemaining > 0f && padMultiplier > 1f;

    public bool Slowed => padRemaining > 0f && padMultiplier < 1f;


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

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.Enable();
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.Disable();
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
        trackSegment = -1;
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
            lean = Mathf.Lerp(lean, 0f, 1f - Mathf.Exp(-leanResponse * deltaTime));
            transform.rotation = Quaternion.Euler(pitch, heading, 0f)
                                 * Quaternion.Euler(0f, 0f, -lean);
            SpinWheels();
            return;
        }

        UpdateSpeed(deltaTime);
        UpdateSteering(deltaTime);
        transform.position = KeepOnRoad(transform.position + Forward * (currentSpeed * deltaTime),
            deltaTime);
        transform.rotation = Quaternion.Euler(pitch, heading, 0f)
                             * Quaternion.Euler(0f, 0f, -lean);
        SpinWheels();
    }
    
    private void SpinWheels()
    {
        if (wheels == null)
        {
            return;
        }

        wheels.speed = wheelPlaybackAtTopSpeed * SpeedNormalized;
    }

    private Vector3 KeepOnRoad(Vector3 position, float deltaTime)
    {
        Scraping = false;
        scrapeIncidence = 0f;

        if (track == null || !track.IsValid)
        {
            return position;
        }

        Vector2 flat = new Vector2(position.x, position.z);
        trackSegment = track.Sample(flat, trackSegment, out Vector2 centre, out Vector2 tangent);

        Vector2 offset = flat - centre;
        float strayed = offset.magnitude;
        float limit = Mathf.Max(track.HalfWidth - barrierMargin, 0.1f);
        if (strayed <= limit)
        {
            return position;
        }

        Scraping = true;

        Vector2 outward = strayed > 1e-4f
            ? offset / strayed
            : new Vector2(-tangent.y, tangent.x);

        Vector2 travel = new Vector2(Forward.x, Forward.z);
        Vector2 along = new Vector2(-outward.y, outward.x);
        if (Vector2.Dot(travel, along) < 0f)
        {
            along = -along;
        }

        scrapeIncidence = Mathf.Abs(Vector2.Dot(travel, outward));

        float barrierHeading = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
        heading = Mathf.MoveTowardsAngle(heading, barrierHeading,
            scrapeSteer * scrapeIncidence * deltaTime);

        Vector2 held = centre + outward * limit;
        return new Vector3(held.x, position.y, held.y);
    }


    public void ApplySpeedModifier(float multiplier, float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
        {
            return;
        }

        padMultiplier = multiplier;
        padRemaining = duration;
    }


    public void ClearSpeedModifier()
    {
        padMultiplier = 1f;
        padRemaining = 0f;
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

        bool padRunning = padRemaining > 0f;
        bool braking = inputReader != null && inputReader.IsBraking;


        float targetSpeed = braking
            ? Mathf.Min(currentSpeed, brakeFloor * maxSpeed)
            : maxSpeed * padMultiplier;
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

    private void UpdateSteering(float deltaTime)
    {
        float steerInput = inputReader != null ? Mathf.Clamp(inputReader.Steer, -1f, 1f) : 0f;
        steer = Mathf.MoveTowards(steer, steerInput, steerResponse * deltaTime);

        float radius = Mathf.Max(minTurnRadius, 0.01f);
        float yawRate = steer * currentSpeed / radius;
        heading += yawRate * Mathf.Rad2Deg * deltaTime;

        float targetLean = steerInput * maxLeanAngle;
        lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-leanResponse * deltaTime));
    }

    private void OnDrawGizmosSelected()
    {
        if (track == null || !track.IsValid)
        {
            return;
        }

        float height = transform.position.y;
        float limit = Mathf.Max(track.HalfWidth - barrierMargin, 0f);

        for (int i = 0; i < track.SegmentCount; i++)
        {
            Vector2 from = track.GetPoint(i);
            Vector2 to = track.GetPoint(i + 1);
            Vector2 normal = new Vector2(-(to.y - from.y), to.x - from.x).normalized;

            Vector3 a = new Vector3(from.x, height, from.y);
            Vector3 b = new Vector3(to.x, height, to.y);
            Vector3 offset = new Vector3(normal.x, 0f, normal.y) * limit;

            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            Gizmos.DrawLine(a, b);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(a + offset, b + offset);
            Gizmos.DrawLine(a - offset, b - offset);
        }
    }
}