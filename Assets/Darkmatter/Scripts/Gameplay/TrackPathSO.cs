using UnityEngine;

/// <summary>
/// The drivable ribbon of a road: a line of centreline points in world XZ, and the one
/// half width that fits the road along all of it.
///
/// The road only exists as pixels on the Track sprite, so nothing in the scene knows
/// where it is. This asset is that knowledge in a form gameplay can ask questions of:
/// given a position, how far off the centreline is it, and how far off is it allowed
/// to be. The barrier itself is never stored; it is this line offset by this width,
/// worked out afresh each frame, so a new road only needs a new line and a width.
///
/// A closed loop is the exception rather than the rule here: <see cref="ClosedLoop"/>
/// joins the last point back to the first for a circuit, and off it the road simply has
/// two ends. A test strip is a handful of points with the loop turned off.
///
/// Two ways to fill it in. Select the asset and draw the line over the track in the
/// scene view (see TrackPathSOEditor), which is the way to go for a new road; or bake it
/// out of the art with Tools/track_path_baker, which reads a circuit out of the pixels
/// and is worth it when the art is already final.
///
/// Points are world space and assume the Track sprite sits on the origin at the scale
/// the line was drawn against (230.4 by 153.6 units for the current art). Move or
/// rescale the Track and the line wants moving with it.
/// </summary>
[CreateAssetMenu(fileName = "TrackPathSO", menuName = "Scriptable Objects/TrackPathSO")]
public class TrackPathSO : ScriptableObject
{
    [Tooltip("Centreline of the road in world XZ, ordered along the racing direction.")]
    [SerializeField] private Vector2[] points;

    [Tooltip("Half the width of the road in world units. The barrier sits this far either " +
             "side of the line, so the road is this wide the whole way along.")]
    [SerializeField] private float halfWidth = 4f;

    [Tooltip("Off for a road with two ends, drawn start to finish. On joins the last point " +
             "back to the first for a circuit, which is worth it only when the road really " +
             "does come back round on itself.")]
    [SerializeField] private bool closedLoop;

    [Tooltip("Length of the road in world units, measured along the centreline. One lap, " +
             "for a closed loop.")]
    [SerializeField] private float lapLength;

    /// <summary>Segments either side of the last known one that <see cref="Sample"/> looks at.</summary>
    private const int SearchWindow = 6;

    public int Count => points != null ? points.Length : 0;

    /// <summary>
    /// Lengths of road between points. One per point when the loop closes, because the last
    /// point joins the first; one fewer when it does not.
    /// </summary>
    public int SegmentCount => closedLoop ? Count : Mathf.Max(Count - 1, 0);

    public bool ClosedLoop => closedLoop;

    public float LapLength => lapLength;

    public float HalfWidth => halfWidth;

    public bool IsValid => Count >= (closedLoop ? 3 : 2) && halfWidth > 0f;

    public Vector2 GetPoint(int index) => points[PointIndex(index)];

    /// <summary>
    /// Works out where <paramref name="position"/> sits on the circuit, and returns the
    /// index of the segment it sits on so the next call can start from there.
    ///
    /// The search is a window around <paramref name="searchHint"/> rather than a sweep of
    /// the whole loop, because the circuit doubles back on itself: at a hairpin the
    /// nearest segment in raw distance can be the one on the far side of the kerb. Pass
    /// a negative hint for the first call, or after teleporting the bike, to sweep
    /// everything; the window widens to a sweep on its own if the position turns out to
    /// be at the edge of it.
    /// </summary>
    /// <param name="centre">Closest point on the centreline. How far off the road the caller
    /// is, is its distance to this point, not the sideways part of that distance: at a corner
    /// the closest point is the join between two segments, and measuring only across one of
    /// them reads a position well past the end of it as barely off the line at all.</param>
    /// <param name="tangent">Unit direction of the road there, along the racing direction.</param>
    public int Sample(Vector2 position, int searchHint, out Vector2 centre, out Vector2 tangent)
    {
        int best = 0;
        float bestSqr = float.MaxValue;
        float bestT = 0f;

        if (searchHint >= 0)
        {
            for (int offset = -SearchWindow; offset <= SearchWindow; offset++)
            {
                Consider(position, SegmentIndex(searchHint + offset), ref best, ref bestSqr, ref bestT);
            }

            // Landing on the last segment the window covers means the bike outran it, and
            // a best that is nowhere near the road means the window is looking at a stale
            // part of the circuit entirely. Either way the real nearest segment is
            // somewhere the window never looked.
            float reach = halfWidth * 3f;
            if (Mathf.Abs(Shortest(best - searchHint)) >= SearchWindow || bestSqr > reach * reach)
            {
                bestSqr = float.MaxValue;
                searchHint = -1;
            }
        }

        if (searchHint < 0)
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                Consider(position, i, ref best, ref bestSqr, ref bestT);
            }
        }

        Vector2 from = points[best];
        Vector2 span = points[PointIndex(best + 1)] - from;

        centre = from + span * bestT;
        tangent = span.sqrMagnitude > 1e-8f ? span.normalized : Vector2.right;
        return best;
    }

    private void Consider(Vector2 position, int index, ref int best, ref float bestSqr, ref float bestT)
    {
        Vector2 from = points[index];
        Vector2 span = points[PointIndex(index + 1)] - from;

        float lengthSqr = span.sqrMagnitude;
        float t = lengthSqr > 1e-8f ? Mathf.Clamp01(Vector2.Dot(position - from, span) / lengthSqr) : 0f;
        float distanceSqr = (position - (from + span * t)).sqrMagnitude;

        if (distanceSqr < bestSqr)
        {
            bestSqr = distanceSqr;
            best = index;
            bestT = t;
        }
    }

    /// <summary>A point index, round the loop when it closes and clamped to the ends when not.</summary>
    private int PointIndex(int index)
    {
        int count = points.Length;
        if (count == 0)
        {
            return 0;
        }

        return closedLoop ? ((index % count) + count) % count : Mathf.Clamp(index, 0, count - 1);
    }

    /// <summary>A segment index, round the loop when it closes and clamped to the ends when not.</summary>
    private int SegmentIndex(int index)
    {
        int count = SegmentCount;
        if (count == 0)
        {
            return 0;
        }

        return closedLoop ? ((index % count) + count) % count : Mathf.Clamp(index, 0, count - 1);
    }

    /// <summary>
    /// How far apart two segment indices are. Signed and the short way round when the loop
    /// closes; a plain difference when it does not, because there is no way round the ends.
    /// </summary>
    private int Shortest(int delta)
    {
        if (!closedLoop)
        {
            return delta;
        }

        int count = SegmentCount;
        delta = ((delta % count) + count) % count;
        return delta > count / 2 ? delta - count : delta;
    }
}
