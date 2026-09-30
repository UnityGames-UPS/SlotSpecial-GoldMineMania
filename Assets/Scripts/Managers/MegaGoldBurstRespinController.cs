using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MegaGoldBurstRespinController : MonoBehaviour, IGoldBurstRespinController
{
    private const int MegaReelCount = 7;

    [SerializeField, Min(1f)] private float introFramesPerSecond = 29f;

    public GoldBurstTier Tier => GoldBurstTier.Mega;
    public int ReelCount => MegaReelCount;
    public bool PreserveAnimationHierarchyOrder => false;
    public bool UsesDeterministicTrainVisualMapping => true;

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
        if (placement == null ||
            placement.startCol < 0 ||
            placement.startCol >= MegaReelCount ||
            placement.startCol >= visualCount)
        {
            return -1;
        }

        return placement.startCol;
    }

    public bool ContainsPlacement(int startColumn, int columnCount)
    {
        return startColumn >= 0 &&
               columnCount > 0 &&
               startColumn + columnCount <= MegaReelCount;
    }

    private static void ActivateLayout(SlotView slotView)
    {
        if (slotView.IsUsingMegaGoldBurstLayout) return;
        if (slotView.MegaGoldBurstReelRoot == null ||
            slotView.MegaGoldBurstLayoutRoot == null)
        {
            Debug.LogError(
                "[MegaGoldBurstRespinController] Mega Gold Burst requires the " +
                "7x3Slot and 7x3SlotHolder scene objects.",
                slotView);
            return;
        }

        slotView.PrepareForGoldBurstLayoutSwitch();
        if (slotView.BaseGameLayoutRoot != null)
        {
            slotView.BaseGameLayoutRoot.SetActive(false);
        }
        if (slotView.UltimateGoldBurstLayoutRoot != null)
        {
            slotView.UltimateGoldBurstLayoutRoot.SetActive(false);
        }
        slotView.MegaGoldBurstLayoutRoot.SetActive(true);
        slotView.SetLandscapeExtraUIForBaseLayout(false);
        slotView.SetGoldBurstLayout(
            slotView.MegaGoldBurstReelRoot,
            useMegaLayout: true,
            useUltimateLayout: false);

        slotView.RebuildGoldBurstLayout();
        slotView.CompleteGoldBurstLayoutSwitch();
    }
}
