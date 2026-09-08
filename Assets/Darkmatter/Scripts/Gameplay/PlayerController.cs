using UnityEngine;

/// <summary>
/// Drives the bike around the circuit. It accelerates on its own, the brake bleeds speed off,
/// and steering turns the heading so the bike can follow the track's left and right bends.
///
/// Steering picks a turn radius, not a turn rate, so the heading only swings as fast as the
/// speed has earned. A fixed degrees per second instead lets a crawling bike spin on the spot
/// and a flat out one carve a circle far tighter than its speed should allow.
///
/// The sprite is a flat billboard, so this transform's rotation is purely presentational and
/// is rebuilt every frame as (pitch, heading, lean roll). Travel uses <see cref="Forward"/>,
/// derived from heading alone, because the lean roll would otherwise skew transform.forward
/// and steer the bike into the ground.
/// </summary>
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

    [Tooltip("Cornering load, in units per second squared, that puts the bike at full lean. " +
             "Lower banks it harder for the same corner.")]
    [SerializeField] private float leanReferenceAccel = 10f;

    private float pitch;
    private float heading;
    private float currentSpeed;
    private float distanceTravelled;
    private float steer;
    private float lean;

    /// <summary>Current speed in units per second. Feed this to the HUD.</summary>
    public float CurrentSpeed => currentSpeed;

    /// <summary>Current speed as 0 to 1 of <see cref="maxSpeed"/>.</summary>
    public float SpeedNormalized => maxSpeed > 0f ? currentSpeed / maxSpeed : 0f;

    /// <summary>Distance covered, for lap and progress tracking.</summary>
    public float DistanceTravelled => distanceTravelled;

    /// <summary>Heading in degrees. The chase camera swings round to match this.</summary>
    public float Heading => heading;

    /// <summary>Bank angle in degrees, signed right positive. Handy for HUD tilt and tyre effects.</summary>
    public float Lean => lean;

    /// <summary>Direction of travel, flat on the ground plane.</summary>
    public Vector3 Forward => Quaternion.Euler(0f, heading, 0f) * Vector3.forward;

    private void Awake()
    {
        if (inputReader == null)
        {
            Debug.LogError($"{name}: no InputReaderSO assigned, the bike will not steer or brake.", this);
        }

        // The scene placement supplies the starting heading and the pitch that keeps the
        // billboard square to the chase camera.
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

        transform.position += Forward * (currentSpeed * deltaTime);
        transform.rotation = Quaternion.Euler(pitch, heading, 0f)
                             * Quaternion.Euler(0f, 0f, -lean);
    }

    /// <summary>
    /// Coasts up to top speed, or sheds speed down to <see cref="brakeFloor"/> while the brake
    /// is held. The brake only ever takes speed away: once the bike is already below the floor
    /// it holds station rather than being pulled back up to it, so holding the brake can never
    /// read as throttle.
    /// </summary>
    private void UpdateSpeed(float deltaTime)
    {
        bool braking = inputReader != null && inputReader.IsBraking;
        float targetSpeed = braking ? Mathf.Min(currentSpeed, brakeFloor * maxSpeed) : maxSpeed;
        float rate = braking ? brakeDeceleration : acceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
        distanceTravelled += currentSpeed * deltaTime;
    }

    /// <summary>
    /// Eases the raw axis into a smoothed steer value, then spends speed to turn: at full lock
    /// the bike traces a circle of <see cref="minTurnRadius"/> whatever it is doing, so the yaw
    /// rate is speed over radius rather than a flat degrees per second. Half speed through a
    /// corner takes twice as long to come round, which is the whole point.
    ///
    /// The lean falls out of that same turn rather than the raw axis, so the bank always
    /// answers to the cornering load and the bike cannot heel over while it is barely rolling.
    /// </summary>
    private void UpdateSteering(float deltaTime)
    {
        float steerInput = inputReader != null ? Mathf.Clamp(inputReader.Steer, -1f, 1f) : 0f;
        steer = Mathf.MoveTowards(steer, steerInput, steerResponse * deltaTime);

        float radius = Mathf.Max(minTurnRadius, 0.01f);
        float yawRate = steer * currentSpeed / radius; // radians per second
        heading += yawRate * Mathf.Rad2Deg * deltaTime;

        // Lateral load through the corner: speed times yaw rate, or speed squared over radius.
        float lateralAccel = currentSpeed * yawRate;
        float leanFraction = leanReferenceAccel > 0f ? lateralAccel / leanReferenceAccel : 0f;
        lean = Mathf.Clamp(leanFraction, -1f, 1f) * maxLeanAngle;
    }
}
