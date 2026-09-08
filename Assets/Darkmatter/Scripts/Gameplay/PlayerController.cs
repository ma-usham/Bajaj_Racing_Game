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

    private float pitch;
    private float heading;
    private float currentSpeed;
    private float distanceTravelled;
    private float steer;
    private float lean;

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
    
    private void UpdateSpeed(float deltaTime)
    {
        bool braking = inputReader != null && inputReader.IsBraking;
        float targetSpeed = braking ? Mathf.Min(currentSpeed, brakeFloor * maxSpeed) : maxSpeed;
        float rate = braking ? brakeDeceleration : acceleration;

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
}
