using System;
using UnityEngine;

namespace SomethingDownThere
{
    // Sealed rooms (concept 03 §5) exist in the seeded ground from the start. Their chunks are
    // built with the session, so the far walls and silt floor are there the moment the player
    // breaks in; the first opening lets dust drift into the dark.
    public sealed partial class TerrainVolume
    {
        // Room index and world point where a tool cut first opened it (audio 028, particles 031).
        public event Action<int, Vector3> BrokeIntoRoom;
        public TerrainGround.Room[] Rooms => grid?.Rooms ?? Array.Empty<TerrainGround.Room>();
        public TerrainGround.GroundLayout GroundLayout => grid?.Layout ?? TerrainGround.GroundLayout.Empty;
        private bool[] roomsOpened;

        private void MaterializeRooms()
        {
            foreach (var room in grid.Rooms)
            {
                Vector3Int first = Vector3Int.Max(Vector3Int.zero, Vector3Int.FloorToInt((Vector3)room.Min / cellSize) - Vector3Int.one * 2) / chunkSize;
                Vector3Int last = Vector3Int.Min(dimensions - Vector3Int.one, Vector3Int.CeilToInt((Vector3)room.Max / cellSize) + Vector3Int.one * 2) / chunkSize;
                for (int z = first.z; z <= last.z; z++)
                for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                    Refresh(new Vector3Int(x, y, z));
            }
        }

        // A cut opened a room when it cleared a shell sample right against the room's air.
        private void CheckBreakIn(BoundsInt cut, Vector3 contact)
        {
            var rooms = grid.Rooms;
            if (rooms.Length == 0) return;
            if (roomsOpened == null || roomsOpened.Length != rooms.Length) roomsOpened = new bool[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                if (roomsOpened[i]) continue;
                var room = rooms[i];
                Vector3Int low = Vector3Int.Max(cut.min, Vector3Int.FloorToInt((Vector3)room.Min / cellSize));
                Vector3Int high = Vector3Int.Min(cut.max, Vector3Int.CeilToInt((Vector3)room.Max / cellSize));
                bool opened = false;
                for (int z = low.z; z <= high.z && !opened; z++)
                for (int y = low.y; y <= high.y && !opened; y++)
                for (int x = low.x; x <= high.x && !opened; x++)
                {
                    var local = Unity.Mathematics.math.mul(room.ToLocal, new Unity.Mathematics.float3(x, y, z) * cellSize - room.Centre) - room.AirCentre;
                    var outside = Unity.Mathematics.math.abs(local) - room.AirHalf;
                    float face = Mathf.Max(outside.x, Mathf.Max(outside.y, outside.z));
                    opened = face > 0 && face <= cellSize * 1.5f && grid.Sample(x, y, z) <= 0;
                }
                if (!opened) continue;
                roomsOpened[i] = true;
                var inside = transform.TransformPoint(room.ToGrid(room.AirCentre));
                EmitBreakIn(contact, inside);
                BrokeIntoRoom?.Invoke(i, contact);
            }
        }

        // Soft dust puffs drift from the opening into the dark room and slowly settle.
        private void EmitBreakIn(Vector3 opening, Vector3 inside)
        {
            if (!EnsureDebrisParticles()) return;
            Vector3 into = (inside - opening).normalized;
            for (int i = 0; i < 26; i++)
            {
                var spread = new Vector3(Random01(-1, 1), Random01(-.6f, .6f), Random01(-1, 1));
                pourDust.Emit(new ParticleSystem.EmitParams
                {
                    position = opening + spread * .2f,
                    velocity = into * Random01(.25f, .7f) + spread * .12f,
                    startLifetime = Random01(2.2f, 3.4f),
                    startSize = Random01(.3f, .6f),
                    rotation = Random01(0, 360),
                    startColor = new Color(.5f, .47f, .42f, .2f)
                }, 1);
            }
        }
    }
}
