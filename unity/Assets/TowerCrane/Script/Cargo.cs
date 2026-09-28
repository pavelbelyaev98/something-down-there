using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cargo : MonoBehaviour
{
    public GameObject clamps;
    public GameObject target;
    public GameObject cargo;

    public bool contactTarget = false;

    // When the hook hits the trigger it disappears and clamps appear
    private void OnTriggerEnter(Collider other)
    {
        contactTarget = true;

        cargo.transform.position = target.transform.position;
        cargo.transform.rotation = target.transform.rotation;

        clamps.GetComponent<SkinnedMeshRenderer>().enabled = false;

        cargo.GetComponent<Rigidbody>().useGravity = false;
        cargo.GetComponent<Rigidbody>().isKinematic = true;

        target.GetComponent<MeshRenderer>().enabled = false;
        target.GetComponent<BoxCollider>().isTrigger = false;
        target.GetComponent<BoxCollider>().enabled = false;
    }
}
