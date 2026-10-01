using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // A single surface glyph, shared by aiming and the active recovery job: it previews the point while
    // the player holds the mark, and the accepted mark is a swivel lifting eye bolted on there for the
    // crane's hook. The saved attachment is authoritative; this view has no independent ownership.
    internal sealed class RecoveryMarkView
    {
        // The hook's seat rests in the eye's ring this far from the load's surface (art/lifting-eye).
        public const float HookReach = .21f;
        private const float GlyphSize = .14f;
        private readonly GameObject root;
        private readonly Transform eye;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private BuriedFind target;
        private Vector3 point, localNormal;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int PlacedId = Shader.PropertyToID("_Placed");

        public RecoveryMarkView(Transform owner, Material material, GameObject liftingEye, ExcavationDaylight lighting)
        {
            root = new GameObject("Recovery surface mark", typeof(MeshFilter), typeof(MeshRenderer))
                { layer = 2, hideFlags = HideFlags.DontSave };
            root.transform.SetParent(owner, false);
            mesh = new Mesh { name = "Recovery mark quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0,1,2, 0,2,3 };
            mesh.RecalculateBounds();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            eye = Object.Instantiate(liftingEye, root.transform, false).transform;
            eye.name = "Lifting eye";
            foreach (var part in eye.GetComponentsInChildren<Transform>(true)) part.gameObject.hideFlags = HideFlags.DontSave;
            eye.localScale = Vector3.one / GlyphSize;
            eye.localPosition = Vector3.back * (.012f / GlyphSize);
            // Lit like the ground around it: underground the eye darkens with the dig instead of shining
            // in full daylight.
            if (lighting != null) foreach (var part in eye.GetComponentsInChildren<Renderer>(true)) lighting.Register(part);
            eye.gameObject.SetActive(false);
        }

        public void Show(BuriedFind find, Vector3 attachment, Vector3 worldNormal, float progress, bool placed)
        {
            if (worldNormal.sqrMagnitude > .5f)
                localNormal = find.transform.InverseTransformDirection(worldNormal).normalized;
            else if (target != find || point != attachment)
                localNormal = RestoreNormal(find, attachment);
            target = find; point = attachment;
            var normal = target.transform.TransformDirection(localNormal).normalized;
            var up = Vector3.ProjectOnPlane(target.transform.up, normal);
            if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(target.transform.forward, normal);
            root.transform.SetPositionAndRotation(target.transform.TransformPoint(point) + normal * .012f,
                Quaternion.LookRotation(normal, up.normalized));
            root.transform.localScale = Vector3.one * GlyphSize;
            properties.SetFloat(ProgressId, Mathf.Clamp01(progress));
            properties.SetFloat(PlacedId, placed ? 1 : 0);
            renderer.SetPropertyBlock(properties);
            renderer.enabled = !placed;
            eye.gameObject.SetActive(placed);
            eye.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            root.SetActive(true);
        }

        // The eye swivels toward the hook pulling on it (never into the load; a zero pull stands it
        // upright) and turns about that so the hook's wire runs through its ring: the ring's hole runs
        // along the eye's local Z (art/lifting-eye).
        public void Aim(Vector3 direction, Vector3 wire)
        {
            Vector3 normal = root.transform.forward;
            if (Vector3.Dot(direction, normal) < 0) direction = Vector3.ProjectOnPlane(direction, normal);
            if (direction.sqrMagnitude < .000001f) direction = normal;
            direction.Normalize();
            Vector3 across = Vector3.ProjectOnPlane(wire, direction);
            eye.rotation = across.sqrMagnitude > .01f ? Quaternion.LookRotation(across, direction)
                : Quaternion.FromToRotation(eye.up, direction) * eye.rotation;
        }

        private static Vector3 RestoreNormal(BuriedFind find, Vector3 attachment)
        {
            // Query only this known payload: terrain must not change the mark's
            // orientation, and the original outward route hint predates its spin.
            Vector3 point = find.transform.TransformPoint(attachment);
            Physics.SyncTransforms();
            float reach = find.WorldBounds.size.magnitude + .1f;
            float nearest = float.PositiveInfinity;
            Vector3 result = Vector3.up;
            for (int axis = 0; axis < 6; axis++)
            {
                Vector3 local = axis / 2 == 0 ? Vector3.right : axis / 2 == 1 ? Vector3.up : Vector3.forward;
                Vector3 direction = find.transform.TransformDirection(axis % 2 == 0 ? local : -local);
                if (!find.HitCollider.Raycast(new Ray(point + direction * reach, -direction), out var hit, reach + .02f)) continue;
                float distance = (hit.point - point).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; result = find.transform.InverseTransformDirection(hit.normal).normalized;
            }
            return result;
        }

        public void Hide() { if (root != null) root.SetActive(false); target = null; }
        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
