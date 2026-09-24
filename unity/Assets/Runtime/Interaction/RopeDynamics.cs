using UnityEngine;

namespace SomethingDownThere
{
    public interface IRopeCollision
    {
        Vector3 Project(Vector3 from, Vector3 point, float radius);
    }

    // The two attachments are prescribed by the winch/load. Interior particles
    // retain velocity; route waypoints only seed the initial cable.
    public sealed class RopeDynamics
    {
        public const int Capacity = 160;
        private readonly Vector3[] positions = new Vector3[Capacity];
        private readonly Vector3[] previous = new Vector3[Capacity];
        private readonly Vector3[] renderPrevious = new Vector3[Capacity];
        private readonly Vector3[] safe = new Vector3[Capacity];
        private readonly Vector3[] scratch = new Vector3[Capacity];
        private readonly Vector3[] scratchPrevious = new Vector3[Capacity];
        private readonly float[] arc = new float[Capacity];
        private float previousStep;
        public int Count { get; private set; }
        public Vector3 Position(int index) => positions[index];
        public Vector3 RenderPosition(int index, float alpha) => Vector3.Lerp(renderPrevious[index], positions[index], alpha);
        public void Clear() { Count = 0; previousStep = 0; }

        public void Seed(Vector3[] path, int pathCount, float spacing)
        {
            float length = 0;
            for (int i = 1; i < pathCount; i++) length += Vector3.Distance(path[i - 1], path[i]);
            Count = Mathf.Clamp(Mathf.CeilToInt(length / spacing) + 1, 2, Capacity);
            int segment = 1;
            float consumed = 0;
            for (int i = 0; i < Count; i++)
            {
                float distance = length * i / (Count - 1);
                while (segment < pathCount - 1 && consumed + Vector3.Distance(path[segment - 1], path[segment]) < distance)
                { consumed += Vector3.Distance(path[segment - 1], path[segment]); segment++; }
                float span = Vector3.Distance(path[segment - 1], path[segment]);
                positions[i] = Vector3.Lerp(path[segment - 1], path[segment], span > .00001f ? (distance - consumed) / span : 0);
                previous[i] = renderPrevious[i] = positions[i];
            }
            previousStep = 0;
        }

        public void Step(float dt, Vector3 start, Vector3 end, float length, float spacing,
            float damping, int iterations, float radius, IRopeCollision collision)
        {
            if (Count < 2 || dt <= 0 || !float.IsFinite(dt)) return;
            dt = Mathf.Min(dt, .05f);
            length = Mathf.Max(length, Vector3.Distance(start, end));
            Resize(Mathf.Clamp(Mathf.CeilToInt(length / spacing) + 1, 2, Capacity));
            for (int i = 0; i < Count; i++) renderPrevious[i] = positions[i];
            Vector3 oldStart = positions[0], oldEnd = positions[Count - 1];
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / .01f), 1, 5);
            float h = dt / steps, rest = Mathf.Max(.001f, length / (Count - 1));
            float drag = Mathf.Exp(-damping * h);
            for (int step = 0; step < steps; step++)
            {
                for (int i = 1; i < Count - 1; i++)
                {
                    safe[i] = positions[i];
                    Vector3 velocity = previousStep > 0 ? (positions[i] - previous[i]) * (h / previousStep) : Vector3.zero;
                    previous[i] = positions[i];
                    positions[i] += Vector3.ClampMagnitude(velocity * drag, h * 12f) + Physics.gravity * (h * h);
                }
                float fraction = (float)(step + 1) / steps;
                previous[0] = positions[0]; previous[Count - 1] = positions[Count - 1];
                positions[0] = Vector3.Lerp(oldStart, start, fraction);
                positions[Count - 1] = Vector3.Lerp(oldEnd, end, fraction);
                for (int pass = 0; pass < iterations; pass++)
                {
                    for (int link = 0; link < Count - 1; link++)
                    {
                        int a = (pass & 1) == 0 ? link : Count - 2 - link;
                        SolveDistance(a, rest);
                    }
                    for (int i = 1; i < Count - 1; i++)
                    {
                        LimitReach(i, 0, rest * i);
                        LimitReach(i, Count - 1, rest * (Count - 1 - i));
                    }
                    if (collision != null && ((pass + 1) % 4 == 0 || pass == iterations - 1)) Collide(collision, radius);
                }
                previousStep = h;
            }
        }

        private void SolveDistance(int a, float rest)
        {
            int b = a + 1;
            float wa = a == 0 ? 0 : 1, wb = b == Count - 1 ? 0 : 1;
            Vector3 delta = positions[b] - positions[a];
            float distance = delta.magnitude;
            if (distance < .00001f || wa + wb == 0) return;
            Vector3 correction = delta * ((distance - rest) / (distance * (wa + wb)));
            positions[a] += correction * wa;
            positions[b] -= correction * wb;
        }

        private void LimitReach(int point, int anchor, float reach)
        {
            Vector3 offset = positions[point] - positions[anchor];
            if (offset.sqrMagnitude > reach * reach)
                positions[point] = positions[anchor] + offset.normalized * reach;
        }

        private void Collide(IRopeCollision collision, float radius)
        {
            for (int i = 1; i < Count - 1; i++)
            {
                Vector3 point = collision.Project(safe[i], positions[i], radius);
                if ((point - positions[i]).sqrMagnitude > .0000001f)
                {
                    Vector3 normal = (point - positions[i]).normalized;
                    Vector3 velocity = Vector3.ProjectOnPlane(point - previous[i], normal) * .82f;
                    previous[i] = point - velocity;
                }
                positions[i] = safe[i] = point;
            }
            // Even with both particles in air, their segment can cross a lip.
            for (int i = 0; i < Count - 1; i++)
            {
                Vector3 mid = (positions[i] + positions[i + 1]) * .5f;
                Vector3 correction = collision.Project(mid, mid, radius) - mid;
                float weight = (i > 0 ? 1 : 0) + (i + 1 < Count - 1 ? 1 : 0);
                if (weight == 0 || correction.sqrMagnitude < .0000001f) continue;
                correction *= 2f / weight;
                if (i > 0) { positions[i] += correction; previous[i] += correction; }
                if (i + 1 < Count - 1) { positions[i + 1] += correction; previous[i + 1] += correction; }
            }
        }

        private void Resize(int count)
        {
            // Reeling resamples the live curve and velocity, not the route.
            if (count == Count) return;
            arc[0] = 0;
            for (int i = 1; i < Count; i++) arc[i] = arc[i - 1] + Vector3.Distance(positions[i - 1], positions[i]);
            int segment = 1;
            for (int i = 0; i < count; i++)
            {
                float distance = arc[Count - 1] * i / (count - 1);
                while (segment < Count - 1 && arc[segment] < distance) segment++;
                float span = arc[segment] - arc[segment - 1];
                float t = span > .00001f ? (distance - arc[segment - 1]) / span : 0;
                scratch[i] = Vector3.Lerp(positions[segment - 1], positions[segment], t);
                scratchPrevious[i] = Vector3.Lerp(previous[segment - 1], previous[segment], t);
            }
            Count = count;
            for (int i = 0; i < Count; i++) { positions[i] = scratch[i]; previous[i] = scratchPrevious[i]; }
        }
    }
}
