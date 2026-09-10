using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.Gameplay
{
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


    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class PropBillboards : MonoBehaviour
    {
        [Tooltip("The camera the props turn to. Left empty, the main camera is used.")] [SerializeField]
        private Camera view;

        [Tooltip("The scenery, split by how freely it may turn. One group per kind of prop.")] [SerializeField]
        private BillboardGroup[] groups = new BillboardGroup[0];


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
}
