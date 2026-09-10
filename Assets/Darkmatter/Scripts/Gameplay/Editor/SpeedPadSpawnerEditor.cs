using UnityEditor;
using UnityEngine;











[CustomEditor(typeof(SpeedPadSpawner))]
public class SpeedPadSpawnerEditor : Editor
{
    
    private const float DrawHeight = 0.12f;

    
    private const float PickDistance = 20f;

    
    private const int PreviewSamples = 32;

    
    private const float DefaultRadius = 8f;

    private static readonly Color ZoneColour = new Color(1.00f, 0.70f, 0.15f);
    private static readonly Color FillColour = new Color(1.00f, 0.70f, 0.15f, 0.05f);
    private static readonly Color LandingColour = new Color(0.25f, 0.90f, 1.00f);

    private SerializedProperty zones;

    private void OnEnable()
    {
        zones = serializedObject.FindProperty("zones");
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "A zone is a patch of road pads are allowed to land on: a position in world XZ " +
            "and a radius. Which pads turn up where, and which of them are boosts, is rolled " +
            "afresh every run and again every time one is collected.\n\n" +
            "Shift click the road in the scene view to drop a zone, drag the middle to move " +
            "it, drag the ring to resize it, ctrl click the middle to delete it.\n\n" +
            "Orange is the zone. Blue dots are where pads can actually land, after each point " +
            "is pulled back inside the road.",
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
        SpeedPadSpawner spawner = (SpeedPadSpawner)target;
        serializedObject.Update();

        
        
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        UnityEngine.Rendering.CompareFunction wasTesting = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        for (int i = 0; i < zones.arraySize; i++)
        {
            DrawZone(spawner, zones.GetArrayElementAtIndex(i));
        }

        Handles.zTest = wasTesting;

        HandleClicks(controlId);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawZone(SpeedPadSpawner spawner, SerializedProperty zone)
    {
        SerializedProperty position = zone.FindPropertyRelative("position");
        SerializedProperty radius = zone.FindPropertyRelative("radius");

        Vector2 flat = position.vector2Value;
        Vector3 centre = new Vector3(flat.x, DrawHeight, flat.y);

        if (Event.current.type == EventType.Repaint)
        {
            Handles.color = FillColour;
            Handles.DrawSolidDisc(centre, Vector3.up, radius.floatValue);
            DrawLandingSpots(spawner, flat, radius.floatValue);
        }

        
        Handles.color = ZoneColour;
        EditorGUI.BeginChangeCheck();
        float resized = Handles.RadiusHandle(Quaternion.LookRotation(Vector3.up, Vector3.forward),
                                             centre, radius.floatValue);
        if (EditorGUI.EndChangeCheck())
        {
            radius.floatValue = Mathf.Max(resized, 0.5f);
        }

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider2D(centre, Vector3.up, Vector3.right, Vector3.forward,
                                         HandleUtility.GetHandleSize(centre) * 0.06f,
                                         Handles.DotHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            position.vector2Value = new Vector2(moved.x, moved.z);
        }
    }

    
    
    
    
    
    private void DrawLandingSpots(SpeedPadSpawner spawner, Vector2 centre, float radius)
    {
        Handles.color = LandingColour;

        for (int i = 0; i < PreviewSamples; i++)
        {
            float angle = i * 2.39996f;
            float distance = radius * Mathf.Sqrt((i + 0.5f) / PreviewSamples);
            Vector2 wanted = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if (!spawner.TryPlace(wanted, out Vector2 placed, out _))
            {
                return;
            }

            Vector3 world = new Vector3(placed.x, DrawHeight, placed.y);
            Handles.DotHandleCap(0, world, Quaternion.identity,
                                 HandleUtility.GetHandleSize(world) * 0.02f, EventType.Repaint);
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

        
        
        
        float radius = zones.arraySize > 0
            ? zones.GetArrayElementAtIndex(zones.arraySize - 1).FindPropertyRelative("radius").floatValue
            : DefaultRadius;

        zones.arraySize++;
        SerializedProperty added = zones.GetArrayElementAtIndex(zones.arraySize - 1);
        added.FindPropertyRelative("position").vector2Value = new Vector2(hit.x, hit.z);
        added.FindPropertyRelative("radius").floatValue = Mathf.Max(radius, 0.5f);
        added.FindPropertyRelative("count").intValue =
            Mathf.Max(added.FindPropertyRelative("count").intValue, 1);
    }

    private void DeleteNearest(Vector2 mouse)
    {
        int nearest = -1;
        float nearestDistance = PickDistance;

        for (int i = 0; i < zones.arraySize; i++)
        {
            Vector2 flat = zones.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector2Value;
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
            zones.DeleteArrayElementAtIndex(nearest);
        }
    }
}
