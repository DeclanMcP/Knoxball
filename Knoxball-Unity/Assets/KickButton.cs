using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public delegate void CustomButtonEvent(bool isPressed);

[AddComponentMenu("UI/KickButton", 30)]
public class KickButton : Button
{
    public CustomButtonEvent customEvent;
    private bool clicked = false;

    public void Update()
    {
        //A public function in the selectable class which button inherits from.
        if (IsPressed() && !clicked)
        {
            clicked = true;
            customEvent(clicked);
        }
        else if (!IsPressed() && clicked)
        {
            clicked = false;
            customEvent(clicked);
        }
    }
}
