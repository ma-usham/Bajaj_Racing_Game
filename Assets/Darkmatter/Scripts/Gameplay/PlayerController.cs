using UnityEngine;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputReaderSO inputReader;

    [Header("Speed")]
    [Tooltip("Top speed in units per second.")]
    [SerializeField] private float maxSpeed = 40f;

    [Tooltip("How quickly the bike builds up to top speed, in units per second squared.")]
    [SerializeField] private float acceleration = 6f;

    [Tooltip("How quickly the brake sheds speed, in units per second squared.")]
    [SerializeField] private float brakeDeceleration = 25f;

    [Header("Steering")]
    [Tooltip("Sideways units per second at top speed. Steering is weaker the slower you go.")]
    [SerializeField] private float steerSpeed = 12f;

    [Tooltip("How quickly steering input eases in and out. Lower is floatier.")]
    [SerializeField] private float steerResponse = 4f;

    [Tooltip("Share of full steering kept at a standstill. 1 steers just as well stopped as at top speed.")]
    [Range(0f, 1f)]
    [SerializeField] private float steerAtRest = 0.4f;

    [Tooltip("How far either side of the starting line the bike may drift.")]
    [SerializeField] private float roadHalfWidth = 6f;

    [Tooltip("Bank angle in degrees at full lock.")]
    [SerializeField] private float maxLeanAngle = 22f;

    private Vector3 railOrigin;
    private Quaternion baseRotation;
    private Vector3 forwardAxis;
    private Vector3 rightAxis;

    private float currentSpeed;
    private float distanceTravelled;
    private float lateralOffset;
    private float steer;

    /// <summary>Current speed in units per second. Feed this to the HUD.</summary>
    public float CurrentSpeed => currentSpeed;

    /// <summary>Current speed as 0 to 1 of <see cref="maxSpeed"/>.</summary>
    public float SpeedNormalized => maxSpeed > 0f ? currentSpeed / maxSpeed : 0f;

    /// <summary>Distance covered along the rail, for lap and progress tracking.</summary>
    public float DistanceTravelled => distanceTravelled;

    private void Awake()
    {
        if (inputReader == null)
        {
            Debug.LogError($"{name}: no InputReaderSO assigned, the bike will not steer or brake.", this);
        }

        baseRotation = transform.rotation;
        railOrigin = transform.position;
        
        forwardAxis = Vector3.ProjectOnPlane(baseRotation * Vector3.forward, Vector3.up).normalized;
        rightAxis = Vector3.Cross(Vector3.up, forwardAxis);
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

        transform.SetPositionAndRotation(
            railOrigin + forwardAxis * distanceTravelled + rightAxis * lateralOffset,
            baseRotation * Quaternion.Euler(0f, 0f, -steer * maxLeanAngle));
    }

    /// <summary>Coasts up to top speed, or sheds speed while the brake is held.</summary>
    private void UpdateSpeed(float deltaTime)
    {
        bool braking = inputReader != null && inputReader.IsBraking;
        float targetSpeed = braking ? 0f : maxSpeed;
        float rate = braking ? brakeDeceleration : acceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
        distanceTravelled += currentSpeed * deltaTime;
    }

    /// <summary>
    /// Eases the raw axis into a smoothed steer value, which drives both the sideways slide
    /// and the lean, so the bank follows the turn without a second smoothing pass. Steering
    /// sharpens with speed but never drops to zero, or the bike would be unsteerable on the
    /// line while it is still building speed.
    /// </summary>
    private void UpdateSteering(float deltaTime)
    {
        float steerInput = inputReader != null ? Mathf.Clamp(inputReader.Steer, -1f, 1f) : 0f;
        steer = Mathf.MoveTowards(steer, steerInput, steerResponse * deltaTime);

        float steerAuthority = Mathf.Lerp(steerAtRest, 1f, SpeedNormalized);
        float slide = steer * steerSpeed * steerAuthority * deltaTime;
        lateralOffset = Mathf.Clamp(lateralOffset + slide, -roadHalfWidth, roadHalfWidth);
    }
}
