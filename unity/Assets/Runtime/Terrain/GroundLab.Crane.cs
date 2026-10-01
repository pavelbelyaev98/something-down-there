using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // The Ground Lab's crane scenes: six holes dug around the bays in plain soil, each holding a computer
    // ready to mark, so the crane's recovery and its effects (breaks, dust, the rupture tell, the dug
    // ground's light) can be watched and compared without digging first. Computers the crane sets down at
    // camp clear away after a few seconds so its spots stay free.
    public static partial class GroundLab
    {
        // Every computer has a pocket around it down to a little below its middle (it needs 60% bare to
        // mark), so its base still sits in the soil and the dug way above decides how the haul goes.
        private const float PocketClearance = .15f, PocketBelow = .2f, PocketAbove = .65f;
        private static readonly Vector3 ComputerHalf = new Vector3(.58f, .45f, .31f);

        public readonly struct CraneScene
        {
            public readonly string Name, Hint;
            // World: the computer's centre and its turn about up (degrees), then the way dug down to it.
            public readonly Vector3 Load;
            public readonly float Turn;
            public readonly (Vector3 a, Vector3 b, float radius)[] Tubes;
            // Loose rocks set half into the shaft wall, from 1 m down, every 0.4 m.
            public readonly int Rocks;
            public CraneScene(string name, string hint, Vector3 load, float turn, int rocks, params (Vector3, Vector3, float)[] tubes)
            { Name = name; Hint = hint; Load = load; Turn = turn; Rocks = rocks; Tubes = tubes; }
        }

        // Outside the bays (south, north and east of them), inside the plot outline and the crane's reach.
        public static readonly CraneScene[] CraneScenes =
        {
            new CraneScene("Straight shaft", "1.7 m wide, 4.5 m down", new Vector3(-4, -4.5f, -8.5f), 0, 0,
                (new Vector3(-4, 1, -8.5f), new Vector3(-4, -3.9f, -8.5f), .85f)),
            new CraneScene("Narrow shaft", "0.9 m wide onto a wider computer: it tears its way up", new Vector3(-1, -3.5f, -8.5f), 0, 0,
                (new Vector3(-1, 1, -8.5f), new Vector3(-1, -2.9f, -8.5f), .45f)),
            new CraneScene("Rocks in the walls", "a shaft lined with loose rocks", new Vector3(2, -3.5f, -8.5f), 0, 6,
                (new Vector3(2, 1, -8.5f), new Vector3(2, -2.9f, -8.5f), .85f)),
            new CraneScene("Open pit", "1.4 m bowl: a clean lift with nothing in the way", new Vector3(2, -1.2f, 6.6f), 0, 0,
                (new Vector3(2, .7f, 6.6f), new Vector3(2, .7f, 6.6f), 2.1f)),
            new CraneScene("Deep shaft", "11 m down: daylight fades", new Vector3(7, -11, 6.5f), 0, 0,
                (new Vector3(7, 1, 6.5f), new Vector3(7, -10.4f, 6.5f), 1)),
            new CraneScene("Bent tunnel", "6 m down, then 3.5 m south and 2 m east", new Vector3(14.4f, -6, 0), 0, 0,
                (new Vector3(12, 1, 3.5f), new Vector3(12, -5.6f, 3.5f), .8f),
                (new Vector3(12, -5.6f, 3.5f), new Vector3(12, -5.6f, 0), .65f),
                (new Vector3(12, -5.6f, 0), new Vector3(14, -5.6f, 0), .65f)),
        };

        private static Vector3 Local(Vector3 world) => world - SiteLayout.Origin;

        // Grid-local air for every crane scene.
        private static void AddCraneCarves(List<ExcavationGrid.LabCarve> carves)
        {
            foreach (var scene in CraneScenes)
            {
                foreach (var (a, b, radius) in scene.Tubes) carves.Add(ExcavationGrid.LabCarve.Tube(Local(a), Local(b), radius));
                Vector3 half = Quaternion.Euler(0, scene.Turn, 0) * ComputerHalf;
                half = new Vector3(Mathf.Abs(half.x), 0, Mathf.Abs(half.z)) + new Vector3(PocketClearance, 0, PocketClearance);
                carves.Add(ExcavationGrid.LabCarve.Box(Local(scene.Load + new Vector3(-half.x, -PocketBelow, -half.z)),
                    Local(scene.Load + new Vector3(half.x, PocketAbove, half.z))));
            }
        }

        // World poses of the crane scenes' finds: each scene's computer, then its rocks.
        public static IEnumerable<(bool computer, Vector3 position, Quaternion rotation)> CraneFinds()
        {
            foreach (var scene in CraneScenes)
            {
                yield return (true, scene.Load, Quaternion.Euler(0, scene.Turn, 0));
                if (scene.Rocks == 0) continue;
                var (top, _, radius) = scene.Tubes[0];
                for (int i = 0; i < scene.Rocks; i++)
                {
                    float angle = i * 137.5f * Mathf.Deg2Rad;
                    var side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var at = new Vector3(top.x, -1 - .4f * i, top.z) + side * radius;
                    yield return (false, at, Quaternion.Euler(i * 41, i * 73, i * 29));
                }
            }
        }

        // The crane scene whose dug way is under the crosshair, or null.
        private static string DescribeCrane(Vector3 world)
        {
            var p = new Vector2(world.x, world.z);
            foreach (var scene in CraneScenes)
            {
                bool near = Vector2.Distance(p, new Vector2(scene.Load.x, scene.Load.z)) < 2.5f;
                foreach (var (a, b, radius) in scene.Tubes)
                {
                    Vector2 from = new Vector2(a.x, a.z), axis = new Vector2(b.x, b.z) - from;
                    float t = axis.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - from, axis) / axis.sqrMagnitude) : 0;
                    near |= Vector2.Distance(p, from + axis * t) < radius + 1;
                }
                if (near) return $"{scene.Name}: {scene.Hint}";
            }
            return null;
        }
    }
}
