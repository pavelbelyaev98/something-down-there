using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SomethingDownThere
{
    // Immutable data shared by the live grid and at most one queued writer.
    // Pages never escape as writable arrays; the live owner copies on first edit.
    public sealed class DensitySnapshot
    {
        internal const int PageShift = 12, PageSize = 1 << PageShift, PageMask = PageSize - 1;
        private readonly float[][] pages;
        public int Length { get; }
        public int PageCount => pages.Length;
        public float this[int index] => pages[index >> PageShift][index & PageMask];
        internal DensitySnapshot(float[][] ownedPages, int length) { pages = ownedPages; Length = length; }
        internal float[][] SharePages() => (float[][])pages.Clone();

        public static DensitySnapshot CopyFrom(float[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var pages = Allocate(samples.Length);
            for (int i = 0; i < pages.Length; i++) Array.Copy(samples, i * PageSize, pages[i], 0, pages[i].Length);
            return new DensitySnapshot(pages, samples.Length);
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

        internal static float[][] Allocate(int length)
        {
            var pages = new float[(length + PageMask) >> PageShift][];
            for (int i = 0; i < pages.Length; i++) pages[i] = new float[Math.Min(PageSize, length - i * PageSize)];
            return pages;
        }

        internal void Validate(float band)
        {
            // Hot path for a 100 m site: 29.8M samples. Keep the loop call-free and only
            // build the failure message when a sample is actually invalid.
            foreach (var page in pages)
                for (int i = 0; i < page.Length; i++)
                {
                    float value = page[i];
                    if (value >= -band && value <= band) continue;
                    if (!WorldSnapshot.Finite(value) || Math.Abs(value) > band)
                        WorldSnapshot.Require(false, "Invalid density sample.");
                }
        }

        internal void Write(BinaryWriter writer)
        {
            var bytes = new byte[PageSize * sizeof(float)];
            foreach (var page in pages)
            {
                int count = page.Length * sizeof(float);
                Buffer.BlockCopy(page, 0, bytes, 0, count);
                writer.Write(bytes, 0, count);
            }
        }

        internal static DensitySnapshot Read(BinaryReader reader, int length)
        {
            var pages = Allocate(length);
            var bytes = new byte[PageSize * sizeof(float)];
            foreach (var page in pages)
            {
                int count = page.Length * sizeof(float), offset = 0;
                while (offset < count)
                {
                    int read = reader.Read(bytes, offset, count - offset);
                    WorldSnapshot.Require(read > 0, "Interrupted checkpoint.");
                    offset += read;
                }
                Buffer.BlockCopy(bytes, 0, page, 0, count);
            }
            return new DensitySnapshot(pages, length);
        }
    }

    // Main-thread owner. Capturing copies only the page table (7 KB for MainGame).
    internal sealed class PagedDensity
    {
        private float[][] pages;
        private readonly bool[] shared;
        public int Length { get; }
        public long CopiedBytes { get; private set; }
        public PagedDensity(int length)
        {
            Length = length; pages = DensitySnapshot.Allocate(length); shared = new bool[pages.Length];
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
            shared[page] = false;
            CopiedBytes += pages[page].Length * sizeof(float);
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
            var snapshot = new DensitySnapshot((float[][])pages.Clone(), Length);
            Array.Fill(shared, true);
            return snapshot;
        }

        public void Restore(DensitySnapshot snapshot)
        {
            if (snapshot.Length != Length) throw new ArgumentException("Terrain sample count differs.");
            pages = snapshot.SharePages();
            Array.Fill(shared, true);
        }
    }
}
