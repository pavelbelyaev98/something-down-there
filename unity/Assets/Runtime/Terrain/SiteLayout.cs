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
        public const int DepthCells = 800;
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
        public static float OpeningRadius(float compass)
        {
            float c = compass * Mathf.Deg2Rad, s = Mathf.Sin(c), k = Mathf.Cos(c);
            float campDistance = Mathf.Abs(Mathf.DeltaAngle(compass, CampCompass)) - CampHalfArc;
            float t = Mathf.Clamp01(campDistance / 25), weight = t * t * (3 - 2 * t);
            float ellipse = 1 / Mathf.Sqrt(s * s / (15.6f * 15.6f) + k * k / (11f * 11f));
            float lobes = 1.2f * Mathf.Sin(3 * c + 4.8f) + .8f * Mathf.Sin(5 * c + 1.5f) + .35f * Mathf.Sin(7 * c + 1.9f);
            return CampOpeningRadius + weight * (ellipse + lobes - CampOpeningRadius);
        }

        public static float OpeningRadius(Vector2 xz) => OpeningRadius(Compass(xz));

        // Metres beyond the opening outline along the bearing; negative inside the plot.
        public static float BeyondOpening(Vector2 xz) => xz.magnitude - OpeningRadius(xz);

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
            for (int i = 0; i < reach.Length; i++) reach[i] = OpeningRadius(i * .5f);
            var corner = new Vector2(Origin.x, Origin.z);
            return xz =>
            {
                var site = xz + corner;
                return site.magnitude <= reach[Mathf.RoundToInt(Compass(site) * 2) % reach.Length];
            };
        }
    }
}
