using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Project the actual stencil vertices onto the same collision surface used by digging.
    // No decal collider: paint must never block a cut, a ray to a find, or the player.
    public sealed class WorldMark
    {
        public GameObject Object { get; }
        public MarkSnapshot State { get; private set; }
        public MeshRenderer Renderer { get; }
        private readonly WorksiteTools owner;
        private readonly Mesh mesh;
        private readonly Vector3[] source, projected, normals;
        public Bounds Bounds => Renderer.bounds;

        public WorldMark(WorksiteTools owner, Mesh stencil, Material material)
        {
            this.owner = owner;
            Object = new GameObject("Route marking", typeof(MeshFilter), typeof(MeshRenderer));
            Object.transform.SetParent(owner.transform, false);
            Object.layer = 2;
            mesh = UnityEngine.Object.Instantiate(stencil); mesh.name = "Projected route stencil";
            source = stencil.vertices; projected = new Vector3[source.Length]; normals = new Vector3[source.Length];
            Object.GetComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = Object.GetComponent<MeshRenderer>(); Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        public bool Project(MarkSnapshot state)
        {
            State = state; Object.transform.SetPositionAndRotation(state.Position, state.Rotation);
            Vector3 normal = state.Rotation * Vector3.forward;
            bool valid = true;
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 point = Object.transform.TransformPoint(source[i]);
                if (owner.ProjectSurface(point, normal, out var hit))
                {
                    projected[i] = Object.transform.InverseTransformPoint(hit.point + hit.normal * .008f);
                    normals[i] = Object.transform.InverseTransformDirection(hit.normal);
                }
                else { projected[i] = source[i]; normals[i] = Vector3.forward; valid = false; }
            }
            mesh.vertices = projected; mesh.normals = normals; mesh.RecalculateBounds();
            return valid;
        }

        public bool Supported()
        {
            Vector3 normal = State.Rotation * Vector3.forward;
            for (int i = 0; i < projected.Length; i++)
                if (!owner.HasSupport(Object.transform.TransformPoint(projected[i]) - normal * .008f, normal)) return false;
            return true;
        }

        public void Dispose()
        {
            if (Object != null) { Object.SetActive(false); UnityEngine.Object.Destroy(Object); }
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
