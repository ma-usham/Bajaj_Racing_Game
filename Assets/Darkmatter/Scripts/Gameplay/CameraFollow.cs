using UnityEngine;

namespace Darkmatter.Gameplay
{
    /// <summary>
    /// A chase camera for a bike that is really a sprite on a painted road. Depth has to be acted
    /// rather than modelled, so everything here is in service of one thing: making the frame move
    /// the way a frame moves when it is bolted to something travelling through a world. Where the
    /// camera sits is left to wherever it was placed in the scene, relative to the bike; this only
    /// leans on that rig.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private PlayerController target;

        [Header("Chase")]
        [Tooltip("Seconds for the camera to swing back in line behind the bike after a turn. A " +
                 "little lag is what puts the corner on the screen: the bike leans and turns " +
                 "inside the frame, instead of sitting dead ahead while the world spins round " +
                 "it. 0 welds the camera to the heading and the turn becomes invisible.")]
        [Min(0f)]
        [SerializeField]
        private float headingLag = 0.14f;

        [Tooltip("Seconds for the camera to catch the bike up. Tight enough to keep it framed, " +
                 "loose enough that a flick of the bars slides the bike across the frame and the " +
                 "camera drops back a length under acceleration. 0 pins it to the bike.")]
        [Min(0f)]
        [SerializeField]
        private float followLag = 0.06f;

        [Header("Corner")]
        [Tooltip("Degrees the horizon tilts at full lean, the way a rider's head tilts with the " +
                 "bike. This is the strongest thing here: a flat sprite world stops reading as " +
                 "flat the moment the horizon is not level. Small, though. Past about 10 the " +
                 "bike's own lean stops reading, because the frame has leaned with it.")]
        [Min(0f)]
        [SerializeField]
        private float bankAtFullLean = 6f;

        [Tooltip("Degrees the camera aims into the bend at full lean, on top of the heading it " +
                 "is already following. The rider looks through the corner rather than at the " +
                 "bars, and the road ahead comes into frame before the bike gets there.")]
        [SerializeField]
        private float lookIntoCorner = 7f;

        [Tooltip("Units the camera hangs to the outside of the bend at full lean. Parallax is " +
                 "the only real depth cue a painted road has, and swinging wide is what gives " +
                 "the near ground something to slide against.")]
        [SerializeField]
        private float swingWide = 0.6f;

        [Tooltip("Seconds for the tilt, the aim and the swing to follow the bike's lean. Wants " +
                 "to be a shade slower than the bike rolls, so the frame answers the bike " +
                 "rather than moving with it.")]
        [Min(0.01f)]
        [SerializeField]
        private float cornerTime = 0.2f;

        [Header("Speed")]
        [Tooltip("Degrees of field of view added at top speed, on top of whatever the camera was " +
                 "authored with. Set to 0 to hold the lens still.")]
        [SerializeField]
        private float speedFieldOfViewGain = 12f;

        [Tooltip("Units the camera drops back at top speed. Together with the widening lens this " +
                 "is the dolly zoom every racing game runs on: the bike holds its size in frame " +
                 "while everything behind it stretches away.")]
        [SerializeField]
        private float speedPullBack = 1.6f;

        [Tooltip("Units the camera sinks at top speed. The lower the lens, the faster the near " +
                 "ground sweeps under it, and the near ground is the only thing in this scene " +
                 "that can report speed at all.")]
        [SerializeField]
        private float speedDrop = 0.5f;

        [Tooltip("Seconds for the lens and the rig to catch up to a change in speed. " +
                 "Deliberately slower than the bike, so the widening reads as building speed " +
                 "rather than tracking the throttle.")]
        [Min(0.01f)]
        [SerializeField]
        private float speedSmoothTime = 0.35f;

        [Header("Load")]
        [Tooltip("Degrees the camera dips towards the road under full braking, and lifts towards " +
                 "the horizon under full power. Stands in for the suspension the bike has not " +
                 "got. A couple of degrees is plenty; much more and it reads as seasickness.")]
        [SerializeField]
        private float diveAngle = 2.5f;

        [Tooltip("Seconds for the dip to follow the throttle and the brake.")]
        [Min(0.01f)]
        [SerializeField]
        private float diveTime = 0.25f;

        private float pitch;
        private float yaw;
        private float yawVelocity;
        private float followDistance;
        private float height;
        private Vector3 followVelocity;

        private float corner;
        private float cornerVelocity;
        private float speed;
        private float speedVelocity;
        private float dive;
        private float diveVelocity;

        private Camera view;
        private float baseFieldOfView;

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

            float deltaTime = Time.deltaTime;

            yaw = Mathf.SmoothDampAngle(yaw, target.Heading, ref yawVelocity, headingLag);
            corner = Mathf.SmoothDamp(corner, target.LeanNormalized, ref cornerVelocity,
                cornerTime, Mathf.Infinity, deltaTime);
            speed = Mathf.SmoothDamp(speed, Mathf.Clamp01(target.SpeedNormalized),
                ref speedVelocity, speedSmoothTime, Mathf.Infinity, deltaTime);
            dive = Mathf.SmoothDamp(dive, target.SurgeNormalized, ref diveVelocity, diveTime,
                Mathf.Infinity, deltaTime);

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

            corner = target.LeanNormalized;
            cornerVelocity = 0f;
            speed = Mathf.Clamp01(target.SpeedNormalized);
            speedVelocity = 0f;
            dive = 0f;
            diveVelocity = 0f;

            Follow(true);


            if (view != null && !view.orthographic)
            {
                view.fieldOfView = baseFieldOfView + speedFieldOfViewGain * speed;
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

            // Follows where the tyres are rather than the transform, or the lean's sideways sway
            // would be handed straight back to the camera and cancel itself out on screen.
            Vector3 wanted = target.Ride
                             + orbit * Vector3.back * (followDistance + speedPullBack * speed)
                             + orbit * Vector3.right * (-swingWide * corner)
                             + Vector3.up * (height - speedDrop * speed);

            transform.position = immediate
                ? wanted
                : Vector3.SmoothDamp(transform.position, wanted, ref followVelocity, followLag);

            // The roll is negative against the lean because rolling the camera one way turns the
            // picture the other: a right-hand bend has to lift the right of the horizon, the way
            // it lifts for a rider who has tipped their head into the corner with the bike.
            transform.rotation = Quaternion.Euler(
                pitch - diveAngle * dive,
                yaw + lookIntoCorner * corner,
                -bankAtFullLean * corner);
        }


        private void UpdateFieldOfView()
        {
            if (view == null || view.orthographic || speedFieldOfViewGain == 0f)
            {
                return;
            }

            view.fieldOfView = baseFieldOfView + speedFieldOfViewGain * speed;
        }
    }
}
