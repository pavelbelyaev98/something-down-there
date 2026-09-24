 

using System.Collections.Generic;
using UnityEngine;

namespace BKPureNature
{

[DisallowMultipleComponent]
public class BK_SimpleBirds : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject birdPrefab;

    [Header("Materials")]
    [SerializeField] private Material[] materialSlots;

    [Header("Flocks")]
    [SerializeField] private int flockCount = 3;
    [SerializeField] private int birdsPerFlock = 64;

    [Header("Speed")]
    [SerializeField] private float minSpeed = 6f;
    [SerializeField] private float maxSpeed = 22f;

    [Header("Volume")]
    [Tooltip("Local-space offset from this GameObject.")]
    [SerializeField] private Vector3 volumeCenterLocal = Vector3.zero;
    [SerializeField] private Vector3 volumeSize = new Vector3(200f, 80f, 200f);

    

    [HideInInspector] [SerializeField] private float neighborRadius = 18f;
    [HideInInspector] [SerializeField] private float separationRadius = 6f;

    [HideInInspector] [SerializeField] private float cohesionWeight = 0.9f;
    [HideInInspector] [SerializeField] private float alignmentWeight = 1.1f;
    [HideInInspector] [SerializeField] private float separationWeight = 2.0f;

    [HideInInspector] [SerializeField] private float wanderWeight = 1.3f;
    [HideInInspector] [SerializeField] private float wanderJitter = 1.2f;
    [HideInInspector] [SerializeField] private float wanderDistance = 10f;
    [HideInInspector] [SerializeField] private float wanderRadius = 6f;

    [HideInInspector] [SerializeField] private float boundsMargin = 18f;
    [HideInInspector] [SerializeField] private float boundsSteerWeight = 3.2f;

    [HideInInspector] [SerializeField] private float maxTurnRateDeg = 140f;
    [HideInInspector] [SerializeField] private float steeringSharpness = 10f;
    [HideInInspector] [SerializeField] private float maxAcceleration = 28f;
    [HideInInspector] [SerializeField] private float rotationSharpness = 12f;

    [HideInInspector] [SerializeField] private bool writeStableRandomToTexcoord1Y = true;

    private readonly List<Bird> _birds = new List<Bird>();
    private System.Random _rng;

    private void OnValidate()
    {
        flockCount = Mathf.Max(0, flockCount);
        birdsPerFlock = Mathf.Max(0, birdsPerFlock);
        minSpeed = Mathf.Max(0f, minSpeed);
        maxSpeed = Mathf.Max(minSpeed, maxSpeed);

        volumeSize.x = Mathf.Max(0.01f, volumeSize.x);
        volumeSize.y = Mathf.Max(0.01f, volumeSize.y);
        volumeSize.z = Mathf.Max(0.01f, volumeSize.z);
    }

    private void Start()
    {
        Respawn();
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
    }

    public void Respawn()
    {
        Clear();

        if (birdPrefab == null || flockCount <= 0 || birdsPerFlock <= 0)
            return;

        _rng = new System.Random();

        Vector3 center = VolumeCenterWorld();
        Vector3 half = volumeSize * 0.5f;

        int total = flockCount * birdsPerFlock;

        for (int i = 0; i < total; i++)
        {
            Vector3 pos = center + RandomInsideBox(half);
            Vector3 dir = RandomOnUnitSphereXZ();

            GameObject go = Instantiate(birdPrefab, pos, Quaternion.LookRotation(dir, Vector3.up), transform);

            AssignRandomMaterial(go);
            if (writeStableRandomToTexcoord1Y)
                ApplyStableRandom_Texcoord1Y(go, (float)_rng.NextDouble());

            Bird b;
            b.t = go.transform;
            b.vel = dir * RandomRange(minSpeed, maxSpeed);
            b.wanderDir = RandomOnUnitSphereXZ();

            _birds.Add(b);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _birds.Count; i++)
            if (_birds[i].t != null)
                Destroy(_birds[i].t.gameObject);

        _birds.Clear();
    }

    private void Simulate(float dt)
    {
        if (_birds.Count == 0) return;

        Vector3 center = VolumeCenterWorld();
        Vector3 half = volumeSize * 0.5f;

        float neighborR2 = neighborRadius * neighborRadius;
        float sepR2 = separationRadius * separationRadius;
        float maxTurnRad = maxTurnRateDeg * Mathf.Deg2Rad;
        float maxAngle = maxTurnRad * dt;

        for (int i = 0; i < _birds.Count; i++)
        {
            Bird bi = _birds[i];
            if (bi.t == null) continue;

            Vector3 pos = bi.t.position;
            Vector3 vel = bi.vel;
            Vector3 fwd = vel.sqrMagnitude > 0.0001f ? vel.normalized : bi.t.forward;

            Vector3 cohesion = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 separation = Vector3.zero;
            int neighbors = 0;

            for (int j = 0; j < _birds.Count; j++)
            {
                if (j == i) continue;
                Bird bj = _birds[j];
                if (bj.t == null) continue;

                Vector3 d = bj.t.position - pos;
                float d2 = d.sqrMagnitude;
                if (d2 > neighborR2) continue;

                neighbors++;
                cohesion += bj.t.position;
                alignment += bj.vel;

                if (d2 < sepR2 && d2 > 0.0001f)
                    separation -= d / d2;
            }

            Vector3 desiredDir = fwd;

            if (neighbors > 0)
            {
                desiredDir =
                    SafeNormalize((cohesion / neighbors) - pos) * cohesionWeight +
                    SafeNormalize(alignment / neighbors) * alignmentWeight +
                    SafeNormalize(separation) * separationWeight;

                desiredDir = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : fwd;
            }

            bi.wanderDir = SafeNormalize(bi.wanderDir + RandomInsideSphere(wanderJitter) * dt);
            desiredDir = SafeNormalize(desiredDir +
                SafeNormalize(fwd * wanderDistance + bi.wanderDir * wanderRadius) * wanderWeight);

            desiredDir = SafeNormalize(desiredDir + BoundsSteer(pos, center, half) * boundsSteerWeight);

            Vector3 newDir = RotateTowardsLimited(fwd, desiredDir, maxAngle);

            float speed = Mathf.Clamp(vel.magnitude, minSpeed, maxSpeed);
            Vector3 desiredVel = newDir * speed;

            Vector3 accel = (desiredVel - vel) / Mathf.Max(dt, 0.0001f);
            accel = ClampMagnitude(accel, maxAcceleration);

            vel += accel * dt;
            vel = Vector3.Lerp(vel, desiredVel, 1f - Mathf.Exp(-steeringSharpness * dt));

            pos += vel * dt;
            pos = ClampToBox(pos, center, half);

            bi.t.position = pos;
            bi.t.rotation = Quaternion.Slerp(
                bi.t.rotation,
                Quaternion.LookRotation(vel.sqrMagnitude > 0.0001f ? vel.normalized : bi.t.forward, Vector3.up),
                1f - Mathf.Exp(-rotationSharpness * dt)
            );

            bi.vel = vel;
            _birds[i] = bi;
        }
    }

    

    private Vector3 VolumeCenterWorld()
    {
        return transform.TransformPoint(volumeCenterLocal);
    }

    private Vector3 ClampToBox(Vector3 p, Vector3 c, Vector3 h)
    {
        p.x = Mathf.Clamp(p.x, c.x - h.x, c.x + h.x);
        p.y = Mathf.Clamp(p.y, c.y - h.y, c.y + h.y);
        p.z = Mathf.Clamp(p.z, c.z - h.z, c.z + h.z);
        return p;
    }

    private Vector3 BoundsSteer(Vector3 pos, Vector3 c, Vector3 h)
    {
        Vector3 l = pos - c;
        Vector3 s = Vector3.zero;

        if (h.x - Mathf.Abs(l.x) < boundsMargin) s.x = -Mathf.Sign(l.x);
        if (h.y - Mathf.Abs(l.y) < boundsMargin) s.y = -Mathf.Sign(l.y);
        if (h.z - Mathf.Abs(l.z) < boundsMargin) s.z = -Mathf.Sign(l.z);

        return s;
    }

    private Vector3 RandomInsideBox(Vector3 half)
    {
        return new Vector3(
            RandomRange(-half.x, half.x),
            RandomRange(-half.y, half.y),
            RandomRange(-half.z, half.z)
        );
    }

    

    private void AssignRandomMaterial(GameObject go)
    {
        if (materialSlots == null || materialSlots.Length == 0) return;

        Material m = materialSlots[_rng.Next(materialSlots.Length)];
        if (m == null) return;

        var r = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < r.Length; i++)
            r[i].sharedMaterial = m;
    }

    
    private void ApplyStableRandom_Texcoord1Y(GameObject go, float value)
    {
        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Mesh m = Instantiate(mf.sharedMesh);
        int vc = m.vertexCount;

        var uv1 = new List<Vector2>(vc);
        m.GetUVs(1, uv1);
        if (uv1.Count != vc)
        {
            uv1.Clear();
            for (int i = 0; i < vc; i++) uv1.Add(Vector2.zero);
        }

        for (int i = 0; i < vc; i++)
        {
            Vector2 v = uv1[i];
            v.y = value;
            uv1[i] = v;
        }

        m.SetUVs(1, uv1);
        mf.sharedMesh = m;
    }

    

    private Vector3 RandomInsideSphere(float r)
    {
        
        Vector3 v = new Vector3(
            RandomRange(-1f, 1f),
            RandomRange(-1f, 1f),
            RandomRange(-1f, 1f)
        );

        if (v.sqrMagnitude < 0.0001f) v = Vector3.right;
        v.Normalize();
        return v * RandomRange(0f, r);
    }

    private Vector3 RandomOnUnitSphereXZ()
    {
        float a = RandomRange(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(a), RandomRange(-0.15f, 0.15f), Mathf.Sin(a)).normalized;
    }

    private float RandomRange(float a, float b)
    {
        if (_rng == null) _rng = new System.Random();
        return (float)(_rng.NextDouble() * (b - a) + a);
    }

    private static Vector3 SafeNormalize(Vector3 v)
    {
        float m = v.magnitude;
        return m > 0.0001f ? v / m : Vector3.zero;
    }

    private static Vector3 ClampMagnitude(Vector3 v, float max)
    {
        float m2 = v.sqrMagnitude;
        return m2 <= max * max ? v : v * (max / Mathf.Sqrt(m2));
    }

    private static Vector3 RotateTowardsLimited(Vector3 from, Vector3 to, float maxAngle)
    {
        if (from.sqrMagnitude < 0.0001f) return to.normalized;
        if (to.sqrMagnitude < 0.0001f) return from.normalized;

        Vector3 f = from.normalized;
        Vector3 t = to.normalized;

        float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(f, t), -1f, 1f));
        if (angle <= maxAngle) return t;

        Vector3 axis = Vector3.Cross(f, t);
        if (axis.sqrMagnitude < 0.0001f) return f;

        return (Quaternion.AngleAxis(maxAngle * Mathf.Rad2Deg, axis.normalized) * f).normalized;
    }

    private struct Bird
    {
        public Transform t;
        public Vector3 vel;
        public Vector3 wanderDir;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 c = transform.TransformPoint(volumeCenterLocal);
        Vector3 s = volumeSize;

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawWireCube(c, s);
    }
#endif
}
}
