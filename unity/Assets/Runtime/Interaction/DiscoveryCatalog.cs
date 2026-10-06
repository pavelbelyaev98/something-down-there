using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    // Generated from the editable source catalog. IDs describe items, never mesh GUIDs.
    [CreateAssetMenu(menuName = "Something Down There/Discovery catalog")]
    public sealed class DiscoveryCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string ItemId;
            public bool AuthoredPlacement;
            public Vector3 AuthoredPosition, AuthoredEuler;
            public BuriedFind Prefab;
            // One catalog item, several saved appearances with identical gameplay specifications.
            public BuriedFind[] AppearanceVariants = Array.Empty<BuriedFind>();
            // Zero-count entries remain resolvable for saved populations without spawning anew.
            public int Count, ShallowCount;
            // Soil above the enclosing sphere. Zero maximum keeps legacy shallow placement.
            public float ShallowMinCover, ShallowMaxCover;
            // Metres below the unchanged surface. Zero retains the legacy placement rule.
            public float MinDepth, MaxDepth;
            // Where the bulk of a type lives: CoreShare of its banded finds land inside the
            // core band, the rest scatter through MinDepth..MaxDepth for variety. Zero share
            // keeps the legacy single-band rule.
            public float CoreMinDepth, CoreMaxDepth, CoreShare;
            public bool LayOnSide, RandomOrientation;
            // Host ground (concept 03 §4): at the same depth, ground listed here carries its weight
            // times the find density of unlisted ground (weight 1). Soft bias with scatter; the
            // depth bands and prices never change.
            public TerrainMaterialId[] HostGrounds = Array.Empty<TerrainMaterialId>();
            public float[] HostWeights = Array.Empty<float>();
            public int AppearanceCount => 1 + (AppearanceVariants?.Length ?? 0);
            // Half the find's height lying level: it rests that far above a chest's floor seat.
            public float RestingHalfHeight
            {
                get
                {
                    var filter = Prefab.GetComponent<MeshFilter>();
                    return filter.sharedMesh.bounds.extents.y * Mathf.Abs(filter.transform.localScale.y);
                }
            }
            // Half the find's footprint lying level, corner to corner: how far it reaches across a chest's floor.
            public float RestingReach
            {
                get
                {
                    var filter = Prefab.GetComponent<MeshFilter>();
                    var extents = Vector3.Scale(filter.sharedMesh.bounds.extents, filter.transform.localScale);
                    return new Vector2(extents.x, extents.z).magnitude;
                }
            }
            public BuriedFind Appearance(int index) => index == 0 ? Prefab : AppearanceVariants[index - 1];
            public float PlacementRadius
            {
                get
                {
                    float radius = 0;
                    for (int i = 0; i < AppearanceCount; i++)
                    {
                        var filter = Appearance(i).GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null)
                            throw new InvalidDataException("Discovery placement requires an approved mesh.");
                        var scale = filter.transform.localScale;
                        // A box diagonal reserves its empty corners as if they were rock.
                        // Actual vertices enclose the whole triangular mesh in any rotation.
                        foreach (var vertex in filter.sharedMesh.vertices)
                            radius = Mathf.Max(radius, Vector3.Scale(vertex, scale).magnitude);
                        var collider = Appearance(i).GetComponent<MeshCollider>();
                        if (collider != null && collider.sharedMesh != null)
                            foreach (var vertex in collider.sharedMesh.vertices)
                                radius = Mathf.Max(radius, Vector3.Scale(vertex, scale).magnitude);
                    }
                    return radius;
                }
            }
        }
        public Entry[] Entries = Array.Empty<Entry>();
        // The stash pits' old chest (106) and what it holds: ChestItems finds per chest, each drawn by weight.
        // A chest's draw weight for a content type at the top of the recent fill (ShallowWeight) and at its bottom
        // (DeepWeight), in between by depth: the deeper a chest, the richer (user, 2026-10-06).
        [Serializable] public sealed class ChestContent
        {
            public string ItemId; public float ShallowWeight = 1, DeepWeight = 1;
            public float WeightAt(float depth) => Mathf.Lerp(ShallowWeight, DeepWeight, Mathf.InverseLerp(TerrainGround.PitTop,
                TerrainGround.ZoneBorders[0] - TerrainGround.PitMargin, depth));
        }
        public BuriedChest Chest;
        public int ChestItems;
        public ChestContent[] ChestContents = Array.Empty<ChestContent>();
        public int TotalCount { get { int total = 0; foreach (var e in Entries) total += e.Count; return total; } }
        public int ShallowCount { get { int total = 0; foreach (var e in Entries) total += e.ShallowCount; return total; } }

        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int shallow = 0;
            if (Entries == null || Entries.Length == 0) throw new InvalidDataException("Missing discovery catalog.");
            foreach (var e in Entries)
            {
                if (e == null || e.Prefab == null || string.IsNullOrWhiteSpace(e.Prefab.SaveContentId)
                    || !ids.Add(e.Prefab.SaveContentId) || e.Count < 0 || e.ShallowCount < 0 || e.ShallowCount > e.Count
                    || !ExcavationGrid.Finite(e.MinDepth) || !ExcavationGrid.Finite(e.MaxDepth)
                    || e.MinDepth < 0 || e.MaxDepth < 0 || (e.MinDepth > 0 && e.MaxDepth == 0)
                    || (e.MaxDepth > 0 && e.MaxDepth <= e.MinDepth))
                    throw new InvalidDataException("Invalid discovery catalog entry.");
                if (!ExcavationGrid.Finite(e.CoreMinDepth) || !ExcavationGrid.Finite(e.CoreMaxDepth)
                    || !ExcavationGrid.Finite(e.CoreShare) || e.CoreShare < 0 || e.CoreShare > 1
                    || (e.CoreShare > 0 && (e.CoreMinDepth < e.MinDepth || e.CoreMaxDepth > e.MaxDepth
                        || e.CoreMaxDepth <= e.CoreMinDepth)))
                    throw new InvalidDataException("Invalid discovery core band.");
                if (e.Prefab.Kind == DiscoveryKind.Unique && (!e.AuthoredPlacement || e.Count != 1 || e.ShallowCount != 0
                    || e.Prefab.Recovery != RecoveryMethod.Rope || e.Prefab.SaleValue != 0 || !e.Prefab.DetectorEligible || !e.Prefab.HasLore))
                    throw new InvalidDataException("Invalid unique discovery policy.");
                if (e.AuthoredPlacement && (e.Count != 1 || e.ShallowCount != 0 || !WorldSnapshot.Valid(e.AuthoredPosition) || !WorldSnapshot.Valid(e.AuthoredEuler)))
                    throw new InvalidDataException("Invalid authored discovery placement.");
                if ((e.HostGrounds?.Length ?? 0) != (e.HostWeights?.Length ?? 0)
                    || (e.HostGrounds != null && Array.Exists(e.HostGrounds, g => g > TerrainMaterialSnapshot.Last))
                    || (e.HostWeights != null && Array.Exists(e.HostWeights, w => !ExcavationGrid.Finite(w) || w < 1)))
                    throw new InvalidDataException("Invalid discovery host ground.");
                shallow += e.ShallowCount;
                if (!ExcavationGrid.Finite(e.ShallowMinCover) || !ExcavationGrid.Finite(e.ShallowMaxCover)
                    || e.ShallowMinCover < 0 || e.ShallowMaxCover < 0
                    || (e.ShallowMaxCover == 0 && e.ShallowMinCover != 0)
                    || (e.ShallowMaxCover > 0 && (e.ShallowMinCover < .01f || e.ShallowMaxCover <= e.ShallowMinCover)))
                    throw new InvalidDataException("Invalid shallow soil cover.");
                for (int i = 1; i < e.AppearanceCount; i++)
                {
                    var appearance = e.Appearance(i);
                    if (appearance == null || string.IsNullOrWhiteSpace(appearance.SaveContentId)
                        || !ids.Add(appearance.SaveContentId) || appearance.DisplayName != e.Prefab.DisplayName
                        || appearance.SaleValue != e.Prefab.SaleValue
                        || appearance.DetectorEligible != e.Prefab.DetectorEligible
                        || appearance.Kind != e.Prefab.Kind || appearance.Recovery != e.Prefab.Recovery
                        || !Mathf.Approximately(appearance.RequiredExposure, e.Prefab.RequiredExposure))
                        throw new InvalidDataException("Item appearances must have unique save keys and matching gameplay specifications.");
                }
            }
            if (TotalCount > DiscoveryField.MaximumPopulation || shallow < 1) throw new InvalidDataException("Starter allocation requires shallow finds and a supported total.");
            if (Chest != null && (ChestItems < 1 || ChestItems > Chest.ContentSeats.Length || ChestContents == null || ChestContents.Length == 0
                || Array.Exists(ChestContents, c => c == null || c.ShallowWeight < 0 || c.DeepWeight < 0 || c.ShallowWeight + c.DeepWeight <= 0
                    || ContentIndex(c.ItemId) < 0
                    || Entries[ContentIndex(c.ItemId)].Prefab.Kind != DiscoveryKind.Common)))
                throw new InvalidDataException("Invalid chest contents.");
        }

        private int ContentIndex(string itemId) => Array.FindIndex(Entries, e => e.ItemId == itemId);

        public BuriedFind Resolve(string id)
        {
            foreach (var entry in Entries)
                for (int i = 0; i < entry.AppearanceCount; i++)
                    if (entry.Appearance(i).SaveContentId == id) return entry.Appearance(i);
            throw new InvalidDataException("This save needs discovery content missing from this game version.");
        }

        // ground: grid-local material sampler (the excavation's immutable field); null ignores host ground.
        // groundLayout: the excavation's pits and stashes; a find settles at every seat (rubbish pit bottoms).
        public DiscoveryPlacement[] Generate(Vector3 extent, int seed, Func<Vector3, TerrainMaterialId> ground = null, TerrainGround.GroundLayout groundLayout = null)
        {
            Validate();
            var shallow = new List<int>(); var remaining = new List<int>();
            for (int i = 0; i < Entries.Length; i++)
                for (int n = 0; n < (Entries[i].AuthoredPlacement ? 0 : Entries[i].Count); n++)
                    (n < Entries[i].ShallowCount ? shallow : remaining).Add(i);
            var random = new System.Random(unchecked(seed ^ 0x45A7123));
            var appearances = new System.Random(unchecked(seed ^ 0x72BD139));
            Shuffle(shallow, random); Shuffle(remaining, random); shallow.AddRange(remaining);
            var entryRadii = new float[Entries.Length];
            for (int i = 0; i < Entries.Length; i++) entryRadii[i] = Entries[i].PlacementRadius;
            var radii = new float[shallow.Count];
            var bands = new Vector2[shallow.Count];
            var covers = new Vector2[shallow.Count];
            for (int i = 0; i < radii.Length; i++) radii[i] = entryRadii[shallow[i]];
            // The dense core holds the identity of a type's depth; the wider band scatters
            // the few outliers that keep every layer from reading as a recipe.
            var coreLeft = new int[Entries.Length];
            for (int i = 0; i < coreLeft.Length; i++)
                coreLeft[i] = Mathf.RoundToInt((Entries[i].Count - Entries[i].ShallowCount) * Entries[i].CoreShare);
            for (int i = 0; i < bands.Length; i++)
            {
                var entry = Entries[shallow[i]];
                covers[i] = new Vector2(entry.ShallowMinCover, entry.ShallowMaxCover);
                if (i >= ShallowCount && coreLeft[shallow[i]] > 0)
                {
                    bands[i] = new Vector2(entry.CoreMinDepth, entry.CoreMaxDepth);
                    coreLeft[shallow[i]]--;
                }
                else bands[i] = new Vector2(entry.MinDepth, entry.MaxDepth);
            }
            var reserved = new List<DiscoveryReservation>();
            var authored = new List<DiscoveryPlacement>();
            for (int i = 0; i < Entries.Length; i++)
            {
                var entry = Entries[i]; if (!entry.AuthoredPlacement) continue;
                // Ordinary finds also keep out of the unique's space (TerrainGround.OddSpotReach).
                Vector3 p = entry.AuthoredPosition; float r = entryRadii[i] + DiscoveryField.SoilClearance;
                if (p.x < r || p.y < r || p.z < r || p.x > extent.x-r || p.y > extent.y-r || p.z > extent.z-r)
                    throw new InvalidDataException("Authored unique does not fit inside untouched soil.");
                foreach (var other in reserved) if (Vector3.Distance(p,other.Position) < r+other.Radius)
                    throw new InvalidDataException("Authored discoveries overlap.");
                reserved.Add(new DiscoveryReservation(p,entryRadii[i] + TerrainGround.OddSpotReach));
                authored.Add(new DiscoveryPlacement(p,Quaternion.Euler(entry.AuthoredEuler),i));
            }
            // Each stash's chest takes its contents from the population (SeatChests): counts and bands are unchanged.
            // Ordinary finds keep out of every stash's chest and the pocket it stands in.
            Vector3[] seats = null;
            var stashes = groundLayout != null && Chest != null ? groundLayout.Stashes : Array.Empty<TerrainGround.Stash>();
            var chestTurns = new Dictionary<int, Quaternion>();
            if (groundLayout != null)
            {
                seats = new Vector3[radii.Length];
                for (int i = 0; i < seats.Length; i++) seats[i] = new Vector3(float.NaN, 0, 0);
                foreach (var stash in stashes)
                    foreach (var (centre, radius) in Chest.PocketReserves())
                        reserved.Add(new DiscoveryReservation((Vector3)stash.Centre + (Quaternion)stash.Rotation * centre, radius + DiscoveryField.SoilClearance));
                SeatChests(stashes, extent, seed, shallow, bands, seats, chestTurns);
            }
            Func<int, Vector3, float> weight = null;
            if (ground != null)
                weight = (i, position) =>
                {
                    var entry = Entries[shallow[i]];
                    return entry.HostGrounds == null || entry.HostGrounds.Length == 0 ? 1
                        : HostWeight(entry.HostGrounds, entry.HostWeights, ground, position);
                };
            var layout = DiscoveryField.Generate(extent, shallow.Count, seed, ShallowCount, radii, bands, covers, reserved.ToArray(),
                SiteLayout.FindFootprint(extent), weight, seats);
            for (int i = 0; i < layout.Length; i++)
            {
                int index = shallow[i];
                float yaw = (float)random.NextDouble() * 360;
                float tilt = ((float)random.NextDouble() - .5f) * 24;
                bool side = Entries[index].LayOnSide && random.NextDouble() < .85;
                var rotation = Quaternion.Euler((side ? 90 : 0) + tilt, yaw, ((float)random.NextDouble() - .5f) * 18);
                if (Entries[index].RandomOrientation)
                {
                    // Uniform quaternion sampling: every 3D orientation is equally likely.
                    double u = appearances.NextDouble(), v = appearances.NextDouble() * Math.PI * 2,
                        w = appearances.NextDouble() * Math.PI * 2;
                    rotation = new Quaternion((float)(Math.Sqrt(1-u)*Math.Sin(v)), (float)(Math.Sqrt(1-u)*Math.Cos(v)),
                        (float)(Math.Sqrt(u)*Math.Sin(w)), (float)(Math.Sqrt(u)*Math.Cos(w)));
                }
                if (chestTurns.TryGetValue(i, out var turn)) rotation = turn;
                layout[i] = new DiscoveryPlacement(layout[i].Position, rotation, index, appearances.Next(Entries[index].AppearanceCount));
            }
            var result = new List<DiscoveryPlacement>(layout); result.AddRange(authored); return result.ToArray();
        }

        // What each stash's chest holds (106): its seats take ChestItems finds of a type drawn by its weight at the chest's
        // depth, like any seat the next of that type whose depth band covers the chest (another content type when none does),
        // so counts never change. They lie heaped (ChestHeap) in the chest's seeded hollow, loose from New Game.
        private void SeatChests(TerrainGround.Stash[] stashes, Vector3 extent, int seed, List<int> order, Vector2[] bands,
            Vector3[] seats, Dictionary<int, Quaternion> turns)
        {
            var random = new System.Random(unchecked(seed ^ 0x5C4E5713));
            foreach (var stash in stashes)
            {
                Quaternion chest = stash.Rotation;
                float depth = extent.y - stash.Centre.y;
                float total = 0; foreach (var content in ChestContents) total += content.WeightAt(depth);
                var heap = new ChestHeap(Chest, random);
                for (int k = 0; k < ChestItems; k++)
                {
                    float pick = (float)random.NextDouble() * total; int choice = 0;
                    while (choice < ChestContents.Length - 1 && pick >= ChestContents[choice].WeightAt(depth)) pick -= ChestContents[choice++].WeightAt(depth);
                    for (int attempt = 0; attempt < ChestContents.Length; attempt++)
                    {
                        int entry = ContentIndex(ChestContents[(choice + attempt) % ChestContents.Length].ItemId), seated = -1;
                        for (int i = ShallowCount; i < seats.Length && seated < 0; i++)
                            if (order[i] == entry && float.IsNaN(seats[i].x) && bands[i].y > 0 && depth >= bands[i].x && depth <= bands[i].y) seated = i;
                        if (seated < 0) continue;
                        var (at, lie) = heap.Place(Chest.ContentSeats[k], Entries[entry]);
                        seats[seated] = (Vector3)stash.Centre + chest * at;
                        turns[seated] = chest * lie;
                        break;
                    }
                }
            }
        }

        // A chest's contents heaped at its back (user, 2026-10-06: evenly spaced looked laid out). Each find takes its seat
        // on the floor turned up to ChestTurn; one that reaches over finds already lying there rests on the highest of them,
        // tipped up to ChestTip, so it settles leaning on them. One that would stand above the walls' rim lies further
        // toward the lock, HeapStep at a time; after HeapSteps it lies level on the floor there, and the others make room
        // as they settle (the chest is only about a crystal deep inside).
        public const float ChestTurn = 80f, ChestTip = 20f;
        private const float HeapStep = .06f;
        private const int HeapSteps = 5;

        internal sealed class ChestHeap
        {
            private readonly BuriedChest chest;
            private readonly System.Random random;
            private readonly List<(Vector2 at, float reach, float top)> placed = new List<(Vector2, float, float)>();

            public ChestHeap(BuriedChest chest, System.Random random) { this.chest = chest; this.random = random; }

            // The find's position and turn in the chest's frame, seated at `seat` on the floor.
            public (Vector3 position, Quaternion lie) Place(Vector3 seat, Entry entry)
            {
                float half = entry.RestingHalfHeight, reach = entry.RestingReach;
                var turn = Quaternion.Euler(0, (float)(random.NextDouble() - .5) * ChestTurn, 0);
                float tipAround = (float)random.NextDouble() * 360, tip = (float)random.NextDouble() * ChestTip;
                var tipped = Quaternion.AngleAxis(tip, Quaternion.Euler(0, tipAround, 0) * Vector3.right) * turn;
                // How far a tipped find reaches below and above its centre at most.
                float tippedHalf = half + reach * Mathf.Sin(tip * Mathf.Deg2Rad);
                var ahead = new Vector2(chest.Front.x, chest.Front.z).normalized;
                for (int step = 0; ; step++)
                {
                    var at = new Vector2(seat.x, seat.z) + ahead * (step * HeapStep);
                    float under = seat.y;
                    foreach (var p in placed)
                        if ((p.at - at).magnitude < p.reach + reach) under = Mathf.Max(under, p.top);
                    bool resting = under > seat.y;
                    float rise = (resting ? tippedHalf : half) + .01f;
                    if (under + 2 * rise > chest.Rim)
                    {
                        if (step < HeapSteps) continue;
                        under = seat.y; resting = false; rise = half + .01f;
                    }
                    placed.Add((at, reach, under + 2 * rise));
                    return (new Vector3(at.x, under + rise, at.y), resting ? tipped : turn);
                }
            }
        }

        // The ground at the find's centre decides.
        private static float HostWeight(TerrainMaterialId[] hosts, float[] weights, Func<Vector3, TerrainMaterialId> ground, Vector3 centre)
        {
            int host = Array.IndexOf(hosts, ground(centre));
            return host >= 0 ? weights[host] : 1;
        }

        // Uniques' spaces for the terrain (grid-local centre, envelope radius): the discovery sync copies them
        // onto TerrainVolume so its pits keep clear of them.
        public Vector4[] OddSpots()
        {
            var spots = new List<Vector4>();
            foreach (var entry in Entries)
                if (entry.AuthoredPlacement)
                    spots.Add(new Vector4(entry.AuthoredPosition.x, entry.AuthoredPosition.y, entry.AuthoredPosition.z,
                        entry.PlacementRadius + DiscoveryField.SoilClearance));
            return spots.ToArray();
        }

        private static void Shuffle(List<int> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); int value = values[i]; values[i] = values[j]; values[j] = value; }
        }
    }
}
