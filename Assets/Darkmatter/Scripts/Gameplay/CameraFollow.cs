using UnityEngine;

/// <summary>
/// Chase camera for the rail racer. It keeps the framing the camera was placed with in the
/// scene: distance down the road is tracked exactly, so the bike holds a constant screen
/// depth no matter how fast it is going, while sideways sway is eased. That easing is what
/// makes steering read as the bike moving across the screen before the camera recentres.
///
/// Rotation is deliberately never written. The bike banks by rolling about its local Z, and
/// matching that would roll the entire view.
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("Seconds for the camera to recentre behind the bike after a turn. 0 pins it dead centre.")]
    [SerializeField] private float lateralSmoothTime = 0.25f;

    private Vector3 forwardAxis;
    private Vector3 rightAxis;
    private float followDistance;
    private float lateralOffset;
    private float heightOffset;

    private float lateral;
    private float lateralVelocity;

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogError($"{name}: no follow target assigned, the camera will not move.", this);
            enabled = false;
            return;
        }

        // Road axes taken from how the camera is aimed in the scene, flattened so its slight
        // downward pitch does not make it climb as it tracks distance.
        forwardAxis = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        rightAxis = Vector3.Cross(Vector3.up, forwardAxis);

        // Whatever framing the scene was authored with becomes the framing to hold.
        Vector3 offset = transform.position - target.position;
        followDistance = Vector3.Dot(offset, forwardAxis);
        lateralOffset = Vector3.Dot(offset, rightAxis);
        heightOffset = offset.y;

        lateral = Vector3.Dot(transform.position, rightAxis);
    }

    /// <summary>
    /// LateUpdate, so the bike has already moved this frame. Following from Update trails it
    /// by a frame and shows up as jitter at speed.
    /// </summary>
    private void LateUpdate()
    {
        Vector3 targetPosition = target.position;

        // forwardAxis, rightAxis and up form an orthonormal basis, so the camera position can
        // be rebuilt from its three components: locked distance, eased sway, fixed height.
        float forward = Vector3.Dot(targetPosition, forwardAxis) + followDistance;
        float desiredLateral = Vector3.Dot(targetPosition, rightAxis) + lateralOffset;
        float height = targetPosition.y + heightOffset;

        lateral = Mathf.SmoothDamp(lateral, desiredLateral, ref lateralVelocity, lateralSmoothTime);

        transform.position = forwardAxis * forward + rightAxis * lateral + Vector3.up * height;
    }
}
