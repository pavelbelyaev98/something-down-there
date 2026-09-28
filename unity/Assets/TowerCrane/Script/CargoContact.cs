using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CargoContact : MonoBehaviour
{
    public GameObject Point_Rotation_Hook;
    public GameObject clamps;
    public GameObject trigger_ancoragePoint;

    public bool contactHook = false;

    // When the hook hits the trigger it disappears and clamps appear
    private void OnTriggerEnter(Collider other)
    {
        contactHook = true;
        trigger_ancoragePoint.GetComponent<MeshRenderer>().enabled = false;
        clamps.GetComponent<SkinnedMeshRenderer>().enabled = true;
    }

    void FixedUpdate()
    {
        // Joining and following the hook
        if (contactHook == true)
        {
            transform.position = Point_Rotation_Hook.transform.position;
            transform.rotation = Point_Rotation_Hook.transform.rotation;
            Point_Rotation_Hook.GetComponent<BoxCollider>().isTrigger = false;
        }

        // Ñargo disconnection
        if (Input.GetKey(KeyCode.Space))
        {
            contactHook = false;
            trigger_ancoragePoint.GetComponent<Rigidbody>().isKinematic = false;
            clamps.GetComponent<SkinnedMeshRenderer>().enabled = false;
            Point_Rotation_Hook.GetComponent<BoxCollider>().isTrigger = true;
        }
    }
}
