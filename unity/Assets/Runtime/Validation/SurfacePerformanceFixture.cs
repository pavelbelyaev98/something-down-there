using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere
{
    // Dedicated validation scene only. Loads MainGame additively with no game
    // save session. Normal device preferences are read but never edited. No
    // component is installed in MainGame.
    public sealed partial class SurfacePerformanceFixture : MonoBehaviour
    {
        private IEnumerator Start()
        {
            int startupLimit = Application.targetFrameRate;
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Additive);
            var player = FindAnyObjectByType<FpsPlayer>();
            if (player == null || player.Persistence != null)
                throw new InvalidOperationException("Surface validation must not own a save.");
            player.SetApplicationFocus(true);
            player.CloseMenu();
            player.enabled = false;
            player.transform.position = new Vector3(0, .05f, -11.6f);
            player.ViewCamera.transform.localPosition = new Vector3(0, 1.65f, 0);
            player.ViewCamera.transform.rotation = Quaternion.Euler(10, 0, 0);
            Screen.SetResolution(2560, 1440, FullScreenMode.Windowed);
            var grass = player.ExcavationTerrain.GetComponent<SurfaceGrassRenderer>();
            while (!player.ExcavationTerrain.CanDig || player.ExcavationTerrain.IsRestoring) yield return null;
            var args = Environment.GetCommandLineArgs();
            int discoveryReport = Array.IndexOf(args, "--discovery-report");
            if (discoveryReport >= 0 && discoveryReport + 1 < args.Length)
            {
                yield return MeasureDiscoveries(player, args[discoveryReport + 1], startupLimit);
                yield break;
            }
            var results = new List<object>();
            var timings = new FrameTiming[1];
            using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            using (var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1))
            {
                // Alternate states to expose warm-up/order effects. Measure the
                // actual player render loop with a fixed camera and resolution.
                foreach (bool enabledGrass in new[] { true, false, true, false })
                {
                    grass.enabled = enabledGrass;
                    for (int i = 0; i < 120; i++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
                    if (enabledGrass && grass.LastDrawCalls == 0)
                        throw new InvalidOperationException("No grass was rendered; discard this surface performance run.");
                    var frames = new List<double>(); var gpu = new List<double>();
                    var cpu = new List<double>(); var mainMs = new List<double>();
                    var renderMs = new List<double>(); var submission = new List<double>();
                    for (int i = 0; i < 300; i++)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        frames.Add(Time.unscaledDeltaTime * 1000.0);
                        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                        {
                            if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
                            if (timings[0].cpuFrameTime > 0) cpu.Add(timings[0].cpuFrameTime);
                        }
                        if (main.Valid) mainMs.Add(main.LastValue / 1000000.0);
                        if (render.Valid) renderMs.Add(render.LastValue / 1000000.0);
                        if (enabledGrass) submission.Add(grass.LastSubmissionMilliseconds);
                    }
                    results.Add(new { scenario = "fixedSurface", grassEnabled = enabledGrass, frame = Summary(frames), gpu = Summary(gpu),
                        cpu = Summary(cpu), mainThread = Summary(mainMs), renderThread = Summary(renderMs),
                        grassSubmission = Summary(submission), grass.LastTriangles, grass.LastDrawCalls, grass.SupportedClumps });
                }
            }
            foreach (bool enabledGrass in new[] { true, true, false })
            {
                grass.enabled=enabledGrass;
                yield return MeasureTravel(player,grass,results);
            }
            foreach (bool enabledGrass in new[] { true, false })
            {
                grass.enabled=enabledGrass;
                yield return MeasureDigging(player,grass,results);
            }
            var arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(arguments, "--surface-report");
            if (outputIndex < 0 || outputIndex + 1 >= arguments.Length)
                throw new InvalidOperationException("Provide --surface-report with an explicit output file.");
            var output = Path.GetFullPath(arguments[outputIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(new {
                scope = "Native fixed surface, camera travel and 16 real terrain cuts per grass state; additive MainGame without world saves; not minimum-hardware or save-IO qualification",
                width = Screen.width, height = Screen.height, gpu = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                developmentBuild = Debug.isDebugBuild, timingEnabled = FrameTimingManager.IsFeatureEnabled(),
                renderScale=(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)?.renderScale,
                msaa=(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)?.msaaSampleCount, results
            }, Newtonsoft.Json.Formatting.Indented));
            Application.Quit();
        }

        private static IEnumerator MeasureTravel(FpsPlayer player,SurfaceGrassRenderer grass,List<object> results)
        {
            player.ExcavationTerrain.ResetExcavation();
            for(int i=0;i<120;i++)yield return null;
            var frames=new List<double>(8192);var gpu=new List<double>(8192);var submission=new List<double>(8192);
            var slowFrames=new List<object>(16);
            int collectionCount=GC.CollectionCount(0);
            using var gc=ProfilerRecorder.StartNew(new ProfilerCategory("GC"),"GC.Collect",1);
            using var allocation=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            var timings=new FrameTiming[1];
            double started=Time.realtimeSinceStartupAsDouble;
            long previous=System.Diagnostics.Stopwatch.GetTimestamp();
            int peakTriangles=0,peakDraws=0;
            while(Time.realtimeSinceStartupAsDouble-started<8)
            {
                float t=(float)((Time.realtimeSinceStartupAsDouble-started)/8);
                player.transform.position=new Vector3(3*Mathf.Sin(t*Mathf.PI*2),.05f,-10+20*t);
                player.ViewCamera.transform.localPosition=new Vector3(0,1.65f,0);
                player.ViewCamera.transform.rotation=Quaternion.Euler(12+5*Mathf.Sin(t*Mathf.PI*2),t*360,0);
                FrameTimingManager.CaptureFrameTimings();yield return null;
                long now=System.Diagnostics.Stopwatch.GetTimestamp();
                double frameMs=(now-previous)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                frames.Add(frameMs);previous=now;
                if(frameMs>12)slowFrames.Add(new {atSeconds=Time.realtimeSinceStartupAsDouble-started,frameMs,
                    gcCollections=GC.CollectionCount(0)-collectionCount,gcMilliseconds=gc.Valid?gc.LastValue/1000000.0:(double?)null,
                    allocatedBytes=allocation.Valid?(long?)allocation.LastValue:null,grass.LastSubmissionMilliseconds,grass.LastRebuildMilliseconds});
                collectionCount=GC.CollectionCount(0);
                if(FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0)gpu.Add(timings[0].gpuFrameTime);
                if(grass.enabled)submission.Add(grass.LastSubmissionMilliseconds);
                peakTriangles=Mathf.Max(peakTriangles,grass.LastTriangles);peakDraws=Mathf.Max(peakDraws,grass.LastDrawCalls);
            }
            results.Add(new {scenario="cameraTravelAndTurn",grassEnabled=grass.enabled,frame=Summary(frames),gpu=Summary(gpu),grassSubmission=Summary(submission),peakTriangles,peakDraws,slowFrames});
        }

        private static IEnumerator MeasureDigging(FpsPlayer player,SurfaceGrassRenderer grass,List<object> results)
        {
            var terrain=player.ExcavationTerrain;terrain.ResetExcavation();
            player.transform.position=new Vector3(-4,.05f,-8);
            player.ViewCamera.transform.localPosition=new Vector3(0,1.65f,0);
            player.ViewCamera.transform.rotation=Quaternion.Euler(25,25,0);
            for(int i=0;i<120;i++)yield return null;
            var frames=new List<double>();var strokes=new List<object>();
            var hits=new RaycastHit[32];
            long previous=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int cut=0;cut<16;cut++)
            {
                Vector3 origin=new Vector3(-5+(cut%4)*2.6f,2,-5+(cut/4)*2.6f);
                int hitCount=Physics.RaycastNonAlloc(origin,Vector3.down,hits,5);
                bool removed=false;
                for(int h=0;h<hitCount;h++)if(hits[h].collider.GetComponentInParent<TerrainVolume>()==terrain) {
                    float radius=cut<8?.345807f:.8095f;
                    removed=terrain.TryDig(hits[h],radius);
                    if(removed)strokes.Add(new {cut,radius,terrain.LastDigMilliseconds,terrain.LastGridMilliseconds,terrain.LastMeshMilliseconds,terrain.LastDiscoveryMilliseconds,terrain.LastRebuiltChunkCount});
                    break;
                }
                if(!removed)throw new InvalidOperationException("Native digging sample missed terrain.");
                double until=Time.realtimeSinceStartupAsDouble+.35;
                do {
                    yield return null;
                    long now=System.Diagnostics.Stopwatch.GetTimestamp();
                    frames.Add((now-previous)*1000.0/System.Diagnostics.Stopwatch.Frequency);previous=now;
                }while(Time.realtimeSinceStartupAsDouble<until);
            }
            results.Add(new {scenario="terrainDigging",grassEnabled=grass.enabled,frame=Summary(frames),strokes});
        }

        private static object Summary(List<double> values)
        {
            if (values.Count == 0) return null;
            values.Sort();
            return new { samples = values.Count, medianMs = values[values.Count / 2],
                p95Ms = values[Math.Min(values.Count - 1, (int)(values.Count * .95))],
                p99Ms = values[Math.Min(values.Count - 1, (int)(values.Count * .99))],
                maxMs = values[values.Count-1], framesOver16Ms=values.Count(v=>v>16.667), framesOver33Ms=values.Count(v=>v>33.333) };
        }
    }
}
