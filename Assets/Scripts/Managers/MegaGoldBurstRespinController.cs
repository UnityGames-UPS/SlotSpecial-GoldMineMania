using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MegaGoldBurstRespinController : MonoBehaviour, IGoldBurstRespinController
{
    public GoldBurstTier Tier => GoldBurstTier.Mega;

    public IEnumerator PlayIntro(SlotFeatureController sharedController)
    {
        if (sharedController != null)
        {
            yield return sharedController.PlayGoldBurstIntroAnimation(gameObject);
        }
    }

    public void ResetIntro(SlotFeatureController sharedController)
    {
        sharedController?.ResetGoldBurstIntroAnimation(gameObject);
    }

    public Action GetLayoutActivation(SlotView slotView)
    {
        return slotView != null ? slotView.ActivateMegaGoldBurstLayout : null;
    }
}
