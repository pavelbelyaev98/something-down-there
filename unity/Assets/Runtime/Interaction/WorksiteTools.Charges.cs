using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // C4 (026, concept 04 section 8): the charge preview, armed charges and their blasts. The preview shows a ghost
    // charge flat on the aimed ground and the ball of ground its blast takes; Dig throws a charge there and keeps the
    // preview open while charges remain. Detonate sets off every armed charge in the order placed. A blast is a terrain
    // edit like any dig (TerrainVolume.Blast), so finds, chests, lamps, marks and geodes react through the usual
    // notifications; nothing here deletes them. It never touches the player.
    public sealed partial class WorksiteTools
    {
        private const int ChargePlacement = 5;
        // Seconds between charges going off; the flash's peak, reach (in blast radii) and fade.
        private const float ChainDelay = .1f, FlashIntensity = 9f, FlashReach = 3.5f, FlashSeconds = .4f;
        private static readonly Color FlashColour = new Color(1f, .78f, .52f);
        private static readonly Color BlastValid = new Color(1f, .62f, .2f, 1f), BlastInvalid = new Color(1f, .15f, .1f, 1f);
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        [SerializeField] private PlacedCharge chargePrefab;
        [SerializeField] private Material blastPreview;
        private readonly List<PlacedCharge> charges = new List<PlacedCharge>(EquipmentProgression.MaximumCharges);
        private readonly List<(Light light, float age)> flashes = new List<(Light, float)>();
        private GameObject chargeGhost, blastGhost;
        private Renderer[] chargeGhostRenderers;
        private MaterialPropertyBlock blastBlock;
        private ChargeSnapshot chargePose;
        private Coroutine detonation;
        private CavernScenery caverns;
        public IReadOnlyList<PlacedCharge> Charges => charges;
        public int ArmedCharges => charges.Count;
        public int AvailableCharges => Mathf.Max(0, player.Charges.Owned - charges.Count);
        public bool Detonating => detonation != null;
        private bool ChargesConfigured => chargePrefab != null && blastPreview != null;

        // Where a charge stuck at `point` on a face with outward `normal` centres its blast.
        public static Vector3 BlastCentre(Vector3 point, Vector3 normal, float radius) => point - normal * (radius * EquipmentProgression.BlastSink);

        private void UpdateChargePreview()
        {
            float radius = player.Charges.Current.BlastRadius;
            reason = $"Aim at diggable ground within {EquipmentProgression.ChargeReach:0} m";
            var eye = player.ViewCamera.transform;
            if (!player.TryGetTarget(EquipmentProgression.ChargeReach, out var aimed) || aimed.collider.GetComponentInParent<TerrainVolume>() != terrain)
            { HideChargeGhosts(); return; }
            chargePose = ChargePose(aimed.point, aimed.normal, eye.forward);
            valid = AvailableCharges > 0;
            reason = player.Charges.Owned == 0 ? "No C4: buy charges at the computer" : "All charges armed: set them off or pick one up";
            EnsureChargeGhosts();
            chargeGhost.SetActive(true); chargeGhost.transform.SetPositionAndRotation(chargePose.Position, chargePose.Rotation);
            foreach (var renderer in chargeGhostRenderers) renderer.sharedMaterial = valid ? validPreview : invalidPreview;
            blastGhost.SetActive(true);
            blastGhost.transform.SetPositionAndRotation(BlastCentre(chargePose.Position, aimed.normal, radius), Quaternion.identity);
            blastGhost.transform.localScale = Vector3.one * (radius * 2);
            var renderer2 = blastGhost.GetComponent<Renderer>();
            renderer2.GetPropertyBlock(blastBlock);
            blastBlock.SetColor(ColorId, valid ? BlastValid : BlastInvalid);
            renderer2.SetPropertyBlock(blastBlock);
        }

        // Flat on the face, its length along the aim.
        private static ChargeSnapshot ChargePose(Vector3 point, Vector3 normal, Vector3 aim)
        {
            Vector3 forward = Vector3.ProjectOnPlane(aim, normal);
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(Vector3.right, normal);
            return new ChargeSnapshot { Position = point, Rotation = Quaternion.LookRotation(forward.normalized, normal), Stuck = true };
        }

        private void EnsureChargeGhosts()
        {
            if (chargeGhost != null) return;
            chargeGhost = Instantiate(chargePrefab.gameObject, transform); chargeGhost.name = "C4 placement preview";
            chargeGhost.GetComponent<PlacedCharge>().enabled = false;
            var body = chargeGhost.GetComponent<Rigidbody>(); body.isKinematic = true; body.detectCollisions = false;
            foreach (var light in chargeGhost.GetComponentsInChildren<Light>()) light.enabled = false;
            foreach (var collider in chargeGhost.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var child in chargeGhost.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
            chargeGhostRenderers = chargeGhost.GetComponentsInChildren<Renderer>();
            foreach (var renderer in chargeGhostRenderers) renderer.shadowCastingMode = ShadowCastingMode.Off;
            blastGhost = GameObject.CreatePrimitive(PrimitiveType.Sphere); blastGhost.name = "C4 blast preview";
            Destroy(blastGhost.GetComponent<Collider>());
            blastGhost.transform.SetParent(transform, false); blastGhost.layer = 2;
            var ball = blastGhost.GetComponent<MeshRenderer>();
            ball.sharedMaterial = blastPreview; ball.shadowCastingMode = ShadowCastingMode.Off; ball.receiveShadows = false;
            blastBlock = new MaterialPropertyBlock();
        }

        private void HideChargeGhosts()
        {
            if (chargeGhost != null) chargeGhost.SetActive(false);
            if (blastGhost != null) blastGhost.SetActive(false);
        }

        // Throws a charge from the tool to exactly the previewed pose. Only a kit with no charge to spare refuses.
        public PlacedCharge PlaceCharge(ChargeSnapshot pose, Vector3? thrownFrom = null)
        {
            if (pose == null || !ChargesConfigured || AvailableCharges <= 0) return null;
            var charge = SpawnCharge(new ChargeSnapshot { Position = pose.Position, Rotation = pose.Rotation, Stuck = true }, thrownFrom);
            Dirty(); player.Persistence?.RequestCheckpoint(); return charge;
        }

        private PlacedCharge SpawnCharge(ChargeSnapshot state, Vector3? thrownFrom = null)
        {
            var charge = Instantiate(chargePrefab, transform); charge.name = "C4 charge " + (charges.Count + 1);
            charge.Initialize(this, state, thrownFrom); charges.Add(charge); return charge;
        }

        public bool RetrieveCharge(PlacedCharge charge)
        {
            if (!charges.Remove(charge)) return false;
            charge.gameObject.SetActive(false); Destroy(charge.gameObject); Dirty();
            player.Persistence?.RequestCheckpoint(); return true;
        }

        // Ground a few centimetres behind the face still holds a stuck charge.
        public bool HasChargeSupport(Vector3 point, Vector3 normal) => terrain.IsSolid(point - normal * .06f);

        // Sets off every armed charge, in the order placed.
        public bool Detonate()
        {
            if (Detonating) return false;
            if (charges.Count == 0) { player.ShowFeedback("No charges armed"); return false; }
            detonation = StartCoroutine(DetonateAll());
            return true;
        }

        private IEnumerator DetonateAll()
        {
            var armed = new List<PlacedCharge>(charges);
            for (int i = 0; i < armed.Count; i++)
            {
                if (i > 0) yield return new WaitForSeconds(ChainDelay);
                if (armed[i] != null && charges.Contains(armed[i])) Blast(armed[i]);
            }
            detonation = null;
            player.Persistence?.RequestCheckpoint();
        }

        // Goes off now: the ground goes (more of a geode's shell), a flash and debris, any crystal cluster in reach bursts.
        public bool Blast(PlacedCharge charge)
        {
            if (charge == null || !charges.Remove(charge)) return false;
            float radius = player.Charges.Current.BlastRadius;
            Vector3 normal = charge.Stuck ? charge.SupportNormal : Vector3.up;
            Vector3 centre = charge.Stuck ? BlastCentre(charge.SupportPoint, normal, radius) : charge.transform.position;
            charge.gameObject.SetActive(false); Destroy(charge.gameObject);
            player.Charges.Spend(1);
            Dirty();
            int seed = Mathf.RoundToInt(centre.x * 97) ^ Mathf.RoundToInt(centre.y * 89) * 7919 ^ Mathf.RoundToInt(centre.z * 83) * 104729;
            bool took = terrain.Blast(centre, radius, radius * EquipmentProgression.ShellReach, seed);
            Flash(centre + normal * (radius * .5f), radius);
            if (took) Debris(centre, normal, radius, terrain.LastRemovedVolume);
            if (caverns == null) caverns = FindAnyObjectByType<CavernScenery>();
            caverns?.BlastWithin(centre, radius + .5f);
            return true;
        }

        private void Flash(Vector3 at, float radius)
        {
            var go = new GameObject("C4 flash"); go.transform.SetParent(transform, false); go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point; light.color = FlashColour; light.range = radius * FlashReach;
            light.intensity = FlashIntensity; light.shadows = LightShadows.None;
            flashes.Add((light, 0));
        }

        // The crane's ground-break debris from round the crater's mouth, thrown out of it.
        private void Debris(Vector3 centre, Vector3 normal, float radius, float removed)
        {
            var crane = player.Crane;
            if (crane == null) return;
            Vector3 across = Vector3.Cross(normal, Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.forward).normalized;
            const int points = 4;
            for (int i = 0; i < points; i++)
            {
                Vector3 side = Quaternion.AngleAxis(i * 360f / points + 30, normal) * across;
                Vector3 at = centre + (normal * .55f + side * .55f) * radius;
                crane.EmitGroundBreak(at, (normal + side * .6f).normalized, removed / points, radius * .6f, 1.5f);
            }
        }

        private void TickCharges(bool playing, float deltaTime)
        {
            for (int i = charges.Count - 1; i >= 0; i--)
                if (charges[i].transform.position.y < terrain.transform.position.y - 2) RetrieveCharge(charges[i]);
            foreach (var charge in charges) charge.Tick(playing, deltaTime);
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var (light, age) = flashes[i];
                age += deltaTime;
                if (light == null || age >= FlashSeconds) { if (light != null) Destroy(light.gameObject); flashes.RemoveAt(i); continue; }
                float t = age / FlashSeconds;
                light.intensity = FlashIntensity * (1 - t) * (1 - t);
                flashes[i] = (light, age);
            }
        }

        private void ChargesChanged(Bounds bounds)
        {
            foreach (var charge in charges) if (bounds.Contains(charge.transform.position)) charge.CheckSupport();
        }

        private void CaptureCharges(WorksiteSnapshot result)
        {
            result.Charges = new ChargeSnapshot[charges.Count];
            for (int i = 0; i < charges.Count; i++) result.Charges[i] = charges[i].Capture();
        }

        private void RestoreCharges(WorksiteSnapshot state)
        {
            if (detonation != null) { StopCoroutine(detonation); detonation = null; }
            for (int i = charges.Count - 1; i >= 0; i--) { charges[i].gameObject.SetActive(false); Destroy(charges[i].gameObject); }
            charges.Clear();
            if (!ChargesConfigured) return;
            foreach (var saved in state.Charges) SpawnCharge(saved).CheckSupport();
        }
    }
}
