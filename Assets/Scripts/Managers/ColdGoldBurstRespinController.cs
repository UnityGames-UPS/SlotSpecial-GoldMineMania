using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ColdGoldBurstRespinController : MonoBehaviour, IGoldBurstRespinController
{
    public GoldBurstTier Tier => GoldBurstTier.Cold;

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
        return null;
    }
}
