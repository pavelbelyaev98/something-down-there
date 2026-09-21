using UnityEngine;

namespace SomethingDownThere
{
    // Surface landmark for HUD orientation. Fuel is purchased explicitly at the computer.
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class SurfaceRecharge : MonoBehaviour
    {
        [SerializeField] private FpsPlayer player;
        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private Vector2 footprint = new Vector2(4f, 3f);
        [SerializeField, Min(0.01f)] private float maximumFeetHeight = 0.35f;

        public FpsPlayer Player => player;
        public TerrainVolume Terrain => terrain;
        public Vector2 Footprint => footprint;
        public bool IsPlayerInZone => player != null && ContainsFeet(player.FeetPosition);
        public bool IsNearby
        {
            get
            {
                if (!isActiveAndEnabled || player == null || terrain == null) return false;
                Vector3 feet = player.FeetPosition;
                float height = feet.y - terrain.SurfaceHeight;
                Vector3 local = transform.InverseTransformPoint(feet);
                return height >= 0f && height <= 1.8f
                    && new Vector2(local.x, local.z).sqrMagnitude <= 36f;
            }
        }

        public void Configure(FpsPlayer owner, TerrainVolume excavation)
        {
            player = owner;
            terrain = excavation;
        }

        public bool ContainsFeet(Vector3 feet)
        {
            if (!isActiveAndEnabled || terrain == null) return false;
            Vector3 local = transform.InverseTransformPoint(feet);
            float height = feet.y - terrain.SurfaceHeight;
            return height >= 0f && height <= maximumFeetHeight
                && Mathf.Abs(local.x) <= footprint.x * 0.5f
                && Mathf.Abs(local.z) <= footprint.y * 0.5f;
        }

    }
}
