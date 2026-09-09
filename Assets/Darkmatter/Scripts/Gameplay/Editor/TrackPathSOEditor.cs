using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws and edits a <see cref="TrackPathSO"/> in the scene view. Select the asset, look down
/// at the road from above, and lay the centreline a point at a time from start to finish: the
/// border either side is drawn live at the asset's own half width, so what you see while
/// placing points is exactly where the bike will be stopped.
///
/// Nothing about the border is stored. It is the centreline offset by the half width, worked
/// out afresh every frame in PlayerController, so redrawing the line or changing the width is
/// all it takes to move it. A new road needs nothing but this; baking one out of track art
/// (Tools/track_path_baker) is only worth it for a circuit already painted in the pixels.
/// </summary>
[CustomEditor(typeof(TrackPathSO))]
public class TrackPathSOEditor : Editor
{
    /// <summary>Drawn a touch above the road, or the Track sprite hides the lines.</summary>
    private const float DrawHeight = 0.1f;

    /// <summary>How near the pointer has to be, in screen pixels, to delete a point.</summary>
    private const float PickDistance = 16f;

    private const int ArrowSpacing = 8;

    private static readonly Color CentreColour = new Color(0.30f, 1.00f, 0.40f);
    private static readonly Color EdgeColour = new Color(1.00f, 0.30f, 0.90f);
    private static readonly Color PointColour = new Color(1.00f, 0.90f, 0.20f);
    private static readonly Color StartColour = new Color(0.30f, 0.80f, 1.00f);

    private SerializedProperty points;
    private SerializedProperty halfWidth;
    private SerializedProperty closedLoop;
    private SerializedProperty lapLength;

    /// <summary>
    /// Lengths of road between points: one per point when the loop closes, one fewer when
    /// the road has two ends and nothing joins them.
    /// </summary>
    private int SegmentCount => closedLoop.boolValue
        ? points.arraySize
        : Mathf.Max(points.arraySize - 1, 0);

    private void OnEnable()
    {
        points = serializedObject.FindProperty("points");
        halfWidth = serializedObject.FindProperty("halfWidth");
        closedLoop = serializedObject.FindProperty("closedLoop");
        lapLength = serializedObject.FindProperty("lapLength");
        SceneView.duringSceneGui += OnSceneView;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneView;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Select this asset and look down on the road in the scene view.\n\n" +
            "Shift click lays the next point of the centreline, so the road is drawn a point " +
            "at a time from start to finish. Drag a point to move it, ctrl click a point to " +
            "delete it.\n\n" +
            "Green is the centreline, magenta is the border at Half Width either side of it, " +
            "which is where the bike gets stopped.",
            MessageType.None);

        EditorGUILayout.PropertyField(halfWidth, new GUIContent(
            "Half Width", "Half the width of the road. The barrier sits this far either side " +
                          "of the line, less the bike's own barrierMargin."));

        EditorGUILayout.PropertyField(closedLoop, new GUIContent(
            "Closed Loop", "On joins the last point back to the first, for a circuit. Off " +
                           "leaves the road with two ends, which is what a test strip wants."));

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField(new GUIContent("Points"), points.arraySize);
            EditorGUILayout.FloatField(new GUIContent(
                "Lap Length", "Measured along the centreline. Kept up to date as you edit."),
                lapLength.floatValue);
        }

        int needed = closedLoop.boolValue ? 3 : 2;
        if (points.arraySize < needed)
        {
            EditorGUILayout.HelpBox(
                $"This road needs at least {needed} points before the bike can be held to it.",
                MessageType.Warning);
        }

        if (halfWidth.floatValue <= 0f)
        {
            EditorGUILayout.HelpBox(
                "Half width is zero, so there is no road either side of the line to drive on.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent(
                    "Reverse Direction", "Flips which end of the road counts as the start.")))
            {
                Reverse();
            }

            using (new EditorGUI.DisabledScope(points.arraySize == 0))
            {
                if (GUILayout.Button("Clear Points") &&
                    EditorUtility.DisplayDialog("Clear the centreline?",
                        $"This throws away all {points.arraySize} points. It can be undone.",
                        "Clear", "Keep"))
                {
                    points.ClearArray();
                }
            }
        }

        KeepLapLengthFresh();

        if (serializedObject.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }
    }

    private void OnSceneView(SceneView view)
    {
        if (target == null)
        {
            return;
        }

        serializedObject.Update();

        // Claimed before any of the point handles, so it is the same id on the layout pass
        // and the pass that acts on the click, however many points there happen to be.
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        // Drawn over the top of the Track sprite rather than fighting it for depth.
        UnityEngine.Rendering.CompareFunction wasTesting = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        DrawRibbon();
        MovePoints();
        HandleClicks(controlId);

        Handles.zTest = wasTesting;

        KeepLapLengthFresh();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRibbon()
    {
        if (points.arraySize < 2 || Event.current.type != EventType.Repaint)
        {
            return;
        }

        float edge = Mathf.Max(halfWidth.floatValue, 0f);
        for (int i = 0; i < SegmentCount; i++)
        {
            Vector3 from = PointAt(i);
            Vector3 to = PointAt(NextIndex(i));
            Vector3 span = to - from;
            if (span.sqrMagnitude < 1e-8f)
            {
                continue;
            }

            Vector3 side = Vector3.Cross(Vector3.up, span.normalized) * edge;

            Handles.color = CentreColour;
            Handles.DrawLine(from, to);

            Handles.color = EdgeColour;
            Handles.DrawLine(from + side, to + side);
            Handles.DrawLine(from - side, to - side);

            if (i % ArrowSpacing == 0)
            {
                Handles.color = CentreColour;
                Vector3 middle = (from + to) * 0.5f;
                Handles.ConeHandleCap(0, middle, Quaternion.LookRotation(span.normalized),
                                      HandleUtility.GetHandleSize(middle) * 0.10f,
                                      EventType.Repaint);
            }
        }
    }

    private void MovePoints()
    {
        for (int i = 0; i < points.arraySize; i++)
        {
            Vector3 world = PointAt(i);
            float size = HandleUtility.GetHandleSize(world) * (i == 0 ? 0.07f : 0.045f);
            Handles.color = i == 0 ? StartColour : PointColour;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(world, Vector3.up, Vector3.right, Vector3.forward,
                                             size, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                points.GetArrayElementAtIndex(i).vector2Value = new Vector2(moved.x, moved.z);
            }
        }
    }

    private void HandleClicks(int controlId)
    {
        Event current = Event.current;
        bool deleting = EditorGUI.actionKey;
        bool adding = current.shift && !deleting;

        if (current.type == EventType.Layout && (adding || deleting))
        {
            // Without this a click in the scene view picks whatever is under it and the
            // asset stops being the selection, taking this editor with it.
            HandleUtility.AddDefaultControl(controlId);
        }

        if (current.type != EventType.MouseDown || current.button != 0)
        {
            return;
        }

        // A point handle claims the click before this runs, so shift clicking one drags it
        // rather than dropping a second point on top of the one already there.
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
        Vector2 placed = new Vector2(hit.x, hit.z);

        // A road with two ends is drawn a point at a time, start to finish, so every click
        // carries on from the last one. Guessing at where a click "meant" to go instead is
        // what makes drawing a corner unpredictable: the click lands beside an earlier
        // stretch and the point lands there rather than under the pointer.
        //
        // A circuit has no end to carry on from, so there the click splits whichever length
        // of road it landed nearest.
        int at = points.arraySize;
        if (closedLoop.boolValue && points.arraySize >= 3)
        {
            at = NearestSegment(placed) + 1;
        }

        if (at >= points.arraySize)
        {
            points.arraySize++;
            at = points.arraySize - 1;
        }
        else
        {
            points.InsertArrayElementAtIndex(at);
        }

        points.GetArrayElementAtIndex(at).vector2Value = placed;
    }

    private void DeleteNearest(Vector2 mouse)
    {
        int nearest = -1;
        float nearestDistance = PickDistance;

        for (int i = 0; i < points.arraySize; i++)
        {
            float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(PointAt(i)), mouse);
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

    private int NearestSegment(Vector2 position)
    {
        int nearest = 0;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < SegmentCount; i++)
        {
            Vector2 from = Flat(i);
            Vector2 span = Flat(NextIndex(i)) - from;
            float lengthSqr = span.sqrMagnitude;
            float t = lengthSqr > 1e-8f
                ? Mathf.Clamp01(Vector2.Dot(position - from, span) / lengthSqr)
                : 0f;

            float distanceSqr = (position - (from + span * t)).sqrMagnitude;
            if (distanceSqr < nearestSqr)
            {
                nearestSqr = distanceSqr;
                nearest = i;
            }
        }

        return nearest;
    }

    private void Reverse()
    {
        int count = points.arraySize;
        Vector2[] copy = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            copy[i] = Flat(i);
        }

        for (int i = 0; i < count; i++)
        {
            points.GetArrayElementAtIndex(i).vector2Value = copy[count - 1 - i];
        }
    }

    private void KeepLapLengthFresh()
    {
        float total = 0f;
        for (int i = 0; i < SegmentCount; i++)
        {
            total += Vector2.Distance(Flat(i), Flat(NextIndex(i)));
        }

        if (Mathf.Abs(total - lapLength.floatValue) > 0.001f)
        {
            lapLength.floatValue = total;
        }
    }

    private int NextIndex(int index)
    {
        int count = points.arraySize;
        if (count == 0)
        {
            return 0;
        }

        return closedLoop.boolValue ? (index + 1) % count : Mathf.Min(index + 1, count - 1);
    }

    private Vector2 Flat(int index) => points.GetArrayElementAtIndex(index).vector2Value;

    private Vector3 PointAt(int index)
    {
        Vector2 flat = Flat(index);
        return new Vector3(flat.x, DrawHeight, flat.y);
    }
}
