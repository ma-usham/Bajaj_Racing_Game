using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LapTracker : MonoBehaviour
{
    [Tooltip("The bike. Its position is what gets measured against the line.")]
    [SerializeField] private PlayerController player;

    [Tooltip("The circuit. Left empty, the bike's own is used, which keeps the lap and the " +
             "barrier measured against the same road.")]
    [SerializeField] private TrackPathSO track;
    
    public int LapsCompleted { get; private set; }
    public float LapDistance { get; private set; }
    
    public float LapProgress => Length > 0f ? Mathf.Clamp01(LapDistance / Length) : 0f;
    public bool Running { get; private set; }

    public event Action<int> LapCompleted;

    private TrackPathSO Road => track != null ? track : (player != null ? player.Track : null);

    private float[] cumulative;
    private float Length;
    private int segmentHint = -1;
    private float lastDistance;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError($"{name}: no PlayerController, so there is nothing to follow round.", this);
            enabled = false;
            return;
        }

        if (!Measure())
        {
            Debug.LogError($"{name}: no baked TrackPathSO, so a lap has no length to be.", this);
            enabled = false;
        }
    }
    
    public void Begin()
    {
        if (cumulative == null && !Measure())
        {
            return;
        }

        segmentHint = -1;                      // the bike may have been put back on the grid
        lastDistance = DistanceAlong(Flat(player.transform.position));
        LapDistance = 0f;
        Running = true;
    }
    
    public void Stop()
    {
        Running = false;
    }
    
    public void ResetLaps()
    {
        LapsCompleted = 0;
        LapDistance = 0f;
        Running = false;
    }

    private void Update()
    {
        if (!Running)
        {
            return;
        }

        float here = DistanceAlong(Flat(player.transform.position));
        
        float step = Mathf.Repeat(here - lastDistance + Length * 0.5f, Length) - Length * 0.5f;
        lastDistance = here;
        LapDistance += step;

        if (LapDistance < Length)
        {
            return;
        }
        
        LapDistance -= Length;
        LapsCompleted++;
        LapCompleted?.Invoke(LapsCompleted);
    }
    private bool Measure()
    {
        TrackPathSO road = Road;
        if (road == null || !road.IsValid)
        {
            return false;
        }

        cumulative = new float[road.SegmentCount + 1];
        for (int i = 0; i < road.SegmentCount; i++)
        {
            cumulative[i + 1] = cumulative[i] + Vector2.Distance(road.GetPoint(i), road.GetPoint(i + 1));
        }

        Length = cumulative[road.SegmentCount];
        return Length > 0f;
    }
    
    private float DistanceAlong(Vector2 position)
    {
        TrackPathSO road = Road;
        segmentHint = road.Sample(position, segmentHint, out Vector2 centre, out _);

        Vector2 from = road.GetPoint(segmentHint);
        float into = Vector2.Distance(from, centre);
        return cumulative[segmentHint] + into;
    }

    private static Vector2 Flat(Vector3 position) => new Vector2(position.x, position.z);
}
