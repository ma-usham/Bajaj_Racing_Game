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

        [Tooltip("Degrees per second the pad turns on the spot. 0 leaves it facing the way the " +
                 "prefab was built.")]
        [SerializeField]
        private float spinSpeed = 0f;

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
        /// Rides the pad up and down, and turns it if asked. Only the pad moves: where it counts
        /// as being is the spot it was authored at.
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
                pad.root.rotation = Quaternion.AngleAxis(now * spinSpeed, Vector3.up);
            }
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
