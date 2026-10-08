using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Cosmetic copies of approved meshes. Original identity/inventory have
    // already committed; interruption never rolls back or repeats collection.
    internal sealed class FindPickupPresentation
    {
        private const float Duration = .18f;
        private const int MaximumCopies = 8;
        private sealed class Flight
        {
            public GameObject Object;
            public MeshFilter Mesh;
            public MeshRenderer Renderer;
            public Vector3 Start, Scale, MeshCenter;
            public Quaternion Rotation;
            public float Elapsed;
        }
        private readonly Transform owner;
        private readonly Camera camera;
        private readonly List<Flight> copies = new List<Flight>(MaximumCopies);
        private MaterialPropertyBlock block;

        public FindPickupPresentation(Transform owner, Camera camera) { this.owner = owner; this.camera = camera; }

        public void Play(MeshRenderer source, MeshFilter mesh)
        {
            if (source == null || mesh == null || mesh.sharedMesh == null) return;
            Flight flight = null;
            foreach (var copy in copies) if (!copy.Object.activeSelf) { flight = copy; break; }
            if (flight == null && copies.Count < MaximumCopies)
            {
                var visual = new GameObject("Pickup visual") { hideFlags = HideFlags.DontSave, layer = 2 };
                visual.transform.SetParent(owner, false);
                flight = new Flight { Object = visual, Mesh = visual.AddComponent<MeshFilter>(), Renderer = visual.AddComponent<MeshRenderer>() };
                flight.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                flight.Renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                copies.Add(flight);
            }
            if (flight == null)
            {
                flight = copies[0];
                foreach (var copy in copies) if (copy.Elapsed > flight.Elapsed) flight = copy;
            }
            flight.Mesh.sharedMesh = mesh.sharedMesh;
            flight.Renderer.sharedMaterials = source.sharedMaterials;
            // Lit as the find was where it lay (user, 2026-10-08: a crystal "light pink" in the pickup, "bluish" before):
            // shadowed by the ground as it was, not lit through it by the sun, with its glow as dimmed (CavernScenery).
            flight.Renderer.receiveShadows = source.receiveShadows;
            flight.Renderer.lightProbeUsage = source.lightProbeUsage;
            flight.Renderer.renderingLayerMask = source.renderingLayerMask;
            block ??= new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            flight.Renderer.SetPropertyBlock(block);
            flight.Start = source.transform.TransformPoint(mesh.sharedMesh.bounds.center);
            flight.Scale = source.transform.lossyScale;
            flight.Rotation = source.transform.rotation;
            flight.MeshCenter = mesh.sharedMesh.bounds.center;
            flight.Elapsed = 0;
            flight.Object.SetActive(true);
            Pose(flight, 0);
        }

        public void Tick(float deltaTime)
        {
            foreach (var copy in copies)
            {
                if (!copy.Object.activeSelf) continue;
                copy.Elapsed += deltaTime;
                if (copy.Elapsed >= Duration) { copy.Object.SetActive(false); continue; }
                Pose(copy, copy.Elapsed / Duration);
            }
        }

        private void Pose(Flight flight, float t)
        {
            var eye = camera.transform;
            Vector3 towardPlayer = eye.position - eye.up * .25f - flight.Start;
            // A short direct tug starts immediately, then the copy disappears.
            // Stop before the camera so a nearly full-size bottle cannot fill it.
            float distance = towardPlayer.magnitude;
            Vector3 pull = distance > .001f ? towardPlayer / distance * Mathf.Min(.95f, distance * .45f) : Vector3.zero;
            float travel = t * (.35f + .65f * t);
            Vector3 center = flight.Start + pull * travel;
            Vector3 scale = flight.Scale * Mathf.Lerp(1, .85f, t);
            flight.Object.transform.SetPositionAndRotation(center - flight.Rotation * Vector3.Scale(flight.MeshCenter, scale), flight.Rotation);
            flight.Object.transform.localScale = scale;
        }

        public void Clear()
        {
            foreach (var copy in copies) if (copy.Object != null) copy.Object.SetActive(false);
        }

        public void Dispose()
        {
            foreach (var copy in copies) if (copy.Object != null) Object.Destroy(copy.Object);
            copies.Clear();
        }
    }
}
