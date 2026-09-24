using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    [DisallowMultipleComponent]
    public sealed class WorksiteTools : MonoBehaviour
    {
        public const int LampCapacity = 8, MaximumMarks = 96;
        public const float LightRange = 9, LightCullDistance = 24;
        public static readonly Vector3 LampCenter = new Vector3(0, .25f, 0);
        public static readonly Vector3 LampHalfSize = new Vector3(.145f, .245f, .145f);
        [SerializeField] private FpsPlayer player;
        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private WorkLamp lampPrefab;
        [SerializeField] private Mesh[] stencils;
        [SerializeField] private Material markMaterial, validPreview, invalidPreview;
        private readonly List<WorkLamp> lamps = new List<WorkLamp>(LampCapacity);
        private readonly List<WorldMark> marks = new List<WorldMark>(MaximumMarks);
        private readonly Collider[] overlaps = new Collider[32];
        private readonly RaycastHit[] surfaceHits = new RaycastHit[32];
        private GameObject lampGhost;
        private Renderer[] ghostRenderers;
        private WorldMark markGhost;
        private int placement; // 0 none, 1 lamp, 2..4 stencils.
        private float rotation;
        private RaycastHit surface;
        private Vector3 proposedPosition, lampSupportPoint, lampSupportNormal;
        private Quaternion proposedRotation;
        private bool valid;
        private string reason = "Aim at ground within reach";
        public FpsPlayer Player => player;
        public TerrainVolume Terrain => terrain;
        public IReadOnlyList<WorkLamp> Lamps => lamps;
        public int MarkCount => marks.Count;
        public int AvailableLamps => LampCapacity - lamps.Count;
        public long Revision { get; private set; }
        public bool IsPlacing => placement != 0;
        public bool PlacementValid => IsPlacing && valid;
        public bool Configured => player != null && terrain != null && lampPrefab != null && lampPrefab.WorkLight != null
            && stencils != null && stencils.Length == 3 && Array.TrueForAll(stencils, mesh => mesh != null)
            && markMaterial != null && validPreview != null && invalidPreview != null;
        public string PlacementPrompt => !IsPlacing ? "" : (valid ? $"{Binding(PlayerBinding.Dig)}  Place {SelectionName}" : reason)
            + $"\n{Binding(PlayerBinding.RotatePlacement)} Rotate  |  {Binding(PlayerBinding.Grab)} Cancel"
            + (placement > 1 ? $"  |  {Binding(PlayerBinding.Mark)} Next symbol" : "");
        private string SelectionName => placement == 1 ? "work lamp" : MarkName((WorldMarkKind)(placement - 2));
        private string Binding(PlayerBinding binding) => player.InputSettings.Display(binding);
        private static string MarkName(WorldMarkKind kind) => kind == WorldMarkKind.Home ? "home" : kind == WorldMarkKind.ReturnHere ? "return-here" : "arrow";

        private void OnEnable() { if (terrain != null) terrain.Changed += TerrainChanged; }
        private void OnDisable() { if (terrain != null) terrain.Changed -= TerrainChanged; Cancel(); }
        public void Dirty() => Revision++;
        public void RegisterRenderer(Renderer renderer) => terrain.GetComponent<ExcavationDaylight>()?.Register(renderer);

        private void LateUpdate()
        {
            if (player == null) return;
            if (!player.GameplayActive) Cancel();
            Vector3 eye = player.ViewCamera.transform.position;
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                var lamp = lamps[i]; lamp.Tick(player.GameplayActive, eye);
                if (lamp.transform.position.y < terrain.transform.position.y - 2) Retrieve(lamp);
            }
        }

        public bool HandleInput(FpsInputFrame frame)
        {
            if (frame.LampPressed || frame.MarkPressed)
            {
                if (player.HeldFind != null) { player.ShowFeedback("Put down the find before placing equipment"); return true; }
                int next = frame.LampPressed ? placement == 1 ? 0 : 1 : placement < 2 ? 2 : placement == 4 ? 2 : placement + 1;
                Cancel(); placement = next; player.SuppressWorldActions();
                if (IsPlacing) UpdatePreview();
                return true;
            }
            if (!IsPlacing) return frame.LampPressed || frame.MarkPressed;
            if (frame.GrabPressed) { Cancel(); player.SuppressWorldActions(); return true; }
            if (frame.RotatePlacementPressed) rotation = Mathf.Repeat(rotation + 45, 360);
            UpdatePreview();
            if (frame.DigPressed)
            {
                if (valid && Commit()) { Cancel(); player.SuppressWorldActions(); }
                else player.ShowFeedback(reason);
            }
            return true;
        }

        public void Cancel()
        {
            placement = 0; rotation = 0; valid = false;
            if (lampGhost != null) lampGhost.SetActive(false);
            markGhost?.Dispose(); markGhost = null;
        }

        private void UpdatePreview()
        {
            valid = false; reason = "Aim at ground within reach";
            bool hit = player.TryGetTarget(player.Tuning.InteractReach, out surface);
            if (placement > 1 && (!hit || !IsSurface(surface.collider)))
            {
                if (lampGhost != null) lampGhost.SetActive(false);
                if (markGhost != null) markGhost.Object.SetActive(false);
                return;
            }
            Vector3 normal = hit ? surface.normal : Vector3.up;
            if (placement == 1)
            {
                lampSupportPoint = hit ? surface.point : player.ViewCamera.transform.position
                    + player.ViewCamera.transform.forward * Mathf.Min(2f, player.Tuning.InteractReach);
                lampSupportNormal = normal;
                Vector3 forward = Vector3.ProjectOnPlane(player.ViewCamera.transform.forward, normal);
                if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(player.ViewCamera.transform.up, normal);
                proposedRotation = Quaternion.AngleAxis(rotation, normal) * Quaternion.LookRotation(forward.normalized, normal);
                valid = TryLampPosition(lampSupportPoint, normal, proposedRotation, out proposedPosition)
                    && AvailableLamps > 0 && (!hit || IsLampSurface(surface.collider));
                reason = AvailableLamps == 0 ? "All lamps placed — pick one up to reuse it" : "Not enough room for the lamp";
                EnsureLampGhost(); lampGhost.SetActive(true); lampGhost.transform.SetPositionAndRotation(proposedPosition, proposedRotation);
                foreach (var renderer in ghostRenderers) renderer.sharedMaterial = valid ? validPreview : invalidPreview;
            }
            else
            {
                proposedPosition = surface.point;
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(player.ViewCamera.transform.forward, normal);
                proposedRotation = Quaternion.AngleAxis(rotation, normal) * Quaternion.LookRotation(normal, up.normalized);
                if (markGhost == null) markGhost = new WorldMark(this, stencils[placement - 2], validPreview);
                markGhost.Object.SetActive(true);
                valid = markGhost.Project(new MarkSnapshot { Kind = (WorldMarkKind)(placement - 2), Position = proposedPosition, Rotation = proposedRotation })
                    && marks.Count < MaximumMarks && !MarkOverlaps(proposedPosition);
                reason = marks.Count >= MaximumMarks ? "Erase a marking before adding another" : "Choose an unmarked, unbroken surface";
                markGhost.Renderer.sharedMaterial = valid ? validPreview : invalidPreview;
            }
        }

        private void EnsureLampGhost()
        {
            if (lampGhost != null) return;
            lampGhost = Instantiate(lampPrefab.gameObject, transform); lampGhost.name = "Lamp placement preview";
            lampGhost.GetComponent<WorkLamp>().enabled = false;
            var body = lampGhost.GetComponent<Rigidbody>(); body.isKinematic = true; body.detectCollisions = false;
            foreach (var light in lampGhost.GetComponentsInChildren<Light>()) light.enabled = false;
            foreach (var collider in lampGhost.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var child in lampGhost.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
            ghostRenderers = lampGhost.GetComponentsInChildren<Renderer>();
            foreach (var renderer in ghostRenderers) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private bool Commit()
        {
            if (placement == 1) return PlaceLamp(lampSupportPoint, lampSupportNormal, proposedRotation) != null;
            return PlaceMark(new MarkSnapshot { Kind = (WorldMarkKind)(placement - 2), Position = proposedPosition, Rotation = proposedRotation });
        }

        public WorkLamp PlaceLamp(Vector3 point, Vector3 normal, Quaternion orientation)
        {
            if (AvailableLamps <= 0 || !TryLampPosition(point, normal, orientation, out var position)) return null;
            int slot = 0;
            for (; slot < LampCapacity; slot++) if (!lamps.Exists(l => l.Slot == slot)) break;
            var lamp = Spawn(new LampSnapshot { Slot = slot, Position = position, Rotation = orientation, Anchored = HasLampSupport(point, normal),
                SupportPoint = point, SupportNormal = normal });
            Dirty(); player.Persistence?.RequestCheckpoint(); return lamp;
        }

        private WorkLamp Spawn(LampSnapshot state)
        {
            var lamp = Instantiate(lampPrefab, transform); lamp.name = "Work lamp " + (state.Slot + 1);
            lamp.Initialize(this, state); lamps.Add(lamp); return lamp;
        }

        public bool Retrieve(WorkLamp lamp)
        {
            if (!lamps.Remove(lamp)) return false;
            lamp.gameObject.SetActive(false); Destroy(lamp.gameObject); Dirty();
            player.Persistence?.RequestCheckpoint(); return true;
        }

        public bool PlaceMark(MarkSnapshot state)
        {
            if (state == null || (int)state.Kind < 0 || (int)state.Kind >= 3 || marks.Count >= MaximumMarks || MarkOverlaps(state.Position)) return false;
            var mark = new WorldMark(this, stencils[(int)state.Kind], markMaterial);
            if (!mark.Project(state)) { mark.Dispose(); return false; }
            marks.Add(mark); RegisterRenderer(mark.Renderer); Dirty(); player.Persistence?.RequestCheckpoint(); return true;
        }

        private bool MarkOverlaps(Vector3 point) => marks.Exists(m => (m.State.Position - point).sqrMagnitude < .25f);

        private WorldMark AimedMark()
        {
            if (!player.TryGetTarget(player.Tuning.InteractReach, out var hit) || !IsSurface(hit.collider)) return null;
            foreach (var mark in marks)
                if ((mark.State.Position - hit.point).sqrMagnitude < .085f && Vector3.Dot(mark.State.Rotation * Vector3.forward, hit.normal) > .5f) return mark;
            return null;
        }

        public string MarkPrompt()
        {
            var mark = AimedMark();
            return mark == null ? "" : $"{Binding(PlayerBinding.Interact)}  Erase {MarkName(mark.State.Kind)} marking";
        }

        public bool TryEraseMark()
        {
            var mark = AimedMark(); if (mark == null) return false;
            marks.Remove(mark); mark.Dispose(); Dirty(); player.Persistence?.RequestCheckpoint(); return true;
        }

        public bool IsSurface(Collider collider) => collider != null && (collider.GetComponentInParent<TerrainVolume>() == terrain
            || collider.GetComponentInParent<PermanentTerrainBoundary>() != null);

        public bool ProjectSurface(Vector3 point, Vector3 normal, out RaycastHit hit)
            => SurfaceRay(point + normal * .18f, normal, .38f, .45f, out hit);

        public bool HasSupport(Vector3 point, Vector3 normal)
            => SurfaceRay(point + normal * .012f, normal, .075f, .35f, out _);

        private bool IsLampSurface(Collider collider) => collider != null && !collider.isTrigger
            && collider.GetComponentInParent<WorkLamp>() == null && collider.GetComponentInParent<FpsPlayer>() == null;

        public bool HasLampSupport(Vector3 point, Vector3 normal)
        {
            if (!Physics.Raycast(point + normal * .012f, -normal, out var hit, .075f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            // Fixed scenery accepts attachment; loose props let the lamp settle with physics.
            return IsLampSurface(hit.collider) && hit.rigidbody == null && Vector3.Dot(hit.normal, normal) > .35f;
        }

        private bool TryLampPosition(Vector3 point, Vector3 normal, Quaternion orientation, out Vector3 position)
        {
            // Lift the compact base clear of an uneven patch instead of requiring a flat footprint.
            for (int step = 0; step < 6; step++)
            {
                position = point + normal * (.02f + step * .035f);
                if (HasLampClearance(position, orientation)) return true;
            }
            position = point + normal * .02f;
            return false;
        }

        private bool SurfaceRay(Vector3 origin, Vector3 normal, float distance, float alignment, out RaycastHit hit)
        {
            // Finds or lamps resting over paint are occluders, not the paint's support.
            // Their movement must not invalidate the whole checkpoint on restore.
            hit = default;
            int count = Physics.RaycastNonAlloc(origin, -normal, surfaceHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == surfaceHits.Length) return false;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (surfaceHits[i].distance < closest && IsSurface(surfaceHits[i].collider))
                { hit = surfaceHits[i]; closest = hit.distance; }
            return hit.collider != null && Vector3.Dot(hit.normal, normal) > alignment;
        }

        public bool HasLampClearance(Vector3 position, Quaternion orientation)
        {
            Vector3 center = position + orientation * LampCenter;
            int count = Physics.OverlapBoxNonAlloc(center, LampHalfSize, overlaps, orientation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (overlaps[i] != null) return false;
            // Player may use Ignore Raycast, so explicitly reject placement inside their body.
            var motor = player.GetComponent<CharacterController>();
            if (motor != null && motor.enabled && motor.bounds.SqrDistance(center) < .06f) return false;
            return true;
        }

        private void TerrainChanged(Bounds bounds)
        {
            if (terrain.IsRestoring) return;
            bounds.Expand(.6f);
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                var lamp = lamps[i];
                if (!bounds.Contains(lamp.transform.position)) continue;
                if (terrain.IsSolid(lamp.transform.TransformPoint(LampCenter))) Retrieve(lamp);
                else lamp.CheckSupport();
            }
            for (int i = marks.Count - 1; i >= 0; i--)
                if (bounds.Intersects(marks[i].Bounds) && !marks[i].Supported())
                { marks[i].Dispose(); marks.RemoveAt(i); Dirty(); }
        }

        public WorksiteSnapshot Capture()
        {
            var result = new WorksiteSnapshot { Lamps = new LampSnapshot[lamps.Count], Marks = new MarkSnapshot[marks.Count] };
            for (int i = 0; i < lamps.Count; i++) result.Lamps[i] = lamps[i].Capture();
            for (int i = 0; i < marks.Count; i++)
            {
                var mark = marks[i].State;
                result.Marks[i] = new MarkSnapshot { Kind = mark.Kind, Position = mark.Position, Rotation = mark.Rotation };
            }
            return result;
        }

        public void Restore(WorksiteSnapshot state)
        {
            state.Validate(); Cancel();
            for (int i = lamps.Count - 1; i >= 0; i--) { lamps[i].gameObject.SetActive(false); Destroy(lamps[i].gameObject); }
            lamps.Clear(); foreach (var mark in marks) mark.Dispose(); marks.Clear();
            foreach (var lamp in state.Lamps) Spawn(lamp).CheckSupport();
            foreach (var saved in state.Marks)
            {
                var mark = new WorldMark(this, stencils[(int)saved.Kind], markMaterial);
                if (!mark.Project(saved)) { mark.Dispose(); throw new System.IO.InvalidDataException("A saved marking has no supporting surface."); }
                marks.Add(mark); RegisterRenderer(mark.Renderer);
            }
            Dirty();
        }

        private void OnDestroy() { foreach (var mark in marks) mark.Dispose(); marks.Clear(); }
    }
}
