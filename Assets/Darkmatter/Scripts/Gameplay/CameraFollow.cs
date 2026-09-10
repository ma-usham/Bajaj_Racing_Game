using UnityEngine;

namespace Darkmatter.Gameplay
{
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private PlayerController target;

        [Tooltip("Seconds for the camera to swing back in line behind the bike after a turn. A " +
                 "little lag is what puts the corner on the screen: the bike leans and turns " +
                 "inside the frame, instead of sitting dead ahead while the world spins round " +
                 "it. 0 welds the camera to the heading and the turn becomes invisible.")]
        [Min(0f)]
        [SerializeField]
        private float yawLag = 0.12f;

        [Tooltip("Seconds for the camera to catch the bike up. Tight enough to keep it framed, " +
                 "loose enough that a flick of the bars slides the bike across the frame and the " +
                 "camera drops back a length under acceleration. 0 pins it to the bike.")]
        [Min(0f)]
        [SerializeField]
        private float followLag = 0.06f;

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
        private Vector3 followVelocity;

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

            yaw = Mathf.SmoothDampAngle(yaw, target.Heading, ref yawVelocity, yawLag);
            Follow(false);

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
            followVelocity = Vector3.zero;
            Follow(true);


            fieldOfViewVelocity = 0f;
            if (view != null && !view.orthographic)
            {
                view.fieldOfView = baseFieldOfView;
            }
        }


        /// <summary>
        /// Immediate is for putting the camera on the grid, where there is nothing to catch up
        /// to yet. Everywhere else it chases, so the bike is not welded to the middle of the
        /// screen and the frame keeps some weight of its own.
        /// </summary>
        private void Follow(bool immediate)
        {
            Quaternion orbit = Quaternion.Euler(0f, yaw, 0f);
            Vector3 wanted = target.transform.position
                             + orbit * Vector3.back * followDistance
                             + Vector3.up * height;

            transform.position = immediate
                ? wanted
                : Vector3.SmoothDamp(transform.position, wanted, ref followVelocity, followLag);
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
}
