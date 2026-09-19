using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class SurfacePerformanceFixture
    {
        private static IEnumerator MeasureEnvironment(FpsPlayer player,string reportPath)
        {
            // Awake already declined save ownership in the additive scene. Use MainGame's
            // authored ambient/sky now, and uncap after device preferences have initialized.
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(player.gameObject.scene);
            Application.targetFrameRate=-1;
            QualitySettings.vSyncCount=0;
            var scenery=GameObject.Find("MainGameRoot/Reservoir Surroundings");
            if(scenery==null) throw new InvalidOperationException("Reservoir scenery is missing.");
            var peaks=scenery.transform.Find("Distant Peaks").gameObject;
            var rows=new List<object>(); var timings=new FrameTiming[1];
            var directory=Path.GetDirectoryName(Path.GetFullPath(reportPath));
            Directory.CreateDirectory(directory);
            double focusDeadline=Time.realtimeSinceStartupAsDouble+45;
            while(!Application.isFocused && Time.realtimeSinceStartupAsDouble<focusDeadline)
                yield return new WaitForSecondsRealtime(.1f);
            if(!Application.isFocused)
            {
                Debug.LogError("Environment measurement requires a visible, focused player window.");
                Application.Quit(1);
                yield break;
            }
            player.transform.position=new Vector3(0,.1f,-8);
            player.ViewCamera.transform.localPosition=new Vector3(0,1.65f,0);
            using var triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1);
            using var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
            foreach(int yaw in new[]{0,90,180,270})
            {
                player.ViewCamera.transform.rotation=Quaternion.Euler(-6,yaw,0);
                foreach(string state in new[]{"full","withoutPeaks","withoutSurroundings"})
                {
                    scenery.SetActive(state!="withoutSurroundings");
                    peaks.SetActive(state!="withoutPeaks");
                    for(int i=0;i<90;i++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
                    var frames=new List<double>(); var gpu=new List<double>(); var cpu=new List<double>();
                    for(int i=0;i<240;i++)
                    {
                        FrameTimingManager.CaptureFrameTimings(); yield return null;
                        frames.Add(Time.unscaledDeltaTime*1000.0);
                        if(FrameTimingManager.GetLatestTimings(1,timings)>0)
                        {
                            if(timings[0].gpuFrameTime>0) gpu.Add(timings[0].gpuFrameTime);
                            if(timings[0].cpuFrameTime>0) cpu.Add(timings[0].cpuFrameTime);
                        }
                    }
                    if(gpu.Count==0 || triangles.Valid && triangles.LastValue==0)
                    {
                        Debug.LogError("Discard environment measurement: the player did not render GPU frames.");
                        Application.Quit(1);
                        yield break;
                    }
                    rows.Add(new {yaw,state,frame=Summary(frames),gpu=Summary(gpu),cpu=Summary(cpu),
                        triangles=triangles.Valid ? triangles.LastValue : -1,
                        drawCalls=draws.Valid && draws.LastValue>0 ? draws.LastValue : -1});
                    if(state=="full")
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"reservoir-"+yaw+".png"));
                        for(int i=0;i<5;i++) yield return null;
                    }
                }
            }
            scenery.SetActive(true); peaks.SetActive(true);
            File.WriteAllText(reportPath,Newtonsoft.Json.JsonConvert.SerializeObject(new {
                scope="Fixed-view scenery comparison in a native development player; additive MainGame without saves; not minimum-hardware qualification",
                width=Screen.width,height=Screen.height,gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,
                graphicsApi=SystemInfo.graphicsDeviceType.ToString(),results=rows},Newtonsoft.Json.Formatting.Indented));
            Application.Quit();
        }
    }
}
