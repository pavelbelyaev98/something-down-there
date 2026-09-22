using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Positive density is soil; zero is the surface. Samples are shared by all chunks.
    public sealed class ExcavationGrid
    {
        // One shared bound for the grid, checkpoints and the save reader. It covers the
        // planned 200 m site (1600 cells) with headroom; the save payload cap is the
        // real memory guard.
        public const int MaximumCellsPerAxis = 2048;
        private readonly PagedDensity density;
        private readonly int strideY, strideZ;
        private readonly float band;
        private readonly List<int> severedSamples = new List<int>(4096);
        private readonly List<int> supportVisited = new List<int>(4096);
        private readonly List<int> supportPending = new List<int>(4096);
        private byte[] supportState; // 0 unknown, 1 current search, 2 anchored in this stroke.
        private readonly List<int> remnantSeeds = new List<int>(2048);
        private readonly List<int> remnantComponent = new List<int>(64);
        private readonly List<int> remnantAttachments = new List<int>(64);
        private readonly List<int> remnantRemoval = new List<int>(256);
        // Local, reused cache: 1 thin, 2 thick, 3 in this component, 4 retained.
        private readonly Dictionary<int, byte> remnantState = new Dictionary<int, byte>(2048);
        private const float RemnantMaxWidth = 0.25f, RemnantMaxExtent = 0.5f, RemnantMaxVolume = 0.03f;
        private int lowestCarvedY;
        public Vector3Int Size { get; }
        public float CellSize { get; }
        public Vector3 Extent => (Vector3)Size * CellSize;
        public int Revision { get; private set; }
        public float RemovedVolume { get; private set; }
        public float LastRemovedVolume { get; private set; }
        public float LastDetachedVolume { get; private set; }
        public int LastDetachedSamples { get; private set; }
        public int LastSupportVisitedSamples { get; private set; }
        public int LastRemnantSamples { get; private set; }
        public float LastRemnantVolume { get; private set; }
        public int LastRemnantCheckedSamples { get; private set; }
        public long SnapshotCopiedBytes => density.CopiedBytes;

        public ExcavationGrid(Vector3Int size, float cellSize)
        {
            if (size.x < 1 || size.y < 1 || size.z < 1
                || size.x > MaximumCellsPerAxis || size.y > MaximumCellsPerAxis || size.z > MaximumCellsPerAxis)
                throw new ArgumentOutOfRangeException(nameof(size), $"Use 1-{MaximumCellsPerAxis} cells per axis.");
            if (!Finite(cellSize) || cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            Size = size;
            CellSize = cellSize;
            band = cellSize * 2f;
            strideY = size.x + 1;
            strideZ = strideY * (size.y + 1);
            density = new PagedDensity(strideZ * (size.z + 1));
            Reset();
        }

        public GridSnapshot Capture() => new GridSnapshot { Size = Size, CellSize = CellSize, Revision = Revision,
            RemovedVolume = RemovedVolume, LowestCarvedY = lowestCarvedY, Density = density.Capture() };

        public void Restore(GridSnapshot snapshot)
        {
            snapshot.Validate();
            if (snapshot.Size != Size || snapshot.CellSize != CellSize) throw new ArgumentException("Terrain size differs from this checkpoint.");
            density.Restore(snapshot.Density);
            Revision = snapshot.Revision;
            RemovedVolume = snapshot.RemovedVolume;
            lowestCarvedY = snapshot.LowestCarvedY;
            LastRemovedVolume = LastDetachedVolume = LastRemnantVolume = 0;
            LastDetachedSamples = LastSupportVisitedSamples = LastRemnantSamples = LastRemnantCheckedSamples = 0;
            severedSamples.Clear(); remnantState.Clear(); ClearSupportSearch();
        }

        public void Reset()
        {
            for (int z = 0; z <= Size.z; z++)
            for (int y = 0; y <= Size.y; y++)
            for (int x = 0; x <= Size.x; x++)
                density[x + y * strideY + z * strideZ] = Mathf.Min(band, (Size.y - y) * CellSize);
            Revision = 0;
            RemovedVolume = LastRemovedVolume = LastDetachedVolume = 0;
            LastDetachedSamples = LastSupportVisitedSamples = 0;
            LastRemnantSamples = LastRemnantCheckedSamples = 0;
            LastRemnantVolume = 0;
            remnantSeeds.Clear();
            remnantState.Clear();
            lowestCarvedY = Size.y;
            severedSamples.Clear();
            ClearSupportSearch();
        }

        // Horizontal ghost samples extend the border; mesh vertices are clipped at the
        // permanent walls. There is no removable outer shell hiding those walls.
        public float Sample(int x, int y, int z)
        {
            if (y > Size.y) return -(y - Size.y) * CellSize;
            return density[Mathf.Clamp(x, 0, Size.x) + Mathf.Clamp(y, 0, Size.y) * strideY
                + Mathf.Clamp(z, 0, Size.z) * strideZ];
        }

        public float Sample(Vector3 point)
        {
            Vector3 p = point / CellSize;
            var a = Vector3Int.FloorToInt(p);
            Vector3 t = p - (Vector3)a;
            float bottom = Mathf.Lerp(
                Mathf.Lerp(Sample(a.x, a.y, a.z), Sample(a.x + 1, a.y, a.z), t.x),
                Mathf.Lerp(Sample(a.x, a.y + 1, a.z), Sample(a.x + 1, a.y + 1, a.z), t.x), t.y);
            float top = Mathf.Lerp(
                Mathf.Lerp(Sample(a.x, a.y, a.z + 1), Sample(a.x + 1, a.y, a.z + 1), t.x),
                Mathf.Lerp(Sample(a.x, a.y + 1, a.z + 1), Sample(a.x + 1, a.y + 1, a.z + 1), t.x), t.y);
            return Mathf.Lerp(bottom, top, t.z);
        }

        // Untouched samples are the analytic base field; digging only lowers them. A chunk
        // whose cell corners still equal that base cannot own a surface, so rebuilding it
        // would produce an empty mesh. A sample feeds every cell touching it, so the test
        // covers one halo sample on each side of the chunk's cell block.
        public bool AnyModified(Vector3Int start, int size)
        {
            if (size < 1) return false;
            int x0 = Mathf.Max(0, start.x - 1), x1 = Mathf.Min(Size.x, start.x + size + 1);
            int y0 = Mathf.Max(0, start.y - 1), y1 = Mathf.Min(Size.y, start.y + size + 1);
            int z0 = Mathf.Max(0, start.z - 1), z1 = Mathf.Min(Size.z, start.z + size + 1);
            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            {
                float untouched = Mathf.Min(band, (Size.y - y) * CellSize);
                int row = x0 + y * strideY + z * strideZ;
                for (int x = x0; x <= x1; x++)
                    if (density[row + x - x0] != untouched) return true;
            }
            return false;
        }

        public Vector3 SurfaceNormal(Vector3 point)
        {
            float h = CellSize * 0.5f;
            var gradient = new Vector3(
                Sample(point + Vector3.right * h) - Sample(point - Vector3.right * h),
                Sample(point + Vector3.up * h) - Sample(point - Vector3.up * h),
                Sample(point + Vector3.forward * h) - Sample(point - Vector3.forward * h));
            return gradient.sqrMagnitude > 1e-12f ? -gradient.normalized : Vector3.up;
        }

        public bool IsSolid(Vector3 point) => Finite(point.x) && Finite(point.y) && Finite(point.z)
            && point.x >= 0 && point.y >= 0 && point.z >= 0
            && point.x < Extent.x && point.y < Extent.y && point.z < Extent.z && Sample(point) > 0;

        public bool RemoveSphere(Vector3 center, float radius, out BoundsInt changed)
            => RemoveBrush(center, radius, Vector3.up, 0, 0, false, 0, out changed);

        public bool RemoveScoop(Vector3 center, float radius, int seed, float variation, out BoundsInt changed)
            => RemoveScoop(center, radius, Vector3.up, seed, variation, out changed);

        public bool RemoveScoop(Vector3 center, float radius, Vector3 normal, int seed, float variation, out BoundsInt changed)
            => RemoveBrush(center, radius, normal, seed, variation, true, 0, out changed);

        public bool RemoveShave(Vector3 surface, float radius, Vector3 normal, float depth, out BoundsInt changed)
        {
            changed = default;
            if (!Finite(depth) || depth <= 0 || depth > radius) return false;
            return RemoveBrush(surface, radius, normal, 0, 0, false, depth, out changed);
        }

        private bool RemoveBrush(Vector3 center, float radius, Vector3 normal, int seed, float variation,
            bool shovel, float shaveDepth, out BoundsInt changed)
        {
            changed = default;
            LastRemovedVolume = LastDetachedVolume = 0;
            LastDetachedSamples = LastSupportVisitedSamples = 0;
            LastRemnantSamples = LastRemnantCheckedSamples = 0;
            LastRemnantVolume = 0;
            severedSamples.Clear();
            remnantSeeds.Clear();
            if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z)
                || !Finite(radius) || radius <= 0f || radius > 1000f
                || !Finite(variation) || variation < 0 || variation > 0.15f
                || !Finite(normal.x) || !Finite(normal.y) || !Finite(normal.z)
                || !Finite(normal.sqrMagnitude) || normal.sqrMagnitude < 0.0001f) return false;
            // Covers the bevelled, tapered bite in every orientation, including its
            // outward cap. The density halo must fit too for matching chunk normals.
            float maximumRadius = radius * (shovel ? 1.8f : 1f);
            float influence = maximumRadius + band;
            Vector3 extent = Extent;
            if (center.x + maximumRadius < 0 || center.y + maximumRadius < 0 || center.z + maximumRadius < 0
                || center.x - maximumRadius > extent.x || center.y - maximumRadius > extent.y || center.z - maximumRadius > extent.z)
                return false;
            // A local hash never consumes UnityEngine.Random (discovery owns its own
            // randomness). Low spatial frequencies keep silhouettes and normals smooth.
            uint random = unchecked((uint)seed) ^ 0x9e3779b9u;
            Quaternion rotation = Quaternion.Euler(Next01(ref random) * 360, Next01(ref random) * 360, Next01(ref random) * 360);
            Vector3 axisA = rotation * Vector3.right * (3.8f / radius);
            Vector3 axisB = rotation * Vector3.up * (4.6f / radius);
            Vector3 axisC = rotation * Vector3.forward * (3.1f / radius);
            Vector3 phase = new Vector3(Next01(ref random), Next01(ref random), Next01(ref random)) * (2 * Mathf.PI);
            normal.Normalize();
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.95f ? Vector3.up : Vector3.forward).normalized;
            tangent = Quaternion.AngleAxis(Next01(ref random) * 360, normal) * tangent;

            Vector3 bitangent = Vector3.Cross(normal, tangent);
            float width = radius * Mathf.Lerp(1.02f, 1.14f, Next01(ref random));
            float length = radius * Mathf.Lerp(0.84f, 0.96f, Next01(ref random));
            float depth = radius * Mathf.Lerp(0.68f, 0.82f, Next01(ref random));
            float tiltX = Mathf.Lerp(-0.16f, 0.16f, Next01(ref random));
            float tiltZ = Mathf.Lerp(-0.12f, 0.12f, Next01(ref random));
            float amplitude = radius * variation, bevel = radius * 0.24f;
            Vector3 boundsCenter = center, boundsExtent = Vector3.one * influence;

            Vector3Int first = Vector3Int.Max(Vector3Int.zero,
                Vector3Int.FloorToInt((boundsCenter - boundsExtent) / CellSize));
            Vector3Int last = Vector3Int.Min(Size,
                Vector3Int.CeilToInt((boundsCenter + boundsExtent) / CellSize));
            Vector3Int changedMin = Size + Vector3Int.one, changedMax = -Vector3Int.one;
            float unitVolume = CellSize * CellSize * CellSize;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int index = x + y * strideY + z * strideZ;
                float before = density[index];
                if (before <= -band) continue;
                Vector3 delta = new Vector3(x, y, z) * CellSize - center;
                float cut;

                if (shaveDepth > 0)
                {
                    float height = Vector3.Dot(delta, normal);
                    float radial = Mathf.Sqrt(Mathf.Max(0, delta.sqrMagnitude - height * height));
                    float side = radial - radius;
                    float floor = -height - shaveDepth;
                    float rounding = Mathf.Min(radius * 0.18f, shaveDepth * 0.5f);
                    float join = Mathf.Max(rounding - Mathf.Abs(side - floor), 0) / rounding;
                    cut = Mathf.Max(side, floor) + join * join * rounding * 0.25f;
                    cut = Mathf.Max(cut, height - radius);
                }
                else if (shovel)
                {
                    float u = Vector3.Dot(delta, tangent), v = Vector3.Dot(delta, bitangent);
                    float height = Vector3.Dot(delta, normal);
                    float floor = -height - depth + u * tiltX + v * tiltZ;
                    float cap = height - radius * 0.8f;
                    if (Mathf.Max(floor, cap) - amplitude >= before) continue;
                    // A broad, slanted fracture face instead of a spherical bottom.
                    // Taper and a rounded superellipse soften the lip without making
                    // a hemisphere; oblique clipped shoulders break the stamped rim.
                    float taper = 1 - 0.16f * Mathf.Clamp01(-height / radius);
                    float a = Mathf.Abs(u / (width * taper)), b = Mathf.Abs(v / (length * taper));
                    // The superellipse is at least max(a,b). Reject unchanged samples
                    // with that cheap bound before powers/noise, especially in deep pits.
                    if ((Mathf.Max(a, b) - 1) * length - amplitude >= before) continue;
                    float side = (Mathf.Pow(Mathf.Pow(a, 2.8f) + Mathf.Pow(b, 2.8f), 1f / 2.8f) - 1) * length;
                    side = Mathf.Max(side, (u * 0.72f + v * 0.69f - radius * 0.98f) * 0.9f);
                    side = Mathf.Max(side, (-u * 0.86f - v * 0.51f - radius * 0.94f) * 0.9f);
                    float join = Mathf.Max(bevel - Mathf.Abs(side - floor), 0) / bevel;
                    cut = Mathf.Max(side, floor) + join * join * bevel * 0.25f;
                    cut = Mathf.Max(cut, cap);
                    if (cut - amplitude >= before) continue;
                    float ripple = variation == 0 ? 0 : 0.5f * Mathf.Sin(Vector3.Dot(delta, axisA) + phase.x)
                        + 0.3f * Mathf.Sin(Vector3.Dot(delta, axisB) + phase.y)
                        + 0.2f * Mathf.Sin(Vector3.Dot(delta, axisC) + phase.z);
                    cut -= amplitude * ripple;
                }
                else cut = delta.magnitude - radius;
                float after = Mathf.Max(-band, Mathf.Min(before, cut));
                if (before - after < 0.00001f) continue;
                density[index] = after;
                if (before > 0) remnantSeeds.Add(index);
                if (before > 0 && after <= 0)
                {
                    severedSamples.Add(index);
                    lowestCarvedY = Mathf.Min(lowestCarvedY, y);
                }
                // Sample quadrature in m3, with half weights at finite-domain boundaries.
                float weight = (x == 0 || x == Size.x ? 0.5f : 1f)
                    * (y == 0 || y == Size.y ? 0.5f : 1f) * (z == 0 || z == Size.z ? 0.5f : 1f);
                LastRemovedVolume += (Mathf.Clamp01(0.5f + before / CellSize)
                    - Mathf.Clamp01(0.5f + after / CellSize)) * unitVolume * weight;
                var sample = new Vector3Int(x, y, z);
                changedMin = Vector3Int.Min(changedMin, sample);
                changedMax = Vector3Int.Max(changedMax, sample);
            }
            if (changedMax.x < 0) return false;
            RemoveDetachedSoil(ref changedMin, ref changedMax);
            RemovePaperThinSlivers(ref changedMin, ref changedMax);
            RemoveTinyRemnants(ref changedMin, ref changedMax);
            if (LastRemnantSamples > 0) RemoveDetachedSoil(ref changedMin, ref changedMax);
            changed = new BoundsInt(changedMin, changedMax - changedMin + Vector3Int.one);
            RemovedVolume += LastRemovedVolume;
            Revision++;
            return true;
        }

        private void ClearSupportSearch()
        {
            foreach (int index in supportVisited) supportState[index] = 0;
            supportVisited.Clear();
            supportPending.Clear();
        }

        private void RemoveTinyRemnants(ref Vector3Int changedMin, ref Vector3Int changedMax)
        {
            // At the production 12.5 cm grid, clear protrusions narrower than 25 cm,
            // at most 50 cm long and 30 litres. A 60 cm player capsule cannot use
            // these as a ledge. Larger sheets/bridges stay unless paper-thin.
            float maxWidth = Mathf.Min(CellSize * 2, RemnantMaxWidth);
            int maxSamples = Mathf.Clamp(Mathf.CeilToInt(RemnantMaxVolume / (CellSize * CellSize * CellSize * 0.5f)), 1, 128);
            while (remnantSeeds.Count > 0)
            {
                remnantState.Clear();
                remnantRemoval.Clear();
                foreach (int seed in remnantSeeds)
                {
                    CheckRemnant(seed, maxWidth, maxSamples);
                    var p = SampleCoordinates(seed);
                    if (p.x > 0) CheckRemnant(seed - 1, maxWidth, maxSamples);
                    if (p.x < Size.x) CheckRemnant(seed + 1, maxWidth, maxSamples);
                    if (p.y > 0) CheckRemnant(seed - strideY, maxWidth, maxSamples);
                    if (p.y < Size.y) CheckRemnant(seed + strideY, maxWidth, maxSamples);
                    if (p.z > 0) CheckRemnant(seed - strideZ, maxWidth, maxSamples);
                    if (p.z < Size.z) CheckRemnant(seed + strideZ, maxWidth, maxSamples);
                }
                LastRemnantCheckedSamples += remnantState.Count;
                remnantSeeds.Clear();
                // Classify a whole pass before changing density, so order cannot
                // turn a retained bridge/sheet into independently removable tips.
                foreach (int index in remnantRemoval)
                    RemoveRemnantSample(index, ref changedMin, ref changedMax);
                // Only neighbours of removed remnants need another pass. This
                // settles within this stroke; idle time and repeated stale hits
                // cannot slowly erode terrain or leave an extra collider frame.
            }
        }

        private void RemovePaperThinSlivers(ref Vector3Int changedMin, ref Vector3Int changedMax)
        {
            // Sub-voxel ribbons can be long or touch two walls, so the ordinary
            // tip limits retain them. Follow only their fragile cross-section
            // from this stroke, stopping at thicker soil and permanent borders.
            remnantState.Clear();
            remnantComponent.Clear();
            foreach (int seed in remnantSeeds)
            {
                VisitSliver(seed);
                VisitSliverNeighbours(seed);
            }
            for (int cursor = 0; cursor < remnantComponent.Count; cursor++)
                VisitSliverNeighbours(remnantComponent[cursor]);
            LastRemnantCheckedSamples += remnantState.Count;
            foreach (int index in remnantComponent)
                RemoveRemnantSample(index, ref changedMin, ref changedMax);
        }

        private void VisitSliverNeighbours(int index)
        {
            var p = SampleCoordinates(index);
            if (p.x > 0) VisitSliver(index - 1);
            if (p.x < Size.x) VisitSliver(index + 1);
            if (p.y > 0) VisitSliver(index - strideY);
            if (p.y < Size.y) VisitSliver(index + strideY);
            if (p.z > 0) VisitSliver(index - strideZ);
            if (p.z < Size.z) VisitSliver(index + strideZ);
        }

        private void VisitSliver(int index)
        {
            if (density[index] <= 0 || remnantState.ContainsKey(index)) return;
            remnantState.Add(index, 1);
            var p = SampleCoordinates(index);
            if (p.x < 2 || p.x > Size.x - 2 || p.y < 2 || p.y >= Size.y
                || p.z < 2 || p.z > Size.z - 2 || p.y < lowestCarvedY) return;
            float maxWidth = Mathf.Min(CellSize * .5f, .0625f);
            if (ThinAcross(index, 1, maxWidth) || ThinAcross(index, strideY, maxWidth, Size.y - p.y)
                || ThinAcross(index, strideZ, maxWidth)) remnantComponent.Add(index);
        }

        private void RemoveRemnantSample(int index, ref Vector3Int changedMin, ref Vector3Int changedMax)
        {
            var p = SampleCoordinates(index);
            float removed = Mathf.Clamp01(0.5f + density[index] / CellSize) * CellSize * CellSize * CellSize;
            density[index] = -band;
            LastRemovedVolume += removed;
            LastRemnantVolume += removed;
            LastRemnantSamples++;
            lowestCarvedY = Mathf.Min(lowestCarvedY, p.y);
            severedSamples.Add(index);
            remnantSeeds.Add(index);
            changedMin = Vector3Int.Min(changedMin, p);
            changedMax = Vector3Int.Max(changedMax, p);
        }

        private byte RemnantKind(int index, float maxWidth)
        {
            if (density[index] <= 0) return 0;
            if (remnantState.TryGetValue(index, out byte kind)) return kind;
            var p = SampleCoordinates(index);
            // Keep the permanent boundary attachment band and unedited deep soil.
            bool thin = p.x >= 2 && p.x <= Size.x - 2 && p.y >= 2 && p.y < Size.y
                && p.z >= 2 && p.z <= Size.z - 2 && p.y >= lowestCarvedY
                && (ThinAcross(index, 1, maxWidth) || ThinAcross(index, strideY, maxWidth, Size.y - p.y)
                    || ThinAcross(index, strideZ, maxWidth));
            kind = thin ? (byte)1 : (byte)2;
            remnantState.Add(index, kind);
            return kind;
        }

        private bool ThinAcross(int index, int stride, float maxWidth, int topDistance = 2)
        {
            float width = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                float previous = density[index];
                bool surface = false;
                for (int step = 1; step <= 2; step++)
                {
                    int neighbour = index + side * stride * step;
                    // Only the top can reach outside the field (the other faces
                    // have a two-sample guard). The top ghost density is air.
                    float next = side > 0 && step > topDistance ? -(step - topDistance) * CellSize : density[neighbour];
                    if (next <= 0)
                    {
                        width += (step - 1 + previous / (previous - next)) * CellSize;
                        surface = true;
                        break;
                    }
                    previous = next;
                }
                if (!surface || width > maxWidth) return false;
            }
            return width <= maxWidth;
        }

        private void CheckRemnant(int seed, float maxWidth, int maxSamples)
        {
            if (RemnantKind(seed, maxWidth) != 1) return;
            remnantComponent.Clear();
            remnantAttachments.Clear();
            remnantComponent.Add(seed);
            remnantState[seed] = 3;
            Vector3Int min = SampleCoordinates(seed), max = min;
            float volume = 0;
            bool keep = false;
            for (int cursor = 0; cursor < remnantComponent.Count && !keep; cursor++)
            {
                int index = remnantComponent[cursor];
                var p = SampleCoordinates(index);
                min = Vector3Int.Min(min, p);
                max = Vector3Int.Max(max, p);
                Vector3 extent = (Vector3)(max - min + Vector3Int.one) * CellSize;
                volume += Mathf.Clamp01(0.5f + density[index] / CellSize) * CellSize * CellSize * CellSize;
                keep = volume > RemnantMaxVolume || extent.x > RemnantMaxExtent
                    || extent.y > RemnantMaxExtent || extent.z > RemnantMaxExtent;
                VisitRemnant(index - 1, maxWidth, ref keep);
                VisitRemnant(index + 1, maxWidth, ref keep);
                VisitRemnant(index - strideY, maxWidth, ref keep);
                VisitRemnant(index + strideY, maxWidth, ref keep);
                VisitRemnant(index - strideZ, maxWidth, ref keep);
                VisitRemnant(index + strideZ, maxWidth, ref keep);
                keep |= remnantComponent.Count > maxSamples;
            }
            // A thin neck between two larger bodies must not remove a useful
            // supported roof/crown. Contacts must be one contiguous attachment.
            if (!keep && remnantAttachments.Count > 1) keep = HasSeparateAttachments(maxWidth);
            foreach (int index in remnantComponent) remnantState[index] = 4;
            if (!keep) remnantRemoval.AddRange(remnantComponent);
        }

        private void VisitRemnant(int index, float maxWidth, ref bool keep)
        {
            byte kind = RemnantKind(index, maxWidth);
            if (kind == 4) keep = true;
            else if (kind == 1)
            {
                remnantState[index] = 3;
                remnantComponent.Add(index);
            }
            else if (kind == 2 && !remnantAttachments.Contains(index)) remnantAttachments.Add(index);
        }

        private bool HasSeparateAttachments(float maxWidth)
        {
            // Small bounded contact set; partition in place with no allocations.
            int connected = 1;
            for (int cursor = 0; cursor < connected; cursor++)
            {
                for (int i = connected; i < remnantAttachments.Count; i++)
                {
                    if (!ContactsJoinThroughCore(remnantAttachments[cursor], remnantAttachments[i], maxWidth)) continue;
                    int swap = remnantAttachments[connected];
                    remnantAttachments[connected++] = remnantAttachments[i];
                    remnantAttachments[i] = swap;
                }
            }
            return connected != remnantAttachments.Count;
        }

        private bool ContactsJoinThroughCore(int from, int to, float maxWidth)
        {
            var delta = SampleCoordinates(to) - SampleCoordinates(from);
            if (Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1 || Mathf.Abs(delta.z) > 1) return false;
            // A diagonal wall's contacts may meet around a thick sample that does
            // not itself touch the remnant. Require a solid-edge path through core
            // soil, never a diagonal shortcut across air or another thin neck.
            for (int axis = 0; axis < 3; axis++)
            {
                int step = axis == 0 ? delta.x : axis == 1 ? delta.y * strideY : delta.z * strideZ;
                if (step == 0) continue;
                int next = from + step;
                if (next == to || (RemnantKind(next, maxWidth) == 2 && ContactsJoinThroughCore(next, to, maxWidth))) return true;
            }
            return false;
        }

        private void RemoveDetachedSoil(ref Vector3Int changedMin, ref Vector3Int changedMax)
        {
            if (severedSamples.Count == 0) return;
            if (supportState == null) supportState = new byte[density.Length];
            ClearSupportSearch();
            // The initial field is connected. Deleting samples can only detach a
            // component next to a newly cut solid edge; no whole-site scan is needed.
            foreach (int index in severedSamples)
            {
                var p = SampleCoordinates(index);
                if (p.x > 0) CheckSupport(index - 1, ref changedMin, ref changedMax);
                if (p.x < Size.x) CheckSupport(index + 1, ref changedMin, ref changedMax);
                if (p.y > 0) CheckSupport(index - strideY, ref changedMin, ref changedMax);
                if (p.y < Size.y) CheckSupport(index + strideY, ref changedMin, ref changedMax);
                if (p.z > 0) CheckSupport(index - strideZ, ref changedMin, ref changedMax);
                if (p.z < Size.z) CheckSupport(index + strideZ, ref changedMin, ref changedMax);
            }
            LastSupportVisitedSamples += supportVisited.Count;
        }

        private Vector3Int SampleCoordinates(int index)
            => new Vector3Int(index % strideY, index / strideY % (Size.y + 1), index / strideZ);

        private bool VisitSupport(int index)
        {
            if (density[index] <= 0) return false;
            if (supportState[index] == 2) return true;
            if (supportState[index] == 0)
            {
                supportState[index] = 1;
                supportVisited.Add(index);
                supportPending.Add(index);
            }
            return false;
        }

        private void CheckSupport(int seed, ref Vector3Int changedMin, ref Vector3Int changedMax)
        {
            if (density[seed] <= 0 || supportState[seed] != 0) return;
            int first = supportVisited.Count;
            VisitSupport(seed);
            bool anchored = false;
            while (supportPending.Count > 0 && !anchored)
            {
                int last = supportPending.Count - 1;
                int index = supportPending[last];
                supportPending.RemoveAt(last);
                var p = SampleCoordinates(index);
                // Untouched layers below the deepest cut still join the bedrock.
                // The top face is deliberately not an anchor: surface islands vanish.
                anchored = p.y < lowestCarvedY || p.y == 0 || p.x == 0 || p.x == Size.x
                    || p.z == 0 || p.z == Size.z;
                if (anchored) break;
                // Solid sample edges define support. Iterative depth-first traversal
                // prefers downward paths and stops as soon as anchorage is proven.
                anchored = (p.y < Size.y && VisitSupport(index + strideY))
                    || (p.z > 0 && VisitSupport(index - strideZ))
                    || (p.z < Size.z && VisitSupport(index + strideZ))
                    || (p.x > 0 && VisitSupport(index - 1))
                    || (p.x < Size.x && VisitSupport(index + 1))
                    || (p.y > 0 && VisitSupport(index - strideY));
            }
            supportPending.Clear();
            float unitVolume = CellSize * CellSize * CellSize;
            for (int i = first; i < supportVisited.Count; i++)
            {
                int index = supportVisited[i];
                if (anchored) { supportState[index] = 2; continue; }
                var p = SampleCoordinates(index);
                float weight = (p.x == 0 || p.x == Size.x ? 0.5f : 1f)
                    * (p.y == 0 || p.y == Size.y ? 0.5f : 1f) * (p.z == 0 || p.z == Size.z ? 0.5f : 1f);
                float removed = Mathf.Clamp01(0.5f + density[index] / CellSize) * unitVolume * weight;
                density[index] = -band;
                LastRemovedVolume += removed;
                LastDetachedVolume += removed;
                LastDetachedSamples++;
                changedMin = Vector3Int.Min(changedMin, p);
                changedMax = Vector3Int.Max(changedMax, p);
            }
        }

        private static float Next01(ref uint state)
        {
            unchecked { state = state * 1664525u + 1013904223u; }
            return (state >> 8) * (1f / 16777216f);
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
