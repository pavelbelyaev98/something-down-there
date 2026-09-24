using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // A single surface glyph, shared by aiming and the active recovery job.
    // The saved attachment is authoritative; this view has no independent ownership.
    internal sealed class RecoveryMarkView
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private BuriedFind target;
        private Vector3 point, localNormal;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int PlacedId = Shader.PropertyToID("_Placed");

        public RecoveryMarkView(Transform owner, Material material)
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
            root.transform.localScale = Vector3.one * .14f;
            properties.SetFloat(ProgressId, Mathf.Clamp01(progress));
            properties.SetFloat(PlacedId, placed ? 1 : 0);
            renderer.SetPropertyBlock(properties);
            root.SetActive(true);
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
