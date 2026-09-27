using UnityEngine;

namespace SomethingDownThere
{
    // Quality presets set every presentation row except render resolution, which stays its own
    // choice. High is the accepted look; lower tiers trade grass reach, shadow detail and
    // contact shading for frame rate. Nothing here touches finds, physics or tunnel darkness.
    public static class GraphicsQuality
    {
        public const int Custom = -1, Low = 0, Medium = 1, High = 2, Ultra = 3;
        public static readonly string[] PresetNames = { "Low", "Medium", "High", "Ultra" };
        public const int AntiAliasingOff = 0, Fxaa = 1, Msaa2 = 2, Msaa4 = 3, Msaa8 = 4;
        public static readonly string[] AntiAliasingNames = { "Off", "FXAA", "2×", "4×", "8×" };
        public const int ViewLow = 0, ViewMedium = 1, ViewHigh = 2;
        public static readonly string[] ViewDistanceNames = { "Low", "Medium", "High" };
        public const int MinimumRenderScale = 50, RenderScaleStep = 5;

        private readonly struct Preset
        {
            public readonly int Shadows, AntiAliasing, ViewDistance, TextureLimit, Filtering;
            public readonly bool AmbientOcclusion;
            public Preset(int shadows, int antiAliasing, int viewDistance, bool ambientOcclusion, int textureLimit, int filtering)
            {
                Shadows = shadows; AntiAliasing = antiAliasing; ViewDistance = viewDistance;
                AmbientOcclusion = ambientOcclusion; TextureLimit = textureLimit; Filtering = filtering;
            }
        }

        private static readonly Preset[] Presets =
        {
            new Preset(1, Fxaa, ViewLow, false, 1, 1),
            // 2× MSAA alone costs about 1 ms at 1440p; FXAA makes Medium a real step down from High.
            new Preset(2, Fxaa, ViewMedium, true, 0, 2),
            new Preset(3, Msaa2, ViewHigh, true, 0, 2),
            new Preset(3, Msaa4, ViewHigh, true, 0, 2),
        };

        // Mesh LOD error for finds. Unity's default of 1 keeps full detail far beyond where it
        // shows; 2.5 is indistinguishable in the pit and saves about an eighth of its GPU time.
        // Savings level off near 4, so lower tiers stop there.
        public static float MeshLodThreshold(int viewDistance) =>
            viewDistance >= ViewHigh ? 2.5f : viewDistance <= ViewLow ? 4f : 3.2f;

        // Terrain beyond this distance draws from a low-resolution basemap. High covers the whole
        // canyon (about 0.5 ms at 1440p on the reference GPU, 1 ms in flight).
        public static float BasemapDistance(int viewDistance) =>
            viewDistance >= ViewHigh ? 1000f : viewDistance <= ViewLow ? 80f : 150f;

        public static int MsaaSamples(int antiAliasing) => antiAliasing == Msaa2 ? 2 : antiAliasing == Msaa4 ? 4 : antiAliasing == Msaa8 ? 8 : 1;

        public static void Apply(GamePreferenceValues values, int preset)
        {
            var p = Presets[Mathf.Clamp(preset, Low, Ultra)];
            values.Shadows = p.Shadows; values.AntiAliasing = p.AntiAliasing; values.ViewDistance = p.ViewDistance;
            values.AmbientOcclusion = p.AmbientOcclusion; values.TextureLimit = p.TextureLimit; values.Filtering = p.Filtering;
        }

        // The preset the rows currently equal, or Custom after any individual change.
        public static int Match(GamePreferenceValues values)
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                if (values.Shadows == p.Shadows && values.AntiAliasing == p.AntiAliasing && values.ViewDistance == p.ViewDistance
                    && values.AmbientOcclusion == p.AmbientOcclusion && values.TextureLimit == p.TextureLimit && values.Filtering == p.Filtering)
                    return i;
            }
            return Custom;
        }

        public static int SnapRenderScale(int percent) =>
            Mathf.Clamp(Mathf.RoundToInt(percent / (float)RenderScaleStep) * RenderScaleStep, MinimumRenderScale, 100);
    }

    // Auto-configure's decision, separate from measurement so it is testable. Candidates run
    // best-first: presets at full resolution, then Low at reduced resolution. Choose the first
    // preset reaching the preferred rate (the monitor, clamped to 60–90); otherwise the best
    // preset holding 60; resolution only drops when even Low misses 60.
    public sealed class GraphicsRecommendationPlan
    {
        public readonly struct Candidate
        {
            public readonly int Preset, RenderScale;
            public Candidate(int preset, int renderScale) { Preset = preset; RenderScale = renderScale; }
        }

        public static readonly Candidate[] Candidates =
        {
            new Candidate(GraphicsQuality.High, 100), new Candidate(GraphicsQuality.Medium, 100), new Candidate(GraphicsQuality.Low, 100),
            new Candidate(GraphicsQuality.Low, 85), new Candidate(GraphicsQuality.Low, 75), new Candidate(GraphicsQuality.Low, 65),
            new Candidate(GraphicsQuality.Low, 50),
        };
        public const int MinimumFps = 60, PreferredCeilingFps = 90;
        // Measured views vary; keep a margin so heavier moments stay above the target.
        public const float Headroom = 1.1f;
        private readonly float preferredMs, minimumMs;
        private int index, fallback = -1;
        public Candidate Next => Candidates[index];
        public bool Decided { get; private set; }
        public Candidate Choice { get; private set; }

        public GraphicsRecommendationPlan(int refreshRate)
        {
            preferredMs = 1000f / Mathf.Clamp(refreshRate, MinimumFps, PreferredCeilingFps);
            minimumMs = 1000f / MinimumFps;
        }

        public void Record(float frameMilliseconds)
        {
            if (Decided) return;
            float worst = frameMilliseconds * Headroom;
            var candidate = Candidates[index];
            if (candidate.RenderScale == 100)
            {
                if (worst <= preferredMs) { Decide(candidate); return; }
                if (fallback < 0 && worst <= minimumMs) fallback = index;
                if (candidate.Preset == GraphicsQuality.Low && fallback >= 0) { Decide(Candidates[fallback]); return; }
            }
            else if (worst <= minimumMs) { Decide(candidate); return; }
            if (++index >= Candidates.Length) Decide(Candidates[Candidates.Length - 1]);
        }

        private void Decide(Candidate candidate) { Choice = candidate; Decided = true; }
    }
}
