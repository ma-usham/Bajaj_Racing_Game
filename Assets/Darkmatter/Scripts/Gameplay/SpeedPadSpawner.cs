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
    public class SpeedPadZone
    {
        [Tooltip("The two spots in this zone, in world XZ. One gets the boost and the other the " +
                 "slowdown, so a zone is always one of each and never two the same. Which spot " +
                 "gets which is rolled fresh every race.")]
        public Vector2 first;

        public Vector2 second;
    }

    [System.Serializable]
    public class SpeedPadSettings
    {
        [Tooltip("Spawned at every spot dealt this kind. How the pad looks is entirely the " +
                 "prefab's business: its size, its colour, its sorting and the way it is turned.")]
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
    /// Puts a pair of pads in each authored zone and hands the bike a change of pace when it
    /// drives over one.
    ///
    /// The spots are taken at their word: nothing is scattered or snapped to the road. What is
    /// rolled is only which of a zone's two spots is the boost, and both prefabs are built at
    /// both spots up front so re-dealing a race costs nothing but two SetActive calls.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedPadSpawner : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("The bike. Pads read its position to know when they have been driven over, and " +
                 "hand it the change of pace when they have.")]
        [SerializeField]
        private PlayerController player;

        [Header("Zones")]
        [Tooltip("Each zone is two spots that come up as one boost and one slowdown. Select this " +
                 "object and shift click the road to drop a zone, drag either dot to move it, " +
                 "ctrl click to remove the zone.")]
        [SerializeField]
        private SpeedPadZone[] zones = new SpeedPadZone[0];

        [Header("Float")]
        [Tooltip("How high above the road a pad rests, in world units. The spots carry no height " +
                 "of their own, so this is the height for all of them.")]
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

        [Tooltip("Degrees per second the pad turns on the spot. 0 leaves it still.\n\n" +
                 "With Face Camera on it spins about the line to the lens, so it turns like a " +
                 "coin held up to the rider and stays readable the whole way round. With it off " +
                 "it spins about the world's up, and shows its edge twice a turn.")]
        [SerializeField]
        private float spinSpeed = 0f;

        [Header("Facing")]
        [Tooltip("The camera the pads turn to. Left empty, the main camera is used.")]
        [SerializeField]
        private Camera view;

        [Tooltip("Turns every pad to face the camera. Without it a pad keeps the one direction " +
                 "its prefab was built facing, and thins away to an edge as the bike comes at it " +
                 "from anywhere else on the circuit.\n\n" +
                 "Aimed at where the camera is, not lined up with the way it looks, so a pad off " +
                 "at the side of the screen is square to the rider rather than square to the road.")]
        [SerializeField]
        private bool faceCamera = true;

        [Header("Pickup")]
        [Tooltip("How near the bike has to pass for a pad to count as driven over, in world " +
                 "units. Measured against the spot, not the bobbing prefab, so how high a pad " +
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

        /// <summary>
        /// Square of how near the camera may stand over a pad before there is no longer a
        /// direction to turn it. Left as it was rather than snapped to nothing.
        /// </summary>
        private const float Overhead = 1e-4f;

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

            if (faceCamera && view == null)
            {
                view = Camera.main;

                if (view == null)
                {
                    Debug.LogWarning($"{name}: no camera to turn the pads to, and no main camera " +
                                     "to fall back on. They will keep the one direction their " +
                                     "prefabs were built facing.", this);
                    faceCamera = false;
                }
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
        /// Puts every collected pad back out and deals the zones again, so a retry runs the same
        /// circuit with a fresh mix rather than a stripped one.
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

            Deal();

            if (player != null)
            {
                lastBike = Flat(player.transform.position);
            }
        }

        private void Build()
        {
            container = new GameObject($"{name} Pads").transform;
            pads = new Pad[zones.Length * 2];

            for (int i = 0; i < zones.Length; i++)
            {
                pads[i * 2] = BuildPad(zones[i].first, i, 0);
                pads[i * 2 + 1] = BuildPad(zones[i].second, i, 1);
            }

            Deal();
        }

        private Pad BuildPad(Vector2 position, int zone, int slot)
        {
            Transform root = new GameObject($"SpeedPad {zone}.{slot}").transform;
            root.SetParent(container, false);
            root.position = new Vector3(position.x, padHeight, position.y);

            return new Pad
            {
                root = root,
                position = position,
                phase = Random.value,
                boostVisual = BuildVisual(boost, root, "Boost"),
                slowVisual = BuildVisual(slowdown, root, "Slowdown"),
                live = true,
            };
        }

        /// <summary>
        /// Both kinds are built at every spot and only one is ever shown. Two spare objects per
        /// zone buys a re-deal that costs nothing, which a lap the rider retries wants.
        /// </summary>
        private GameObject BuildVisual(SpeedPadSettings settings, Transform parent, string label)
        {
            if (settings.prefab == null)
            {
                Debug.LogError($"{name}: the {label} pad has no prefab, so half of every zone " +
                               "will be invisible.", this);
                return null;
            }

            GameObject built = Instantiate(settings.prefab, parent, false);
            built.name = label;
            built.transform.localPosition = Vector3.zero;

            // The prefab was built facing one way, and that way is about to be decided afresh
            // every frame. Left in, it would be added on top of the facing and turn every pad
            // edge on. Only its yaw goes; any tilt it was drawn with is its own business.
            if (faceCamera)
            {
                Vector3 angles = built.transform.localEulerAngles;
                built.transform.localEulerAngles = new Vector3(angles.x, 0f, angles.z);
            }

            return built;
        }

        /// <summary>
        /// Hands each zone one boost and one slowdown. Dealing the pair together rather than
        /// rolling each spot on its own is what makes two of the same kind in one zone
        /// impossible, rather than merely unlikely.
        /// </summary>
        private void Deal()
        {
            for (int i = 0; i < zones.Length; i++)
            {
                bool firstIsBoost = Random.value < 0.5f;
                Wear(pads[i * 2], firstIsBoost ? SpeedPadKind.Boost : SpeedPadKind.Slowdown);
                Wear(pads[i * 2 + 1], firstIsBoost ? SpeedPadKind.Slowdown : SpeedPadKind.Boost);
            }
        }

        private void Wear(Pad pad, SpeedPadKind kind)
        {
            pad.kind = kind;

            if (pad.boostVisual != null)
            {
                pad.boostVisual.SetActive(kind == SpeedPadKind.Boost);
            }

            if (pad.slowVisual != null)
            {
                pad.slowVisual.SetActive(kind == SpeedPadKind.Slowdown);
            }
        }

        /// <summary>
        /// Rides the pad up and down, and turns it to the rider. Only the pad moves: where it
        /// counts as being is the spot it was authored at, so how it is turned never changes how
        /// easy it is to collect.
        /// </summary>
        private void Hover(Pad pad, float now)
        {
            if (bobHeight > 0f && bobSpeed > 0f)
            {
                float swing = Mathf.Sin((now * bobSpeed + pad.phase) * 2f * Mathf.PI) * bobHeight;
                pad.root.position = new Vector3(pad.position.x, padHeight + swing, pad.position.y);
            }

            if (!faceCamera)
            {
                if (spinSpeed != 0f)
                {
                    pad.root.rotation = Quaternion.AngleAxis(now * spinSpeed, Vector3.up);
                }

                return;
            }

            // Away from the camera rather than towards it, because a sprite's face is its own
            // local back. Aimed at where the camera stands and not lined up with the way it
            // looks, or a pad off at the side of the screen shows its edge.
            Vector3 away = pad.root.position - view.transform.position;
            if (away.x * away.x + away.z * away.z < Overhead)
            {
                return;
            }

            Quaternion facing = Quaternion.Euler(0f, Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg, 0f);

            // Spun about the line to the camera rather than about the world's up, so it turns on
            // the spot in front of the rider instead of turning its edge to them twice a lap.
            pad.root.rotation = spinSpeed != 0f
                ? facing * Quaternion.AngleAxis(now * spinSpeed, Vector3.forward)
                : facing;
        }

        private void Collect(Pad pad)
        {
            SpeedPadSettings settings = pad.kind == SpeedPadKind.Boost ? boost : slowdown;
            player.ApplySpeedModifier(settings.speedMultiplier, settings.duration);

            if (settings.sound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(settings.sound);
            }

            pad.live = false;
            pad.root.gameObject.SetActive(false);
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
            public GameObject boostVisual;
            public GameObject slowVisual;
            public SpeedPadKind kind;
            public Vector2 position;

            /// <summary>Where in the rise and fall this pad starts, so a row of them does not pulse as one.</summary>
            public float phase;

            public bool live;
        }
    }
}
