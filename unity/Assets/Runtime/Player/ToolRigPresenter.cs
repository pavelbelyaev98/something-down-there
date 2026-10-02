using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SomethingDownThere
{
    // The one machine in first person (concept 04 section 1): model parts are named
    // L<from>[-<to>]_<Part>__<Material> and show while the owned tool level is in range. The rig
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

        [SerializeField] private FpsPlayer player;
        [SerializeField] private Transform model;
        // Rising from the bottom edge right of centre with the blade face turned to the view; only the head
        // and what is bolted behind it show. Visible parts stay about 0.3 m from the eye.
        private static readonly Vector3 RestPosition = new Vector3(.14f, -.26f, -.05f), RestEuler = new Vector3(-36f, -11f, 4f);
        private const float ModelScale = .3f;
        // Strokes turn the tool about its socket, so the head dips instead of the whole shaft swinging.
        private static readonly Vector3 Pivot = new Vector3(0f, 0f, .9f);

        private readonly List<(GameObject part, int from, int to)> parts = new List<(GameObject, int, int)>();
        private readonly List<(Transform part, Quaternion rest)> spinners = new List<(Transform, Quaternion)>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private int shownLevel = -1, seenStrokes = -1;
        private float stroke = 1f, strokeSeconds = .3f, lowered = 1f, spinSpeed, spinAngle, sinceCut = 10f;
        private MotionFamily family;
        private bool crisp, sink;
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

        public static MotionFamily Family(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Clay or TerrainMaterialId.PondClay => MotionFamily.Bite,
            TerrainMaterialId.Rock or TerrainMaterialId.Concrete or TerrainMaterialId.FracturedRock
                or TerrainMaterialId.FracturedConcrete or TerrainMaterialId.Crack => MotionFamily.Hard,
            _ => MotionFamily.Scoop
        };

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

            if (player.SuccessfulStrokes != seenStrokes)
            {
                if (seenStrokes >= 0 && player.SuccessfulStrokes > seenStrokes) BeginStroke(level);
                seenStrokes = player.SuccessfulStrokes;
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
            float power = 1f + .05f * (level - 1);
            if (stroke < 1f)
            {
                stroke = Mathf.Min(1f, stroke + dt / strokeSeconds);
                // A quick push (first 30%) and an eased return, not a symmetric swing.
                float jab = stroke < .3f ? 1f - (1f - stroke / .3f) * (1f - stroke / .3f) : 1f - Mathf.SmoothStep(0f, 1f, (stroke - .3f) / .7f);
                float reach = (sink ? 1.5f : 1f) * (crisp ? 1.1f : 1f) * power;
                // A small thrust along the tool, never a swing: soft ground takes a longer push, hard
                // ground a short jab with a little shudder.
                Vector3 along = Quaternion.Euler(RestEuler) * Vector3.forward;
                float thrust = family == MotionFamily.Scoop ? .02f : family == MotionFamily.Bite ? .016f : .012f;
                offset = along * (thrust * reach * strokeDepth * jab) + new Vector3(.003f * strokeSide, 0f, 0f) * jab;
                turn = new Vector3(1.2f * strokeSide, 0f, 2.5f * strokeRoll) * jab;
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

            var restPose = Quaternion.Euler(RestEuler);
            var pose = Quaternion.Euler(RestEuler + turn);
            var pivot = Pivot * ModelScale;
            model.localPosition = RestPosition + restPose * pivot - pose * pivot + offset;
            model.localRotation = pose;
            model.localScale = Vector3.one * ModelScale;
            var view = player.ViewCamera;
            float k = view == null ? 1f : Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(ReferenceFov * .5f * Mathf.Deg2Rad);
            transform.localScale = new Vector3(k, k, 1f);
        }

        private void BeginStroke(int level)
        {
            sinceCut = 0f;
            var material = player.LastDigMaterial;
            family = Family(material);
            crisp = material is TerrainMaterialId.FracturedRock or TerrainMaterialId.Crack or TerrainMaterialId.FracturedConcrete;
            sink = material == TerrainMaterialId.Backfill;
            if (player.ShavingEnabled) return;
            stroke = 0f;
            strokeSeconds = Mathf.Clamp(player.LastDigInterval * .35f, .12f, .26f)
                * (crisp ? .8f : 1f) * (family == MotionFamily.Bite ? .9f : 1f);
            strokeDepth = Random.Range(.8f, 1.2f); strokeSide = Random.Range(-1f, 1f); strokeRoll = Random.Range(-1f, 1f);
        }

        private void ShowLevel(int level)
        {
            shownLevel = level;
            foreach (var (part, from, to) in parts) part.SetActive(level >= from && level <= to);
        }
    }
}
