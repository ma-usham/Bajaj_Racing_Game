using UnityEngine;


[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private PlayerController target;

    [Tooltip("Seconds for the camera to swing back in line behind the bike after a turn.")] [SerializeField]
    private float yawSmoothTime = 0.25f;

    [Header("Speed")]
    [Tooltip("Degrees of field of view added at top speed, on top of whatever the camera was " +
             "authored with. Set to 0 to hold the lens still.")]
    [SerializeField]
    private float speedFieldOfViewGain = 12f;

    [Tooltip("Seconds for the lens to catch up to a change in speed. Deliberately slower than " +
             "the bike, so the widening reads as building speed rather than tracking the throttle.")]
    [SerializeField]
    private float fieldOfViewSmoothTime = 0.4f;

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


        Vector3 offset = transform.position - target.transform.position;
        height = offset.y;
        followDistance = new Vector2(offset.x, offset.z).magnitude;


        view = GetComponent<Camera>();
        if (view != null)
        {
            baseFieldOfView = view.fieldOfView;
        }
    }


    private void LateUpdate()
    {
        if (!target.isActiveAndEnabled)
        {
            return;
        }

        yaw = Mathf.SmoothDampAngle(yaw, target.Heading, ref yawVelocity, yawSmoothTime);
        ApplyFraming();

        UpdateFieldOfView();
    }


    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        yaw = target.Heading;
        yawVelocity = 0f;
        ApplyFraming();


        fieldOfViewVelocity = 0f;
        if (view != null && !view.orthographic)
        {
            view.fieldOfView = baseFieldOfView;
        }
    }


    private void ApplyFraming()
    {
        Quaternion orbit = Quaternion.Euler(0f, yaw, 0f);
        transform.position = target.transform.position
                             + orbit * Vector3.back * followDistance
                             + Vector3.up * height;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }


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