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

        [Header("Impact")]
        [Tooltip("Degrees of field of view punched on over a boost pad. A pad worth half as much " +
                 "again is half a kick, so this is the figure for a pad that doubles the bike's " +
                 "speed.")]
        [SerializeField]
        private float boostKick = 16f;

        [Tooltip("Units the camera drops back over that same pad, so the bike pulls away from " +
                 "the lens before the lens hauls it in. Set to 0 to leave the rig alone and let " +
                 "the lens do all the work, which is the safer of the two if the boost ever " +
                 "reads as a jump rather than as a shove.")]
        [SerializeField]
        private float boostPullBack = 0.8f;

        [Tooltip("Seconds for a boost's kick to fade out. Wants to be well short of the pad's " +
                 "own duration: the shove is the moment it takes hold, not the whole of it.")]
        [Min(0.01f)]
        [SerializeField]
        private float boostFade = 0.6f;

        [Tooltip("Units the camera shakes when the bike goes square into the barrier at top " +
                 "speed. Anything less than square-on, or slower, is proportionally less, so " +
                 "running along the wall costs nothing and only a real hit registers.")]
        [SerializeField]
        private float impactShake = 0.3f;

        [Tooltip("Seconds for a hit to shake itself out.")]
        [Min(0.01f)]
        [SerializeField]
        private float impactFade = 0.45f;

        /// <summary>
        /// Seconds for a boost to punch on. A hit is a step and wants to arrive in one frame, but
        /// a boost is a shove in the back: straight to full in a single frame reads as the camera
        /// being cut to a new position rather than being pushed to one. Short enough to still
        /// land as a punch, long enough that the lens and the rig are seen to move there.
        /// </summary>
        private const float BoostAttack = 0.12f;

        /// <summary>Shakes per second. Fast enough to read as a knock rather than as a wobble.</summary>
        private const float ShakeFrequency = 24f;

        /// <summary>Degrees of roll the frame snaps through per unit of shake.</summary>
        private const float ShakeRoll = 9f;

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

        private float boostCharge;
        private float boost;
        private float boostVelocity;
        private float impact;
        private Vector3 rig;

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
            rig = transform.position;


            view = GetComponent<Camera>();
            if (view != null)
            {
                baseFieldOfView = view.fieldOfView;
            }
        }


        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            target.Boosted += OnBoosted;
            target.Scraped += OnScraped;
        }


        private void OnDisable()
        {
            if (target == null)
            {
                return;
            }

            target.Boosted -= OnBoosted;
            target.Scraped -= OnScraped;
        }


        /// <summary>
        /// Takes the stronger of the new shove and whatever is still fading, rather than adding
        /// them up. Two pads in a row are one kick held for longer, not a kick twice as big.
        /// </summary>
        private void OnBoosted(float strength)
        {
            boostCharge = Mathf.Max(boostCharge, Mathf.Clamp01(strength));
        }


        private void OnScraped(float strength)
        {
            impact = Mathf.Max(impact, Mathf.Clamp01(strength));
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

            // The charge is what the pad asked for and bleeds away on its own; the kick is what
            // the camera is actually doing, and it has to be eased into or the boost arrives as
            // a cut. A hit needs no such manners, which is why the shake is stepped straight in.
            boostCharge = Mathf.MoveTowards(boostCharge, 0f, deltaTime / boostFade);
            boost = Mathf.SmoothDamp(boost, boostCharge, ref boostVelocity, BoostAttack,
                Mathf.Infinity, deltaTime);
            impact = Mathf.MoveTowards(impact, 0f, deltaTime / impactFade);

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
            boostCharge = 0f;
            boost = 0f;
            boostVelocity = 0f;
            impact = 0f;

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
            // would be handed straight back to the camera and cancel itself out on screen. A
            // boost drops the lens back on top of whatever speed has already bought, so the bike
            // pulls away from it for a moment before it hauls the bike back in.
            Vector3 wanted = target.Ride
                             + orbit * Vector3.back * (followDistance + speedPullBack * speed
                                                                      + boostPullBack * boost)
                             + orbit * Vector3.right * (-swingWide * corner)
                             + Vector3.up * (height - speedDrop * speed);

            rig = immediate
                ? wanted
                : Vector3.SmoothDamp(rig, wanted, ref followVelocity, followLag);

            // Squared, so a glance off the barrier comes to nothing and a square-on hit is the
            // whole of it.
            float knock = impact * impact;
            float jitter = Time.time * ShakeFrequency;

            // The roll is negative against the lean because rolling the camera one way turns the
            // picture the other: a right-hand bend has to lift the right of the horizon, the way
            // it lifts for a rider who has tipped their head into the corner with the bike.
            transform.rotation = Quaternion.Euler(
                pitch - diveAngle * dive,
                yaw + lookIntoCorner * corner,
                -bankAtFullLean * corner + ShakeRoll * knock * Wobble(jitter, 7f));

            // The shake is hung off the settled rig rather than fed back into it. Shaken into the
            // position the follow smooths from, the camera would spend the next second swimming
            // the shake back out again.
            transform.position = rig + transform.rotation
                * new Vector3(Wobble(jitter, 0f), Wobble(jitter, 3f), 0f) * (impactShake * knock);
        }


        /// <summary>
        /// Perlin rather than Random, or the shake strobes from frame to frame instead of
        /// shaking. Runs -1 to 1; the seed picks a stream, so two axes do not move as one.
        /// </summary>
        private static float Wobble(float jitter, float seed)
        {
            return (Mathf.PerlinNoise(jitter, seed) - 0.5f) * 2f;
        }


        private void UpdateFieldOfView()
        {
            if (view == null || view.orthographic || (speedFieldOfViewGain == 0f && boostKick == 0f))
            {
                return;
            }

            view.fieldOfView = baseFieldOfView + speedFieldOfViewGain * speed + boostKick * boost;
        }
    }
}
