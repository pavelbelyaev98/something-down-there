using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere
{
    public sealed partial class SurfacePerformanceFixture
    {
        [HideInInspector] public GameObject[] EnvironmentReferenceTrees;
        // Vendor LOD thresholds of every scenery LOD group, flattened in hierarchy order.
        [HideInInspector] public float[] EnvironmentReferenceLodHeights;
        public static readonly string[] EnvironmentScenery = { "Cliffs", "Peaks", "BigBoulders", "Boulders", "Rubble_dense", "Rubble_sparse", "Ruins", "Trees" };
        // Opt-in attribution experiments only, in the disposable validation player.
        // Runtime overrides are never written to scene assets or device preferences.
        private IEnumerator MeasureEnvironment(FpsPlayer player, string reportPath)
        {
            // Focus callbacks still reach disabled MonoBehaviours. Give this fixture
            // its own preferences and URP copy so focus cannot restore the user's cap
            // or overwrite the experimental graphics settings halfway through a sample.
            var preferences = player.GameSettings.Values.Copy();
            var pipeline = Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
            player.ConfigureGamePreferences(new EnvironmentPreferences(JsonUtility.ToJson(preferences)));
            QualitySettings.renderPipeline = pipeline;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            foreach (var hud in FindObjectsByType<FpsHud>()) hud.enabled = false;
            foreach (var ui in FindObjectsByType<UnityEngine.UIElements.UIDocument>()) ui.enabled = false;
            var environment = player.transform.root.Find("Environment");
            var terrain = environment.GetComponentInChildren<Terrain>();
            var terrainData = terrain.terrainData;
            var referenceData = Instantiate(terrainData);
            var referencePrototypes = terrainData.treePrototypes;
            if (EnvironmentReferenceTrees == null || EnvironmentReferenceTrees.Length != referencePrototypes.Length)
                throw new InvalidOperationException("Rebuild the validation player with the environment reference prototypes.");
            var referencePrefabs = EnvironmentReferenceTrees.Distinct().ToArray();
            // Replacing a prototype table validates the old instance indices immediately.
            // Clear the disposable population first, then restore it with remapped indices.
            referenceData.SetTreeInstances(Array.Empty<TreeInstance>(), false);
            referenceData.treePrototypes = referencePrefabs.Select(prefab => new TreePrototype {
                prefab = prefab, bendFactor = referencePrototypes[Array.IndexOf(EnvironmentReferenceTrees, prefab)].bendFactor
            }).ToArray();
            var referenceInstances = terrainData.treeInstances;
            for (int i = 0; i < referenceInstances.Length; i++)
                referenceInstances[i].prototypeIndex = Array.IndexOf(referencePrefabs, EnvironmentReferenceTrees[referenceInstances[i].prototypeIndex]);
            referenceData.SetTreeInstances(referenceInstances, false);
            if (referenceData.treeInstanceCount != terrainData.treeInstanceCount)
                throw new InvalidOperationException("The previous-quality reference must retain every tree.");
            var camera = player.ViewCamera;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            var sun = player.transform.root.Find("Sun").GetComponent<Light>();
            if (RenderSettings.sun != sun) throw new InvalidOperationException("Environment review must use MainGame's scene lighting.");
            var sunData = sun.GetUniversalAdditionalLightData();
            var softQuality = sunData.softShadowQuality;
            var scenery = EnvironmentScenery.Select(n => environment.Find(n).gameObject).ToArray();
            var rockRenderers = scenery.SelectMany(o => o.GetComponentsInChildren<Renderer>(true)).ToArray();
            var lods = scenery.SelectMany(o => o.GetComponentsInChildren<LODGroup>(true)).ToArray();
            var lodLevels = lods.Select(l => l.GetLODs()).ToArray();
            if (EnvironmentReferenceLodHeights == null || EnvironmentReferenceLodHeights.Length != lodLevels.Sum(l => l.Length))
                throw new InvalidOperationException("Rebuild the validation player with the environment reference LOD thresholds.");
            var water = environment.Find("Water").gameObject;
            bool environmentActive = environment.gameObject.activeSelf, waterActive = water.activeSelf;
            var sceneryActive = scenery.Select(o => o.activeSelf).ToArray();
            var shadows = rockRenderers.Select(r => r.shadowCastingMode).ToArray();
            var colliders = environment.GetComponentsInChildren<Collider>(true);
            var colliderEnabled = colliders.Select(c => c.enabled).ToArray();
            float originalBias = QualitySettings.lodBias, detailDistance = terrain.detailObjectDistance;
            float density = terrain.detailObjectDensity, treeDistance = terrain.treeDistance;
            float pixelError = terrain.heightmapPixelError, basemapDistance = terrain.basemapDistance;
            float scale = pipeline.renderScale;
            int msaa = pipeline.msaaSampleCount;
            int shadowResolution = pipeline.mainLightShadowmapResolution, cascades = pipeline.shadowCascadeCount;
            float shadowDistance = pipeline.shadowDistance;
            float cascadeBorder = pipeline.cascadeBorder;
            var cascade4Split = pipeline.cascade4Split;
            var sunShadows = sun.shadows;
            bool post = cameraData.renderPostProcessing, heightmap = terrain.drawHeightmap;
            bool occlusion = camera.useOcclusionCulling;
            string output = Path.GetFullPath(reportPath);
            string directory = Path.GetDirectoryName(output);
            Directory.CreateDirectory(directory);
            var results = new List<object>();
            var timings = new FrameTiming[1];
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            while (!player.Discoveries.Initialized) yield return null;
            void Restore()
            {
                environment.gameObject.SetActive(environmentActive);
                for (int i = 0; i < scenery.Length; i++) scenery[i].SetActive(sceneryActive[i]);
                water.SetActive(waterActive);
                terrain.detailObjectDistance = detailDistance;
                if (terrain.terrainData != terrainData) terrain.terrainData = terrainData;
                terrain.detailObjectDensity = density;
                terrain.treeDistance = treeDistance;
                terrain.heightmapPixelError = pixelError;
                terrain.basemapDistance = basemapDistance;
                terrain.drawHeightmap = heightmap;
                QualitySettings.lodBias = originalBias;
                for (int i = 0; i < lods.Length; i++) { lods[i].ForceLOD(-1); lods[i].SetLODs(lodLevels[i]); }
                for (int i = 0; i < rockRenderers.Length; i++) rockRenderers[i].shadowCastingMode = shadows[i];
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderEnabled[i];
                sun.shadows = sunShadows;
                sunData.softShadowQuality = softQuality;
                cameraData.renderPostProcessing = post;
                camera.useOcclusionCulling = occlusion;
                pipeline.msaaSampleCount = msaa;
                pipeline.renderScale = scale;
                pipeline.mainLightShadowmapResolution = shadowResolution;
                pipeline.shadowCascadeCount = cascades;
                pipeline.shadowDistance = shadowDistance;
                pipeline.cascadeBorder = cascadeBorder;
                pipeline.cascade4Split = cascade4Split;
            }
            void ShadowQuality(bool low)
            {
                pipeline.mainLightShadowmapResolution = Mathf.Min(shadowResolution, low ? 1024 : Mathf.Max(1024, shadowResolution / 2));
                pipeline.shadowCascadeCount = Mathf.Min(cascades, low ? 1 : 2);
                pipeline.shadowDistance = Mathf.Max(WorksiteTools.LightCullDistance + WorksiteTools.LightRange,
                    Mathf.Min(shadowDistance, low ? 25 : 35));
            }
            void Apply(string variant)
            {
                Restore();
                switch (variant)
                {
                    // Same current geometry/placements, restoring the previous presentation
                    // settings and vendor trees. This is a reference, never a shipped preset.
                    case "previous_quality":
                    case "previous_quality_end":
                        QualitySettings.lodBias = 2; terrain.heightmapPixelError = 3;
                        terrain.basemapDistance = 1000; terrain.detailObjectDistance = 60;
                        terrain.terrainData = referenceData;
                        pipeline.msaaSampleCount = 4; pipeline.mainLightShadowmapResolution = 4096;
                        pipeline.shadowCascadeCount = 4; pipeline.shadowDistance = 52;
                        pipeline.cascade4Split = new Vector3(.1f, .26f, .55f); pipeline.cascadeBorder = .12f;
                        sunData.softShadowQuality = SoftShadowQuality.High; camera.useOcclusionCulling = false;
                        foreach (var renderer in rockRenderers) renderer.shadowCastingMode = ShadowCastingMode.On;
                        for (int i = 0, k = 0; i < lods.Length; i++)
                        {
                            var levels = lods[i].GetLODs();
                            for (int j = 0; j < levels.Length; j++) levels[j].screenRelativeTransitionHeight = EnvironmentReferenceLodHeights[k++];
                            lods[i].SetLODs(levels);
                        }
                        break;
                    case "environment_off": environment.gameObject.SetActive(false); break;
                    case "occlusion_off": camera.useOcclusionCulling = false; break;
                    case "scenery_off": foreach (var group in scenery) group.SetActive(false); break;
                    case "terrain_details_off": terrain.detailObjectDensity = 0; break;
                    case "terrain_trees_off": terrain.treeDistance = 0; break;
                    case "water_off": water.SetActive(false); break;
                    case "sun_shadows_off": sun.shadows = LightShadows.None; break;
                    case "scenery_shadows_off": foreach (var renderer in rockRenderers) renderer.shadowCastingMode = ShadowCastingMode.Off; break;
                    case "post_off": cameraData.renderPostProcessing = false; break;
                    case "terrain_surface_off": terrain.drawHeightmap = false; break;
                    case "lod_bias_1": QualitySettings.lodBias = 1; break;
                    case "lod_bias_half": QualitySettings.lodBias = .5f; break;
                    case "scenery_lowest_lod": foreach (var lod in lods) lod.ForceLOD(lod.lodCount - 1); break;
                    case "terrain_pixel_error_10": terrain.heightmapPixelError = 10; break;
                    case "terrain_basemap_150": terrain.basemapDistance = 150; break;
                    case "terrain_details_30": terrain.detailObjectDistance = 30; break;
                    case "msaa_2": pipeline.msaaSampleCount = 2; break;
                    case "scale_75_diagnostic": pipeline.renderScale = .75f; break;
                    case "environment_colliders_off": foreach (var collider in colliders) collider.enabled = false; break;
                    case "sun_shadows_medium": ShadowQuality(false); break;
                    case "sun_shadows_low": ShadowQuality(true); break;
                    case "geometry_candidate":
                        QualitySettings.lodBias = 1; terrain.heightmapPixelError = 6;
                        terrain.basemapDistance = 150; terrain.detailObjectDistance = 40; break;
                    case "balanced_candidate":
                        QualitySettings.lodBias = 1; terrain.heightmapPixelError = 6;
                        terrain.basemapDistance = 150; terrain.detailObjectDistance = 40;
                        ShadowQuality(false); pipeline.msaaSampleCount = 2; break;
                    case "aggressive_candidate":
                        QualitySettings.lodBias = .5f; terrain.heightmapPixelError = 10;
                        terrain.basemapDistance = 75; terrain.detailObjectDistance = 25;
                        ShadowQuality(true); pipeline.msaaSampleCount = 2; break;
                }
            }
            bool candidates = Environment.GetCommandLineArgs().Contains("--environment-candidates");
            var variants = new[] { "baseline_start", "environment_off", "scenery_off", "terrain_details_off", "terrain_trees_off",
                "water_off", "sun_shadows_off", "scenery_shadows_off", "post_off", "terrain_surface_off",
                "lod_bias_1", "lod_bias_half", "scenery_lowest_lod", "terrain_pixel_error_10", "terrain_basemap_150",
                "terrain_details_30", "msaa_2", "scale_75_diagnostic", "environment_colliders_off", "baseline_end" };
            if (candidates) variants = new[] { "baseline_start", "sun_shadows_medium", "sun_shadows_low", "geometry_candidate",
                "balanced_candidate", "aggressive_candidate", "baseline_end" };
            var arguments = Environment.GetCommandLineArgs();
            bool finalReview = arguments.Contains("--environment-final");
            if (finalReview) variants = new[] { "previous_quality", "baseline_start", "occlusion_off", "msaa_2", "sun_shadows_medium",
                "sun_shadows_low", "sun_shadows_off", "baseline_end", "previous_quality_end" };
            int selectedVariants = Array.IndexOf(arguments, "--environment-variants");
            if (selectedVariants >= 0 && selectedVariants + 1 < arguments.Length)
            {
                var requested = arguments[selectedVariants + 1].Split(',');
                if (requested.Any(v => !variants.Contains(v))) throw new ArgumentException("Unknown environment variant.");
                variants = requested;
            }
            var views = new[] {
                (name: "dig_site", position: new Vector3(0, .05f, -11.6f), angles: new Vector3(10, 0, 0)),
                (name: "shore", position: new Vector3(-32, -.35f, 18), angles: new Vector3(8, 320, 0)),
                (name: "flight", position: new Vector3(0, 15, -11.6f), angles: new Vector3(15, 0, 0))
            };
            if (finalReview) views = views.Concat(new[] {
                (name: "camp_shadows", position: new Vector3(0, .05f, -10), angles: new Vector3(24, 180, 0)),
                (name: "cliff_boundary", position: new Vector3(28, .05f, -8), angles: new Vector3(-5, 90, 0))
            }).ToArray();
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            bool completed = false;
            try
            {
                foreach (var view in views)
                {
                    player.transform.SetPositionAndRotation(view.position, Quaternion.identity);
                    camera.transform.localPosition = new Vector3(0, 1.65f, 0);
                    camera.transform.rotation = Quaternion.Euler(view.angles);
                    Physics.SyncTransforms();
                    foreach (string variant in variants)
                    {
                        Apply(variant);
                        for (int i = 0; i < 120; i++) { Time.timeScale = 1; FrameTimingManager.CaptureFrameTimings(); yield return null; }
                        var frames = new List<double>(240); var gpu = new List<double>(240);
                        var main = new List<double>(240); var render = new List<double>(240); var wait = new List<double>(240);
                        var drawCounts = new List<double>(240); var batchCounts = new List<double>(240);
                        var triangleCounts = new List<double>(240); var passCounts = new List<double>(240);
                        for (int i = 0; i < 240; i++)
                        {
                            Time.timeScale = 1;
                            FrameTimingManager.CaptureFrameTimings();
                            yield return null;
                            frames.Add(Time.unscaledDeltaTime * 1000.0);
                            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                            {
                                if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
                                main.Add(timings[0].cpuMainThreadFrameTime);
                                render.Add(timings[0].cpuRenderThreadFrameTime);
                                wait.Add(timings[0].cpuMainThreadPresentWaitTime);
                            }
                            if (draws.Valid) drawCounts.Add(draws.LastValue);
                            if (batches.Valid) batchCounts.Add(batches.LastValue);
                            if (triangles.Valid) triangleCounts.Add(triangles.LastValue);
                            if (setPass.Valid) passCounts.Add(setPass.LastValue);
                        }
                        if (gpu.Count < frames.Count / 2)
                            throw new InvalidOperationException("Missing native GPU timings; discard the environment run.");
                        if (Application.targetFrameRate != -1 || QualitySettings.vSyncCount != 0)
                            throw new InvalidOperationException("Frame cap changed; discard the environment run.");
                        object Counts(List<double> values) => values.Count == 0 || values.All(v => v == 0) ? null : new { median = values.OrderBy(v => v).ElementAt(values.Count / 2) };
                        results.Add(new { view = view.name, variant, frame = Summary(frames), gpu = Summary(gpu),
                            mainThread = Summary(main), renderThread = Summary(render), presentWait = Summary(wait),
                            frameLimit = Application.targetFrameRate, vsync = QualitySettings.vSyncCount,
                            terrainTrees = terrain.terrainData.treeInstanceCount,
                            renderedFinds = player.Discoveries.Finds.Count(f => f.GetComponent<MeshRenderer>().enabled),
                            draws = Counts(drawCounts), batches = Counts(batchCounts), triangles = Counts(triangleCounts), setPass = Counts(passCounts) });
                        File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(new {
                            scope = "Native uncapped environment attribution; disposable single-scene MainGame copy, no game saves or persisted settings",
                            complete = false, width = Screen.width, height = Screen.height,
                            gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), developmentBuild = Debug.isDebugBuild,
                            defaults = new { originalBias, detailDistance, density, treeDistance, pixelError, basemapDistance, scale, msaa,
                                shadowResolution, cascades, shadowDistance }, results
                        }, Newtonsoft.Json.Formatting.Indented));
                        Debug.Log("Environment sample completed: " + view.name + " / " + variant);
                        if (variant == "baseline_start" || variant == "lod_bias_1" || variant == "terrain_basemap_150" || variant.EndsWith("_candidate") || finalReview)
                        {
                            ScreenCapture.CaptureScreenshot(Path.Combine(directory, view.name + "-" + variant + ".png"));
                            yield return null; yield return null;
                        }
                    }
                }
                completed = true;
            }
            finally { Restore(); Destroy(referenceData); if (!completed) Application.Quit(1); }
            var report = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(output));
            report["complete"] = true;
            File.WriteAllText(output, report.ToString(Newtonsoft.Json.Formatting.Indented));
            Application.Quit();
        }

        private sealed class EnvironmentPreferences : IDevicePreferencesStore
        {
            private string contents;
            public EnvironmentPreferences(string contents) => this.contents = contents;
            public string Read() => contents;
            public void Write(string value) => contents = value;
        }
    }
}
