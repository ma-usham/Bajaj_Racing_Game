using UnityEngine;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputReaderSO inputReader;

    [Header("Speed")]
    [Tooltip("Top speed in units per second. The circuit is only 76.8 units across, so this wants to stay low.")]
    [SerializeField] private float maxSpeed = 9f;

    [Tooltip("How quickly the bike builds up to top speed, in units per second squared.")]
    [SerializeField] private float acceleration = 6f;

    [Tooltip("How quickly the brake sheds speed, in units per second squared.")]
    [SerializeField] private float brakeDeceleration = 25f;

    [Tooltip("Share of top speed the brake bleeds down to instead of stopping the bike dead. " +
             "0.35 leaves enough speed to still steer through a corner; 0 brings it to a halt.")]
    [Range(0f, 1f)]
    [SerializeField] private float brakeFloor = 0.35f;

    [Header("Steering")]
    [Tooltip("Tightest circle the bike can carve, in units, at full lock. Smaller turns harder. " +
             "A hairpin on a 76.8 unit circuit wants roughly 6 to 10.")]
    [SerializeField] private float minTurnRadius = 8f;

    [Tooltip("How quickly steering input eases in and out. Lower is floatier.")]
    [SerializeField] private float steerResponse = 5f;

    [Tooltip("Bank angle in degrees at full lean.")]
    [SerializeField] private float maxLeanAngle = 20f;

    [Tooltip("How quickly the bike rolls into and out of a lean. Lower is heavier.")]
    [SerializeField] private float leanResponse = 6f;

    [Header("Barrier")]
    [Tooltip("Baked centreline of the circuit. The road is only pixels on the Track sprite, " +
             "so without this the bike has nothing to tell it where the asphalt ends.")]
    [SerializeField] private TrackPathSO track;

    [Tooltip("How far inside the edge of the asphalt the barrier sits, in units. Roughly half " +
             "the bike's width, so the sprite does not hang out over the kerb. The baked half " +
             "width is already the narrowest the road gets, so this does not want to be large.")]
    [SerializeField] private float barrierMargin = 0.7f;

    [Tooltip("Speed the barrier scrapes off per second when the bike hits it square on, in " +
             "units per second squared. A glancing hit costs proportionally less. Wants to " +
             "rise with maxSpeed, or a scrape stops reading as one.")]
    [SerializeField] private float scrapeDeceleration = 25f;

    [Tooltip("Share of top speed a scrape bleeds down to. The bike keeps moving along the " +
             "barrier rather than sticking to it.")]
    [Range(0f, 1f)]
    [SerializeField] private float scrapeSpeedFloor = 0.45f;

    [Tooltip("Degrees per second the barrier turns the bike back in line with itself, at a " +
             "square-on hit. 0 leaves the bike pointing into the wall while it slides along.")]
    [SerializeField] private float scrapeSteer = 150f;

    private float pitch;
    private float heading;
    private float currentSpeed;
    private float distanceTravelled;
    private float steer;
    private float lean;
    private int trackSegment = -1;
    private float scrapeIncidence;

    /// <summary>True on any frame the bike is being held against the edge of the road.</summary>
    public bool Scraping { get; private set; }

    public float CurrentSpeed => currentSpeed;
    public float SpeedNormalized => maxSpeed > 0f ? currentSpeed / maxSpeed : 0f;
    public float DistanceTravelled => distanceTravelled;
    public float Heading => heading;
    public float Lean => lean;
    public Vector3 Forward => Quaternion.Euler(0f, heading, 0f) * Vector3.forward;

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

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        UpdateSpeed(deltaTime);
        UpdateSteering(deltaTime);

        // The barrier can shave speed and turn the bike, so it runs on the move the
        // frame wanted to make, before that move and the rotation are committed.
        transform.position = KeepOnRoad(transform.position + Forward * (currentSpeed * deltaTime),
                                        deltaTime);
        transform.rotation = Quaternion.Euler(pitch, heading, 0f)
                             * Quaternion.Euler(0f, 0f, -lean);
    }

    /// <summary>
    /// Holds the bike inside the painted road.
    ///
    /// A position that has crossed the barrier is put back onto it, which keeps the part
    /// of the move that ran along the barrier and drops only the part that ran into it:
    /// the bike scrapes along the edge instead of stopping dead against it. How square on
    /// the hit was decides how much the barrier turns the bike, and is handed to
    /// <see cref="UpdateSpeed"/>, which owns what a scrape costs in speed.
    /// </summary>
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

        // Which way the barrier faces here. Taking it from the bike's own offset rather
        // than from the segment keeps it right around a corner, where the barrier curves
        // about the join between two segments instead of running straight.
        Vector2 outward = strayed > 1e-4f
            ? offset / strayed
            : new Vector2(-tangent.y, tangent.x);

        // The direction the barrier runs, taken the way the bike is already going: the
        // circuit shares a straight between its outbound and return legs, so the stored
        // tangent can point back down the road the bike is travelling.
        Vector2 travel = new Vector2(Forward.x, Forward.z);
        Vector2 along = new Vector2(-outward.y, outward.x);
        if (Vector2.Dot(travel, along) < 0f)
        {
            along = -along;
        }

        // 1 driving straight into the barrier, 0 running alongside it. UpdateSpeed reads
        // this on the next frame and holds the speed down for as long as contact lasts.
        scrapeIncidence = Mathf.Abs(Vector2.Dot(travel, outward));

        float barrierHeading = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
        heading = Mathf.MoveTowardsAngle(heading, barrierHeading,
                                         scrapeSteer * scrapeIncidence * deltaTime);

        Vector2 held = centre + outward * limit;
        return new Vector3(held.x, position.y, held.y);
    }

    private void UpdateSpeed(float deltaTime)
    {
        bool braking = inputReader != null && inputReader.IsBraking;
        float targetSpeed = braking ? Mathf.Min(currentSpeed, brakeFloor * maxSpeed) : maxSpeed;
        float rate = braking ? brakeDeceleration : acceleration;

        // A scrape is a ceiling on speed for as long as the bike is against the barrier,
        // not a one-off bite taken out of it: taking the speed off in the barrier code
        // alone left the throttle to put it back here the next frame, and acceleration
        // outruns the scrape at any angle short of head on, so the throttle simply won.
        // Contact sets the ceiling; how square on the contact is sets how fast the bike
        // falls to it, and running exactly alongside is a rate of zero, which holds the
        // speed where it is rather than letting it climb back while grinding.
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
    }
    
    private void UpdateSteering(float deltaTime)
    {
        float steerInput = inputReader != null ? Mathf.Clamp(inputReader.Steer, -1f, 1f) : 0f;
        steer = Mathf.MoveTowards(steer, steerInput, steerResponse * deltaTime);

        float radius = Mathf.Max(minTurnRadius, 0.01f);
        float yawRate = steer * currentSpeed / radius; // radians per second
        heading += yawRate * Mathf.Rad2Deg * deltaTime;
        
        float targetLean = steerInput * maxLeanAngle;
        lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-leanResponse * deltaTime));
    }

    /// <summary>
    /// Draws the baked centreline and the barrier either side of it, so the ribbon can be
    /// checked against the painted road without entering play mode. Select the bike to see it.
    /// </summary>
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
