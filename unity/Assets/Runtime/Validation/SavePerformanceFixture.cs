using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere
{
    // Explicit validation fixture, instantiated only by the separate validation build.
    // It loads MainGame additively so the production save owner cannot open user data.
    public sealed class SavePerformanceFixture : MonoBehaviour
    {
        [Serializable] private sealed class Distribution
        {
            public int count, over33, over50;
            public double mean, p95, p99, max;
            public Distribution(List<double> values)
            {
                count = values.Count;
                if (count == 0) return;
                values.Sort(); mean = values.Average(); max = values[count - 1];
                p95 = values[(int)Math.Ceiling(count * 0.95) - 1]; p99 = values[(int)Math.Ceiling(count * 0.99) - 1];
                over33 = values.Count(v => v > 33.333); over50 = values.Count(v => v > 50);
            }
        }
        [Serializable] private sealed class Report
        {
            public string scenario, utc, hardware, graphics, os;
            public string frameClock = "Stopwatch between consecutive rendered-loop iterations";
            public int width, height, sampleSeconds, warmupSeconds, cuts, chunks, unfocusedFrames, menuFrames;
            public float removedVolume, workingDepth;
            // Reopening the scenario's own checkpoint: disk read to Ready, the longest frame
            // while loading, and the first cut afterwards (the first-dig hitch).
            public double loadMilliseconds = -1, loadLongestFrameMilliseconds = -1, firstCutMilliseconds = -1, firstCutFrameMilliseconds = -1;
            public long loadPeakPrivateBytes = -1;
            public long peakPrivateBytes, peakUnityBytes, maxCaptureAllocatedBytes = -1, maxCutAllocatedBytes = -1, copiedDensityBytes, encodedBytes;
            public long maxFrameAllocatedBytes, maxCaptureFrameAllocatedBytes, maxCutFrameAllocatedBytes;
            public bool frameAllocationCounterValid;
            public Distribution frames, capture, captureFrames, edits, grid, mesh, discovery, encode, write, flush, replace, checkpoint, durability;
        }
        private sealed class MemoryPreferences : ICameraPreferencesStore
        { private string data; public string Read() => data; public void Write(string value) => data = value; }

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private FpsPlayer player;
        private TerrainVolume terrain;
        private WorldSaveController save;
        private string evidence;
        private int sampleSeconds = 600, warmupSeconds = 60, cutIndex;
        // -saveProfileUnfocused measures without waiting for focus, e.g. while someone else uses
        // the desktop. The unfocused frame cap then bounds the frame distribution.
        private bool requireFocus = true;
        // Metres below the ground plane where the scenario's cuts hit the floor.
        private float workingDepth = 18;
        private double loadMilliseconds = -1, loadLongestFrame = -1, firstCut = -1, firstCutFrame = -1;
        private long loadPeakPrivate = -1;
        private ProfilerRecorder frameAllocations;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)] private struct ProcessMemory
        {
            public uint Size, PageFaultCount;
            public UIntPtr PeakWorkingSet, WorkingSet, QuotaPeakPaged, QuotaPaged, QuotaPeakNonPaged, QuotaNonPaged, Pagefile, PeakPagefile, Private;
        }
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemory memory, uint size);
#endif

        private static long PrivateBytes()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!GetProcessMemoryInfo(GetCurrentProcess(), out var memory, (uint)Marshal.SizeOf<ProcessMemory>())) return -1;
            return Math.Max((long)memory.Private.ToUInt64(), (long)memory.PeakPagefile.ToUInt64());
#else
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.PrivateMemorySize64;
#endif
        }
        private void OnDestroy() => frameAllocations.Dispose();

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            frameAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence"));
            Directory.CreateDirectory(evidence);
            // Short smoke runs use the same fixture without changing the normal player.
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-saveProfileSeconds" && int.TryParse(args[i + 1], out int seconds))
                { sampleSeconds = Mathf.Clamp(seconds, 5, 3600); warmupSeconds = sampleSeconds >= 600 ? 60 : 2; }
            requireFocus = Array.IndexOf(args, "-saveProfileUnfocused") < 0;
            // Fresh ground: cuts deepen the untouched plot from the ground plane.
            workingDepth = 0;
            yield return Open("fresh");
            yield return Measure("fresh", 0.41f);
            yield return SceneManager.UnloadSceneAsync(player.gameObject.scene);
            workingDepth = 16;
            yield return Open("late");
            // Seeded materials, as in play, so the checkpoint compresses like a real one.
            var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize, terrain.ExcavationSeed);
            // Many adjoining cavities through three depths, plus an open working layer.
            // Depths are measured from the ground plane so the scenario follows the site
            // when the reservoir gets deeper.
            for (int z = 0; z < 9; z++)
            for (int x = 0; x < 9; x++)
            for (int y = 0; y < 4; y++)
                grid.RemoveSphere(new Vector3(2 + x * 2.5f, SurfaceMetres - (2 + y * 3.1f), 2 + z * 2.5f), 1.1f, out _);
            CarveWorkingRoom(grid);
            yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
            save.RequestCheckpoint();
            while (save.State == WorldSaveState.Saving) yield return null;
            yield return Measure("late", 0.96f);
            save.RequestCheckpoint(); yield return null;
            while (save.State == WorldSaveState.Saving) yield return null;
            yield return SceneManager.UnloadSceneAsync(player.gameObject.scene);
            yield return MeasureDeep();
            File.WriteAllText(Path.Combine(evidence, "completed.txt"), DateTime.UtcNow.ToString("u"));
            save.RequestExit();
        }

        // Heavily dug at full depth: a wide shaft to the floor and chambers at four depths
        // down to the bottom. The checkpoint is reopened from disk, then digging resumes in a
        // working room near the floor.
        private IEnumerator MeasureDeep()
        {
            string directory = Path.Combine(evidence, "deep-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            workingDepth = 1000;
            yield return Open("deep", directory);
            float bottom = SurfaceMetres;
            workingDepth = bottom - 4;
            var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize, terrain.ExcavationSeed);
            for (float depth = .5f; depth < bottom - 1.5f; depth += 1.2f)
                grid.RemoveSphere(new Vector3(12, SurfaceMetres - depth, 8), 1.4f, out _);
            foreach (float level in new[] { .25f, .5f, .75f })
                for (int z = 0; z < 7; z++)
                for (int x = 0; x < 9; x++)
                for (int y = 0; y < 2; y++)
                    grid.RemoveSphere(new Vector3(3 + x * 3.3f, SurfaceMetres - level * bottom + y * 2.2f, 3 + z * 3.2f), 1.2f, out _);
            CarveWorkingRoom(grid);
            yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
            save.RequestCheckpoint(); yield return null;
            while (save.State == WorldSaveState.Saving) yield return null;
            yield return SceneManager.UnloadSceneAsync(player.gameObject.scene);
            GC.Collect();
            yield return Open("deep-reload", directory, true);
            // First dig after loading, in the bottom working room. The new view renders first,
            // so its own first frames are not counted as the cut's hitch.
            for (int i = 0; i < 60; i++) yield return null;
            long before = System.Diagnostics.Stopwatch.GetTimestamp();
            if (Cut(.96f)) firstCut = terrain.LastDigMilliseconds;
            yield return null;
            firstCutFrame = (System.Diagnostics.Stopwatch.GetTimestamp() - before) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            yield return Measure("deep", 0.96f);
            save.RequestCheckpoint(); yield return null;
            while (save.State == WorldSaveState.Saving) yield return null;
        }

        // An open room whose floor lies at the working depth, under every cut position.
        private void CarveWorkingRoom(ExcavationGrid grid)
        {
            for (float z = 1.5f; z <= 23; z += 1.6f)
            for (float x = 1.5f; x <= 23; x += 1.6f)
                grid.RemoveSphere(new Vector3(x, SurfaceMetres - workingDepth + 1.1f, z), 1.2f, out _);
        }

        private IEnumerator Open(string name, string directory = null, bool timed = false)
        {
            SceneManager.sceneLoaded += Configure;
            yield return SceneManager.LoadSceneAsync("MainGame", LoadSceneMode.Additive);
            SceneManager.sceneLoaded -= Configure;
            player = FindObjectsByType<FpsPlayer>().Single(p => p.gameObject.scene.name == "MainGame");
            SceneManager.SetActiveScene(player.gameObject.scene);
            player.enabled = false; player.SetApplicationFocus(true);
            terrain = player.ExcavationTerrain;
            save = player.GetComponent<WorldSaveController>();
            long started = System.Diagnostics.Stopwatch.GetTimestamp(), previous = started;
            double longest = 0; long peak = PrivateBytes();
            save.BeginSession(directory ?? Path.Combine(evidence, name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            while (save.State == WorldSaveState.Loading || save.State == WorldSaveState.Saving || save.CompletedSequence == 0)
            {
                if (save.State == WorldSaveState.LoadFailed) throw new IOException(save.ErrorDetail);
                yield return null;
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                longest = Math.Max(longest, (now - previous) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                previous = now;
                peak = Math.Max(peak, PrivateBytes());
            }
            if (timed)
            {
                loadMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                loadLongestFrame = longest; loadPeakPrivate = peak;
            }
            player.CloseMenu();
            // Inside the working room (or just above fresh ground), looking across its floor.
            float view = Mathf.Min(workingDepth, SurfaceMetres - 6);
            player.ViewCamera.transform.position = terrain.transform.TransformPoint(new Vector3(12, SurfaceMetres - view + 1.5f, 3));
            player.ViewCamera.transform.LookAt(terrain.transform.TransformPoint(new Vector3(12, SurfaceMetres - view, 14)));
            cutIndex = 0;
        }

        private static void Configure(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var p in root.GetComponentsInChildren<FpsPlayer>(true))
                { p.ConfigureInputPreferences(new MemoryPreferences()); p.ConfigureCameraPreferences(new MemoryPreferences()); }
        }

        private IEnumerator Measure(string name, float radius)
        {
            // A hidden/occluded DX12 window can skip rendering and run thousands of
            // empty frames per second. Begin only after the review window is active.
            while (requireFocus && !Application.isFocused) yield return null;
            player.SetApplicationFocus(true);
            player.CloseMenu();
            var frames = new List<double>(160000); var captures = new List<double>(); var captureFrames = new List<double>();
            var edits = new List<double>(); var encodes = new List<double>(); var writes = new List<double>();
            var grids = new List<double>(); var meshes = new List<double>(); var discovery = new List<double>();
            var flushes = new List<double>(); var replacements = new List<double>(); var checkpoints = new List<double>(); var durability = new List<double>();
            var report = new Report { scenario = name, utc = DateTime.UtcNow.ToString("u"), hardware = SystemInfo.processorType,
                graphics = SystemInfo.graphicsDeviceName, os = SystemInfo.operatingSystem, width = Screen.width, height = Screen.height,
                sampleSeconds = sampleSeconds, warmupSeconds = warmupSeconds, workingDepth = workingDepth,
                loadMilliseconds = loadMilliseconds, loadLongestFrameMilliseconds = loadLongestFrame, loadPeakPrivateBytes = loadPeakPrivate,
                firstCutMilliseconds = firstCut, firstCutFrameMilliseconds = firstCutFrame };
            double started = Time.realtimeSinceStartupAsDouble, nextCut = started, nextTransaction = started + 4, nextMemory = started;
            long captureCount = save.CaptureCount, sequence = save.CompletedSequence, copied = terrain.SnapshotCopiedBytes;
            bool previousCut = false;
            long previousFrame = System.Diagnostics.Stopwatch.GetTimestamp();
            while (Time.realtimeSinceStartupAsDouble - started < warmupSeconds + sampleSeconds)
            {
                long frameStamp = System.Diagnostics.Stopwatch.GetTimestamp();
                double frame = (frameStamp - previousFrame) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                previousFrame = frameStamp;
                double now = Time.realtimeSinceStartupAsDouble;
                bool measured = now - started >= warmupSeconds;
                bool captured = save.CaptureCount != captureCount;
                if (save.State == WorldSaveState.WriteFailed || save.State == WorldSaveState.LoadFailed)
                    throw new IOException(save.ErrorDetail);
                if (measured)
                {
                    if (!Application.isFocused) report.unfocusedFrames++;
                    if (player.IsMenuOpen) report.menuFrames++;
                    frames.Add(frame);
                    if (captured) captureFrames.Add(frame);
                    long frameBytes = frameAllocations.LastValue;
                    report.frameAllocationCounterValid = frameAllocations.Valid;
                    report.maxFrameAllocatedBytes = Math.Max(report.maxFrameAllocatedBytes, frameBytes);
                    if (captured) report.maxCaptureFrameAllocatedBytes = Math.Max(report.maxCaptureFrameAllocatedBytes, frameBytes);
                    if (previousCut) report.maxCutFrameAllocatedBytes = Math.Max(report.maxCutFrameAllocatedBytes, frameBytes);
                }
                previousCut = false;
                if (captured && measured)
                { captures.Add(save.LastCaptureMilliseconds); report.maxCaptureAllocatedBytes = Math.Max(report.maxCaptureAllocatedBytes, save.LastCaptureAllocatedBytes); }
                captureCount = save.CaptureCount;
                if (sequence != save.CompletedSequence && measured && save.LastCommitMetrics != null)
                {
                    var m = save.LastCommitMetrics;
                    encodes.Add(m.EncodeMilliseconds); writes.Add(m.WriteMilliseconds); flushes.Add(m.FlushMilliseconds); replacements.Add(m.ReplaceMilliseconds);
                    checkpoints.Add(save.LastCheckpointLatencyMilliseconds); durability.Add(save.LastDurabilitySeconds * 1000);
                    report.encodedBytes = Math.Max(report.encodedBytes, m.EncodedBytes);
                }
                sequence = save.CompletedSequence;
                if (now >= nextMemory)
                {
                    report.peakPrivateBytes = Math.Max(report.peakPrivateBytes, PrivateBytes());
                    report.peakUnityBytes = Math.Max(report.peakUnityBytes, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()); nextMemory = now + 1;
                }
                if (now >= nextCut)
                {
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    bool cut = Cut(radius);
                    previousCut = cut;
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (measured && cut) {
                        edits.Add(terrain.LastDigMilliseconds); grids.Add(terrain.LastGridMilliseconds);
                        meshes.Add(terrain.LastMeshMilliseconds); discovery.Add(terrain.LastDiscoveryMilliseconds);
                        report.cuts++; if (allocated > 0) report.maxCutAllocatedBytes = Math.Max(report.maxCutAllocatedBytes, allocated);
                    }
                    nextCut = now + 0.5;
                }
                if (now >= nextTransaction)
                {
                    player.Wallet.TryCredit(10);
                    player.Trade.TryUpgrade(player.Trade.OfferUpgrade());
                    save.RequestCheckpoint(); nextTransaction = now + 23;
                }
                yield return null;
            }
            report.frames = new Distribution(frames); report.capture = new Distribution(captures); report.captureFrames = new Distribution(captureFrames);
            report.edits = new Distribution(edits); report.encode = new Distribution(encodes); report.write = new Distribution(writes);
            report.grid = new Distribution(grids); report.mesh = new Distribution(meshes); report.discovery = new Distribution(discovery);
            report.flush = new Distribution(flushes); report.replace = new Distribution(replacements); report.checkpoint = new Distribution(checkpoints);
            report.durability = new Distribution(durability); report.copiedDensityBytes = terrain.SnapshotCopiedBytes - copied;
            report.removedVolume = terrain.RemovedVolume; report.chunks = terrain.ChunkCount;
            File.WriteAllText(Path.Combine(evidence, name + ".json"), JsonUtility.ToJson(report, true));
            Debug.Log("Save performance finished: " + name);
        }

        private bool Cut(float radius)
        {
            int index = cutIndex++ % 256;
            Vector3 origin = terrain.transform.TransformPoint(new Vector3(2 + index % 16 * 1.3f, SurfaceMetres - workingDepth + 1.2f, 2 + index / 16 * 1.3f));
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 20);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.GetComponentInParent<TerrainVolume>() == terrain)
                { player.Battery.TrySpend(0.01f); return terrain.TryDig(hits[i], radius); }
            return false;
        }

        // Local metres from the volume's bottom up to the ground plane.
        private float SurfaceMetres => terrain.Dimensions.y * terrain.CellSize;
    }
}
