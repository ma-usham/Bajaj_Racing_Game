using UnityEngine;

namespace Darkmatter.Gameplay
{
    public struct BarrierHit
    {
        public bool Scraping;

        public float Incidence;

        public Vector3 Position;
        public float Heading;
    }

    public class TrackBarrier
    {
        private int segment = -1;

        public void Forget()
        {
            segment = -1;
        }

        public BarrierHit Hold(TrackPathSO track, Vector3 position, float heading, Vector3 forward,
            float margin, float steer, float deltaTime)
        {
            BarrierHit hit = new BarrierHit
            {
                Scraping = false,
                Incidence = 0f,
                Position = position,
                Heading = heading,
            };

            if (track == null || !track.IsValid)
            {
                return hit;
            }

            Vector2 flat = new Vector2(position.x, position.z);
            segment = track.Sample(flat, segment, out Vector2 centre, out Vector2 tangent);

            Vector2 offset = flat - centre;
            float strayed = offset.magnitude;
            float limit = Limit(track, margin);
            if (strayed <= limit)
            {
                return hit;
            }

            hit.Scraping = true;

            Vector2 outward = strayed > 1e-4f
                ? offset / strayed
                : new Vector2(-tangent.y, tangent.x);

            Vector2 travel = new Vector2(forward.x, forward.z);
            Vector2 along = new Vector2(-outward.y, outward.x);
            if (Vector2.Dot(travel, along) < 0f)
            {
                along = -along;
            }

            hit.Incidence = Mathf.Abs(Vector2.Dot(travel, outward));

            float barrierHeading = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            hit.Heading = Mathf.MoveTowardsAngle(heading, barrierHeading,
                steer * hit.Incidence * deltaTime);

            Vector2 held = centre + outward * limit;
            hit.Position = new Vector3(held.x, position.y, held.y);
            return hit;
        }

        public static float Limit(TrackPathSO track, float margin)
        {
            return Mathf.Max(track.HalfWidth - margin, 0.1f);
        }

        public static void DrawGizmos(TrackPathSO track, float height, float margin)
        {
            if (track == null || !track.IsValid)
            {
                return;
            }

            float limit = Mathf.Max(track.HalfWidth - margin, 0f);

            for (int i = 0; i < track.SegmentCount; i++)
            {
                Vector2 from = track.GetPoint(i);
                Vector2 to = track.GetPoint(i + 1);
                Vector2 normal = new Vector2(-(to.y - from.y), to.x - from.x).normalized;

                Vector3 a = new Vector3(from.x, height, from.y);
                Vector3 b = new Vector3(to.x, height, to.y);
                Vector3 offset = new Vector3(normal.x, 0f, normal.y) * limit;

                Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
                Gizmos.DrawLine(a, b);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(a + offset, b + offset);
                Gizmos.DrawLine(a - offset, b - offset);
            }
        }
    }
}