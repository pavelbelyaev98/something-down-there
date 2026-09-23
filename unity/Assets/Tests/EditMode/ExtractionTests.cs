using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class ExtractionTests
    {
        [TestCase(.22f)]
        [TestCase(.5f)]
        public void RouteFollowsBentAirPassageWithoutCuttingItsRoof(float radius)
        {
            var grid = new ExcavationGrid(new Vector3Int(48, 48, 48), .125f);
            var start = new Vector3(1.5f, 2, 3);
            for (float x = start.x; x <= 4.25f; x += .1f) grid.RemoveSphere(new Vector3(x, 2, 3), radius, out _);
            for (float y = 2; y <= 6.25f; y += .1f) grid.RemoveSphere(new Vector3(4.25f, y, 3), radius, out _);
            var planner = new ExtractionRoutePlanner(grid.Capture(), .018f, 32, 50000);
            Complete(planner.Search(start, Vector3.right));
            Assert.That(planner.Route, Is.Not.Null, planner.Error);
            Assert.That(planner.Route.Last().y, Is.GreaterThan(6));
            Assert.That(planner.Clear(start, new Vector3(start.x, 6.2f, start.z)), Is.False);
            for (int i = 1; i < planner.Route.Length; i++)
                Assert.That(planner.Clear(planner.Route[i - 1], planner.Route[i]), Is.True);
            Assert.That(planner.Route.Any(p => p.x > 3.9f && p.y < 3), Is.True);
        }

        [Test]
        public void DisconnectedChamberFailsWithoutMutatingSoil()
        {
            var grid = new ExcavationGrid(new Vector3Int(24, 24, 24), .125f);
            grid.RemoveSphere(Vector3.one * 1.5f, .4f, out _);
            var snapshot = grid.Capture();
            var planner = new ExtractionRoutePlanner(snapshot, .018f, 16, 5000);
            Complete(planner.Search(Vector3.one * 1.5f, Vector3.up));
            Assert.That(planner.Route, Is.Null);
            Assert.That(planner.Error, Is.Not.Empty);
            Assert.That(grid.Revision, Is.EqualTo(snapshot.Revision));
        }

        [Test]
        public void RotatedSweptHullClearsEveryPoseAndPreservesSnapshotAndDistantSoil()
        {
            var grid = new ExcavationGrid(new Vector3Int(48, 48, 48), .125f);
            var before = grid.Capture();
            var from = new Vector3(1.9f, 3, 3);
            var to = from + new Vector3(.18f, .12f, 0);
            var rotation = Quaternion.Euler(0, 31, 0);
            var half = new Vector3(.6f, .45f, .32f);
            Assert.That(grid.RemoveBoxSweep(from, to, rotation, half, out var changed), Is.True);
            Assert.That(changed.xMin, Is.LessThan(16));
            Assert.That(changed.xMax, Is.GreaterThan(16));
            for (int i = 0; i <= 10; i++)
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / 10f) + rotation * Vector3.Scale(half * .85f, new Vector3(x, y, z));
                Assert.That(grid.Sample(p), Is.LessThan(0), p.ToString());
            }
            Assert.That(grid.Sample(Vector3.one), Is.GreaterThan(0));
            var saved = new ExtractionRoutePlanner(before, .018f, 16, 100);
            Assert.That(saved.Sample(from), Is.GreaterThan(0), "A writer's captured pages remain immutable.");
            Assert.That(grid.LastRemovedVolume, Is.GreaterThan(0));
        }

        [Test]
        public void CachedClearanceRejectsRotatedCornersAndTranslationEvenAtTheSameCenter()
        {
            var center=new Vector3(3,2,1);
            var rotation=Quaternion.Euler(17,31,12);
            var outer=new ExcavationGrid.SweptBox(center,center,rotation,new Vector3(1,.5f,.25f));
            var half=new Vector3(.85f,.4f,.15f);
            Assert.That(outer.Contains(new ExcavationGrid.SweptBox(center,center,rotation,half)),Is.True);
            Assert.That(outer.Contains(new ExcavationGrid.SweptBox(center,center,
                rotation*Quaternion.Euler(0,20,0),half)),Is.False,"The center fits but the rotating long edge does not.");
            Assert.That(outer.Contains(new ExcavationGrid.SweptBox(center,center+rotation*Vector3.right*.2f,
                rotation,half)),Is.False,"The whole sweep must fit, including its far endpoint.");
        }

        [Test]
        public void ReusingOneCellOfClearanceReducesCutsWhileEveryMovingHullCornerStaysClear()
        {
            var grid=new ExcavationGrid(new Vector3Int(64,48,48),.125f);
            var cleared=default(ExcavationGrid.SweptBox);
            int cuts=0;
            for(int step=0;step<80;step++)
            {
                var from=new Vector3(2+step*.025f,3,3);
                var to=from+new Vector3(.05f,.02f,0);
                var rotation=Quaternion.Euler(15,step*.4f,8);
                var half=new Vector3(.35f,.3f,.24f);
                // Match the production separation between the real hull and its
                // clearance envelope: interpolation can round an SDF box corner.
                var clearance=half+Vector3.one*grid.CellSize*2;
                var required=new ExcavationGrid.SweptBox(from,to,rotation,clearance);
                if(cuts==0 || !cleared.Contains(required))
                {
                    cleared=new ExcavationGrid.SweptBox(from,to,rotation,clearance+Vector3.one*grid.CellSize);
                    Assert.That(grid.RemoveBoxSweep(from,to,rotation,clearance+Vector3.one*grid.CellSize,out _),Is.True);
                    cuts++;
                }
                for(int endpoint=0;endpoint<2;endpoint++)
                for(int corner=0;corner<8;corner++)
                {
                    var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    var point=(endpoint==0?from:to)+rotation*Vector3.Scale(half,sign);
                    Assert.That(grid.Sample(point),Is.LessThanOrEqualTo(0),$"Pose {step}, corner {corner}");
                }
            }
            Assert.That(cuts,Is.LessThan(27),"Slow travel must not remesh at the physics rate.");
        }

        [TestCase(ExtractionPhase.Planning)]
        [TestCase(ExtractionPhase.Deploying)]
        [TestCase(ExtractionPhase.Attaching)]
        [TestCase(ExtractionPhase.Hauling)]
        [TestCase(ExtractionPhase.Delivering)]
        [TestCase(ExtractionPhase.Obstructed)]
        public void JobAndSingleOwnerRoundTripTogether(ExtractionPhase phase)
        {
            var state = Snapshot();
            state.Extraction.Phase = phase;
            state.Extraction.Attached = phase >= ExtractionPhase.Hauling;
            if(state.Extraction.Attached) { state.Extraction.LinearVelocity=Vector3.up*.8f; state.Extraction.AngularVelocity=Vector3.right*.4f; }
            if (phase == ExtractionPhase.Planning) state.Extraction.Route = Array.Empty<Vector3>();
            if (phase == ExtractionPhase.Delivering)
            {
                state.Extraction.Progress = ExtractionSnapshot.Length(state.Extraction.Route, state.Extraction.AnchorIndex);
                state.Finds[0].Position = ExtractionSnapshot.Point(state.Extraction.Route, state.Extraction.Progress, out _);
            }
            using var bytes = new MemoryStream();
            WorldSaveCodec.Write(bytes, state); bytes.Position = 0;
            var loaded = WorldSaveCodec.Read(bytes);
            Assert.That(loaded.Extraction.Phase, Is.EqualTo(phase));
            Assert.That(loaded.Extraction.Route, Is.EqualTo(state.Extraction.Route));
            Assert.That(loaded.Extraction.AttachLocal, Is.EqualTo(state.Extraction.AttachLocal));
            Assert.That(loaded.Extraction.LinearVelocity, Is.EqualTo(state.Extraction.LinearVelocity));
            Assert.That(loaded.Extraction.AngularVelocity, Is.EqualTo(state.Extraction.AngularVelocity));
            Assert.That(loaded.Finds.Single().Item.Kind, Is.EqualTo(DiscoveryKind.Unique));
            Assert.That(loaded.Finds.Single().DiscoveryDepth, Is.EqualTo(.6f));
            Assert.That(loaded.Inventory, Is.Empty);
        }

        [Test]
        public void UniqueCannotEnterBagBeDuplicatedOrLoseItsJob()
        {
            var state = Snapshot();
            Assert.That(new SessionInventory().TryAdd(state.Finds[0].Item.Restore()), Is.False);
            state.Inventory = new[] { state.Finds[0].Item };
            Assert.Throws<InvalidDataException>(() => state.Validate());
            state.Inventory = Array.Empty<ItemSnapshot>();
            var original = state.Finds[0];
            state.Finds = new[] { original, new FindSnapshot { ContentId = original.ContentId,
                Item = new ItemSnapshot { Id = "duplicate", Name = "Computer", Kind = DiscoveryKind.Unique },
                Position = original.Position, Rotation = Quaternion.identity, Scale = Vector3.one } };
            Assert.Throws<InvalidDataException>(() => state.Validate());
            state.Finds = new[] { original }; state.Extraction = null;
            Assert.Throws<InvalidDataException>(() => state.Validate());
        }

        [TestCase(FindState.World)]
        [TestCase(FindState.Stored)]
        [TestCase(FindState.Displayed)]
        public void WorldPadAndExhibitKeepTheirIdentityOnRepeatedLoads(FindState lifecycle)
        {
            var state = Snapshot();
            state.Extraction = null;
            state.Finds[0].State = lifecycle;
            state.Finds[0].DisplaySocket = lifecycle == FindState.Displayed ? "first-exhibit" : "";
            for (int repeat = 0; repeat < 3; repeat++)
            {
                using var bytes = new MemoryStream();
                WorldSaveCodec.Write(bytes, state); bytes.Position = 0;
                state = WorldSaveCodec.Read(bytes);
                Assert.That(state.Finds.Single().State, Is.EqualTo(lifecycle));
                Assert.That(state.Finds.Single().Item.Id, Is.EqualTo("unique-1"));
            }
        }

        [Test]
        public void PhysicalRopeStretchIsValidButRunawayPoseAndCorruptMotionCannotResume()
        {
            var state = Snapshot();
            state.Extraction.Phase = ExtractionPhase.Hauling;
            state.Extraction.Attached = true;
            state.Extraction.Progress = .5f;
            Assert.DoesNotThrow(() => state.Validate(), "The physical attachment may lag the winch guide.");
            state.Extraction.LinearVelocity=new Vector3(float.NaN,0,0);
            Assert.Throws<InvalidDataException>(() => state.Validate());
            state.Extraction.LinearVelocity=Vector3.zero;
            state.Finds[0].Position+=Vector3.right*3;
            Assert.Throws<InvalidDataException>(() => state.Validate());
            state.Finds[0].Position = ExtractionSnapshot.Point(state.Extraction.Route, .5f, out _);
            Assert.DoesNotThrow(() => state.Validate());
            state.Extraction.Phase = ExtractionPhase.Delivering;
            Assert.Throws<InvalidDataException>(() => state.Validate());
        }

        private static WorldSnapshot Snapshot() => new WorldSnapshot
        {
            Sequence = 1, UtcTicks = DateTime.UtcNow.Ticks,
            Terrain = new ExcavationGrid(new Vector3Int(16, 16, 16), .125f).Capture(),
            TerrainRotation = Quaternion.identity, PlayerRotation = Quaternion.identity,
            Finds = new[] { new FindSnapshot { ContentId = "computer", Item = new ItemSnapshot {
                Id = "unique-1", Name = "Computer", Kind = DiscoveryKind.Unique },
                Position = Vector3.one, Rotation = Quaternion.identity, Scale = Vector3.one,
                State = FindState.Extracting, DepthRecorded = true, DiscoveryDepth = .6f } },
            Extraction = new ExtractionSnapshot { FindId = "unique-1", Outward = Vector3.up,
                Route = new[] { Vector3.one, new Vector3(1, 3, 1), new Vector3(2, 3, 1), new Vector3(2, 2.5f, 1) } },
            Inventory = Array.Empty<ItemSnapshot>(), InventoryCapacity = 10,
            ShovelLevel = 1, BatteryCapacity = 100, BatteryCharge = 50
        };

        private static void Complete(IEnumerator search)
        {
            int frames = 0;
            while (search.MoveNext()) Assert.That(++frames, Is.LessThan(20000), "Search must be bounded.");
        }
    }
}
