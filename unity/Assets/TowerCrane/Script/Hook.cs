using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hook : MonoBehaviour
{
    // Ñhecking the intersection with the cargo trigger
    private void OnTriggerEnter(Collider col)
    {
            col.GetComponent<CargoContact>();
    }
}
