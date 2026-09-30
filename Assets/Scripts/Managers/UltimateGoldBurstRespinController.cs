using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UltimateGoldBurstRespinController : MonoBehaviour, IGoldBurstRespinController
{
    private const int ReelsPerBoard = 7;
    private const int UltimateReelCount = ReelsPerBoard * 2;

    [SerializeField, Min(1f)] private float introFramesPerSecond = 29f;

    public GoldBurstTier Tier => GoldBurstTier.Ultimate;
    public int ReelCount => UltimateReelCount;
    public bool PreserveAnimationHierarchyOrder => true;
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
        if (placement == null || visualCount < 2) return -1;

        int visualsPerBoard = visualCount / 2;
        int boardIndex = placement.startCol / ReelsPerBoard;
        int localStartColumn = placement.startCol % ReelsPerBoard;
        int visualIndex = boardIndex * visualsPerBoard + localStartColumn;
        return boardIndex >= 0 &&
               boardIndex < 2 &&
               localStartColumn >= 0 &&
               localStartColumn < visualsPerBoard &&
               visualIndex >= 0 &&
               visualIndex < visualCount
            ? visualIndex
            : -1;
    }

    public bool ContainsPlacement(int startColumn, int columnCount)
    {
        if (startColumn < 0 ||
            columnCount <= 0 ||
            startColumn + columnCount > UltimateReelCount)
        {
            return false;
        }

        int boardStart = startColumn < ReelsPerBoard ? 0 : ReelsPerBoard;
        return startColumn + columnCount <= boardStart + ReelsPerBoard;
    }

    private static void ActivateLayout(SlotView slotView)
    {
        if (slotView.IsUsingUltimateGoldBurstLayout) return;
        if (slotView.UltimateGoldBurstFirstReelRoot == null ||
            slotView.UltimateGoldBurstSecondReelRoot == null ||
            slotView.UltimateGoldBurstLayoutRoot == null)
        {
            Debug.LogError(
                "[UltimateGoldBurstRespinController] Ultimate Gold Burst " +
                "requires Two7x3Slot and both authored reel holders.",
                slotView);
            return;
        }

        slotView.PrepareForGoldBurstLayoutSwitch();
        if (slotView.BaseGameLayoutRoot != null)
        {
            slotView.BaseGameLayoutRoot.SetActive(false);
        }
        if (slotView.MegaGoldBurstLayoutRoot != null)
        {
            slotView.MegaGoldBurstLayoutRoot.SetActive(false);
        }
        slotView.UltimateGoldBurstLayoutRoot.SetActive(true);
        slotView.SetLandscapeExtraUIForBaseLayout(false);
        slotView.SetGoldBurstLayout(
            slotView.UltimateGoldBurstFirstReelRoot,
            useMegaLayout: false,
            useUltimateLayout: true);

        slotView.RebuildGoldBurstLayout();
        slotView.CompleteGoldBurstLayoutSwitch();
    }
}
