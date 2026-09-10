using Darkmatter.Core;
using UnityEngine;

namespace Darkmatter.Gameplay
{
    public enum SpeedPadKind
    {
        Boost,

        Slowdown,
    }

    [System.Serializable]
    public class SpeedPadPoint
    {
        [Tooltip("Where the pad goes, in world XZ. The prefab spawns exactly here, at the height " +
                 "the spawner floats pads at.")]
        public Vector2 position;

        [Tooltip("Which of the two prefabs spawns at this point.")]
        public SpeedPadKind kind = SpeedPadKind.Boost;
    }

    [System.Serializable]
    public class SpeedPadSettings
    {
        [Tooltip("Spawned at every point of this kind. How the pad looks is entirely the prefab's " +
                 "business: its size, its colour, its sorting and the way it is turned.")]
        public GameObject prefab;

        [Tooltip("What this pad does to top speed while it lasts. Above 1 is a boost, below 1 a " +
                 "slowdown. 1 is a pad that does nothing.")]
        [Min(0.05f)]
        public float speedMultiplier = 1.5f;

        [Tooltip("Seconds the change lasts. Driving over a second pad replaces this one outright " +
                 "rather than stacking with it, so a slowdown always cancels a boost.")]
        [Min(0f)]
        public float duration = 2.5f;

        [Tooltip("Played on pickup. Needs an AudioManager in the scene; without one the pad is silent.")]
        public AudioClip sound;
    }

    /// <summary>
    /// Puts a prefab at each authored point and hands the bike a change of pace when it drives
    /// over one.
    ///
    /// The points are taken at their word: nothing is scattered, snapped to the road or rolled
    /// for. Where a pad is and which kind it is are both decided in the inspector, so what is in
    /// the scene view is what turns up in the race.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedPadSpawner : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("The bike. Pads read its position to know when they have been driven over, and " +
                 "hand it the change of pace when they have.")]
        [SerializeField]
        private PlayerController player;

        [Header("Pads")]
        [Tooltip("Where the pads go. Select this object and shift click the road in the scene " +
                 "view to drop one, drag it to move it, ctrl click it to remove it.")]
        [SerializeField]
        private SpeedPadPoint[] points = new SpeedPadPoint[0];

        [Header("Float")]
        [Tooltip("How high above the road a pad rests, in world units. The points carry no " +
                 "height of their own, so this is the height for all of them.")]
        [SerializeField]
        private float padHeight = 2.5f;

        [Tooltip("How far the pad rises and falls either side of that height. 0 holds it still.")]
        [Min(0f)]
        [SerializeField]
        private float bobHeight = 0.35f;

        [Tooltip("Full rise-and-fall cycles per second.")]
        [Min(0f)]
        [SerializeField]
        private float bobSpeed = 0.6f;

        [Tooltip("Degrees per second the pad turns on the spot. 0 leaves it facing the way the " +
                 "prefab was built.")]
        [SerializeField]
        private float spinSpeed = 0f;

        [Header("Pickup")]
        [Tooltip("How near the bike has to pass for a pad to count as driven over, in world " +
                 "units. Measured against the point, not the bobbing prefab, so how high a pad " +
                 "happens to be never changes how easy it is to collect.")]
        [SerializeField]
        private float collectRadius = 2.5f;

        [Header("Boost")]
        [SerializeField]
        private SpeedPadSettings boost = new SpeedPadSettings
        {
            speedMultiplier = 1.5f,
            duration = 2.5f,
        };

        [Header("Slowdown")]
        [SerializeField]
        private SpeedPadSettings slowdown = new SpeedPadSettings
        {
            speedMultiplier = 0.55f,
            duration = 2f,
        };

        private Pad[] pads;
        private Transform container;
        private Vector2 lastBike;

        private void Start()
        {
            if (player == null)
            {
                Debug.LogError($"{name}: no PlayerController, so no pad can ever be collected.", this);
                enabled = false;
                return;
            }

            lastBike = Flat(player.transform.position);
            Build();
        }

        private void OnDestroy()
        {
            if (container != null)
            {
                Destroy(container.gameObject);
            }
        }

        private void Update()
        {
            Vector2 bike = Flat(player.transform.position);
            float now = Time.time;

            foreach (Pad pad in pads)
            {
                if (!pad.live)
                {
                    continue;
                }

                Hover(pad, now);

                if (DistanceToSegment(pad.position, lastBike, bike) <= collectRadius)
                {
                    Collect(pad);
                }
            }

            lastBike = bike;
        }

        /// <summary>
        /// Puts every collected pad back out. Called when the bike goes back to the grid, so a
        /// second run round has the same pads on it as the first.
        /// </summary>
        public void ResetPads()
        {
            if (pads == null)
            {
                return;
            }

            foreach (Pad pad in pads)
            {
                pad.live = true;
                pad.root.gameObject.SetActive(true);
            }

            if (player != null)
            {
                lastBike = Flat(player.transform.position);
            }
        }

        private void Build()
        {
            container = new GameObject($"{name} Pads").transform;
            pads = new Pad[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                SpeedPadPoint point = points[i];
                GameObject prefab = Settings(point.kind).prefab;

                if (prefab == null)
                {
                    Debug.LogError($"{name}: point {i} is a {point.kind} but that kind has no " +
                                   "prefab, so nothing will spawn there.", this);
                }

                Transform root = prefab != null
                    ? Instantiate(prefab).transform
                    : new GameObject($"SpeedPad {i}").transform;

                root.name = $"SpeedPad {i} {point.kind}";
                root.SetParent(container, false);
                root.position = new Vector3(point.position.x, padHeight, point.position.y);

                pads[i] = new Pad
                {
                    root = root,
                    kind = point.kind,
                    position = point.position,
                    facing = root.rotation,
                    phase = Random.value,
                    live = true,
                };
            }
        }

        /// <summary>
        /// Rides the pad up and down, and turns it if asked. Only the pad moves: where it counts
        /// as being is the point it was authored at.
        /// </summary>
        private void Hover(Pad pad, float now)
        {
            if (bobHeight > 0f && bobSpeed > 0f)
            {
                float swing = Mathf.Sin((now * bobSpeed + pad.phase) * 2f * Mathf.PI) * bobHeight;
                pad.root.position = new Vector3(pad.position.x, padHeight + swing, pad.position.y);
            }

            if (spinSpeed != 0f)
            {
                pad.root.rotation = Quaternion.AngleAxis(now * spinSpeed, Vector3.up) * pad.facing;
            }
        }

        private void Collect(Pad pad)
        {
            SpeedPadSettings settings = Settings(pad.kind);
            player.ApplySpeedModifier(settings.speedMultiplier, settings.duration);

            if (settings.sound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(settings.sound);
            }

            pad.live = false;
            pad.root.gameObject.SetActive(false);
        }

        private SpeedPadSettings Settings(SpeedPadKind kind)
        {
            return kind == SpeedPadKind.Boost ? boost : slowdown;
        }

        /// <summary>
        /// Against the line the bike travelled this frame rather than where it ended up, or a
        /// pad can be driven straight through between two frames at speed.
        /// </summary>
        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 span = to - from;
            float lengthSqr = span.sqrMagnitude;
            float t = lengthSqr > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - from, span) / lengthSqr) : 0f;
            return Vector2.Distance(point, from + span * t);
        }

        private static Vector2 Flat(Vector3 position) => new Vector2(position.x, position.z);

        private class Pad
        {
            public Transform root;
            public SpeedPadKind kind;
            public Vector2 position;

            /// <summary>Where in the rise and fall this pad starts, so a row of them does not pulse as one.</summary>
            public float phase;

            /// <summary>The prefab's own rotation, which the spin turns away from rather than replacing.</summary>
            public Quaternion facing;

            public bool live;
        }
    }
}
