using UnityEngine;


public enum SpeedPadKind
{
    Boost,


    Slowdown,
}


[System.Serializable]
public class SpeedPadZone
{
    [Tooltip("Centre of the zone in world XZ: the X and Z of a position in the scene. A pad " +
             "lies on the road, so there is no height to give.")]
    public Vector2 position;

    [Tooltip("How far from the centre a pad may land, in world units. The road is only 7 " +
             "units either side of its centreline, so a radius much past 10 starts covering " +
             "more than one stretch of it and pads can turn up somewhere unintended.")]
    [Min(0.5f)]
    public float radius = 8f;

    [Tooltip("How many pads this zone keeps on the road at once. Each one is rolled for boost " +
             "or slowdown on its own, so a zone of three can come up as any mix of the two.")]
    [Min(1)]
    public int count = 1;
}


[System.Serializable]
public class SpeedPadSettings
{
    [Header("Look")] [Tooltip("Painted flat on the road for this kind of pad. Ignored when a prefab is given.")]
    public Sprite sprite;

    [Tooltip("Tint laid over the sprite. Worth using when both kinds share one arrow sprite " +
             "and only the colour tells them apart.")]
    public Color tint = Color.white;

    [Tooltip("How big the pad is drawn, in world units, whatever the sprite's own pixels per " +
             "unit. The road is 14 units across, so 5 by 5 is a pad the bike can miss.")]
    public Vector2 size = new Vector2(5f, 5f);

    [Tooltip("Optional. Used instead of the sprite, for a pad that wants particles or an " +
             "animation. It is placed, turned on and turned off by the spawner like any other pad.")]
    public GameObject prefab;

    [Header("Effect")]
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


[DisallowMultipleComponent]
public class SpeedPadSpawner : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("The bike. Pads read its position to know when they have been driven over, and " +
             "hand it the change of pace when they have.")]
    [SerializeField]
    private PlayerController player;

    [Tooltip("Baked centreline of the circuit, the same asset the bike holds. Left empty, the " +
             "bike's own is used, which is one less thing to keep in step.")]
    [SerializeField]
    private TrackPathSO track;

    [Header("Zones")]
    [Tooltip("Where pads are allowed to land. Fill in a position and a radius per zone, or " +
             "select this object and shift click the road in the scene view to drop one.")]
    [SerializeField]
    private SpeedPadZone[] zones = new SpeedPadZone[0];

    [Header("Placement")]
    [Tooltip("How far inside the edge of the road a pad is kept, in world units. Roughly half " +
             "the pad's own width, or a pad hangs over the kerb where the road bends.")]
    [SerializeField]
    private float roadMargin = 2f;

    [Tooltip("How far apart two pads from the same zone are kept, in world units. Only bites " +
             "on a zone holding more than one pad, and gives way rather than leaving a hole " +
             "if the zone is too small to honour it.")]
    [SerializeField]
    private float minSeparation = 6f;

    [Tooltip("How far above the road a pad sits, in world units. Just enough to clear the " +
             "Track sprite. An upright pad wants raising to about half its own height.")]
    [SerializeField]
    private float padHeight = 0.05f;

    [Tooltip("On paints the pad flat on the road pointing down the racing direction, which is " +
             "what an arrow or an oil slick wants. Off stands it upright facing the oncoming " +
             "bike, which is what a floating pickup wants.")]
    [SerializeField]
    private bool layFlat = true;

    [Tooltip("Sorting layer the pad is drawn on. Ground puts it in with the road; the bike is " +
             "on Player and so still draws over the top of it.")]
    [SerializeField]
    private string sortingLayer = "Ground";

    [Tooltip("Order within that layer. The Track sprite is on 1, so anything above that paints " +
             "on top of the road rather than under it.")]
    [SerializeField]
    private int sortingOrder = 2;

    [Header("Mix")]
    [Tooltip("Share of pads that come up as a boost. 1 is all boosts, 0 all slowdowns, and " +
             "every pad is rolled on its own, so a handful of pads can still come up all one way.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float boostShare = 0.5f;

    [Tooltip("Seed for the scatter. 0 leaves it to the clock and every run is laid out " +
             "differently; anything else gives the same layout every run, which is what " +
             "tuning a lap or chasing a bug wants.")]
    [SerializeField]
    private int seed;

    [Header("Pickup")]
    [Tooltip("How near the bike has to pass for a pad to count as driven over, in world units. " +
             "Wants to be about half the pad's width, or pads read as collected off to one side.")]
    [SerializeField]
    private float collectRadius = 2.5f;

    [Tooltip("On puts a collected pad back out, in a new spot in its own zone and with a newly " +
             "rolled kind. Off is one pad per zone per race.")]
    [SerializeField]
    private bool respawn = true;

    [Tooltip("Seconds before a collected pad comes back. Wants to be long enough that the bike " +
             "is somewhere else on the circuit by then.")]
    [Min(0f)]
    [SerializeField]
    private float respawnDelay = 8f;

    [Header("Boost")] [SerializeField] private SpeedPadSettings boost = new SpeedPadSettings
    {
        tint = new Color(0.35f, 1f, 0.55f),
        speedMultiplier = 1.5f,
        duration = 2.5f,
    };

    [Header("Slowdown")] [SerializeField] private SpeedPadSettings slowdown = new SpeedPadSettings
    {
        tint = new Color(1f, 0.45f, 0.3f),
        speedMultiplier = 0.55f,
        duration = 2f,
    };


    private const int PlacementAttempts = 8;


    private const float RespawnClearance = 2.5f;


    private const float RetryDelay = 0.5f;

    private Pad[] pads;
    private Transform container;
    private System.Random random;
    private Vector2 lastBike;
    private bool hasSortingLayer;


    public TrackPathSO Road => track != null ? track : (player != null ? player.Track : null);


    public bool TryPlace(Vector2 wanted, out Vector2 placed, out Vector2 tangent)
    {
        placed = wanted;
        tangent = Vector2.right;

        TrackPathSO road = Road;
        if (road == null || !road.IsValid)
        {
            return false;
        }

        road.Sample(wanted, -1, out Vector2 centre, out tangent);

        Vector2 offset = wanted - centre;
        float strayed = offset.magnitude;
        float limit = Mathf.Max(road.HalfWidth - roadMargin, 0f);
        if (strayed > limit && strayed > 1e-4f)
        {
            placed = centre + offset * (limit / strayed);
        }

        return true;
    }

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError($"{name}: no PlayerController assigned, so no pad can be picked up.", this);
            enabled = false;
            return;
        }

        TrackPathSO road = Road;
        if (road == null || !road.IsValid)
        {
            Debug.LogError($"{name}: no baked TrackPathSO to lay pads on, here or on the bike.", this);
            enabled = false;
            return;
        }

        if (zones == null || zones.Length == 0)
        {
            Debug.LogWarning($"{name}: no zones, so no pads. Give it a position and a radius " +
                             "for each patch of road you want pads on.", this);
            enabled = false;
            return;
        }

        hasSortingLayer = HasSortingLayer(sortingLayer);
        if (!hasSortingLayer)
        {
            Debug.LogWarning($"{name}: there is no sorting layer called \"{sortingLayer}\", so " +
                             "pads are left on the default one and may end up under the road.", this);
        }

        random = seed == 0 ? new System.Random() : new System.Random(seed);
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
                if ((respawn || pad.waitingForSpot) && now >= pad.readyAt)
                {
                    Place(pad, bike, now);
                }

                continue;
            }


            if (DistanceToSegment(pad.position, lastBike, bike) <= collectRadius)
            {
                Collect(pad, now);
            }
        }

        lastBike = bike;
    }


    private void Build()
    {
        container = new GameObject($"{name} Pads").transform;

        int total = 0;
        foreach (SpeedPadZone zone in zones)
        {
            total += Mathf.Max(zone.count, 1);
        }

        pads = new Pad[total];
        Vector2 bike = lastBike;
        float now = Time.time;

        int index = 0;
        foreach (SpeedPadZone zone in zones)
        {
            for (int i = 0; i < Mathf.Max(zone.count, 1); i++)
            {
                Pad pad = new Pad
                {
                    zone = zone,
                    root = new GameObject($"SpeedPad {index}").transform,
                };

                pad.root.SetParent(container, false);


                pad.boostVisual = BuildVisual(boost, pad.root, "Boost");
                pad.slowVisual = BuildVisual(slowdown, pad.root, "Slowdown");


                pad.root.gameObject.SetActive(false);

                pads[index] = pad;
                Place(pad, bike, now);
                index++;
            }
        }
    }

    private GameObject BuildVisual(SpeedPadSettings settings, Transform parent, string label)
    {
        if (settings.prefab != null)
        {
            GameObject fromPrefab = Instantiate(settings.prefab, parent);
            fromPrefab.name = label;
            fromPrefab.transform.localPosition = Vector3.zero;
            fromPrefab.transform.localRotation = Quaternion.identity;
            return fromPrefab;
        }

        GameObject built = new GameObject(label);
        built.transform.SetParent(parent, false);

        SpriteRenderer renderer = built.AddComponent<SpriteRenderer>();
        renderer.sprite = settings.sprite;
        renderer.color = settings.tint;
        renderer.sortingOrder = sortingOrder;
        if (hasSortingLayer)
        {
            renderer.sortingLayerName = sortingLayer;
        }


        if (settings.sprite != null)
        {
            Vector2 drawn = settings.sprite.bounds.size;
            built.transform.localScale = new Vector3(
                drawn.x > 1e-4f ? settings.size.x / drawn.x : 1f,
                drawn.y > 1e-4f ? settings.size.y / drawn.y : 1f,
                1f);
        }
        else
        {
            Debug.LogWarning($"{name}: the {label} pad has neither a sprite nor a prefab, so it " +
                             "works but cannot be seen.", this);
        }

        return built;
    }


    private void Place(Pad pad, Vector2 bike, float now)
    {
        if (!TryFindSpot(pad, bike, out Vector2 placed, out Vector2 tangent))
        {
            pad.waitingForSpot = true;
            pad.readyAt = now + RetryDelay;
            return;
        }

        pad.kind = random.NextDouble() < boostShare ? SpeedPadKind.Boost : SpeedPadKind.Slowdown;
        pad.position = placed;
        pad.live = true;
        pad.waitingForSpot = false;

        pad.root.SetPositionAndRotation(new Vector3(placed.x, padHeight, placed.y), Facing(tangent));
        pad.boostVisual.SetActive(pad.kind == SpeedPadKind.Boost);
        pad.slowVisual.SetActive(pad.kind == SpeedPadKind.Slowdown);
        pad.root.gameObject.SetActive(true);
    }

    private void Collect(Pad pad, float now)
    {
        SpeedPadSettings settings = pad.kind == SpeedPadKind.Boost ? boost : slowdown;
        player.ApplySpeedModifier(settings.speedMultiplier, settings.duration);

        if (settings.sound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(settings.sound);
        }

        pad.live = false;
        pad.readyAt = now + respawnDelay;
        pad.root.gameObject.SetActive(false);
    }


    private bool TryFindSpot(Pad pad, Vector2 bike, out Vector2 placed, out Vector2 tangent)
    {
        placed = Vector2.zero;
        tangent = Vector2.right;

        bool haveFallback = false;
        Vector2 fallback = Vector2.zero;
        Vector2 fallbackTangent = Vector2.right;
        float clearance = collectRadius * RespawnClearance;

        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            Vector2 wanted = pad.zone.position + InsideCircle(pad.zone.radius);
            if (!TryPlace(wanted, out Vector2 spot, out Vector2 along))
            {
                return false;
            }

            if (Vector2.Distance(spot, bike) < clearance)
            {
                continue;
            }

            if (!haveFallback)
            {
                haveFallback = true;
                fallback = spot;
                fallbackTangent = along;
            }

            if (IsClearOfSiblings(pad, spot))
            {
                placed = spot;
                tangent = along;
                return true;
            }
        }

        placed = fallback;
        tangent = fallbackTangent;
        return haveFallback;
    }

    private bool IsClearOfSiblings(Pad pad, Vector2 spot)
    {
        foreach (Pad other in pads)
        {
            if (other == null || other == pad || !other.live || other.zone != pad.zone)
            {
                continue;
            }

            if (Vector2.Distance(other.position, spot) < minSeparation)
            {
                return false;
            }
        }

        return true;
    }


    private Vector2 InsideCircle(float radius)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float distance = radius * Mathf.Sqrt((float)random.NextDouble());
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }


    private Quaternion Facing(Vector2 tangent)
    {
        Vector3 along = new Vector3(tangent.x, 0f, tangent.y);
        if (along.sqrMagnitude < 1e-6f)
        {
            along = Vector3.forward;
        }

        return layFlat
            ? Quaternion.LookRotation(Vector3.down, along)
            : Quaternion.LookRotation(along, Vector3.up);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 span = to - from;
        float lengthSqr = span.sqrMagnitude;
        float t = lengthSqr > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - from, span) / lengthSqr) : 0f;
        return Vector2.Distance(point, from + span * t);
    }

    private static bool HasSortingLayer(string wanted)
    {
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == wanted)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 Flat(Vector3 position) => new Vector2(position.x, position.z);


    private class Pad
    {
        public SpeedPadZone zone;
        public Transform root;
        public GameObject boostVisual;
        public GameObject slowVisual;
        public SpeedPadKind kind;
        public Vector2 position;
        public bool live;


        public bool waitingForSpot;


        public float readyAt;
    }
}