using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Allows taking control of buttons instead of the player
public class ControlToggle
{
    public static bool isActive { get; private set; }
    private static bool activationFlag;
    private static string[] unpauseButtons = { Button.grab, Button.jump, Button.use, Button.sit, Button.diary };
    private static Action closeCallback;
    private Localization loc;

    private static bool isManualControl;

    private static float timer;


    public static void Update()
    {
        if (activationFlag)
        {
            // prevent unpausing the frame it opens
            isActive = true;
            activationFlag = false;
            return;
        }

        if (isActive)
        {
            if (isManualControl)
            {
                foreach (string button in unpauseButtons)
                {
                    if (Input.GetButtonDown(button))
                    {
                        Unpause();
                    }
                }
            }
            else
            {
                timer -= Time.deltaTime;
                if (timer <= 0)
                {
                    Unpause();
                }
            }
        }

    }

    public static void TakeControl(Action callback)
    {
        activationFlag = true;
        isManualControl = true;
        PlayerController.instance.Pause(true);
        PlayerItem.instance.Pause(true);
        HUDController.instance.BackInput(true);
        closeCallback = callback;
    }

    public static void TakeControl(float time)
    {
        activationFlag = true;
        isManualControl = false;
        timer = time;
        PlayerController.instance.Pause(true);
        PlayerItem.instance.Pause(true);
        closeCallback = null;
    }

    public static void Unpause()
    {
        isActive = false;
        HUDController.instance.BackInput(false);
        PlayerController.instance.Pause(false);
        PlayerItem.instance.Pause(false);
        if (closeCallback != null) closeCallback();
    }
}
