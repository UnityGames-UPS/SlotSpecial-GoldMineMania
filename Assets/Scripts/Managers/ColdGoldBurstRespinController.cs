using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ColdGoldBurstRespinController : MonoBehaviour, IGoldBurstRespinController
{
    private const int ColdReelCount = 5;

    [SerializeField, Min(1f)] private float introFramesPerSecond = 29f;

    public GoldBurstTier Tier => GoldBurstTier.Cold;
    public int ReelCount => ColdReelCount;
    public bool PreserveAnimationHierarchyOrder => false;
    public bool UsesDeterministicTrainVisualMapping => false;

    public IEnumerator PlayIntro()
    {
        ImageAnimation animation = GetComponent<ImageAnimation>();
        if (animation == null ||
            animation.textureArray == null ||
            animation.textureArray.Count == 0)
        {
            gameObject.SetActive(true);
            yield return null;
            gameObject.SetActive(false);
            yield break;
        }

        bool completed = false;
        Action<int> completionHandler = _ => completed = true;
        animation.onLoopComplete += completionHandler;
        animation.doLoopAnimation = false;
        float duration = animation.textureArray.Count /
                         Mathf.Max(1f, introFramesPerSecond);
        animation.SetLoopDuration(duration);
        gameObject.SetActive(true);
        animation.StartAnimation();

        float elapsed = 0f;
        float timeout = duration + 0.5f;
        while (!completed && elapsed < timeout && gameObject.activeInHierarchy)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        animation.onLoopComplete -= completionHandler;
        ResetIntro();
    }

    public void ResetIntro()
    {
        ImageAnimation animation = GetComponent<ImageAnimation>();
        if (animation != null)
        {
            animation.onLoopComplete = null;
            animation.StopAnimation();
            animation.ClearLoopDuration();
        }
        gameObject.SetActive(false);
    }

    public Action GetLayoutActivation(SlotView slotView)
    {
        return slotView != null ? () => ActivateLayout(slotView) : null;
    }

    public int GetTrainVisualIndex(TrainPlacement placement, int visualCount)
    {
        // Cold uses a reusable 5x3 pool, so the shared controller selects the
        // first free visual rather than binding a train to a reel index.
        return -1;
    }

    public bool ContainsPlacement(int startColumn, int columnCount)
    {
        return startColumn >= 0 &&
               columnCount > 0 &&
               startColumn + columnCount <= ColdReelCount;
    }

    private static void ActivateLayout(SlotView slotView)
    {
        // Cold stays on the authored 5x3 layout, but it still needs a clean
        // board handoff so no trigger-spin feature survives into the first respin.
        slotView.PrepareForGoldBurstLayoutSwitch();
        slotView.CompleteGoldBurstLayoutSwitch();
    }
}
