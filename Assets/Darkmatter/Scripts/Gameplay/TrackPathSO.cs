using UnityEngine;

namespace Darkmatter.Gameplay
{
    [CreateAssetMenu(fileName = "TrackPathSO", menuName = "Scriptable Objects/TrackPathSO")]
    public class TrackPathSO : ScriptableObject
    {
        [Tooltip("Centreline of the road in world XZ, ordered along the racing direction.")] [SerializeField]
        private Vector2[] points;

        [Tooltip("Half the width of the road in world units. The barrier sits this far either " +
                 "side of the line, so the road is this wide the whole way along.")]
        [SerializeField]
        private float halfWidth = 4f;

        [Tooltip("Off for a road with two ends, drawn start to finish. On joins the last point " +
                 "back to the first for a circuit, which is worth it only when the road really " +
                 "does come back round on itself.")]
        [SerializeField]
        private bool closedLoop;

        [Tooltip("Length of the road in world units, measured along the centreline. One lap, " +
                 "for a closed loop.")]
        [SerializeField]
        private float lapLength;


        private const int SearchWindow = 6;

        public int Count => points != null ? points.Length : 0;


        public int SegmentCount => closedLoop ? Count : Mathf.Max(Count - 1, 0);

        public bool ClosedLoop => closedLoop;

        public float LapLength => lapLength;

        public float HalfWidth => halfWidth;

        public bool IsValid => Count >= (closedLoop ? 3 : 2) && halfWidth > 0f;

        public Vector2 GetPoint(int index) => points[PointIndex(index)];


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


        private int PointIndex(int index)
        {
            int count = points.Length;
            if (count == 0)
            {
                return 0;
            }

            return closedLoop ? ((index % count) + count) % count : Mathf.Clamp(index, 0, count - 1);
        }


        private int SegmentIndex(int index)
        {
            int count = SegmentCount;
            if (count == 0)
            {
                return 0;
            }

            return closedLoop ? ((index % count) + count) % count : Mathf.Clamp(index, 0, count - 1);
        }


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
}
