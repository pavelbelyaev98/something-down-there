using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Read-only, incremental search. Paths may pass through narrow air but never through soil.
    public sealed class ExtractionRoutePlanner
    {
        private readonly GridSnapshot snapshot;
        private readonly Func<Vector3, Vector3, bool> obstacleClear;
        private readonly float radius;
        private readonly int budget, limit;
        private readonly Vector3 extent;
        public Vector3[] Route { get; private set; }
        public int Visited { get; private set; }
        public string Error { get; private set; }
        private struct Node { public Vector3Int Parent; public float Cost; }
        private struct Work { public Vector3Int Cell; public float Score, Cost; }
        private readonly List<Work> heap = new List<Work>();
        private static readonly Vector3Int[] Directions = { Vector3Int.up, Vector3Int.right, Vector3Int.left,
            new Vector3Int(0,0,1), new Vector3Int(0,0,-1), Vector3Int.down };

        public ExtractionRoutePlanner(GridSnapshot snapshot, float ropeRadius, int workPerFrame, int maxNodes,
            Func<Vector3, Vector3, bool> permanentClear = null)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            radius = ropeRadius; budget = Mathf.Max(1, workPerFrame); limit = Mathf.Max(budget, maxNodes);
            extent = (Vector3)snapshot.Size * snapshot.CellSize; obstacleClear = permanentClear;
        }

        public IEnumerator Search(Vector3 attachment, Vector3 outward)
        {
            Route = null; Error = null; Visited = 0;
            int work = 0;
            long sliceStart = System.Diagnostics.Stopwatch.GetTimestamp();
            long sliceTicks = Math.Max(1, System.Diagnostics.Stopwatch.Frequency / 500);
            // Physics queries and air tests vary with the passage. A node cap alone
            // does not bound frame time in a densely populated tunnel.
            bool YieldNeeded() => ++work >= budget || System.Diagnostics.Stopwatch.GetTimestamp() - sliceStart >= sliceTicks;
            void BeginSlice() { work = 0; sliceStart = System.Diagnostics.Stopwatch.GetTimestamp(); }
            // Broad passages use fewer nodes; a failed coarse search is retried at density resolution.
            foreach (float step in new[] { snapshot.CellSize * 4, snapshot.CellSize })
            {
                var nodes = new Dictionary<Vector3Int, Node>();
                var clear = new Dictionary<Vector3Int, bool>();
                heap.Clear();
                Vector3 seed = attachment + outward.normalized * (radius * 2 + snapshot.CellSize * .25f);
                Vector3Int near = Vector3Int.RoundToInt(seed / step);
                for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                {
                    if (YieldNeeded()) { yield return null; BeginSlice(); }
                    var key = near + new Vector3Int(x,y,z); Vector3 p = (Vector3)key * step;
                    if (!InBounds(p) || !Clear(attachment, p) || !Permanent(attachment, p)) continue;
                    float cost = Vector3.Distance(attachment, p);
                    nodes[key] = new Node { Parent = key, Cost = cost };
                    Push(new Work { Cell = key, Cost = cost, Score = cost + Mathf.Max(0, extent.y + step - p.y) });
                }
                int thisPass = 0;
                while (heap.Count > 0 && thisPass < limit)
                {
                    Work current = Pop();
                    if (!nodes.TryGetValue(current.Cell, out var node) || current.Cost > node.Cost) continue;
                    Vector3 a = (Vector3)current.Cell * step;
                    thisPass++; Visited++;
                    if (a.y >= extent.y + step)
                    {
                        var points = new List<Vector3>(); var cursor = current.Cell;
                        while (true)
                        {
                            points.Add((Vector3)cursor * step);
                            var parent = nodes[cursor].Parent; if (cursor == parent) break; cursor = parent;
                        }
                        points.Add(attachment); points.Reverse();
                        var simplified = new List<Vector3> { attachment };
                        int from = 0;
                        while (from < points.Count - 1)
                        {
                            int to = from + 1;
                            // Bounded lookahead prevents quadratic simplification of a long maze.
                            for (int candidate = Math.Min(points.Count - 1, from + 24); candidate > to; candidate--)
                            {
                                if (Clear(points[from], points[candidate]) && Permanent(points[from], points[candidate])) { to = candidate; break; }
                                if (YieldNeeded()) { yield return null; BeginSlice(); }
                            }
                            simplified.Add(points[to]); from = to;
                            if (YieldNeeded()) { yield return null; BeginSlice(); }
                        }
                        Route = simplified.ToArray(); yield break;
                    }
                    foreach (var direction in Directions)
                    {
                        var key = current.Cell + direction; Vector3 b = (Vector3)key * step;
                        if (!InBounds(b)) continue;
                        if (!clear.TryGetValue(key, out bool open)) { open = Open(b); clear.Add(key, open); }
                        if (!open) continue;
                        float cost = node.Cost + step;
                        if (nodes.TryGetValue(key, out var previous) && previous.Cost <= cost) continue;
                        if (!Clear(a,b) || !Permanent(a,b)) continue;
                        nodes[key] = new Node { Parent = current.Cell, Cost = cost };
                        Push(new Work { Cell = key, Cost = cost, Score = cost + Mathf.Max(0, extent.y + step - b.y) });
                    }
                    if (YieldNeeded()) { yield return null; BeginSlice(); }
                }
                yield return null;
                BeginSlice();
            }
            Error = "No clear rope route to the opening. Widen the exposed connection and try again.";
        }

        private bool InBounds(Vector3 p) => p.x >= radius && p.z >= radius && p.y >= radius
            && p.x <= extent.x-radius && p.z <= extent.z-radius && p.y <= extent.y + snapshot.CellSize * 5;
        private bool Permanent(Vector3 a, Vector3 b) => obstacleClear == null || obstacleClear(a,b);
        private bool Open(Vector3 p)
        {
            if (Sample(p) > -.001f) return false;
            foreach (var d in Directions) if (Sample(p + (Vector3)d * radius) > .001f) return false;
            return true;
        }
        public bool Clear(Vector3 a, Vector3 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a,b) / (snapshot.CellSize * .4f)));
            for (int i = 0; i <= steps; i++) if (!Open(Vector3.Lerp(a,b,i/(float)steps))) return false;
            return true;
        }
        public float Sample(Vector3 p)
        {
            if (p.y > extent.y) return extent.y - p.y;
            Vector3 c = p / snapshot.CellSize;
            int x = Mathf.FloorToInt(c.x), y = Mathf.FloorToInt(c.y), z = Mathf.FloorToInt(c.z);
            float tx=c.x-x, ty=c.y-y, tz=c.z-z;
            float At(int i,int j,int k)
            {
                if(j>snapshot.Size.y) return (snapshot.Size.y-j)*snapshot.CellSize;
                i=Mathf.Clamp(i,0,snapshot.Size.x); j=Mathf.Clamp(j,0,snapshot.Size.y); k=Mathf.Clamp(k,0,snapshot.Size.z);
                return snapshot.Density[i+(snapshot.Size.x+1)*(j+(snapshot.Size.y+1)*k)];
            }
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(At(x,y,z),At(x+1,y,z),tx),Mathf.Lerp(At(x,y+1,z),At(x+1,y+1,z),tx),ty),
                Mathf.Lerp(Mathf.Lerp(At(x,y,z+1),At(x+1,y,z+1),tx),Mathf.Lerp(At(x,y+1,z+1),At(x+1,y+1,z+1),tx),ty),tz);
        }
        private void Push(Work value)
        {
            int i=heap.Count; heap.Add(value);
            while(i>0) { int parent=(i-1)/2; if(heap[parent].Score<=value.Score) break; heap[i]=heap[parent]; i=parent; }
            heap[i]=value;
        }
        private Work Pop()
        {
            Work result=heap[0], last=heap[heap.Count-1]; heap.RemoveAt(heap.Count-1);
            if(heap.Count==0) return result;
            int i=0;
            while(i*2+1<heap.Count) { int child=i*2+1; if(child+1<heap.Count&&heap[child+1].Score<heap[child].Score) child++;
                if(last.Score<=heap[child].Score) break; heap[i]=heap[child]; i=child; }
            heap[i]=last; return result;
        }
    }
}
