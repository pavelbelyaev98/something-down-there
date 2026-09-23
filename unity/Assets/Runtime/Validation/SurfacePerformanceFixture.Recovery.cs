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
        // Explicit opt-in in the existing disposable player. Read a copied save;
        // no real profile is opened for writing and MainGame has no fixture component.
        private IEnumerator MeasureRecovery(FpsPlayer player,string input,string output,bool xray)
        {
            if(player.Persistence!=null) throw new InvalidOperationException("Recovery validation must not own a save.");
            var terrain=player.ExcavationTerrain;
            var winch=player.Winch;
            winch.enabled=false;
            var snapshot=WorldSaveStore.Read(Path.GetFullPath(input));
            if(snapshot.Extraction==null || snapshot.Extraction.Phase!=ExtractionPhase.Obstructed)
                throw new InvalidOperationException("Provide a copied obstructed recovery to exercise normal Retry.");
            yield return terrain.Restore(snapshot.Terrain,snapshot.ExcavationSeed);
            player.Discoveries.Restore(snapshot.Finds,snapshot.DiscoverySeed);
            winch.Restore(snapshot.Extraction);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            var find=player.Discoveries.Find(snapshot.Extraction.FindId);
            player.ViewCamera.transform.position=find.transform.position+new Vector3(-1,1.5f,-3);
            player.ViewCamera.transform.LookAt(find.transform.position);
            Vector3 eyePosition=player.ViewCamera.transform.position;
            Quaternion eyeRotation=player.ViewCamera.transform.rotation;
            if(player.AdminXray!=xray) player.ToggleAdminXray();
            Time.timeScale=0;
            // Use the same device frame pacing for idle and hauling. Focus
            // changes reapply those preferences, so report the actual cap too.
            player.SetApplicationFocus(true);
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            for(int i=0;i<120;i++)yield return null;
            GC.Collect();
            for(int i=0;i<60;i++)yield return null;
            var idle=new List<double>();
            for(int i=0;i<180;i++){yield return null;idle.Add(Time.unscaledDeltaTime*1000.0);}
            var frames=new List<double>(8192);
            var clearing=new List<double>(2048);
            var notifications=new List<double>(2048);
            var physics=new List<double>(2048);
            var gpu=new List<double>(8192);
            var timings=new FrameTiming[1];
            var markers=new[]{"Excavation.LoadSweep","Excavation.Commit","Excavation.Mesh","Excavation.Collision",
                "Excavation.Cleanup","Discovery.TerrainChanged","Discovery.Exposure","Discovery.Physics","Winch.Haul","Winch.Contacts","Winch.Planning"};
            var recorders=new ProfilerRecorder[markers.Length];
            for(int i=0;i<markers.Length;i++)recorders[i]=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,markers[i],1);
            var slowFrames=new List<object>();
            player.Tuning.Gravity=0;player.enabled=true;
            player.SetApplicationFocus(true);player.CloseMenu();
            Time.timeScale=1;winch.enabled=true;
            using(var sweep=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Excavation.LoadSweep",1))
            using(var notify=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Discovery.TerrainChanged",1))
            using(var simulate=ProfilerRecorder.StartNew(ProfilerCategory.Physics,"Physics.Simulate",1))
            {
                if(!winch.Retry(find))throw new InvalidOperationException("Recovery retry refused.");
                double start=Time.realtimeSinceStartupAsDouble;
                while(winch.Busy && Time.realtimeSinceStartupAsDouble-start<90)
                {
                    player.SetApplicationFocus(true);player.CloseMenu();
                    player.ViewCamera.transform.SetPositionAndRotation(eyePosition,eyeRotation);
                    if(winch.Capture().Phase==ExtractionPhase.Obstructed)break;
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime*1000.0);
                    if(sweep.Valid && sweep.LastValue>0)clearing.Add(sweep.LastValue/1000000.0);
                    if(notify.Valid && notify.LastValue>0)notifications.Add(notify.LastValue/1000000.0);
                    if(simulate.Valid && simulate.LastValue>0)physics.Add(simulate.LastValue/1000000.0);
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0)gpu.Add(timings[0].gpuFrameTime);
                    if(Time.unscaledDeltaTime>.012f)
                    {
                        var costs=new double[markers.Length];
                        for(int i=0;i<costs.Length;i++)costs[i]=recorders[i].Valid?recorders[i].LastValue/1000000.0:-1;
                        slowFrames.Add(new{at=Time.realtimeSinceStartupAsDouble-start,frameMs=Time.unscaledDeltaTime*1000.0,
                            loadY=find.transform.position.y,terrain.Revision,terrain.LastRebuiltChunkCount,
                            physicsMs=simulate.Valid?simulate.LastValue/1000000.0:-1,costs});
                    }
                }
                var result=new {
                    scope="Copied populated recovery, fixed camera, normal simulation speed; no profile writes",
                    xray, Screen.width, Screen.height, cpu=SystemInfo.processorType, gpu=SystemInfo.graphicsDeviceName,
                    frameLimit=Application.targetFrameRate,vsync=QualitySettings.vSyncCount,
                    state=find.State.ToString(), identity=find.Item.InstanceId, population=player.Discoveries.Finds.Count,
                    elapsedSeconds=Time.realtimeSinceStartupAsDouble-start, terrain.RemovedVolume,
                    idle=Summary(idle), frame=Summary(frames),gpuFrame=Summary(gpu),
                    clearing=Summary(clearing),notifications=Summary(notifications),physics=Summary(physics),markers,slowFrames
                };
                output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            }
            foreach(var recorder in recorders)recorder.Dispose();
            Application.Quit(find.State==FindState.Stored?0:1);
        }
    }
}
