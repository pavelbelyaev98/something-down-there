using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Measures this PC once (first launch, or on request) and stores the best preset that keeps
    // play smooth. Candidates render uncapped from a fixed survey view over the site (the
    // heaviest benchmark view); the pose is swapped in only while the camera renders, so no
    // gameplay system sees the camera move.
    public sealed class GraphicsAutoTuner
    {
        // The first frames of the survey compile shaders and stream terrain detail; measuring them
        // penalised whichever candidate ran first, so the survey primes before any sample.
        private const int PrimeFrames = 90, WarmupFrames = 24, SampleFrames = 40;
        private static readonly Vector3 SurveyOffset = new Vector3(0, 16.65f, -11.6f);
        private static readonly Quaternion SurveyTilt = Quaternion.Euler(15, 0, 0);
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private readonly List<float> samples = new List<float>(SampleFrames);
        private Camera camera;
        private Vector3 surveyPosition, savedPosition;
        private Quaternion surveyRotation, savedRotation;
        private GamePreferences settings;
        public bool Running { get; private set; }
        public event Action Changed;

        public IEnumerator Run(GamePreferences preferences, Camera viewCamera, TerrainVolume site, Func<bool> stillValid)
        {
            if (Running || preferences == null || viewCamera == null || site == null) yield break;
            settings = preferences; camera = viewCamera;
            var top = new Vector3(site.Dimensions.x * .5f, site.Dimensions.y, site.Dimensions.z * .5f) * site.CellSize;
            surveyPosition = site.transform.TransformPoint(top) + site.transform.rotation * SurveyOffset;
            surveyRotation = site.transform.rotation * SurveyTilt;
            var plan = new GraphicsRecommendationPlan(preferences.RefreshRate);
            Running = true;
            Changed?.Invoke();
            RenderPipelineManager.beginCameraRendering += BeginSurvey;
            RenderPipelineManager.endCameraRendering += EndSurvey;
            bool measured = false;
            var report = new System.Text.StringBuilder("Graphics auto-configure at " + preferences.RefreshRate + " Hz:");
            try
            {
                bool primed = false;
                while (!plan.Decided)
                {
                    var candidate = preferences.Values.Copy();
                    GraphicsQuality.Apply(candidate, plan.Next.Preset);
                    candidate.RenderScale = plan.Next.RenderScale;
                    candidate.VSync = false; candidate.FrameLimit = -1;
                    preferences.PreviewGraphics(candidate);
                    for (int i = 0, warmup = primed ? WarmupFrames : PrimeFrames; i < warmup; i++)
                    {
                        if (!stillValid()) yield break;
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                    }
                    primed = true;
                    samples.Clear();
                    for (int i = 0; i < SampleFrames; i++)
                    {
                        if (!stillValid()) yield break;
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        samples.Add(FrameCost());
                    }
                    samples.Sort();
                    float median = samples[samples.Count / 2];
                    report.Append(' ').Append(GraphicsQuality.PresetNames[plan.Next.Preset]).Append('@').Append(plan.Next.RenderScale)
                        .Append("% ").Append(median.ToString("F1")).Append(" ms;");
                    plan.Record(median);
                }
                measured = true;
                Debug.Log(report.Append(" chose ").Append(GraphicsQuality.PresetNames[plan.Choice.Preset]).Append('@')
                    .Append(plan.Choice.RenderScale).Append('%'));
            }
            finally
            {
                Stop();
                if (measured) preferences.ApplyRecommendation(plan.Choice.Preset, plan.Choice.RenderScale);
            }
        }

        // Scene teardown or an interrupted run: restore the stored settings and the camera hooks.
        public void Stop()
        {
            if (!Running) return;
            RenderPipelineManager.beginCameraRendering -= BeginSurvey;
            RenderPipelineManager.endCameraRendering -= EndSurvey;
            settings?.EndGraphicsPreview();
            Running = false;
            Changed?.Invoke();
        }

        // GPU and main-thread work, independent of VSync or driver caps; the frame interval
        // stands in where the platform provides no GPU timing.
        private float FrameCost()
        {
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0)
                return (float)Math.Max(timing[0].gpuFrameTime, timing[0].cpuMainThreadFrameTime - timing[0].cpuMainThreadPresentWaitTime);
            return Time.unscaledDeltaTime * 1000f;
        }

        private void BeginSurvey(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != camera) return;
            var view = camera.transform;
            savedPosition = view.position; savedRotation = view.rotation;
            view.SetPositionAndRotation(surveyPosition, surveyRotation);
        }

        private void EndSurvey(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering == camera) camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
        }
    }
}
