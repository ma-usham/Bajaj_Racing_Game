using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A set of props that turn together, and how far they are allowed to turn.
/// </summary>
[System.Serializable]
public class BillboardGroup
{
    [Tooltip("For reading in the inspector. Nothing else uses it.")]
    public string name = "Props";

    [Tooltip("Every sprite under here belongs to this group, however deeply nested. Point it " +
             "at a container holding one kind of prop: trees want turning and houses do not, " +
             "so the two cannot share a group.")]
    public Transform root;

    [Tooltip("Degrees a prop may turn from the way it was left facing, to follow the camera.\n\n" +
             "180 is a full billboard, always square on to the lens. That is what a tree, a " +
             "pole or a bush wants, because a symmetric silhouette gives nothing away.\n\n" +
             "0 never turns, and is what a house wants: a building that swivels to keep facing " +
             "you as you drive past reads as a building on a turntable. Point those at the road " +
             "once with the Face Props To Road button instead.\n\n" +
             "In between leans towards the camera without giving the game away. It suits a prop " +
             "the road never gets behind, because at the far edge of the swing the prop is held " +
             "against its limit, and a camera that carries on round past the back of it swaps " +
             "one limit for the other in a single frame.")]
    [Range(0f, 180f)]
    public float swing = 180f;
}

/// <summary>
/// Turns the flat scenery to face the camera.
///
/// Every prop in this world is one upright sprite, so left alone each is only right from the
/// angle it was authored at: come round the circuit and it thins to a sliver, or shows its
/// back. Turning them with the camera is what hides that.
///
/// Two things worth knowing about how they are turned.
///
/// Only the yaw is touched. The chase camera sits at about six degrees of pitch, so tilting a
/// sprite back to square up with the lens would gain well under a percent of height and cost
/// the prop its footing on the ground, which is the one thing selling it as standing there.
///
/// Every prop takes the camera's yaw rather than aiming at the camera's position. Aiming at
/// the point turns a prop at the edge of a 52 degree lens several degrees away from the one
/// beside it, so a row of trees fans out as it goes past. One shared yaw keeps the row
/// parallel, and costs one quaternion for the lot of them.
///
/// How far each prop is allowed to follow the camera is its group's business, and a group that
/// cannot turn at all is not tracked here: it is only somewhere for the road facing tool to
/// find those props. See <see cref="BillboardGroup"/>.
/// </summary>
/// <remarks>
/// Runs after CameraFollow, which moves the camera in LateUpdate and has no order of its own.
/// Two LateUpdates with the same order run in an undefined order, and coming first would leave
/// the props a frame behind the camera.
/// </remarks>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class PropBillboards : MonoBehaviour
{
    [Tooltip("The camera the props turn to. Left empty, the main camera is used.")]
    [SerializeField] private Camera view;

    [Tooltip("The scenery, split by how freely it may turn. One group per kind of prop.")]
    [SerializeField] private BillboardGroup[] groups = new BillboardGroup[0];

    /// <summary>Above this a prop simply takes the camera's yaw, and the clamp is beside the point.</summary>
    private const float FullSwing = 179.9f;

    private Transform[] props;
    private float[] resting;
    private float[] swings;
    private bool[] free;
    private float lastYaw = float.NaN;

    private void Awake()
    {
        if (view == null)
        {
            view = Camera.main;
        }

        if (view == null)
        {
            Debug.LogError($"{name}: no camera to turn the props to, and no main camera to " +
                           "fall back on.", this);
            enabled = false;
            return;
        }

        Refresh();

        if (props.Length == 0)
        {
            Debug.LogWarning($"{name}: no props to turn. Every group is either empty or on a " +
                             "swing of 0, which is a group the road facing tool writes to but " +
                             "that has nothing to do at runtime.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Reads the scenery back out of the groups. Called once at Awake, because scenery does not
    /// come and go; call it again if some ever does.
    ///
    /// Each prop's own rotation at the moment it is read becomes the facing it swings about, so
    /// the road facing tool and the inspector both stay the authority on where a prop points.
    /// </summary>
    public void Refresh()
    {
        List<Transform> found = new List<Transform>();
        List<float> restingYaws = new List<float>();
        List<float> allowed = new List<float>();

        foreach (BillboardGroup group in groups)
        {
            if (group == null || group.root == null || group.swing <= 0f)
            {
                continue;
            }

            foreach (SpriteRenderer renderer in group.root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                found.Add(renderer.transform);
                restingYaws.Add(renderer.transform.eulerAngles.y);
                allowed.Add(group.swing);
            }
        }

        props = found.ToArray();
        resting = restingYaws.ToArray();
        swings = allowed.ToArray();

        free = new bool[props.Length];
        for (int i = 0; i < props.Length; i++)
        {
            free[i] = swings[i] >= FullSwing;
        }

        lastYaw = float.NaN;
    }

    private void LateUpdate()
    {
        float cameraYaw = view.transform.eulerAngles.y;
        if (cameraYaw == lastYaw)
        {
            return;
        }

        lastYaw = cameraYaw;

        // Built once and handed to every prop that turns freely, which is most of them.
        Quaternion square = Quaternion.Euler(0f, cameraYaw, 0f);

        for (int i = 0; i < props.Length; i++)
        {
            Transform prop = props[i];
            if (prop == null)
            {
                continue;
            }

            if (free[i])
            {
                prop.rotation = square;
                continue;
            }

            float lean = Mathf.Clamp(Mathf.DeltaAngle(resting[i], cameraYaw), -swings[i], swings[i]);
            prop.rotation = Quaternion.Euler(0f, resting[i] + lean, 0f);
        }
    }
}
