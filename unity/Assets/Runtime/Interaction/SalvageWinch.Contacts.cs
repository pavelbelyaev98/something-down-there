using UnityEngine;

namespace SomethingDownThere
{
    public sealed partial class SalvageWinch
    {
        private readonly ContactPoint[] loadContacts = new ContactPoint[64];
        private readonly LoadObstruction[] obstructions = new LoadObstruction[64];
        private struct LoadObstruction
        {
            public ContactPoint Contact;
            public Vector3 Normal;
            public BuriedFind Find;
            public WorkLamp Lamp;
            public float Priority;
        }
        private int contactCount;
        private long contactTerrainRevision = -1;
        private Object pressureObstacle;
        private Vector3 pressurePosition;
        private float pressureSeconds;
        private bool continuingJam;
        private float ContactDelay => continuingJam ? settings.FollowupContactSeconds : settings.ContactStallSeconds;

        private void RecordLoadContacts(Collision collision)
        {
            if (job == null || payload == null || !player.GameplayActive || LoadBody.isKinematic) return;
            if (contactTerrainRevision != terrain.StateRevision) contactCount = 0;
            contactTerrainRevision = terrain.StateRevision;
            for (int i = 0; i < collision.contactCount && contactCount < loadContacts.Length; i++)
            {
                var contact = collision.GetContact(i);
                // Speculative CCD can report a future impact well before touching.
                // Accept only the small solver separation of a real resting contact.
                if (contact.separation <= Mathf.Min(.01f, terrain.CellSize * .08f))
                    loadContacts[contactCount++] = contact;
            }
        }

        private void ResetContactPressure(bool endJam = true)
        {
            pressureObstacle = null; pressureSeconds = 0;
            if (endJam) continuingJam = false;
        }

        private bool RelieveBlockedContact(float dt, Vector3 pull)
        {
            using var profile = ContactsMarker.Auto();
            if (contactCount == 0 || contactTerrainRevision != terrain.StateRevision
                || pull.magnitude < .22f || !terrain.CanDig || terrain.IsRestoring)
            { ResetContactPressure(); return false; }
            Vector3 direction = pull.normalized;
            Object obstacle = null;
            int obstructionCount = 0;
            float best = float.NegativeInfinity;
            for (int i = 0; i < contactCount; i++)
            {
                var contact = loadContacts[i];
                bool ours = contact.thisCollider == payload.HitCollider;
                var other = ours ? contact.otherCollider : contact.thisCollider;
                if (other == null || !other.enabled || !other.gameObject.activeInHierarchy) continue;
                Vector3 outward = ours ? contact.normal : -contact.normal;
                float opposition = -Vector3.Dot(outward, direction);
                // Pulling away from a supporting floor does not break that floor.
                if (opposition < -.25f) continue;
                // A permanent contact must not veto a simultaneous soil contact.
                // Keep pulling against it, but only authorize edits to real dirt.
                if (Blocker(other)) continue;
                var find = other.GetComponentInParent<BuriedFind>();
                var lamp = other.GetComponentInParent<WorkLamp>();
                if (find != null && (find == payload || find.Kind != DiscoveryKind.Common
                    || find.State != FindState.World || find.IsHeld)) continue;
                // A released rock can also become pinned between the load and
                // soil. Give a moving rock time to get out of the way first.
                float priority = opposition + 1; // Contacted soil before an already loose rock.
                if (find != null || lamp != null)
                {
                    var otherBody = find != null ? find.GetComponent<FindPhysics>().Body : lamp.Body;
                    if (!otherBody.isKinematic && job.Phase != ExtractionPhase.Retensioning
                        && Vector3.Dot(otherBody.linearVelocity, direction) > .2f) continue;
                    // A loose rock resting on the load can have the strongest
                    // opposing normal while a buried side neighbour pins both.
                    // Release that actual retaining contact first.
                    priority = opposition + (otherBody.isKinematic ? 2 : 0);
                }
                if (find == null && lamp == null && other.GetComponentInParent<TerrainVolume>() != terrain) continue;
                bool duplicate = false;
                if (find != null || lamp != null)
                    for (int j = 0; j < obstructionCount; j++)
                        if (find != null && obstructions[j].Find == find || lamp != null && obstructions[j].Lamp == lamp)
                        { duplicate = true; break; }
                if (!duplicate) obstructions[obstructionCount++] = new LoadObstruction
                    { Contact = contact, Normal = outward, Find = find, Lamp = lamp, Priority = priority };
                if (priority <= best) continue;
                best = priority; obstacle = find != null ? (Object)find : lamp != null ? lamp : terrain;
            }
            if (obstacle == null) { ResetContactPressure(); return false; }

            // Rotation or side-to-side rocking alone is not escape. Only useful
            // progress toward the pull (or lost contact) starts a fresh attempt.
            bool progress = Vector3.Dot(AttachWorld - pressurePosition, direction) > terrain.CellSize * .3f;
            // Several contacting rocks can alternate as the strongest blocker
            // while the load stays wedged. That is still one continuous jam.
            if (pressureObstacle == null || progress)
            {
                if (progress) continuingJam = false;
                pressureSeconds = 0;
                pressurePosition = AttachWorld;
            }
            pressureObstacle = obstacle;
            pressureSeconds += dt;
            if (pressureSeconds < ContactDelay) return false;
            ResetContactPressure(false);
            // A fully freed rock can still carry the largest contact force in a
            // pile. A no-op there must not starve another real retaining contact.
            // Try a bounded set, stopping after the first committed soil edit.
            for (int attempt = 0; attempt < 4 && obstructionCount > 0; attempt++)
            {
                int chosen = 0;
                for (int i = 1; i < obstructionCount; i++)
                    if (obstructions[i].Priority > obstructions[chosen].Priority) chosen = i;
                var obstruction = obstructions[chosen];
                obstructions[chosen] = obstructions[--obstructionCount];
                if (BreakContact(obstruction)) { continuingJam = true; return true; }
            }
            continuingJam = false;
            return false;
        }

        private bool BreakContact(LoadObstruction obstruction)
        {
            // Mounted lamps are portable equipment, not permanent site walls.
            // Sustained contact knocks one loose; preserve the lamp and let the
            // normal solver push it away. No dirt edit or fake dust is involved.
            if (obstruction.Lamp != null) return obstruction.Lamp.ReleaseFromSupport();
            long before = terrain.StateRevision;
            float volume = terrain.RemovedVolume;
            var blockingFind = obstruction.Find;
            if (blockingFind != null)
            {
                // Free only this physically blocking common, never broad-phase
                // neighbours or a find in the predicted path of a swing.
                var physical = blockingFind.GetComponent<FindPhysics>();
                Vector3 center = physical.Body.position + physical.Body.rotation
                    * Vector3.Scale(blockingFind.LocalHull.center, blockingFind.transform.lossyScale);
                Vector3 half = Vector3.Scale(blockingFind.LocalHull.extents, blockingFind.transform.lossyScale)
                    + Vector3.one * terrain.CellSize * 1.5f;
                terrain.ClearLoadSweep(center, center, physical.Body.rotation, half);
                blockingFind.RefreshExposure(); physical.TryReleaseFromSoil();
            }
            else
            {
                // A shallow patch at the contact, with just enough depth for the
                // voxel surface approximation. No payload envelope or future sweep.
                float radius = Mathf.Max(settings.ContactBreakRadius, terrain.CellSize * 1.5f);
                float depth = settings.ContactBreakDepth;
                Vector3 center = obstruction.Contact.point - obstruction.Normal * (depth * .5f);
                terrain.ClearLoadSweep(center, center, Quaternion.LookRotation(obstruction.Normal),
                    new Vector3(radius, radius, depth * .5f + terrain.CellSize * .75f));
            }
            if (terrain.StateRevision == before) return false;
            EmitSoilBreak(obstruction.Contact.point, obstruction.Normal, terrain.RemovedVolume - volume);
            return true;
        }
    }
}
