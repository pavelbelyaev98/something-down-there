using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class SurfacePerformanceFixture
    {
        // Opt-in native A/B measurement in the existing disposable validation player.
        // Identical camera, graphics settings, density and terrain in both states.
        private IEnumerator MeasureDiscoveries(FpsPlayer player, string output, int startupLimit)
        {
            int appliedLimit = Application.targetFrameRate;
            bool appliedVSync = QualitySettings.vSyncCount != 0;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            var terrain = player.ExcavationTerrain;
            var field = player.Discoveries;
            while (!field.Initialized) yield return null;
            var renderers = field.Finds.Select(f => f.GetComponent<MeshRenderer>()).ToArray();
            if (renderers.Length == 0 || renderers.Length != field.Catalog.TotalCount)
                throw new InvalidOperationException("Discovery benchmark requires the complete catalog population.");
            var results = new List<object>();
            var timings = new FrameTiming[1];
            string directory = Path.GetDirectoryName(Path.GetFullPath(output));
            Directory.CreateDirectory(directory);
            foreach (bool excavated in new[] { false, true })
            {
                if (excavated)
                {
                    var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize);
                    for (float depth = .2f; depth <= 3.05f; depth += .2f)
                    for (float x = 9; x <= 13; x += .3f)
                    for (float z = 3; z <= 7; z += .3f)
                        grid.RemoveSphere(new Vector3(x, SiteLayout.Extent.y - depth + .6f, z), .6f, out _);
                    yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
                    player.ViewCamera.transform.position = terrain.transform.TransformPoint(new Vector3(11, 103, 1.4f));
                    player.ViewCamera.transform.LookAt(terrain.transform.TransformPoint(new Vector3(11, 97, 5.1f)));
                }
                var daylight = terrain.GetComponent<ExcavationDaylight>();
                while (daylight.IsUpdating) yield return null;
                double settleUntil = Time.realtimeSinceStartupAsDouble + 4;
                while (Time.realtimeSinceStartupAsDouble < settleUntil) yield return null;
                var visibility = renderers.Select(r => r.enabled).ToArray();
                foreach (bool allRendering in new[] { true, false, true, false })
                {
                    for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = allRendering || visibility[i];
                    for (int i = 0; i < 80; i++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
                    var frames = new List<double>(); var gpu = new List<double>();
                    for (int i = 0; i < 240; i++)
                    {
                        FrameTimingManager.CaptureFrameTimings(); yield return null;
                        frames.Add(Time.unscaledDeltaTime * 1000);
                        if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                            gpu.Add(timings[0].gpuFrameTime);
                    }
                    if (gpu.Count < frames.Count / 2)
                    {
                        Application.Quit(1);
                        throw new InvalidOperationException("No reliable GPU timings. Keep the validation window visible; discard this run.");
                    }
                    results.Add(new { scenario = excavated ? "excavatedPit" : "surface", allRendering,
                        renderedFinds = renderers.Count(r => r.enabled), frame = Summary(frames), gpu = Summary(gpu) });
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, excavated ? "pit.png" : "surface.png"));
                yield return null; yield return null;
            }
            yield return MeasureDigging(player, terrain.GetComponent<SurfaceGrassRenderer>(), results);
            // Check the normal runtime limiter too; the A/B samples above deliberately disable it.
            Application.targetFrameRate = GamePreferences.DefaultFrameLimit;
            for (int i = 0; i < 80; i++) yield return null;
            var capped = new List<double>();
            for (int i = 0; i < 240; i++) { yield return null; capped.Add(Time.unscaledDeltaTime * 1000); }
            File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(new {
                scope = "Native uncapped renderer A/B and real cuts in disposable world; existing device preferences, no save writes",
                startupLimit, appliedLimit, appliedVSync, capped = Summary(capped),
                width = Screen.width, height = Screen.height, gpu = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType, population = field.Finds.Count,
                renderScale = (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)?.renderScale, msaa = player.GameSettings.Values.Msaa,
                results
            }, Newtonsoft.Json.Formatting.Indented));
            Application.Quit();
        }
    }
}
