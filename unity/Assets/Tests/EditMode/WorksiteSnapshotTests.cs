using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class WorksiteSnapshotTests
    {
        [Test]
        public void WholeCheckpointKeepsLampOwnershipMotionAndMarkOrientation()
        {
            var state = new WorldSnapshot { Sequence = 1, UtcTicks = DateTime.UtcNow.Ticks,
                Terrain = new ExcavationGrid(new Vector3Int(8, 8, 8), .125f).Capture(), TerrainRotation = Quaternion.identity,
                PlayerRotation = Quaternion.identity, InventoryCapacity = 10, ShovelLevel = 1,
                BatteryCapacity = 100, BatteryCharge = 90, Finds = Array.Empty<FindSnapshot>(), Inventory = Array.Empty<ItemSnapshot>(),
                Worksite = Fixture() };
            using var stream = new MemoryStream();
            WorldSaveCodec.Write(stream, state); stream.Position = 0;
            var loaded = WorldSaveCodec.Read(stream).Worksite;
            Assert.That(loaded.Lamps.Length, Is.EqualTo(1));
            Assert.That(loaded.Lamps[0].Slot, Is.EqualTo(3));
            Assert.That(loaded.Lamps[0].Position, Is.EqualTo(state.Worksite.Lamps[0].Position));
            Assert.That(loaded.Lamps[0].Rotation, Is.EqualTo(state.Worksite.Lamps[0].Rotation));
            Assert.That(loaded.Lamps[0].LinearVelocity, Is.EqualTo(Vector3.down));
            Assert.That(loaded.Lamps[0].AngularVelocity, Is.EqualTo(Vector3.right));
            Assert.That(loaded.Lamps[0].Anchored, Is.False);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(loaded.Marks[i].Kind, Is.EqualTo((WorldMarkKind)i));
                Assert.That(loaded.Marks[i].Rotation, Is.EqualTo(state.Worksite.Marks[i].Rotation));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void InvalidKitCannotDuplicateLampsOrRestoreCorruptGeometry(int corruption)
        {
            var state = Fixture();
            if (corruption == 0) state.Lamps = new[] { state.Lamps[0], state.Lamps[0] };
            if (corruption == 1) state.Lamps[0].Slot = WorksiteTools.LampCapacity;
            if (corruption == 2) state.Lamps[0].Position = new Vector3(float.NaN, 0, 0);
            if (corruption == 3) state.Lamps[0].SupportNormal = Vector3.zero;
            if (corruption == 4) state.Marks[0].Kind = (WorldMarkKind)99;
            if (corruption == 5) state.Marks = new MarkSnapshot[WorksiteTools.MaximumMarks + 1];
            Assert.Throws<InvalidDataException>(() => state.Validate());
        }

        private static WorksiteSnapshot Fixture() => new WorksiteSnapshot
        {
            Lamps = new[] { new LampSnapshot { Slot = 3, Position = new Vector3(.5f, 1, .5f), Rotation = Quaternion.Euler(12, 30, 2),
                SupportNormal = Vector3.up, SupportPoint = new Vector3(.5f, 1, .5f), LinearVelocity = Vector3.down, AngularVelocity = Vector3.right } },
            Marks = new[] {
                new MarkSnapshot { Kind = WorldMarkKind.Arrow, Rotation = Quaternion.Euler(90, 0, 45) },
                new MarkSnapshot { Kind = WorldMarkKind.Home, Rotation = Quaternion.Euler(0, 90, 0) },
                new MarkSnapshot { Kind = WorldMarkKind.ReturnHere, Rotation = Quaternion.Euler(0, 180, 90) } }
        };
    }
}
