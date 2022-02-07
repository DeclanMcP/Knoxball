using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerComponent : MonoBehaviour
{
    Material material;
    private bool kicking = false;
    private Color kickColor;
    private Color normalColor;

    void Start()
    {
        material = GetComponent<Renderer>().material;
        normalColor = material.color;
        kickColor = material.color + new Color(0.5f,0.5f,0.5f);
        //material.SetColor("_EmissionColor", material.color);
    }

    public bool IsKicking()
    {
        return this.kicking;
    }

    public void OnKickStateChange(bool kicking)
    {
        if (kicking && !this.kicking)
        {
            Game.instance.ball.GetComponent<BallComponent>().Kick(gameObject.transform.position);
            LightUp();
        }
        else if (!kicking && this.kicking)
        {
            LightDown();
        }
        this.kicking = kicking;
    }

    void LightUp() {
        material.SetColor("_Color", kickColor);
        //material.EnableKeyword("_EMISSION");
    }

    void LightDown() {
        material.SetColor("_Color", normalColor);
        //material.DisableKeyword("_EMISSION");
    }
}
