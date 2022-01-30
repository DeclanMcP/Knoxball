using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerComponent : MonoBehaviour
{
    Material material;

    void Start()
    {
        material = GetComponent<Renderer>().material;
        material.SetColor("_EmissionColor", material.color);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LightUp() {
        material.EnableKeyword("_EMISSION");
    }

    public void LightDown() {
        material.DisableKeyword("_EMISSION");
    }
}
