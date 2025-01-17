using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RainZone : MonoBehaviour
{
    [SerializeField] ParticleSystem rainParticles;
    [SerializeField] AK.Wwise.Event musicStartEvent;

    private bool isActive = false;
    private bool isInside;
    private float initialAlpha;

    private Volume postProcess;
    private ColorAdjustments colorAdjustments;
    private ColorCurves colorCurves;
    private SplitToning splitToning;

    private readonly Vector3 upFwd = Vector3.up + Vector3.forward;
    private PlayerController player;

    private void Start()
    {
        player = PlayerController.instance;
        postProcess = WorldManager.instance.postProcessVolume;
        postProcess.profile.TryGet<ColorAdjustments>(out colorAdjustments);
        postProcess.profile.TryGet<ColorCurves>(out colorCurves);
        postProcess.profile.TryGet<SplitToning>(out splitToning);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != Layer.player) return;

        rainParticles.Play();
        ActivateRainPostProcess(true);
        isActive = true;
        GetComponent<Collider>().enabled = false;
        musicStartEvent.Post(gameObject);
    }

    private void Update()
    {
        if (!isActive) return;

        if (isInside && !player.isInside)
        {
            isInside = false;
            ActivateRainPostProcess(true);
        }
        else if (!isInside && player.isInside)
        {
            isInside = true;
            ActivateRainPostProcess(false);
        }

        if (!player.isInside)
        {
            rainParticles.transform.position = player.transform.position + upFwd * 20f;
        }
    }

    private void ActivateRainPostProcess(bool activate)
    {
        colorCurves.active = !activate;
        colorAdjustments.active = activate;
        splitToning.active = activate;
    }
}
