using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SomethingDownThere
{
    // The one machine in first person (concept 04 section 1): model parts are named L<from>[-<to>]_<Part>__<Material>
    // and show while the owned tool level is in range (art/stylized-western-shovel, art/hand-mining-drill, task 105). The rig
    // sits small and close, inside the player's capsule, so it can never poke through a wall; its
    // width and height follow the field of view so it keeps its place on screen. Only the tool
    // moves, never the camera: scoops per stroke up to level 6, a spinning bit from the drill on,
    // and each material family (wide scoop, precise bite, hard head) moves differently.
    [DisallowMultipleComponent]
    public sealed class ToolRigPresenter : MonoBehaviour
    {
        public enum MotionFamily { Scoop, Bite, Hard }
        public const float ReferenceFov = 75f;
        private const float LowerSeconds = .15f, CutWindow = .15f, MaxSpinStep = 70f;
        private static readonly Regex StageName = new Regex(@"^L(\d{2})(?:-(\d{2}))?_");
        // A shovel stroke pries and then scoops; the drill has no stroke, only its spin and chatter. The shovel's stroke
        // takes ScoopLength times a plain push and turns about the blade's tip; the drill lowers away about the socket
        // (model metres). The scoop lifts the blade from ScoopAt of the stroke, where the dirt is removed.
        private const float ScoopLength = 2.2f, ScoopPivot = 1.2f, DrillPivot = .9f, ScoopAt = .6f;

        [SerializeField] private FpsPlayer player;
        [SerializeField] private Transform model;
        // Rising from the bottom edge right of centre with the blade face turned to the view; only the head
        // and what is bolted behind it show. Visible parts stay about 0.3 m from the eye.
        private static readonly Vector3 RestPosition = new Vector3(.14f, -.26f, -.05f), RestEuler = new Vector3(-36f, -11f, 4f);
        // The shovel at 75% of its first size and the drill at the player's Size dial (admin), scaled about the socket
        // (Socket, model metres), which keeps its place on screen; the Position dial then moves the drill along the tool.
        private const float ModelScale = .3f, ShovelScale = .75f, Socket = .9f;
        // The drill (the purchased jackhammer) sits further forward along the tool (model metres) so its body shows in
        // the lower right, and tips down so its head points below the crosshair.
        private static readonly Vector3 DrillTilt = new Vector3(10f, 0f, 0f), DrillShift = new Vector3(0f, 0f, .3f);

        private readonly List<(GameObject part, int from, int to)> parts = new List<(GameObject, int, int)>();
        private readonly List<(Transform part, Quaternion rest)> spinners = new List<(Transform, Quaternion)>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private int shownLevel = -1, seenStrokes = -1;
        private float stroke = 1f, strokeSeconds = .3f, lowered = 1f, spinSpeed, spinAngle, sinceCut = 10f;
        private MotionFamily family;
        private bool sink;
        // Each stroke differs a little in depth, side and roll so held digging never looks mechanical.
        private float strokeDepth = 1f, strokeSide, strokeRoll;

        public int ShownLevel => shownLevel;
        public bool Hidden => lowered >= 1f;
        public float SpinSpeed => spinSpeed;

        public static bool TryParseStage(string name, out int from, out int to)
        {
            from = to = 0;
            var match = StageName.Match(name ?? "");
            if (!match.Success) return false;
            from = int.Parse(match.Groups[1].Value);
            to = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : EquipmentProgression.LevelCount;
            return from >= 1 && from <= to && to <= EquipmentProgression.LevelCount;
        }

        // Every current ground scoops; harder grounds bite or hammer.
        public static MotionFamily Family(TerrainMaterialId material) => MotionFamily.Scoop;

        private void Awake()
        {
            if (model == null) return;
            foreach (var part in model.GetComponentsInChildren<Transform>(true))
            {
                if (!TryParseStage(part.name, out int from, out int to)) continue;
                parts.Add((part.gameObject, from, to));
                if (part.name.Contains("Spin")) spinners.Add((part, part.localRotation));
            }
            model.GetComponentsInChildren(true, renderers);
        }

        private void LateUpdate()
        {
            if (player == null || model == null) return;
            int level = player.EffectiveShovelLevel;
            if (level != shownLevel) ShowLevel(level);
            bool hide = !player.isActiveAndEnabled || player.IsMenuOpen
                || (player.WorksiteTools != null && player.WorksiteTools.IsPlacing) || player.ExtractionMarkProgress > 0f;
            lowered = Mathf.MoveTowards(lowered, hide ? 1f : 0f, Time.unscaledDeltaTime / LowerSeconds);
            foreach (var renderer in renderers) renderer.enabled = lowered < 1f;

            if (player.StrokesStarted != seenStrokes)
            {
                if (seenStrokes >= 0 && player.StrokesStarted > seenStrokes) BeginStroke(level);
                seenStrokes = player.StrokesStarted;
            }
            float dt = Time.deltaTime;
            sinceCut += dt;
            bool cutting = sinceCut < CutWindow;
            float spinTarget = player.ShavingEnabled && cutting ? (family == MotionFamily.Hard ? 1900f : 3200f) : 0f;
            spinSpeed = Mathf.MoveTowards(spinSpeed, spinTarget, 12000f * dt);
            // A two-flute bit turning more than a quarter turn per frame reads as spinning backwards.
            spinAngle = Mathf.Repeat(spinAngle + Mathf.Min(spinSpeed * dt, MaxSpinStep), 360f);
            foreach (var (part, rest) in spinners) part.localRotation = rest * Quaternion.AngleAxis(spinAngle, Vector3.forward);

            Vector3 offset = Vector3.zero, turn = Vector3.zero;
            bool drill = EquipmentProgression.UsesDrill(level);
            Vector3 restEuler = drill ? RestEuler + DrillTilt : RestEuler;
            float power = 1f + .05f * (level - 1);
            if (stroke < 1f)
            {
                stroke = Mathf.Min(1f, stroke + dt / strokeSeconds);
                // Soft ground takes a longer push, hard ground a shorter one with a little shudder.
                float reach = (sink ? 1.5f : 1f) * power;
                float amount = (family == MotionFamily.Scoop ? .02f : family == MotionFamily.Bite ? .016f : .012f) * reach * strokeDepth;
                var (move, angles) = PryScoop(stroke, amount);
                offset = Quaternion.Euler(restEuler) * move;
                turn = angles;
                if (family == MotionFamily.Hard)
                {
                    float shudder = Mathf.Sin(stroke * 38f) * (1f - stroke);
                    offset += new Vector3(.0008f, .0008f, 0f) * shudder;
                }
            }
            if (player.ShavingEnabled && cutting)
            {
                float t = Time.time * 31f, chatter = (family == MotionFamily.Hard ? .0008f : .0004f) * power;
                offset += new Vector3((Mathf.PerlinNoise(t, 0f) - .5f) * chatter, (Mathf.PerlinNoise(0f, t) - .5f) * chatter - .0015f, .0015f);
            }
            float away = lowered * lowered * (3f - 2f * lowered);
            offset += new Vector3(.03f, -.2f, -.02f) * away;
            turn.x += 20f * away;

            float scale = ModelScale * (drill ? player.DrillLook(FpsPlayer.DrillDial.Size) : ShovelScale);
            var restPose = Quaternion.Euler(restEuler);
            var pose = Quaternion.Euler(restEuler + turn);
            // A stroke turns the tool about its own pivot along it.
            var anchor = Vector3.forward * (Socket * (ModelScale - scale))
                + (drill ? DrillShift * scale + Vector3.forward * player.DrillLook(FpsPlayer.DrillDial.Position) : Vector3.zero);
            var pivot = Vector3.forward * ((drill ? DrillPivot : ScoopPivot) * scale);
            model.localPosition = RestPosition + restPose * anchor + restPose * pivot - pose * pivot + offset;
            model.localRotation = pose;
            model.localScale = Vector3.one * scale;
            var view = player.ViewCamera;
            float k = view == null ? 1f : Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(ReferenceFov * .5f * Mathf.Deg2Rad);
            transform.localScale = new Vector3(k, k, 1f);
        }

        private void BeginStroke(int level)
        {
            sinceCut = 0f;
            var material = player.LastDigMaterial;
            family = Family(material);
            sink = material == TerrainMaterialId.Backfill;
            if (player.ShavingEnabled) return;
            stroke = 0f;
            strokeSeconds = StrokeSeconds(player.LastDigInterval, material);
            strokeDepth = Random.Range(.8f, 1.2f); strokeSide = Random.Range(-1f, 1f); strokeRoll = Random.Range(-1f, 1f);
        }

        // Seconds from a shovel stroke's start to its scoop, when its dirt is removed, for a dig of this cadence and ground.
        public static float ScoopDelay(float digInterval, TerrainMaterialId material) => StrokeSeconds(digInterval, material) * ScoopAt;

        private static float StrokeSeconds(float digInterval, TerrainMaterialId material)
        {
            return Mathf.Clamp(digInterval * .35f, .12f, .26f) * (Family(material) == MotionFamily.Bite ? .9f : 1f)
                * ScoopLength;
        }

        // One stroke at progress t (0..1): the move in the tool's own axes (x side, y off its face, z along it, metres)
        // and the turn (degrees: x tips the blade down, y swings it right, z rolls it); `amount` is how far the
        // ground lets it push.
        private (Vector3 move, Vector3 turn) PryScoop(float t, float amount)
        {
            // Push in and lever on the blade's tip, then, as the lever eases, lift the blade clear with its face
            // tipping up: a scoop of what it levered loose, held a moment before the return.
            float push = Rise(t, 0f, .2f) * (1f - Rise(t, .8f, 1f)), lever = Pulse(t, .2f, .42f, .7f);
            float scoop = Rise(t, .45f, .68f) * (1f - Rise(t, .84f, 1f));
            float side = strokeSide, roll = strokeRoll;
            return (new Vector3(.003f * side * push, .032f * scoop, 1.2f * amount * push - .6f * amount * scoop),
                    new Vector3(-14f * lever - 13f * scoop, 1f * side * lever, 3f * roll * lever + 3f * roll * scoop));
        }

        private static float Rise(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        private static float Pulse(float t, float a, float peak, float b) => t < peak ? Rise(t, a, peak) : 1f - Rise(t, peak, b);

        private void ShowLevel(int level)
        {
            shownLevel = level;
            foreach (var (part, from, to) in parts) part.SetActive(level >= from && level <= to);
        }
    }
}
