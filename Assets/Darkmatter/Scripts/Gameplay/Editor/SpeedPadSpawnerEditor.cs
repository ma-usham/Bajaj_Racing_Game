using UnityEditor;
using UnityEngine;

namespace Darkmatter.Gameplay.Editors
{
    [CustomEditor(typeof(SpeedPadSpawner))]
    public class SpeedPadSpawnerEditor : Editor
    {
        private const float DrawHeight = 0.12f;

        private const float PickDistance = 20f;

        /// <summary>How far apart the two spots of a freshly dropped zone start, in world units.</summary>
        private const float PairSpread = 5f;

        private static readonly Color ZoneColour = new Color(1.00f, 0.70f, 0.15f);
        private static readonly Color PairColour = new Color(1.00f, 0.70f, 0.15f, 0.45f);

        private SerializedProperty zones;

        private void OnEnable()
        {
            zones = serializedObject.FindProperty("zones");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "A zone is two spots. One comes up as a boost and the other as a slowdown, so a " +
                "zone is never two of the same kind, and which spot gets which is rolled fresh " +
                "every race.\n\n" +
                "Shift click the road in the scene view to drop a zone, drag either dot to move " +
                "it, ctrl click near a zone to delete it.\n\n" +
                "The dots are not coloured by kind because nothing has been dealt yet at edit " +
                "time.",
                MessageType.None);

            DrawDefaultInspector();

            if (zones.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No zones yet, so this spawner will not lay any pads.",
                    MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();

            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            UnityEngine.Rendering.CompareFunction wasTesting = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int i = 0; i < zones.arraySize; i++)
            {
                DrawZone(zones.GetArrayElementAtIndex(i), i);
            }

            Handles.zTest = wasTesting;

            HandleClicks(controlId);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawZone(SerializedProperty zone, int index)
        {
            SerializedProperty first = zone.FindPropertyRelative("first");
            SerializedProperty second = zone.FindPropertyRelative("second");

            Vector3 a = World(first.vector2Value);
            Vector3 b = World(second.vector2Value);

            if (Event.current.type == EventType.Repaint)
            {
                // The line is what makes the pair read as one zone rather than two loose spots.
                Handles.color = PairColour;
                Handles.DrawDottedLine(a, b, 4f);

                Handles.color = ZoneColour;
                Handles.Label((a + b) * 0.5f + Vector3.up * 0.5f, $"Zone {index}");
            }

            Handles.color = ZoneColour;
            Drag(first, a);
            Drag(second, b);
        }

        private void Drag(SerializedProperty position, Vector3 at)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(at, Vector3.up, Vector3.right, Vector3.forward,
                                             HandleUtility.GetHandleSize(at) * 0.08f,
                                             Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                position.vector2Value = new Vector2(moved.x, moved.z);
            }
        }

        private void HandleClicks(int controlId)
        {
            Event current = Event.current;
            bool deleting = EditorGUI.actionKey;
            bool adding = current.shift && !deleting;

            if (current.type == EventType.Layout && (adding || deleting))
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            if (current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (GUIUtility.hotControl != 0)
            {
                return;
            }

            if (deleting)
            {
                DeleteNearest(current.mousePosition);
                current.Use();
            }
            else if (adding)
            {
                AddAt(current.mousePosition);
                current.Use();
            }
        }

        private void AddAt(Vector2 mouse)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            Plane road = new Plane(Vector3.up, new Vector3(0f, DrawHeight, 0f));
            if (!road.Raycast(ray, out float distance))
            {
                return;
            }

            Vector3 hit = ray.GetPoint(distance);

            // A zone is always born complete, with both spots set. There is no half a zone to
            // represent, so there is no way to leave one in the scene by clicking away.
            zones.arraySize++;
            SerializedProperty added = zones.GetArrayElementAtIndex(zones.arraySize - 1);
            added.FindPropertyRelative("first").vector2Value =
                new Vector2(hit.x - PairSpread * 0.5f, hit.z);
            added.FindPropertyRelative("second").vector2Value =
                new Vector2(hit.x + PairSpread * 0.5f, hit.z);
        }

        private void DeleteNearest(Vector2 mouse)
        {
            int nearest = -1;
            float nearestDistance = PickDistance;

            for (int i = 0; i < zones.arraySize; i++)
            {
                SerializedProperty zone = zones.GetArrayElementAtIndex(i);

                foreach (string slot in new[] { "first", "second" })
                {
                    Vector3 world = World(zone.FindPropertyRelative(slot).vector2Value);
                    float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), mouse);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = i;
                    }
                }
            }

            if (nearest >= 0)
            {
                zones.DeleteArrayElementAtIndex(nearest);
            }
        }

        private static Vector3 World(Vector2 flat) => new Vector3(flat.x, DrawHeight, flat.y);
    }
}
