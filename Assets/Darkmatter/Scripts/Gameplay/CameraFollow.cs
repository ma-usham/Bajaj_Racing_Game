using UnityEngine;

/// <summary>
/// Chase camera. It orbits to sit directly behind the bike's heading, holding the distance
/// and height the camera was placed with in the scene, and opens the lens out as the bike
/// gains speed.
///
/// Only the yaw is smoothed, never the position. Easing the position instead would leave the
/// camera trailing by speed * smoothTime, so the bike would drift away as it accelerated and
/// loom closer under braking. Smoothing the angle alone keeps the distance exact and still
/// gives the camera its swing coming out of a turn.
///
/// <see cref="SnapToTarget"/> takes that swing back out for the frames the bike is placed
/// rather than ridden. The smoothing cannot tell a teleport from a corner, so the race says
/// when a teleport happened.
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private PlayerController target;

    [Tooltip("Seconds for the camera to swing back in line behind the bike after a turn.")]
    [SerializeField] private float yawSmoothTime = 0.25f;

    [Header("Speed")]
    [Tooltip("Degrees of field of view added at top speed, on top of whatever the camera was " +
             "authored with. Set to 0 to hold the lens still.")]
    [SerializeField] private float speedFieldOfViewGain = 12f;

    [Tooltip("Seconds for the lens to catch up to a change in speed. Deliberately slower than " +
             "the bike, so the widening reads as building speed rather than tracking the throttle.")]
    [SerializeField] private float fieldOfViewSmoothTime = 0.4f;

    private float pitch;
    private float yaw;
    private float yawVelocity;
    private float followDistance;
    private float height;

    private Camera view;
    private float baseFieldOfView;
    private float fieldOfViewVelocity;

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogError($"{name}: no follow target assigned, the camera will not move.", this);
            enabled = false;
            return;
        }

        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;

        // Whatever framing the scene was authored with becomes the framing to hold.
        Vector3 offset = transform.position - target.transform.position;
        height = offset.y;
        followDistance = new Vector2(offset.x, offset.z).magnitude;

        // The authored field of view is the resting one, so the speed effect is purely additive
        // and the scene still decides how tight the shot is at a standstill.
        view = GetComponent<Camera>();
        if (view != null)
        {
            baseFieldOfView = view.fieldOfView;
        }
    }

    /// <summary>
    /// LateUpdate, so the bike has already moved this frame. Following from Update trails it
    /// by a frame and shows up as jitter at speed.
    /// </summary>
    private void LateUpdate()
    {
        // Nothing to follow while the world is switched off. The bike's own Awake has not run
        // at that point, so its heading still reads zero, and smoothing towards a heading the
        // bike does not actually have walks the camera off its authored framing while the menu
        // is up. It is then that walk, not the start of the race, that the camera swings back
        // from when the world appears.
        if (!target.isActiveAndEnabled)
        {
            return;
        }

        yaw = Mathf.SmoothDampAngle(yaw, target.Heading, ref yawVelocity, yawSmoothTime);
        ApplyFraming();

        UpdateFieldOfView();
    }

    /// <summary>
    /// Drops the camera straight into place behind the bike, with no swing and the lens back at
    /// rest. For the frames the bike is put somewhere rather than driven there: the world coming
    /// up out of the menu, and the bike going back to the grid for another go. Smoothing across
    /// one of those is the camera orbiting the start line while the numbers count down.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        yaw = target.Heading;
        yawVelocity = 0f;
        ApplyFraming();

        // The lens is left wherever the last run's speed had opened it out to, and would
        // otherwise ease shut across the countdown of the next one.
        fieldOfViewVelocity = 0f;
        if (view != null && !view.orthographic)
        {
            view.fieldOfView = baseFieldOfView;
        }
    }

    /// <summary>
    /// Puts the camera behind the bike at the current yaw, holding the distance and height the
    /// scene was authored with.
    /// </summary>
    private void ApplyFraming()
    {
        Quaternion orbit = Quaternion.Euler(0f, yaw, 0f);
        transform.position = target.transform.position
                             + orbit * Vector3.back * followDistance
                             + Vector3.up * height;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Opens the lens out with speed. Widening the shot stretches the periphery past the camera
    /// faster than the bike is actually travelling, which is what sells the speed.
    ///
    /// The distance is held, so a wider lens also renders the bike smaller: on screen size goes
    /// as 1 / tan(fov / 2), which is about a quarter smaller across a 40 to 52 degree swing. If
    /// that reads as the bike shrinking rather than the world rushing, pull followDistance in by
    /// the same ratio as the lens opens.
    /// </summary>
    private void UpdateFieldOfView()
    {
        if (view == null || view.orthographic || speedFieldOfViewGain == 0f)
        {
            return;
        }

        float wanted = baseFieldOfView + speedFieldOfViewGain * Mathf.Clamp01(target.SpeedNormalized);
        view.fieldOfView = Mathf.SmoothDamp(view.fieldOfView, wanted,
                                            ref fieldOfViewVelocity, fieldOfViewSmoothTime);
    }
}
