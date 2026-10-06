using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // The stash pits' old chest (106) from art/old-chest/catalog.json: a variant of the URP old chest (BuriedPropsSetup)
    // with a kinematic body, box colliders for its floor and walls and one on its lid bone (it swings with the lid), and
    // the geometry BuriedChest reads, measured from the closed model: its hollow, pocket, floor seats, lid space and footing.
    public static class ChestSetup
    {
        public const string Folder = "Assets/Content/Discoveries/Chest", PrefabPath = Folder + "/OldChest.prefab";
        private const string Body = "Chest", Lid = "MainAxis";
        // The hollow reaches this far past the chest's outer walls and lid top: the ground's surface cuts a box's edges
        // and corners inward by most of a 12.5 cm cell, and a tighter hollow gripped an undercut chest by its corners
        // instead of letting it fall. The lid opens with this much of its space measured above it.
        private const float HollowMargin = .09f, LidClearance = .02f;
        // The pocket of air the chest stands in (user, 2026-10-06) takes in the hollow and the lid's whole swing, PocketSide
        // past them sideways (room to stand beside it) and PocketHeadroom above; its floor lies PocketFloor above the
        // chest's base, so the base sits that little way in the ground and the footing under it stays solid.
        private const float PocketSide = .75f, PocketHeadroom = .5f, PocketFloor = .02f;
        // In front of its lock the pocket reaches PocketFront further, room to stand there and open it (user, 2026-10-06).
        private const float PocketFront = .8f;
        [Serializable] private sealed class Source { public int schema_version; public string prefab, display_name; public int items; public Content[] contents; }
        [Serializable] private sealed class Content { public string content_id; public float shallow_weight, deep_weight; }

        internal static void Configure(DiscoveryCatalog catalog)
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/old-chest/catalog.json"))));
            if (source == null || source.schema_version != 1 || string.IsNullOrWhiteSpace(source.display_name) || source.items < 1
                || source.contents == null || source.contents.Length == 0 || source.contents.Any(c => c == null || c.shallow_weight < 0 || c.deep_weight < 0 || c.shallow_weight + c.deep_weight <= 0
                    || string.IsNullOrWhiteSpace(c.content_id)))
                throw new InvalidDataException("Invalid old chest source.");
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.prefab);
            if (basePrefab == null) throw new InvalidDataException("Missing " + source.prefab + " (run Configure Buried Props).");
            RetroComputerSetup.EnsureFolder(Folder);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, scene);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var opening = root.GetComponent<Animation>();
                opening.playAutomatically = false;
                opening.clip.SampleAnimation(root, 0);
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                var bones = skin.bones;
                var baked = new Mesh(); skin.BakeMesh(baked, true);
                var toRoot = root.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                var vertices = baked.vertices.Select(v => toRoot.MultiplyPoint3x4(v)).ToArray();
                var weights = skin.sharedMesh.boneWeights;
                var triangles = skin.sharedMesh.triangles;
                UnityEngine.Object.DestroyImmediate(baked);
                string BoneOf(int i) => bones[weights[i].boneIndex0].name;
                var outer = Enclose(vertices);
                var body = Enclose(vertices.Where((v, i) => BoneOf(i) == Body));
                var lid = Enclose(vertices.Where((v, i) => BoneOf(i) == Lid));
                // Inner faces: the body's triangles facing into the box (a low-poly box has vertices only at its corners,
                // so faces are told by their centres and normals).
                var faces = new List<(Vector3 centre, Vector3 normal)>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (BoneOf(a) != Body || BoneOf(b) != Body || BoneOf(c) != Body) continue;
                    var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    if (cross.sqrMagnitude > 1e-10f) faces.Add(((vertices[a] + vertices[b] + vertices[c]) / 3, cross.normalized));
                }
                float Median(IEnumerable<float> values, string face)
                {
                    var sorted = values.OrderBy(v => v).ToArray();
                    return sorted.Length > 0 ? sorted[sorted.Length / 2] : throw new InvalidDataException("No inner chest face: " + face);
                }
                float floor = Median(faces.Where(f => f.normal.y > .8f && f.centre.y < body.center.y
                    && Mathf.Abs(f.centre.x) < outer.extents.x * .8f && Mathf.Abs(f.centre.z) < outer.extents.z * .85f).Select(f => f.centre.y), "floor");
                float innerX = Median(faces.Where(f => Mathf.Abs(f.normal.x) > .8f && Mathf.Sign(f.normal.x) != Mathf.Sign(f.centre.x)
                    && f.centre.y > floor && f.centre.y < body.max.y && Mathf.Abs(f.centre.z) < outer.extents.z * .85f)
                    .Select(f => Mathf.Abs(f.centre.x)), "side");
                float innerZ = Median(faces.Where(f => Mathf.Abs(f.normal.z) > .8f && Mathf.Sign(f.normal.z) != Mathf.Sign(f.centre.z)
                    && f.centre.y > floor && f.centre.y < body.max.y && Mathf.Abs(f.centre.x) < outer.extents.x * .8f)
                    .Select(f => Mathf.Abs(f.centre.z)), "end");
                if (!(floor > outer.min.y && innerX < outer.max.x && innerZ < outer.max.z)) throw new InvalidDataException("The chest's walls did not measure.");

                // Colliders: the vendor box becomes the floor; four walls up to the rim, one child each; the lid's box rides
                // its bone.
                var material = Contact();
                var floorBox = root.GetComponent<BoxCollider>();
                Box(floorBox, new Vector3(0, (outer.min.y + floor) * .5f, 0), new Vector3(outer.size.x, floor - outer.min.y, outer.size.z), material);
                float wallMid = (outer.min.y + body.max.y) * .5f, wallHeight = body.max.y - outer.min.y;
                BoxCollider Wall(string name)
                {
                    var wall = new GameObject(name).transform;
                    wall.SetParent(root.transform, false);
                    return wall.gameObject.AddComponent<BoxCollider>();
                }
                foreach (int side in new[] { -1, 1 })
                {
                    Box(Wall(side > 0 ? "WallFront" : "WallBack"), new Vector3(side * (innerX + outer.max.x) * .5f, wallMid, 0),
                        new Vector3(Mathf.Max(.03f, outer.max.x - innerX), wallHeight, outer.size.z), material);
                    Box(Wall(side > 0 ? "WallEndA" : "WallEndB"), new Vector3(0, wallMid, side * (innerZ + outer.max.z) * .5f),
                        new Vector3(outer.size.x, wallHeight, Mathf.Max(.03f, outer.max.z - innerZ)), material);
                }
                var hinge = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == Lid);
                var lidCollider = new GameObject("LidCollider").transform;
                lidCollider.SetParent(hinge, false);
                lidCollider.SetPositionAndRotation(root.transform.position, root.transform.rotation);
                Box(lidCollider.gameObject.AddComponent<BoxCollider>(), lid.center, lid.size, material);

                if (!root.TryGetComponent<Rigidbody>(out var rigid)) rigid = root.AddComponent<Rigidbody>();
                rigid.isKinematic = true; rigid.useGravity = false; rigid.mass = 30;
                rigid.linearDamping = .2f; rigid.angularDamping = 1;
                rigid.interpolation = RigidbodyInterpolation.Interpolate;
                rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rigid.maxDepenetrationVelocity = 1.5f;

                // Geometry in the chest's frame: the hollow from floor to just above the lid, past the walls; seats along
                // the floor; the arc the lid sweeps about its hinge to its open angle; points just under the base.
                float hollowTop = outer.max.y + HollowMargin, hollowBottom = (outer.min.y + floor) * .5f;
                var hollowHalf = new Vector3(outer.extents.x + HollowMargin, (hollowTop - hollowBottom) * .5f, outer.extents.z + HollowMargin);
                var hollowCentre = new Vector3(outer.center.x, (hollowTop + hollowBottom) * .5f, outer.center.z);
                // Ten seats from the back, away from the lock (user, 2026-10-06: equal spacing looked laid out; "add more
                // ingots"): three along the back wall, two over the gaps between them, one before those, then four more
                // toward the middle. DiscoveryCatalog.ChestHeap sets each find on those already under it, so the contents
                // settle into a heap. front is the lock's side, measured below.
                float lockSide = Mathf.Sign(outer.center.x - root.transform.InverseTransformPoint(hinge.position).x + 1e-4f);
                var seats = new[] { new Vector2(-.62f, -.5f), new Vector2(-.62f, .05f), new Vector2(-.62f, .6f),
                        new Vector2(-.42f, -.22f), new Vector2(-.42f, .33f), new Vector2(-.2f, .08f),
                        new Vector2(-.2f, -.45f), new Vector2(-.2f, .58f), new Vector2(.02f, -.18f), new Vector2(.02f, .36f) }
                    .Select(s => new Vector3(s.x * lockSide * innerX, floor, s.y * innerZ)).ToArray();
                if (source.items > seats.Length) throw new InvalidDataException($"The old chest seats {seats.Length} items at most.");
                var pivot = root.transform.InverseTransformPoint(hinge.position);
                float reach = vertices.Where((v, i) => BoneOf(i) == Lid).Max(v => new Vector2(v.x - pivot.x, v.y - pivot.y).magnitude);
                float thickness = lid.max.y - pivot.y;
                // The lid turns about its hinge (its free edge points to the front, +x, when closed): sample its middle and
                // outer face along the arc, so the soil behind the hinge counts as well as the soil above.
                var lidSpace = new List<Vector3>();
                float front = Mathf.Sign(outer.center.x - pivot.x + 1e-4f);
                foreach (float angle in new[] { 15f, 35f, 55f, 75f, 95f })
                foreach (float share in new[] { .15f, .4f, .7f, 1f })
                foreach (float lift in new[] { thickness * .5f, thickness + LidClearance })
                foreach (float along in new[] { -.8f, -.27f, .27f, .8f })
                {
                    float a = angle * Mathf.Deg2Rad, r = reach * share;
                    var edge = new Vector2(Mathf.Cos(a) * front, Mathf.Sin(a)); var face = new Vector2(-Mathf.Sin(a) * front, Mathf.Cos(a));
                    var point = new Vector2(pivot.x, pivot.y) + edge * r + face * lift;
                    lidSpace.Add(new Vector3(point.x, point.y, along * innerZ));
                }
                var space = new Bounds(outer.center, outer.size);
                space.Encapsulate(new Bounds(hollowCentre, hollowHalf * 2));
                foreach (var point in lidSpace) space.Encapsulate(point);
                float pocketBottom = Mathf.Min(hollowBottom, outer.min.y + PocketFloor), pocketTop = space.max.y + PocketHeadroom;
                float pocketBack = space.min.x - PocketSide - (front < 0 ? PocketFront : 0), pocketAhead = space.max.x + PocketSide + (front > 0 ? PocketFront : 0);
                var pocketHalf = new Vector3((pocketAhead - pocketBack) * .5f, (pocketTop - pocketBottom) * .5f, space.extents.z + PocketSide);
                var pocketCentre = new Vector3((pocketAhead + pocketBack) * .5f, (pocketTop + pocketBottom) * .5f, space.center.z);
                var footing = new List<Vector3>();
                foreach (float x in new[] { -.75f, 0, .75f })
                foreach (float z in new[] { -.85f, -.42f, 0, .42f, .85f })
                    footing.Add(new Vector3(x * outer.extents.x, outer.min.y - .05f, z * outer.extents.z));

                if (!root.TryGetComponent<BuriedChest>(out var chest)) chest = root.AddComponent<BuriedChest>();
                var data = new SerializedObject(chest);
                data.FindProperty("displayName").stringValue = source.display_name;
                data.FindProperty("hollowCentre").vector3Value = hollowCentre;
                data.FindProperty("hollowHalf").vector3Value = hollowHalf;
                data.FindProperty("pocketCentre").vector3Value = pocketCentre;
                data.FindProperty("pocketHalf").vector3Value = pocketHalf;
                data.FindProperty("front").vector3Value = new Vector3(front, 0, 0);
                data.FindProperty("rim").floatValue = body.max.y;
                data.FindProperty("floorHalf").vector2Value = new Vector2(innerX, innerZ);
                // Ordinary finds keep out of the whole pocket, not only the wood.
                data.FindProperty("radius").floatValue = pocketHalf.magnitude + pocketCentre.magnitude + .02f;
                Write(data.FindProperty("contentSeats"), seats);
                Write(data.FindProperty("lidSpace"), lidSpace);
                Write(data.FindProperty("footing"), footing);
                data.ApplyModifiedPropertiesWithoutUndo();
                root.name = Path.GetFileNameWithoutExtension(PrefabPath);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                catalog.Chest = saved.GetComponent<BuriedChest>();
                catalog.ChestItems = source.items;
                catalog.ChestContents = source.contents.Select(c => new DiscoveryCatalog.ChestContent
                    { ItemId = c.content_id, ShallowWeight = c.shallow_weight, DeepWeight = c.deep_weight }).ToArray();
                Debug.Log($"Old chest: outer {outer.size:F3}, inner floor {floor:F3}, inner walls x {innerX:F3} z {innerZ:F3}, hinge {pivot:F3}, lid reach {reach:F2}, pocket {pocketCentre:F2} half {pocketHalf:F2}.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Bounds Enclose(IEnumerable<Vector3> points)
        {
            bool any = false; var bounds = new Bounds();
            foreach (var p in points) { if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p); }
            return any ? bounds : throw new InvalidDataException("Missing chest part.");
        }

        private static void Box(BoxCollider box, Vector3 centre, Vector3 size, PhysicsMaterial material)
        { box.center = centre; box.size = size; box.sharedMaterial = material; box.enabled = true; box.isTrigger = false; }

        private static void Write(SerializedProperty array, IList<Vector3> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).vector3Value = values[i];
        }

        private static PhysicsMaterial Contact()
        {
            string path = Folder + "/ChestContact.physicMaterial";
            var contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (contact == null) { contact = new PhysicsMaterial("Chest contact"); AssetDatabase.CreateAsset(contact, path); }
            contact.dynamicFriction = .8f; contact.staticFriction = .9f; contact.bounciness = 0;
            contact.frictionCombine = PhysicsMaterialCombine.Maximum; contact.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(contact);
            return contact;
        }
    }
}
