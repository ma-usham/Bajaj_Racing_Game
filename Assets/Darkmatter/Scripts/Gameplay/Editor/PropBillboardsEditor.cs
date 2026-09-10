using System.Collections.Generic;
using UnityEditor;
using UnityEngine;













[CustomEditor(typeof(PropBillboards))]
public class PropBillboardsEditor : Editor
{
    private SerializedProperty groups;

    private void OnEnable()
    {
        groups = serializedObject.FindProperty("groups");
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "One group per kind of prop. Swing is how far a prop may turn from the way it was " +
            "left facing to follow the camera: 180 for trees and poles, which can face the " +
            "camera outright, 0 for houses and landmarks, which cannot swivel without looking " +
            "like they are on a turntable.\n\n" +
            "Point the ones that cannot swivel at the road instead, with the button below. It " +
            "is worth running over the trees too: facing the road is a sensible place for a " +
            "prop to rest, and it is what the swing is measured from.",
            MessageType.None);

        DrawDefaultInspector();

        EditorGUILayout.Space();

        TrackPathSO road = FindRoad();
        if (road == null)
        {
            EditorGUILayout.HelpBox(
                "No baked TrackPathSO found, on the bike or in the project, so there is no road " +
                "to face the props towards.",
                MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(groups.arraySize == 0))
        {
            if (GUILayout.Button(new GUIContent(
                    "Face Props To Road",
                    "Turns every prop in every group to face the nearest point on the " +
                    "centreline. Undoable.")))
            {
                FaceRoad(road);
            }
        }
    }

    
    
    
    
    private static TrackPathSO FindRoad()
    {
        PlayerController bike = FindAnyObjectByType<PlayerController>();
        if (bike != null && bike.Track != null && bike.Track.IsValid)
        {
            return bike.Track;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:TrackPathSO"))
        {
            TrackPathSO found = AssetDatabase.LoadAssetAtPath<TrackPathSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (found != null && found.IsValid)
            {
                return found;
            }
        }

        return null;
    }

    private void FaceRoad(TrackPathSO road)
    {
        List<Transform> props = Collect();
        if (props.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to turn",
                "None of the groups have a root with sprites under it.", "Fine");
            return;
        }

        if (!EditorUtility.DisplayDialog("Face props to the road?",
                $"This turns {props.Count} props to face the nearest point on the centreline, " +
                "whatever way they are facing now. It can be undone.",
                "Turn them", "Leave them"))
        {
            return;
        }

        Undo.RecordObjects(props.ToArray(), "Face Props To Road");

        int turned = 0;
        foreach (Transform prop in props)
        {
            Vector2 flat = new Vector2(prop.position.x, prop.position.z);
            road.Sample(flat, -1, out Vector2 centre, out _);

            
            
            
            
            
            
            
            
            
            
            Vector2 fromRoad = flat - centre;
            if (fromRoad.sqrMagnitude < 1e-6f)
            {
                continue;
            }

            prop.rotation = Quaternion.LookRotation(new Vector3(fromRoad.x, 0f, fromRoad.y), Vector3.up);
            turned++;
        }

        Debug.Log($"{target.name}: turned {turned} props to face the road.", target);
    }

    private List<Transform> Collect()
    {
        List<Transform> props = new List<Transform>();

        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty root = groups.GetArrayElementAtIndex(i).FindPropertyRelative("root");
            if (root.objectReferenceValue is not Transform parent)
            {
                continue;
            }

            foreach (SpriteRenderer renderer in parent.GetComponentsInChildren<SpriteRenderer>(true))
            {
                props.Add(renderer.transform);
            }
        }

        return props;
    }
}
