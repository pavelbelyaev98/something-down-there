using UnityEngine;

namespace SomethingDownThere
{
    // The shipped MainGame site in one place. The scene owns the serialized copy;
    // editor tooling, bounds checks and tests read these numbers from here so the
    // reservoir depth is a single authored value. Deepening appends soil below the
    // surface, which is why the origin moves down with the depth.
    public static class SiteLayout
    {
        public const float CellSize = 0.125f;
        public const int DepthCells = 1200;
        // East-west is wider than north-south; the grid stays inside the save sample budget.
        public const int WidthCells = 288;
        public const int LengthCells = 208;
        public const int ChunkSize = 16;
        // Permanent lakebed ground around the opening sits just above the voxel top.
        public const float GroundTop = .03f;
        // The rim collar covers the terrain-hole edge along the opening outline; its closed
        // underside roofs the rest of the grid, which stays reachable by lateral digging.
        public const float RimTop = .045f;
        public const float RimBand = .9f;
        // The collar's inner edge sits just above the dig surface and bevels up to RimTop.
        public const float RimLip = .004f, RimBevel = .35f;
        // Keep the fixed surface above the shallowest saved finds.
        public const float RimBottom = -.01f;
        // The stations, winch and spawn stand on this arc; the rest of the plot widens.
        public const float CampOpeningRadius = 12f;
        public const float CampCompass = 170f, CampHalfArc = 45f;
        public static readonly Vector3Int Size = new Vector3Int(WidthCells, DepthCells, LengthCells);
        public static readonly Vector3 Extent = new Vector3(WidthCells * CellSize, DepthCells * CellSize, LengthCells * CellSize);
        public static readonly Vector3 Origin = new Vector3(-Extent.x * .5f, -Extent.y, -Extent.z * .5f);

        // Compass bearing of a site-local XZ point: 0 is north (+Z), 90 is east (+X).
        public static float Compass(Vector2 xz) => Mathf.Repeat(Mathf.Atan2(xz.x, xz.y) * Mathf.Rad2Deg, 360);

        // Irregular dig plot outline: a wide east-west ellipse with lobes and bays, always inside
        // the grid. The camp arc keeps the tested 12 m circle for the stations and winch.
        public static float PlotRadius(float compass)
        {
            float c = compass * Mathf.Deg2Rad, s = Mathf.Sin(c), k = Mathf.Cos(c);
            float ellipse = 1 / Mathf.Sqrt(s * s / (15.6f * 15.6f) + k * k / (11f * 11f));
            float lobes = 1.2f * Mathf.Sin(3 * c + 4.8f) + .8f * Mathf.Sin(5 * c + 1.5f) + .35f * Mathf.Sin(7 * c + 1.9f);
            return CampOpeningRadius + CampFade(compass) * (ellipse + lobes - CampOpeningRadius);
        }

        // The physical edge (collar, terrain hole, markers, bank): the plot outline with small bumps
        // and bays every few metres, so cut edges never follow one clean curve. They fade out on the
        // camp arc and where the collar needs the grid's last metre.
        public const float EdgeWobble = .38f;

        public static float OpeningRadius(float compass)
        {
            float c = compass * Mathf.Deg2Rad, radius = PlotRadius(compass);
            float margin = Mathf.Min(Extent.x * .5f - Mathf.Abs(Mathf.Sin(c)) * radius, Extent.z * .5f - Mathf.Abs(Mathf.Cos(c)) * radius);
            float room = Mathf.Clamp01((margin - 1) / EdgeWobble);
            float wobble = .32f * Mathf.Sin(13 * c + 2.3f) + .37f * Mathf.Sin(23 * c + 4.1f) + .31f * Mathf.Sin(41 * c + 5.3f);
            return radius + EdgeWobble * wobble * CampFade(compass) * room;
        }

        private static float CampFade(float compass)
        {
            float t = Mathf.Clamp01((Mathf.Abs(Mathf.DeltaAngle(compass, CampCompass)) - CampHalfArc) / 25);
            return t * t * (3 - 2 * t);
        }

        public static float OpeningRadius(Vector2 xz) => OpeningRadius(Compass(xz));

        // Find centres stay a find's reach (DiscoveryField.MaximumFindRadius) inside the physical
        // edge, so no find is ever embedded in the permanent bank.
        public const float FindInset = .5f;
        public static float FootprintRadius(float compass) => OpeningRadius(compass) - FindInset;

        // Permanent soil beyond the edge: tools never remove it within this depth, so pit edges are
        // soil walls and lateral digging under the site starts below it.
        public const float BankDepth = 1.2f;

        // Per grid column (x + z * (Size.x + 1)), metres beyond the physical edge; null for other
        // grids (fixtures, benchmarks), which have no bank.
        public static float[] BankColumns(Vector3Int size, float cellSize)
        {
            if (size != Size || !Mathf.Approximately(cellSize, CellSize)) return null;
            var columns = new float[(size.x + 1) * (size.z + 1)];
            for (int z = 0; z <= size.z; z++)
            for (int x = 0; x <= size.x; x++)
                columns[x + z * (size.x + 1)] = BeyondOpening(new Vector2(Origin.x + x * cellSize, Origin.z + z * cellSize));
            return columns;
        }

        // Metres beyond the opening outline along the bearing; negative inside the plot.
        public static float BeyondOpening(Vector2 xz) => xz.magnitude - OpeningRadius(xz);

        // Metres beyond the smooth find footprint; negative inside it.
        public static float BeyondFootprint(Vector2 xz) => xz.magnitude - FootprintRadius(Compass(xz));

        public static Vector2 OpeningPoint(float compass, float offset = 0)
        {
            float c = compass * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(c), Mathf.Cos(c)) * (OpeningRadius(compass) + offset);
        }

        // Find centres lie beneath the plot (larger finds reach under its collar), so the plot keeps
        // the accepted find density; lateral digging beyond meets plain soil. Other extents
        // (fixtures, benchmarks) fill their whole grid. The predicate takes grid-local XZ metres.
        public static System.Func<Vector2, bool> FindFootprint(Vector3 extent)
        {
            if (extent != Extent) return null;
            var reach = new float[720];
            for (int i = 0; i < reach.Length; i++) reach[i] = FootprintRadius(i * .5f);
            var corner = new Vector2(Origin.x, Origin.z);
            return xz =>
            {
                var site = xz + corner;
                return site.magnitude <= reach[Mathf.RoundToInt(Compass(site) * 2) % reach.Length];
            };
        }
    }
}
