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
            // Additional lower-reservoir finds, separate from the early progression bands.
            public int DeepCount;
            public float DeepMinDepth, DeepMaxDepth;
            public bool LayOnSide, RandomOrientation;
            public int AppearanceCount => 1 + (AppearanceVariants?.Length ?? 0);
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
                if (e.DeepCount < 0 || e.DeepCount > e.Count - e.ShallowCount
                    || !ExcavationGrid.Finite(e.DeepMinDepth) || !ExcavationGrid.Finite(e.DeepMaxDepth)
                    || e.DeepMinDepth < 0 || e.DeepMaxDepth < 0
                    || (e.DeepCount > 0 && (e.DeepMinDepth < e.MaxDepth || e.DeepMaxDepth <= e.DeepMinDepth)))
                    throw new InvalidDataException("Invalid discovery deep allocation.");
                if (e.Prefab.Kind == DiscoveryKind.Unique && (!e.AuthoredPlacement || e.Count != 1 || e.ShallowCount != 0
                    || e.Prefab.Recovery != RecoveryMethod.Rope || e.Prefab.SaleValue != 0 || !e.Prefab.DetectorEligible || !e.Prefab.HasLore))
                    throw new InvalidDataException("Invalid unique discovery policy.");
                if (e.AuthoredPlacement && (e.Count != 1 || e.ShallowCount != 0 || !WorldSnapshot.Valid(e.AuthoredPosition) || !WorldSnapshot.Valid(e.AuthoredEuler)))
                    throw new InvalidDataException("Invalid authored discovery placement.");
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
                        || appearance.SaleValue != e.Prefab.SaleValue || appearance.Size != e.Prefab.Size
                        || appearance.DetectorEligible != e.Prefab.DetectorEligible
                        || appearance.Kind != e.Prefab.Kind || appearance.Recovery != e.Prefab.Recovery
                        || !Mathf.Approximately(appearance.RequiredExposure, e.Prefab.RequiredExposure))
                        throw new InvalidDataException("Item appearances must have unique save keys and matching gameplay specifications.");
                }
            }
            if (TotalCount > DiscoveryField.MaximumPopulation || shallow < 1) throw new InvalidDataException("Starter allocation requires shallow finds and a supported total.");

        }

        public BuriedFind Resolve(string id)
        {
            foreach (var entry in Entries)
                for (int i = 0; i < entry.AppearanceCount; i++)
                    if (entry.Appearance(i).SaveContentId == id) return entry.Appearance(i);
            throw new InvalidDataException("This save needs discovery content missing from this game version.");
        }

        public DiscoveryPlacement[] Generate(Vector3 extent, int seed)
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
            var deepLeft = new int[Entries.Length];
            for (int i = 0; i < coreLeft.Length; i++)
            {
                deepLeft[i] = Entries[i].DeepCount;
                coreLeft[i] = Mathf.RoundToInt((Entries[i].Count - Entries[i].ShallowCount - deepLeft[i]) * Entries[i].CoreShare);
            }
            for (int i = 0; i < bands.Length; i++)
            {
                var entry = Entries[shallow[i]];
                covers[i] = new Vector2(entry.ShallowMinCover, entry.ShallowMaxCover);
                if (i >= ShallowCount && deepLeft[shallow[i]] > 0)
                {
                    bands[i] = new Vector2(entry.DeepMinDepth, entry.DeepMaxDepth);
                    deepLeft[shallow[i]]--;
                }
                else if (i >= ShallowCount && coreLeft[shallow[i]] > 0)
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
                Vector3 p = entry.AuthoredPosition; float r = entryRadii[i] + DiscoveryField.SoilClearance;
                if (p.x < r || p.y < r || p.z < r || p.x > extent.x-r || p.y > extent.y-r || p.z > extent.z-r)
                    throw new InvalidDataException("Authored unique does not fit inside untouched soil.");
                foreach (var other in reserved) if (Vector3.Distance(p,other.Position) < r+other.Radius)
                    throw new InvalidDataException("Authored discoveries overlap.");
                reserved.Add(new DiscoveryReservation(p,entryRadii[i]));
                authored.Add(new DiscoveryPlacement(p,Quaternion.Euler(entry.AuthoredEuler),i));
            }
            var layout = DiscoveryField.Generate(extent, shallow.Count, seed, ShallowCount, radii, bands, covers, reserved.ToArray(),
                SiteLayout.FindFootprint(extent));
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
                layout[i] = new DiscoveryPlacement(layout[i].Position, rotation, index, appearances.Next(Entries[index].AppearanceCount));
            }
            var result = new List<DiscoveryPlacement>(layout); result.AddRange(authored); return result.ToArray();
        }

        private static void Shuffle(List<int> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); int value = values[i]; values[i] = values[j]; values[j] = value; }
        }
    }
}
