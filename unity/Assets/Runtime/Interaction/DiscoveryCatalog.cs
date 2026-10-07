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
            // Lines geodes only (110): its instances are the geodes' seats (SeatGeodes), never loose in the ground.
            public bool Geode;
            // Lines caves only (115): a cave's crystals, one kind to a cave (SeatCaves), never loose in the ground.
            public bool Cave;
            // A great cave's crystal trophy (116): a unique the generator seats on the floor of its zone's great cave
            // (SeatTrophies) instead of at an authored position.
            public bool CaveTrophy;
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
                if (e.Prefab.Kind == DiscoveryKind.Unique && (e.AuthoredPlacement == e.CaveTrophy || e.Count != 1 || e.ShallowCount != 0
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
            // A trophy is seated, never spaced among loose finds: its spacing radius stops at the largest loose find's.
            for (int i = 0; i < radii.Length; i++)
                radii[i] = Entries[shallow[i]].CaveTrophy ? Mathf.Min(entryRadii[shallow[i]], DiscoveryField.MaximumLargeFindRadius) : entryRadii[shallow[i]];
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
            // Each geode's crystals likewise (SeatGeodes); ordinary finds keep out of its shell.
            Vector3[] seats = null;
            var stashes = groundLayout != null && Chest != null ? groundLayout.Stashes : Array.Empty<TerrainGround.Stash>();
            var seatTurns = new Dictionary<int, Quaternion>();
            if (groundLayout != null)
            {
                seats = new Vector3[radii.Length];
                for (int i = 0; i < seats.Length; i++) seats[i] = new Vector3(float.NaN, 0, 0);
                foreach (var stash in stashes)
                {
                    foreach (var (centre, radius) in Chest.PocketReserves())
                        reserved.Add(new DiscoveryReservation((Vector3)stash.Centre + (Quaternion)stash.Rotation * centre, radius + DiscoveryField.SoilClearance));
                    var dome = TerrainGround.PocketDomeReserve(stash);
                    reserved.Add(new DiscoveryReservation(dome.centre, dome.radius + DiscoveryField.SoilClearance));
                }
                foreach (var geode in groundLayout.Geodes)
                    reserved.Add(new DiscoveryReservation(geode.Centre, geode.Reach + DiscoveryField.SoilClearance));
                foreach (var cavern in groundLayout.Caverns)
                    for (int c = 0; c < cavern.Centres.Length; c++)
                        reserved.Add(new DiscoveryReservation(cavern.Centres[c], Unity.Mathematics.math.cmax(cavern.Radii[c]) + cavern.Shell
                            + (cavern.Great ? TerrainGround.GreatWarp : TerrainGround.CavernWarp) + DiscoveryField.SoilClearance));
                SeatChests(stashes, extent, seed, shallow, bands, seats, seatTurns);
                SeatGeodes(groundLayout.Geodes, extent, seed, shallow, bands, seats, seatTurns);
                // Trophies first: the caves' crystals keep clear of them.
                var trophies = SeatTrophies(groundLayout.Caverns, extent, seed, shallow, seats, seatTurns);
                SeatCaves(groundLayout.Caverns, extent, seed, shallow, seats, seatTurns, trophies);
            }
            Func<int, Vector3, float> weight = null;
            if (ground != null)
                weight = (i, position) =>
                {
                    var entry = Entries[shallow[i]];
                    return entry.HostGrounds == null || entry.HostGrounds.Length == 0 ? 1
                        : HostWeight(entry.HostGrounds, entry.HostWeights, ground, position);
                };
            // No find aims for the depths a great cave's hall fills.
            var gaps = new List<Vector2>();
            if (groundLayout != null)
                foreach (var cavern in groundLayout.Caverns)
                    if (cavern.Great) gaps.Add(new Vector2(extent.y - cavern.Max.y, extent.y - cavern.Min.y));
            var layout = DiscoveryField.Generate(extent, shallow.Count, seed, ShallowCount, radii, bands, covers, reserved.ToArray(),
                SiteLayout.FindFootprint(extent), weight, seats, gaps.ToArray());
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
                if (seatTurns.TryGetValue(i, out var turn)) rotation = turn;
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
        // tipped up to ChestTip, so it settles leaning on them. One that would stand above the walls' rim (the chest is only
        // about a crystal deep inside) takes the nearest place on the floor, a HeapStep grid inside its walls, where it fits
        // on the floor or on what lies there. Finds never start inside each other: pushed apart, one went through the floor.
        public const float ChestTurn = 80f, ChestTip = 20f;
        private const float HeapStep = .06f;
        // A find's centre keeps this share of its reach from the inner walls.
        private const float WallMargin = .8f;

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
                var origin = new Vector2(seat.x, seat.z);
                // The seat first, then the floor nearest it.
                (Vector2 at, float under, bool resting) best = (origin, seat.y, false);
                float bestDistance = float.MaxValue;
                void Try(Vector2 at)
                {
                    float distance = (at - origin).sqrMagnitude;
                    if (distance >= bestDistance) return;
                    float under = seat.y;
                    foreach (var p in placed)
                        if ((p.at - at).magnitude < p.reach + reach) under = Mathf.Max(under, p.top);
                    bool resting = under > seat.y;
                    if (under + 2 * ((resting ? tippedHalf : half) + .01f) > chest.Rim) return;
                    best = (at, under, resting);
                    bestDistance = distance;
                }
                Try(origin);
                Vector2 inside = chest.FloorHalf - Vector2.one * (reach * WallMargin);
                for (float x = -inside.x; x <= inside.x + 1e-4f; x += HeapStep)
                    for (float z = -inside.y; z <= inside.y + 1e-4f; z += HeapStep)
                        Try(new Vector2(x, z));
                float rise = (best.resting ? tippedHalf : half) + .01f;
                placed.Add((best.at, reach, best.under + 2 * rise));
                return (new Vector3(best.at.x, best.under + rise, best.at.y), best.resting ? tipped : turn);
            }
        }

        // What each geode holds (110): GeodeCrystals crystals of one kind (user, 2026-10-06: "one crystal type inside"),
        // the geode type whose band covers its depth with the most instances left (a tie drawn), each the next unseated
        // instance, so the instances fill the seats exactly and counts never change. Should that kind run out, the next
        // fills the rest. They line its hollow (GeodeSeat).
        public const int GeodeCrystals = 16;
        private void SeatGeodes(TerrainGround.Geode[] geodes, Vector3 extent, int seed, List<int> order, Vector2[] bands,
            Vector3[] seats, Dictionary<int, Quaternion> turns)
        {
            var random = new System.Random(unchecked(seed ^ 0x6E0DE5));
            for (int g = 0; g < geodes.Length; g++)
            {
                float depth = extent.y - geodes[g].Centre.y;
                // Unseated geode instances whose band covers this geode, by type.
                var left = new SortedDictionary<int, List<int>>();
                for (int i = ShallowCount; i < seats.Length; i++)
                    if (Entries[order[i]].Geode && float.IsNaN(seats[i].x) && bands[i].y > 0 && depth >= bands[i].x && depth <= bands[i].y)
                    {
                        if (!left.TryGetValue(order[i], out var list)) left[order[i]] = list = new List<int>();
                        list.Add(i);
                    }
                int entry = -1;
                var taken = new List<(Vector3 at, float radius)>();
                for (int k = 0; k < GeodeCrystals; k++)
                {
                    if (!left.ContainsKey(entry))
                    {
                        if (left.Count == 0) break;
                        int most = 0; foreach (var list in left.Values) most = Math.Max(most, list.Count);
                        var tied = new List<int>();
                        foreach (var pair in left) if (pair.Value.Count == most) tied.Add(pair.Key);
                        entry = tied[random.Next(tied.Count)];
                    }
                    var instances = left[entry];
                    int seated = instances[0];
                    instances.RemoveAt(0);
                    if (instances.Count == 0) left.Remove(entry);
                    // Big crystals in an uneven hollow: a few seeded tries for a seat clear of those already there, on a face
                    // that looks into the hollow (a bulge's flank can face sideways).
                    var centre = (Vector3)geodes[g].Centre;
                    bool Good(Vector3 at, Quaternion turn) => Clear(taken, at, Entries[entry].PlacementRadius)
                        && Vector3.Angle(turn * Vector3.up, centre - at) < GeodeFacing;
                    var (position, rotation) = GeodeSeat(geodes[g], k, Entries[entry], random);
                    for (int attempt = 1; attempt < GeodeSeatTries && !Good(position, rotation); attempt++)
                        (position, rotation) = GeodeSeat(geodes[g], k, Entries[entry], random);
                    taken.Add((position, Entries[entry].PlacementRadius));
                    seats[seated] = position;
                    turns[seated] = rotation;
                }
            }
        }

        // The kth crystal's seat on a geode's hollow (grid-local): the first GeodeLow round the floor and lower walls, the
        // rest higher, spread round it, each where the ray from the centre meets the hollow's face, pointing into the
        // hollow (its up along the inward normal, a seeded twist) and sunk GeodeSink of its height into the shell, so it
        // stays anchored until the shell around it is dug.
        public const float GeodeSink = 1 / 3f, GeodeFacing = 50f;
        private const int GeodeLow = 6;
        internal static (Vector3 position, Quaternion rotation) GeodeSeat(TerrainGround.Geode geode, int k, Entry entry, System.Random random)
        {
            bool low = k < GeodeLow;
            float step = 360f / Mathf.Max(1, low ? GeodeLow : GeodeCrystals - GeodeLow);
            float around = (low ? k : k - GeodeLow + .5f) * step + ((float)random.NextDouble() - .5f) * step * .6f;
            float elevation = low ? Mathf.Lerp(-55f, -15f, (float)random.NextDouble()) : Mathf.Lerp(5f, 40f, (float)random.NextDouble());
            var direction = Quaternion.Euler(-elevation, around, 0) * Vector3.forward;
            var (surface, outward) = TerrainGround.HollowFace(geode, direction);
            var inward = -(Vector3)outward;
            float half = entry.RestingHalfHeight;
            var position = (Vector3)surface + inward * (half * (1 - 2 * GeodeSink));
            var rotation = Quaternion.FromToRotation(Vector3.up, inward) * Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
            return (position, rotation);
        }

        // What each cave holds (115, 116): crystals of one kind, the kind whose band covers its floor (user, 2026-10-07: "each
        // cave MUST have only one colour"): MiniCaveCrystals in a mini cave, GreatCaveCrystals round a great cave's chambers,
        // every GreatRoofEvery-th hanging from its roof (user, 2026-10-07: "a bit more crystals ... and on the roof as well"),
        // half-buried, clear of each other and of its trophy. Instances no cave had a seat for go to a cave of their kind, the
        // fewest seated first, so none lie loose.
        public const int MiniCaveCrystals = 5, GreatCaveCrystals = 16, GreatRoofEvery = 4;
        public const float CavernSink = .45f;
        private void SeatCaves(TerrainGround.Cavern[] caves, Vector3 extent, int seed, List<int> order, Vector3[] seats,
            Dictionary<int, Quaternion> turns, List<(int cave, Vector3 at, float radius)> standing)
        {
            var random = new System.Random(unchecked(seed ^ 0x0CA7E5));
            var kinds = CaveKinds();
            if (kinds.Count == 0) return;
            var pool = new Dictionary<int, Queue<int>>();
            foreach (int kind in kinds) pool[kind] = new Queue<int>();
            for (int i = ShallowCount; i < seats.Length; i++)
                if (Entries[order[i]].Cave && float.IsNaN(seats[i].x)) pool[order[i]].Enqueue(i);
            var held = new int[caves.Length];
            var seated = new int[caves.Length];
            var taken = new List<(Vector3 at, float radius)>[caves.Length];
            void Seat(int c, int i)
            {
                (seats[i], turns[i]) = ClearCavernSeat(caves[c], seated[c] % caves[c].Centres.Length, Entries[order[i]], random, taken[c],
                    OnRoof(caves[c], seated[c]));
                seated[c]++;
            }
            for (int c = 0; c < caves.Length; c++)
            {
                taken[c] = new List<(Vector3 at, float radius)>();
                foreach (var stand in standing) if (stand.cave == c) taken[c].Add((stand.at, stand.radius));
                int kind = held[c] = KindAt(kinds, extent.y - caves[c].Floor);
                int count = caves[c].Great ? GreatCaveCrystals : MiniCaveCrystals;
                for (int k = 0; k < count && pool[kind].Count > 0; k++) Seat(c, pool[kind].Dequeue());
            }
            foreach (int kind in kinds)
                while (pool[kind].Count > 0)
                {
                    int best = -1;
                    for (int c = 0; c < caves.Length; c++)
                        if (held[c] == kind && (best < 0 || seated[c] < seated[best])) best = c;
                    if (best < 0) break;
                    Seat(best, pool[kind].Dequeue());
                }
        }

        // The caves' crystal kinds, shallow to deep.
        public List<int> CaveKinds()
        {
            var kinds = new List<int>();
            for (int i = 0; i < Entries.Length; i++) if (Entries[i].Cave) kinds.Add(i);
            kinds.Sort((a, b) => Entries[a].MinDepth.CompareTo(Entries[b].MinDepth));
            return kinds;
        }

        // The kind whose band covers a depth, else the nearest band.
        private int KindAt(List<int> kinds, float depth)
        {
            int best = kinds[0]; float nearest = float.MaxValue;
            foreach (int kind in kinds)
            {
                var e = Entries[kind];
                float off = depth < e.MinDepth ? e.MinDepth - depth : depth > e.MaxDepth ? depth - e.MaxDepth : 0;
                if (off < nearest) { nearest = off; best = kind; }
            }
            return best;
        }

        // Each great cave's crystal trophy (116, user 2026-10-07: "each cave has one crystal to excavate and it becomes a
        // unique people look at"): the trophy whose band covers the cave's floor (its zone's), upright on the floor of one of
        // its chambers under the dig plot (TrophySeat). Without a great cave of its zone, any great cave still without one.
        private List<(int cave, Vector3 at, float radius)> SeatTrophies(TerrainGround.Cavern[] caves, Vector3 extent, int seed, List<int> order,
            Vector3[] seats, Dictionary<int, Quaternion> turns)
        {
            var random = new System.Random(unchecked(seed ^ 0x7209E5));
            var footprint = SiteLayout.FindFootprint(extent);
            var holding = new bool[caves.Length];
            var standing = new List<(int cave, Vector3 at, float radius)>();
            for (int i = 0; i < seats.Length; i++)
            {
                var entry = Entries[order[i]];
                if (!entry.CaveTrophy || !float.IsNaN(seats[i].x)) continue;
                int best = -1;
                for (int c = 0; c < caves.Length; c++)
                {
                    if (!caves[c].Great || holding[c]) continue;
                    float depth = extent.y - caves[c].Floor;
                    if (depth >= entry.MinDepth && depth <= entry.MaxDepth) { best = c; break; }
                    if (best < 0) best = c;
                }
                if (best < 0 || !TrophySeat(caves[best], entry, random, footprint, out var position, out var rotation)) continue;
                seats[i] = position; turns[i] = rotation; holding[best] = true;
                standing.Add((best, position, entry.PlacementRadius));
            }
            return standing;
        }

        // A trophy stands upright on a level stretch of a chamber's floor (grid-local), preferably under the dig plot, sunk
        // TrophySink of its height into the cave rock, with clear air round it (no pillar, arch or stalagmite at hand).
        public const float TrophySink = .45f;
        internal static bool TrophySeat(TerrainGround.Cavern cave, Entry entry, System.Random random, Func<Vector2, bool> footprint,
            out Vector3 position, out Quaternion rotation, List<int> chambers = null)
        {
            float reach = entry.RestingReach, half = entry.RestingHalfHeight;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                int chamber = chambers != null ? chambers[random.Next(chambers.Count)] : random.Next(cave.Centres.Length);
                var centre = cave.Centres[chamber]; var radius = cave.Radii[chamber];
                float x = centre.x + ((float)random.NextDouble() - .5f) * radius.x, z = centre.z + ((float)random.NextDouble() - .5f) * radius.z;
                if (footprint != null && attempt < 50 && !footprint(new Vector2(x, z))) continue;
                var (floor, roof) = TerrainGround.CavernSpan(cave, x, z);
                if (float.IsNaN(floor) || roof - floor < half * 2 * (1 - TrophySink) + 1f) continue;
                bool room = true;
                for (int a = 0; a < 8 && room; a++)
                {
                    float angle = a * Mathf.PI / 4;
                    float px = x + Mathf.Cos(angle) * (reach + .5f), pz = z + Mathf.Sin(angle) * (reach + .5f);
                    // Air at knee and head height, and the floor there within a hand of the trophy's: rock just below, air
                    // just above.
                    room = TerrainGround.CavernHollow(cave, new Unity.Mathematics.float3(px, floor + .7f, pz)) < -.1f
                        && TerrainGround.CavernHollow(cave, new Unity.Mathematics.float3(px, floor + 1.6f, pz)) < -.1f
                        && TerrainGround.CavernHollow(cave, new Unity.Mathematics.float3(px, floor - .35f, pz)) >= 0
                        && TerrainGround.CavernHollow(cave, new Unity.Mathematics.float3(px, floor + .35f, pz)) < 0;
                }
                if (!room) continue;
                position = new Vector3(x, floor + half * (1 - 2 * TrophySink), z);
                rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                return true;
            }
            position = default; rotation = default;
            return false;
        }

        // A cavern seat in a chamber clear of those already taken in the cavern: a few seeded tries, the last one kept if
        // none is.
        private const int CavernSeatTries = 8;
        // A geode's sixteen crystals nearly fill its hollow, so each looks longer for room.
        private const int GeodeSeatTries = 32;
        internal static (Vector3 position, Quaternion rotation) ClearCavernSeat(TerrainGround.Cavern cavern, int chamber, Entry entry,
            System.Random random, List<(Vector3 at, float radius)> taken, bool roof = false)
        {
            (Vector3 position, Quaternion rotation) seat = default;
            for (int attempt = 0; attempt < CavernSeatTries; attempt++)
            {
                seat = CavernSeat(cavern, chamber, entry, random, roof);
                var at = seat.position;
                if (taken.TrueForAll(t => (t.at - at).sqrMagnitude >= Square(t.radius + entry.PlacementRadius + DiscoveryField.SoilClearance))) break;
            }
            taken.Add((seat.position, entry.PlacementRadius));
            return seat;
        }

        private static float Square(float value) => value * value;

        private static bool Clear(List<(Vector3 at, float radius)> taken, Vector3 at, float radius)
            => taken.TrueForAll(t => (t.at - at).sqrMagnitude >= Square(t.radius + radius + DiscoveryField.SoilClearance));

        // Whether a cave's n-th crystal hangs from a great cave's roof.
        internal static bool OnRoof(in TerrainGround.Cavern cavern, int n) => cavern.Great && n % GreatRoofEvery == GreatRoofEvery - 1;

        // A crystal's seat in a cave's chamber (grid-local): a seeded direction from the chamber's heart into the floor, the
        // walls or the roof (a great cave's lower walls, or with `roof` its roof), where it meets the stone; pointing out of
        // it, sunk CavernSink.
        internal static (Vector3 position, Quaternion rotation) CavernSeat(TerrainGround.Cavern cavern, int chamber, Entry entry, System.Random random,
            bool roof = false)
        {
            float around = (float)random.NextDouble() * 360f, elevation = roof ? Mathf.Lerp(55f, 85f, (float)random.NextDouble())
                : Mathf.Lerp(-65f, cavern.Great ? 30f : 70f, (float)random.NextDouble());
            var direction = Quaternion.Euler(-elevation, around, 0) * Vector3.forward;
            var (surface, outward) = TerrainGround.CavernFace(cavern, TerrainGround.CavernHeart(cavern, chamber), direction);
            var inward = -(Vector3)outward;
            float half = entry.RestingHalfHeight;
            var position = (Vector3)surface + inward * (half * (1 - 2 * CavernSink));
            var rotation = Quaternion.FromToRotation(Vector3.up, inward) * Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
            return (position, rotation);
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
