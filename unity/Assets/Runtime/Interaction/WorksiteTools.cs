using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    [DisallowMultipleComponent]
    public sealed class WorksiteTools : MonoBehaviour
    {
        public const int MaximumMarks = 96;
        // Only the nearest lamps shine: each needs six faces of the shared shadow atlas, and the
        // Forward renderer takes eight lights per object. The rest keep their glowing dome.
        public const int LitLampBudget = 8;
        // Range + cull distance is the sun shadow distance (URP drops local shadows beyond it),
        // so the surface keeps its authored shadows; lamps fade out over the last metres.
        public const float LightRange = 22, LightCullDistance = 30, LightFadeDistance = 6;
        public static readonly Vector3 LampCenter = new Vector3(0, .056f, 0);
        public static readonly Vector3 LampHalfSize = new Vector3(.056f, .056f, .056f);
        [SerializeField] private FpsPlayer player;
        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private WorkLamp lampPrefab;
        [SerializeField] private Mesh[] stencils;
        [SerializeField] private Material markMaterial, validPreview, invalidPreview;
        private readonly List<WorkLamp> lamps = new List<WorkLamp>(EquipmentProgression.StarterLamps);
        private readonly List<WorldMark> marks = new List<WorldMark>(MaximumMarks);
        private readonly Collider[] overlaps = new Collider[32];
        private readonly RaycastHit[] surfaceHits = new RaycastHit[32];
        private readonly float[] lampDistances = new float[EquipmentProgression.MaximumLamps];
        private readonly int[] lampOrder = new int[EquipmentProgression.MaximumLamps];
        private GameObject lampGhost;
        private Renderer[] ghostRenderers;
        private WorldMark markGhost;
        private int placement; // 0 none, 1 lamp, 2..4 stencils.
        private float rotation;
        private RaycastHit surface;
        private Vector3 proposedPosition;
        private Quaternion proposedRotation;
        private LampSnapshot lampPose;
        private bool valid;
        private string reason = "Aim at ground within reach";
        public FpsPlayer Player => player;
        public TerrainVolume Terrain => terrain;
        public IReadOnlyList<WorkLamp> Lamps => lamps;
        public int MarkCount => marks.Count;
        public int AvailableLamps => Mathf.Max(0, player.LampKit.Owned - lamps.Count);
        public long Revision { get; private set; }
        public bool IsPlacing => placement != 0;
        public bool PlacementValid => IsPlacing && valid;
        public bool Configured => player != null && terrain != null && lampPrefab != null && lampPrefab.WorkLight != null
            && stencils != null && stencils.Length == 3 && Array.TrueForAll(stencils, mesh => mesh != null)
            && markMaterial != null && validPreview != null && invalidPreview != null;
        // A round lamp has no visible heading, so only markings offer rotation.
        public string PlacementPrompt => !IsPlacing ? "" : (valid ? $"{Binding(PlayerBinding.Dig)}  Place {SelectionName}" : reason)
            + (placement > 1 ? $"\n{Binding(PlayerBinding.RotatePlacement)} Rotate  |  " : "\n") + $"{Binding(PlayerBinding.Grab)} Cancel"
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
                if (lamps[i].transform.position.y < terrain.transform.position.y - 2) Retrieve(lamps[i]);
            int candidates = 0;
            for (int i = 0; i < lamps.Count; i++)
            {
                float distance = (lamps[i].transform.position - eye).sqrMagnitude;
                if (distance >= LightCullDistance * LightCullDistance) continue;
                lampDistances[candidates] = distance; lampOrder[candidates++] = i;
            }
            Array.Sort(lampDistances, lampOrder, 0, candidates);
            for (int i = 0; i < lamps.Count; i++) lamps[i].Shine = 0;
            for (int i = 0; i < Mathf.Min(candidates, LitLampBudget); i++)
                lamps[lampOrder[i]].Shine = Mathf.Clamp01((LightCullDistance - Mathf.Sqrt(lampDistances[i])) / LightFadeDistance);
            foreach (var lamp in lamps) lamp.Tick(player.GameplayActive, Time.unscaledDeltaTime);
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
            if (placement == 1)
            {
                var eye = player.ViewCamera.transform;
                lampPose = SolveLamp(eye.position, eye.forward, player.Tuning.InteractReach, rotation);
                valid = AvailableLamps > 0;
                reason = "All lamps placed: pick one up, or buy more at the computer";
                EnsureLampGhost(); lampGhost.SetActive(true); lampGhost.transform.SetPositionAndRotation(lampPose.Position, lampPose.Rotation);
                foreach (var renderer in ghostRenderers) renderer.sharedMaterial = valid ? validPreview : invalidPreview;
                return;
            }
            bool hit = player.TryGetTarget(player.Tuning.InteractReach, out surface);
            if (!hit || !IsSurface(surface.collider))
            {
                if (lampGhost != null) lampGhost.SetActive(false);
                if (markGhost != null) markGhost.Object.SetActive(false);
                return;
            }
            Vector3 normal = surface.normal;
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
            if (placement == 1) return PlaceLamp(lampPose) != null;
            return PlaceMark(new MarkSnapshot { Kind = (WorldMarkKind)(placement - 2), Position = proposedPosition, Rotation = proposedRotation });
        }

        // Places exactly the previewed pose. Only an empty kit refuses.
        public WorkLamp PlaceLamp(LampSnapshot pose)
        {
            if (pose == null || AvailableLamps <= 0) return null;
            int slot = 0;
            for (; slot < EquipmentProgression.MaximumLamps; slot++) if (!lamps.Exists(l => l.Slot == slot)) break;
            var lamp = Spawn(new LampSnapshot { Slot = slot, Position = pose.Position, Rotation = pose.Rotation, Anchored = pose.Anchored,
                SupportPoint = pose.SupportPoint, SupportNormal = pose.SupportNormal });
            Dirty(); player.Persistence?.RequestCheckpoint(); return lamp;
        }

        // Turns an aim into a lamp pose and never fails. Floors stand the lamp upright; walls and
        // ceilings take it spike-first along the surface. A blocked spot (crevice, corner, next to a
        // find or lamp) lifts the lamp a little, then backs it along the aim to the first clear spot,
        // never behind the aimed surface. It anchors only when its base touches fixed scenery;
        // otherwise it is a loose lamp that falls and settles.
        public LampSnapshot SolveLamp(Vector3 origin, Vector3 direction, float reach, float yaw)
        {
            direction = direction.normalized;
            bool hit = Physics.Raycast(origin, direction, out var aimed, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float distance = hit ? aimed.distance : Mathf.Min(2f, reach);
            Vector3 up = hit && aimed.normal.y <= .7f ? aimed.normal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(direction, up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(Vector3.right, up);
            var orientation = Quaternion.AngleAxis(yaw, up) * Quaternion.LookRotation(forward.normalized, up);
            Vector3 point = origin + direction * distance;
            for (int step = 0; step < 8; step++)
            {
                Vector3 position = point + up * (.004f + step * .025f);
                if (HasLampClearance(position, orientation)) return LampPose(position, orientation, up);
            }
            Vector3 offset = orientation * LampCenter, start = origin + direction * Mathf.Min(.3f, distance);
            if (HasLampClearance(start - offset, orientation))
            {
                float travel = distance - Mathf.Min(.3f, distance);
                if (Physics.BoxCast(start, LampHalfSize, direction, out var block, orientation, travel,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) travel = Mathf.Max(0, block.distance - .01f);
                return LampPose(start + direction * travel - offset, orientation, up);
            }
            // Pressed against a wall in a crawl space: a loose lamp just ahead; physics settles it.
            return LampPose(start - offset, orientation, up);
        }

        private LampSnapshot LampPose(Vector3 position, Quaternion orientation, Vector3 up) => new LampSnapshot
        {
            Position = position, Rotation = orientation, SupportPoint = position, SupportNormal = up,
            Anchored = HasLampSupport(position, up)
        };

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

        // The spike reaches a few centimetres past the base: support within that distance holds it.
        public bool HasLampSupport(Vector3 point, Vector3 normal)
        {
            if (!Physics.Raycast(point + normal * .012f, -normal, out var hit, .075f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            // Fixed scenery accepts attachment; loose props (lamps, finds) let the lamp settle with physics.
            return hit.rigidbody == null && Vector3.Dot(hit.normal, normal) > .35f;
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
            // Lamps ignore the player's body (like finds), so it never blocks placement.
            for (int i = 0; i < count; i++) if (overlaps[i] != null) return false;
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
                else lamp.CheckSupport(); // Also wakes a loose lamp whose ground was dug away.
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
