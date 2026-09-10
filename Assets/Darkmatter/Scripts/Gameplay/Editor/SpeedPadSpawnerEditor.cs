using UnityEditor;
using UnityEngine;

namespace Darkmatter.Gameplay.Editors
{
    [CustomEditor(typeof(SpeedPadSpawner))]
    public class SpeedPadSpawnerEditor : Editor
    {
        private const float DrawHeight = 0.12f;

        private const float PickDistance = 20f;

        private static readonly Color BoostColour = new Color(0.35f, 1.00f, 0.55f);
        private static readonly Color SlowColour = new Color(1.00f, 0.45f, 0.30f);

        private SerializedProperty points;

        private void OnEnable()
        {
            points = serializedObject.FindProperty("points");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "A point is exactly where a pad goes: a position in world XZ and which kind " +
                "spawns there. Nothing is scattered or snapped, so what you place is what you " +
                "race.\n\n" +
                "Shift click the road in the scene view to drop a point, drag it to move it, " +
                "ctrl click it to delete it. A new point copies the kind of the last one, so a " +
                "run of boosts is quick to lay down.\n\n" +
                "Green is a boost, orange a slowdown.",
                MessageType.None);

            DrawDefaultInspector();

            if (points.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No points yet, so this spawner will not lay any pads.",
                    MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();

            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            UnityEngine.Rendering.CompareFunction wasTesting = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int i = 0; i < points.arraySize; i++)
            {
                DrawPoint(points.GetArrayElementAtIndex(i), i);
            }

            Handles.zTest = wasTesting;

            HandleClicks(controlId);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPoint(SerializedProperty point, int index)
        {
            SerializedProperty position = point.FindPropertyRelative("position");
            SerializedProperty kind = point.FindPropertyRelative("kind");

            Vector2 flat = position.vector2Value;
            Vector3 centre = new Vector3(flat.x, DrawHeight, flat.y);

            Handles.color = kind.enumValueIndex == 0 ? BoostColour : SlowColour;

            if (Event.current.type == EventType.Repaint)
            {
                Handles.DrawWireDisc(centre, Vector3.up, HandleUtility.GetHandleSize(centre) * 0.25f);
                Handles.Label(centre + Vector3.up * 0.5f, $"{index}  {(SpeedPadKind)kind.enumValueIndex}");
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(centre, Vector3.up, Vector3.right, Vector3.forward,
                                             HandleUtility.GetHandleSize(centre) * 0.08f,
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

            // A new point picks up the kind of the last one, so laying a run of the same kind is
            // one modifier and a click rather than a trip back to the inspector each time.
            int kind = points.arraySize > 0
                ? points.GetArrayElementAtIndex(points.arraySize - 1).FindPropertyRelative("kind").enumValueIndex
                : 0;

            points.arraySize++;
            SerializedProperty added = points.GetArrayElementAtIndex(points.arraySize - 1);
            added.FindPropertyRelative("position").vector2Value = new Vector2(hit.x, hit.z);
            added.FindPropertyRelative("kind").enumValueIndex = kind;
        }

        private void DeleteNearest(Vector2 mouse)
        {
            int nearest = -1;
            float nearestDistance = PickDistance;

            for (int i = 0; i < points.arraySize; i++)
            {
                Vector2 flat = points.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector2Value;
                Vector3 world = new Vector3(flat.x, DrawHeight, flat.y);

                float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), mouse);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            if (nearest >= 0)
            {
                points.DeleteArrayElementAtIndex(nearest);
            }
        }
    }
}
