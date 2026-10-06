using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace SomethingDownThere
{
    // Immutable data shared by the live grid and at most one queued writer.
    // Pages never escape as writable arrays; the live owner copies on first edit.
    // A uniform page (115) holds one value throughout, as untouched solid ground does: it is one shared, read-only array
    // per value and length, flagged, so the field costs memory, save size and save time only where it differs (near the
    // surface, and where it was dug or seeded with air). Saves write a marker and the value for it instead of 16 KB.
    public sealed class DensitySnapshot
    {
        internal const int PageShift = 12, PageSize = 1 << PageShift, PageMask = PageSize - 1;
        private const byte UniformPage = 0, RawPage = 1;
        private readonly float[][] pages;
        private readonly bool[] uniform;
        public int Length { get; }
        public int PageCount => pages.Length;
        public float this[int index] => pages[index >> PageShift][index & PageMask];
        internal DensitySnapshot(float[][] ownedPages, bool[] uniformPages, int length) { pages = ownedPages; uniform = uniformPages; Length = length; }
        internal (float[][] pages, bool[] uniform) SharePages() => ((float[][])pages.Clone(), (bool[])uniform.Clone());

        public static DensitySnapshot CopyFrom(float[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var pages = Allocate(samples.Length);
            for (int i = 0; i < pages.Length; i++) Array.Copy(samples, i * PageSize, pages[i], 0, pages[i].Length);
            return new DensitySnapshot(pages, new bool[pages.Length], samples.Length);
        }

        public float[] ToArray()
        {
            var samples = new float[Length];
            for (int i = 0; i < pages.Length; i++) Array.Copy(pages[i], 0, samples, i * PageSize, pages[i].Length);
            return samples;
        }

        public int SharedPageCount(DensitySnapshot other)
        {
            int shared = 0;
            if (other != null)
                for (int i = 0; i < Math.Min(pages.Length, other.pages.Length); i++)
                    if (ReferenceEquals(pages[i], other.pages[i])) shared++;
            return shared;
        }

        // How many pages are uniform (shared arrays), for diagnostics.
        public int UniformPageCount { get { int count = 0; foreach (bool u in uniform) if (u) count++; return count; } }

        internal static int PageCountFor(int length) => (length + PageMask) >> PageShift;
        internal static int PageLength(int length, int page) => Math.Min(PageSize, length - page * PageSize);

        internal static float[][] Allocate(int length)
        {
            var pages = new float[PageCountFor(length)][];
            for (int i = 0; i < pages.Length; i++) pages[i] = new float[PageLength(length, i)];
            return pages;
        }

        // The shared read-only page of one value; never written (the live owner copies a page before its first edit).
        private static readonly Dictionary<(float, int), float[]> uniformPages = new Dictionary<(float, int), float[]>();
        internal static float[] Uniform(float value, int length)
        {
            lock (uniformPages)
            {
                if (!uniformPages.TryGetValue((value, length), out var page))
                {
                    page = new float[length];
                    Array.Fill(page, value);
                    uniformPages[(value, length)] = page;
                }
                return page;
            }
        }

        private static bool AllEqual(float[] page)
        {
            float first = page[0];
            for (int i = 1; i < page.Length; i++) if (page[i] != first) return false;
            return true;
        }

        internal void Validate(float band)
        {
            // Hot path for the full site: tens of millions of samples. Keep the loop call-free and only build the
            // failure message when a sample is actually invalid. A uniform page needs one look.
            for (int p = 0; p < pages.Length; p++)
            {
                var page = pages[p];
                for (int i = 0, n = uniform[p] ? 1 : page.Length; i < n; i++)
                {
                    float value = page[i];
                    if (value >= -band && value <= band) continue;
                    if (!WorldSnapshot.Finite(value) || Math.Abs(value) > band)
                        WorldSnapshot.Require(false, "Invalid density sample.");
                }
            }
        }

        internal void Write(BinaryWriter writer)
        {
            var bytes = new byte[PageSize * sizeof(float)];
            for (int p = 0; p < pages.Length; p++)
            {
                var page = pages[p];
                if (uniform[p] || AllEqual(page)) { writer.Write(UniformPage); writer.Write(page[0]); continue; }
                writer.Write(RawPage);
                int count = page.Length * sizeof(float);
                Buffer.BlockCopy(page, 0, bytes, 0, count);
                writer.Write(bytes, 0, count);
            }
        }

        internal static DensitySnapshot Read(BinaryReader reader, int length)
        {
            var pages = new float[PageCountFor(length)][];
            var flags = new bool[pages.Length];
            var bytes = new byte[PageSize * sizeof(float)];
            for (int p = 0; p < pages.Length; p++)
            {
                int size = PageLength(length, p);
                byte kind = reader.ReadByte();
                WorldSnapshot.Require(kind == UniformPage || kind == RawPage, "Invalid density page.");
                if (kind == UniformPage)
                {
                    float value = reader.ReadSingle();
                    WorldSnapshot.Require(WorldSnapshot.Finite(value), "Invalid density sample.");
                    pages[p] = Uniform(value, size);
                    flags[p] = true;
                    continue;
                }
                var page = pages[p] = new float[size];
                int count = size * sizeof(float), offset = 0;
                while (offset < count)
                {
                    int read = reader.Read(bytes, offset, count - offset);
                    WorldSnapshot.Require(read > 0, "Interrupted checkpoint.");
                    offset += read;
                }
                Buffer.BlockCopy(bytes, 0, page, 0, count);
            }
            return new DensitySnapshot(pages, flags, length);
        }
    }

    // Main-thread owner. Capturing copies only the page tables.
    internal sealed class PagedDensity
    {
        private float[][] pages;
        private readonly bool[] shared, uniform;
        public int Length { get; }
        public long CopiedBytes { get; private set; }
        public PagedDensity(int length)
        {
            Length = length;
            pages = new float[DensitySnapshot.PageCountFor(length)][];
            shared = new bool[pages.Length]; uniform = new bool[pages.Length];
            for (int p = 0; p < pages.Length; p++) SetUniform(p, 0);
        }

        public float this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => pages[index >> DensitySnapshot.PageShift][index & DensitySnapshot.PageMask];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                int page = index >> DensitySnapshot.PageShift;
                if (shared[page]) CopyPage(page);
                pages[page][index & DensitySnapshot.PageMask] = value;
            }
        }

        private void CopyPage(int page)
        {
            pages[page] = (float[])pages[page].Clone();
            shared[page] = uniform[page] = false;
            CopiedBytes += pages[page].Length * sizeof(float);
        }

        private void SetUniform(int page, float value)
        {
            pages[page] = DensitySnapshot.Uniform(value, DensitySnapshot.PageLength(Length, page));
            shared[page] = uniform[page] = true;
        }

        // Every sample set from its row (index = x + y * strideY + z * strideZ): a page within one z slice whose rows all
        // take one value is that value's uniform page, so untouched solid ground allocates nothing.
        public void Fill(int strideY, int strideZ, Func<int, float> valueAtRow)
        {
            for (int p = 0; p < pages.Length; p++)
            {
                int start = p << DensitySnapshot.PageShift, end = start + DensitySnapshot.PageLength(Length, p) - 1;
                int firstRow = start % strideZ / strideY, lastRow = end % strideZ / strideY;
                bool oneSlice = start / strideZ == end / strideZ;
                float value = valueAtRow(firstRow);
                bool same = oneSlice;
                for (int y = firstRow + 1; same && y <= lastRow; y++) same = valueAtRow(y) == value;
                if (same) { SetUniform(p, value); continue; }
                var page = pages[p] = new float[end - start + 1];
                shared[p] = uniform[p] = false;
                for (int i = start; i <= end; i++) page[i - start] = valueAtRow(i % strideZ / strideY);
            }
        }

        // Whether every sample from first to last (inclusive) lies in uniform pages of this value.
        public bool UniformRun(int first, int last, float value)
        {
            for (int p = first >> DensitySnapshot.PageShift, end = last >> DensitySnapshot.PageShift; p <= end; p++)
                if (!uniform[p] || pages[p][0] != value) return false;
            return true;
        }

        public void CopyTo(int source,float[] target,int destination,int count)
        {
            while(count>0)
            {
                int page=source>>DensitySnapshot.PageShift;
                int offset=source&DensitySnapshot.PageMask;
                int length=Math.Min(count,pages[page].Length-offset);
                Array.Copy(pages[page],offset,target,destination,length);
                source+=length;destination+=length;count-=length;
            }
        }

        public DensitySnapshot Capture()
        {
            var snapshot = new DensitySnapshot((float[][])pages.Clone(), (bool[])uniform.Clone(), Length);
            Array.Fill(shared, true);
            return snapshot;
        }

        public void Restore(DensitySnapshot snapshot)
        {
            if (snapshot.Length != Length) throw new ArgumentException("Terrain sample count differs.");
            var (restored, flags) = snapshot.SharePages();
            pages = restored;
            Array.Copy(flags, uniform, flags.Length);
            Array.Fill(shared, true);
        }
    }
}
