using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Owns the shared cinematic and UI transitions used by Free Games and
/// Gold Burst. SlotFeatureController inherits this controller so existing
/// Unity scene serialization remains intact while transition behavior lives
/// in a focused script.
/// </summary>
public abstract class TransitionController : MonoBehaviour
{
    private const string GoldBurstResultIntroAnimation = "Symbol_4";
    private const string GoldBurstResultLoopAnimation = "Symbol_4_Loop";
    private const string GoldBurstResultDissolveAnimation = "dissolved";
    private const float GoldBurstResultCountDuration = 2f;
    private const string FreeGamesStartIntroAnimation = "Symbol_3";
    private const string FreeGamesStartLoopAnimation = "Symbol_3_Loop";
    private const string FreeGamesStartDissolveAnimation = "dissolved";
    private const string TrainTrackAnimationName = "animation";

    [Header("Shared Transition References")]
    [SerializeField] protected GameObject darkBackground;
    [SerializeField] protected GameObject landscapeExtraUI;
    [SerializeField] protected GameObject track;
    [SerializeField] private GameObject trolleyMan;

    [Header("Shared Free Games / Gold Burst Result Presentation")]
    [SerializeField] private GameObject goldBurstResultPanel;
    [SerializeField] private RectTransform goldBurstResultWinBox;
    [SerializeField] private TMP_Text goldBurstResultWinAmount;
    [SerializeField] private Button goldBurstResultCollectButton;
    [SerializeField, Min(0.01f)] private float goldBurstResultPopupDuration = 0.35f;
    [SerializeField, Min(1f)] private float goldBurstResultHeartbeatScale = 1.08f;

    [Header("Shared Result Portrait Layout")]
    [SerializeField] private Vector2 portraitGoldBurstResultPanelPosition = Vector2.zero;
    [SerializeField] private Vector2 portraitGoldBurstResultPanelSize = new Vector2(80f, 80f);
    [SerializeField] private Vector3 portraitGoldBurstResultPanelScale =
        new Vector3(1.3f, 1.3f, 1.3f);
    [SerializeField] private Vector2 portraitGoldBurstResultWinBoxPosition =
        new Vector2(0f, -37f);
    [SerializeField] private Vector2 portraitGoldBurstResultWinBoxSize =
        new Vector2(600f, 250f);
    [SerializeField] private Vector3 portraitGoldBurstResultWinBoxScale =
        new Vector3(1.017801f, 1.017801f, 1.017801f);
    [SerializeField] private Vector2 portraitGoldBurstResultAmountPosition =
        new Vector2(0f, 7.1f);
    [SerializeField] private Vector2 portraitGoldBurstResultAmountSize =
        new Vector2(272.428f, 88.447f);
    [SerializeField, Min(1f)] private float portraitGoldBurstResultAmountFontSize = 65f;
    [SerializeField] private FontStyles portraitGoldBurstResultAmountFontStyle =
        FontStyles.Bold;
    [SerializeField] private Vector2 portraitGoldBurstResultCollectPosition =
        new Vector2(0f, -200f);
    [SerializeField] private Vector2 portraitGoldBurstResultCollectSize =
        new Vector2(240f, 80f);
    [SerializeField] private Vector3 portraitGoldBurstResultCollectScale = Vector3.one;

    [Header("Free Games Presentation")]
    [SerializeField] private GameObject freeGamesStartPanel;
    [SerializeField] private Button freeGamesStartButton;
    [SerializeField] private GameObject trainTrackAnimation;
    [SerializeField, Min(0.01f)] private float freeGamesStartButtonPopupDuration = 0.35f;

    [Header("Shared Transition Orientation")]
    [SerializeField] protected OrientationChange trainJourneyOrientation;
    [SerializeField] protected OCController orientationLayoutController;

    [Header("Gold Burst Train Journey Presentation")]
    [SerializeField] private RectTransform trainJourneyGreenTrain;
    [SerializeField] private RectTransform trainJourneyPurpleTrain;
    [SerializeField] private RectTransform trainJourneyRedTrain;
    [SerializeField] private RectTransform trainJourneyGoldenTrain;
    [SerializeField] private RectTransform trainJourneyActor;
    [SerializeField] private RectTransform trainJourneySecondActor;
    [SerializeField] private Vector2 portraitTrainJourneyActorPosition =
        new Vector2(-503f, -95f);
    [SerializeField] private Vector2 portraitTrainJourneyActorSize =
        new Vector2(600f, 600f);
    [SerializeField] private Vector3 portraitTrainJourneyActorScale = Vector3.one;
    [SerializeField] private Vector2 portraitTrainJourneySecondActorPosition =
        new Vector2(497f, -58f);
    [SerializeField] private Vector2 portraitTrainJourneySecondActorSize =
        new Vector2(500f, 500f);
    [SerializeField] private Vector3 portraitTrainJourneySecondActorScale = Vector3.one;
    [SerializeField] private RectTransform trainJourneyWinBox;
    [SerializeField] private TMP_Text[] trainJourneyWagonAmounts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] trainJourneyPurpleWagonAmounts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] trainJourneyRedWagonAmounts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] trainJourneyGoldenWagonAmounts = new TMP_Text[4];
    [SerializeField] private TMP_Text trainJourneyWinAmount;
    [SerializeField, Min(0.1f)] private float trainJourneyPhaseDuration = 1.25f;
    [SerializeField, Min(0f)] private float trainJourneySettleDelay = 0.2f;
    [SerializeField, Min(0f)] private float trainJourneyResultHold = 0.3f;
    [SerializeField, Min(0.1f)] private float trainJourneyExitDuration = 1f;
    [SerializeField] private float trainJourneyCenterX;
    [SerializeField] private float trainJourneyExitX = 2813f;
    [SerializeField] private float trainJourneyCollectionXOffset;
    [SerializeField, Min(0.01f)] private float trainJourneyCollectionCountDuration = 0.25f;
    [SerializeField, Min(0f)] private float trainJourneyLineActivationLead = 0.18f;
    [Tooltip("The box animation starts after this many line-animation frames have completed.")]
    [SerializeField, Min(1)] private int trainJourneyBoxStartAfterLineFrames = 8;
    [SerializeField, Min(0.01f)] private float trainJourneyActorExitLeadTime = 0.3f;
    [SerializeField, Min(0f)] private float trainJourneyActorExitDistance = 900f;

    [Header("Train Journey Result Handoff")]
    [SerializeField, Min(0.01f)] private float trainJourneyWinBoxFadeDuration = 0.6f;
    [SerializeField, Min(0.01f)] private float trainJourneyAmountTransferDuration = 0.7f;
    [SerializeField, Min(1f)] private float trainJourneyAmountPopScale = 1.2f;
    [SerializeField, Min(0.01f)] private float trainJourneyAmountPopDuration = 0.3f;

    private RectTransform trolleyManRect;
    private Vector2 trolleyManStartPosition;
    private Quaternion trolleyManStartRotation;
    private Vector3 trolleyManStartScale;
    private bool hasCapturedTrolleyManState;
    private RectTransform transitionTrackRect;
    private Vector2 transitionTrackStartPosition;
    private Vector3 transitionTrackStartScale;
    private bool hasCapturedTransitionTrackState;

    private SkeletonGraphic goldBurstResultSkeleton;
    private RectTransform goldBurstResultPanelRect;
    private RectTransform goldBurstResultCollectRect;
    private Vector2 goldBurstResultPanelBasePosition;
    private Vector2 goldBurstResultPanelBaseSize;
    private Vector3 goldBurstResultPanelBaseScale;
    private Vector2 goldBurstResultWinBoxBasePosition;
    private Vector2 goldBurstResultWinBoxBaseSize;
    private Vector3 goldBurstResultWinBoxAuthoredScale;
    private Vector3 goldBurstResultWinBoxBaseScale;
    private Vector2 goldBurstResultAmountBasePosition;
    private Vector2 goldBurstResultAmountBaseSize;
    private float goldBurstResultAmountBaseFontSize;
    private float goldBurstResultAmountBaseFontSizeMax;
    private FontStyles goldBurstResultAmountBaseFontStyle;
    private bool goldBurstResultAmountBaseAutoSizing;
    private Vector2 goldBurstResultCollectBasePosition;
    private Vector2 goldBurstResultCollectBaseSize;
    private Vector3 goldBurstResultCollectAuthoredScale;
    private Vector3 goldBurstResultCollectBaseScale;
    private bool hasCapturedGoldBurstResultState;
    private Tween goldBurstResultPopupTween;
    private Tween goldBurstResultHeartbeatTween;
    private Tween goldBurstResultCountTween;
    private UnityAction goldBurstResultCollectListener;
    private UIManager activeGoldBurstResultUi;
    private Transform goldBurstResultContainer;
    private GameObject goldBurstResultStartPanel;
    private int goldBurstResultContainerSiblingIndex;
    private bool goldBurstResultStartPanelWasActive;
    private bool isGoldBurstResultHierarchyRaised;

    private SkeletonGraphic freeGamesStartSkeleton;
    private SkeletonGraphic trainTrackSkeleton;
    private Vector3 freeGamesStartButtonBaseScale;
    private bool hasCapturedFreeGamesStartState;
    private UnityAction freeGamesStartButtonListener;
    private Tween freeGamesStartButtonTween;
    private int freeGamesStartContainerSiblingIndex;
    private bool freeGamesEndPanelWasActive;
    private bool isFreeGamesStartHierarchyRaised;
    private int trainTrackSiblingIndex;
    private bool hasCapturedTrainTrackSiblingIndex;
    private bool transitionsCached;

    private readonly Dictionary<TrainVisualType, RectTransform> trainJourneyTrains =
        new Dictionary<TrainVisualType, RectTransform>();
    private readonly Dictionary<TrainVisualType, TMP_Text[]> trainJourneyAmountsByType =
        new Dictionary<TrainVisualType, TMP_Text[]>();
    private readonly Dictionary<RectTransform, Vector2> trainJourneyStartPositions =
        new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector3> trainJourneyStartScales =
        new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, bool> trainJourneyOriginalLoops =
        new Dictionary<RectTransform, bool>();
    private bool areBigWinActorsActive;
    private Coroutine trainJourneyRoutine;
    private Coroutine trainJourneyCountRoutine;
    private RectTransform activeTrainJourneyTrain;
    private TMP_Text[] activeTrainJourneyWagonAmounts;
    private ImageAnimation trainJourneyLineAnimation;
    private ImageAnimation trainJourneyBoxAnimation;
    private Image trainJourneyWinBoxImage;
    private Color trainJourneyWinBoxBaseColor;
    private bool hasCapturedTrainJourneyWinBoxColor;
    private RectTransform trainJourneyTrackRect;
    private Vector2 trainJourneyTrackStartPosition;
    private Vector3 trainJourneyTrackStartScale;
    private bool hasCapturedTrainJourneyTrackPosition;
    private Vector2 trainJourneyWinBoxStartPosition;
    private Vector3 trainJourneyWinBoxStartScale;
    private bool hasCapturedTrainJourneyWinBoxTransform;
    private TMP_Text activeTrainJourneyTransferAmount;
    private Vector2 trainJourneyActorStartPosition;
    private Vector2 trainJourneySecondActorStartPosition;
    private Vector2 trainJourneyActorStartSize;
    private Vector2 trainJourneySecondActorStartSize;
    private Vector3 trainJourneyActorStartScale;
    private Vector3 trainJourneySecondActorStartScale;
    private Vector2 activeTrainJourneyActorStartPosition;
    private Vector2 activeTrainJourneySecondActorStartPosition;
    private float activeTrainJourneyScaleCompensation = 1f;
    private bool hasCapturedTrainJourneyState;
    private bool trainJourneyOwnsDarkBackground;

    protected void CacheTransitionSceneObjects()
    {
        if (orientationLayoutController == null)
        {
            orientationLayoutController =
                UnityEngine.Object.FindFirstObjectByType<OCController>();
        }

        darkBackground = darkBackground != null
            ? darkBackground
            : FindTransitionSceneGameObject("DarkBackground");
        landscapeExtraUI = landscapeExtraUI != null
            ? landscapeExtraUI
            : FindTransitionSceneGameObject("Landscape ExtraUI");
        track = track != null ? track : FindTransitionSceneGameObject("Track");
        trolleyMan = trolleyMan != null
            ? trolleyMan
            : FindTransitionSceneGameObject("TrolleyMan");

        CaptureTrackState();
        CaptureTrolleyManState();
        CacheGoldBurstResultPresentation();
        CacheFreeGamesPresentation();
        CacheTrainJourneyPresentation();
        transitionsCached = true;
    }

    protected void ResetGoldBurstSharedTransitions()
    {
        ResetGoldBurstResultPresentation();
        SetDarkBackgroundActive(false);
        if (track != null) track.SetActive(false);
        ResetTrolleyMan();
    }

    protected void ResetFreeGamesTransition()
    {
        AudioManager.Instance?.StopGoldMineTrainTransition();
        AudioManager.Instance?.StopGoldMineFreeSpinStartPanel();
        ResetFreeGamesStartPresentation();
        ResetSpineAnimation(trainTrackSkeleton);
        if (trainTrackAnimation != null)
        {
            trainTrackAnimation.SetActive(false);
        }
    }

    internal IEnumerator PlayGoldBurstOutroPresentation()
    {
        EnsureTransitionsCached();
        SetDarkBackgroundActive(true, showInLandscape: true);
        yield return PlayTrolleyManAnimation();
        SetDarkBackgroundActive(false);
    }

    internal IEnumerator PlayFreeGamesStartPresentation(UIManager uiManager)
    {
        EnsureTransitionsCached();
        ResetFreeGamesStartPresentation();

        SetDarkBackgroundActive(true, showInLandscape: true);
        if (freeGamesStartPanel == null || freeGamesStartSkeleton == null)
        {
            yield return PlayFreeGamesTrackAnimation();
            SetDarkBackgroundActive(false);
            yield break;
        }

        RaiseFreeGamesStartHierarchy();
        freeGamesStartPanel.SetActive(true);

        bool startRequested = false;
        Action requestStart = () =>
        {
            if (startRequested) return;
            startRequested = true;
            if (freeGamesStartButton != null)
            {
                SetButtonInteractableWithoutAlphaChange(
                    freeGamesStartButton,
                    false);
            }
            uiManager?.ShowDisabledSpinButtonForFreeGamesTransition();
        };

        if (freeGamesStartButton != null)
        {
            freeGamesStartButton.gameObject.SetActive(true);
            SetButtonInteractableWithoutAlphaChange(freeGamesStartButton, false);
            freeGamesStartButton.transform.localScale = Vector3.zero;
            freeGamesStartButtonListener = () =>
            {
                AudioManager.Instance?.PlayButton();
                requestStart();
            };
            freeGamesStartButton.onClick.AddListener(freeGamesStartButtonListener);
        }

        AudioManager.Instance?.PlayGoldMineFreeSpinStartPanel();
        float introDuration = PlaySpineAnimation(
            freeGamesStartSkeleton,
            FreeGamesStartIntroAnimation,
            false);
        if (freeGamesStartButton != null)
        {
            freeGamesStartButtonTween = freeGamesStartButton.transform
                .DOScale(
                    freeGamesStartButtonBaseScale,
                    freeGamesStartButtonPopupDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        bool hasBottomStartButton = uiManager != null &&
                                    uiManager.ShowFreeGamesStartButton(requestStart);
        float buttonPopupDuration = freeGamesStartButton != null ||
                                    hasBottomStartButton
            ? Mathf.Max(freeGamesStartButtonPopupDuration, 0.35f)
            : 0f;
        float startPresentationDuration = Mathf.Max(
            introDuration,
            buttonPopupDuration);
        if (startPresentationDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(startPresentationDuration);
        }

        PlaySpineAnimation(
            freeGamesStartSkeleton,
            FreeGamesStartLoopAnimation,
            true);
        if (freeGamesStartButton != null)
        {
            SetButtonInteractableWithoutAlphaChange(freeGamesStartButton, true);
        }
        uiManager?.SetFreeGamesStartButtonInteractable(hasBottomStartButton);

        if (freeGamesStartButton == null && !hasBottomStartButton)
        {
            startRequested = true;
        }
        while (!startRequested)
        {
            yield return null;
        }

        if (freeGamesStartButton != null)
        {
            SetButtonInteractableWithoutAlphaChange(freeGamesStartButton, false);
        }
        uiManager?.HideFreeGamesStartButton();
        RemoveFreeGamesStartListener();

        float dissolveDuration = PlaySpineAnimation(
            freeGamesStartSkeleton,
            FreeGamesStartDissolveAnimation,
            false);
        float buttonDismissDuration = 0f;
        if (freeGamesStartButton != null)
        {
            freeGamesStartButtonTween?.Kill();
            buttonDismissDuration = Mathf.Max(
                0.01f,
                freeGamesStartButtonPopupDuration);
            freeGamesStartButtonTween = freeGamesStartButton.transform
                .DOScale(Vector3.zero, buttonDismissDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true);
        }

        float exitDuration = Mathf.Max(dissolveDuration, buttonDismissDuration);
        if (exitDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(exitDuration);
        }

        if (freeGamesStartButton != null)
        {
            freeGamesStartButton.transform.localScale = Vector3.zero;
        }
        freeGamesStartPanel.SetActive(false);
        RestoreFreeGamesStartHierarchy();
        yield return PlayFreeGamesTrackAnimation();
        SetDarkBackgroundActive(false);
    }

    internal IEnumerator PlayFreeGamesEndPresentation(
        double totalWin,
        UIManager uiManager)
    {
        yield return PlayGoldBurstResultPresentation(
            totalWin,
            uiManager,
            showDarkBackgroundInLandscape: true);
        yield return PlayFreeGamesTrackAnimation();
        RestoreGoldBurstResultHierarchy();
        SetDarkBackgroundActive(false);
    }

    internal IEnumerator PlayGoldBurstResultPresentation(
        double totalWin,
        UIManager uiManager,
        bool showDarkBackgroundInLandscape = false)
    {
        EnsureTransitionsCached();
        if (goldBurstResultPanel == null || goldBurstResultSkeleton == null)
        {
            yield break;
        }

        ResetGoldBurstResultPresentation();
        ApplyGoldBurstResultLayout();
        activeGoldBurstResultUi = uiManager;
        SetDarkBackgroundActive(true, showDarkBackgroundInLandscape);
        RaiseGoldBurstResultHierarchy();

        if (goldBurstResultWinAmount != null)
        {
            SetSpriteAmount(goldBurstResultWinAmount, 0d);
            goldBurstResultWinAmount.gameObject.SetActive(true);
        }
        if (goldBurstResultWinBox != null)
        {
            goldBurstResultWinBox.gameObject.SetActive(true);
            goldBurstResultWinBox.localScale = Vector3.zero;
        }
        if (goldBurstResultCollectButton != null)
        {
            goldBurstResultCollectButton.gameObject.SetActive(true);
            SetButtonInteractableWithoutAlphaChange(
                goldBurstResultCollectButton,
                false);
            goldBurstResultCollectButton.transform.localScale = Vector3.zero;
        }
        goldBurstResultPanel.SetActive(true);

        bool collectRequested = false;
        goldBurstResultCollectListener = () =>
        {
            if (collectRequested) return;

            AudioManager.Instance?.PlayTakeButton();
            uiManager?.ShowDisabledSpinButtonForGoldBurstOutro();
            collectRequested = true;
        };
        if (goldBurstResultCollectButton != null)
        {
            goldBurstResultCollectButton.onClick.AddListener(
                goldBurstResultCollectListener);
        }

        bool hasTakeButton = uiManager != null &&
                             uiManager.ShowGoldBurstTakeButton(
                                 () =>
                                 {
                                     uiManager.ShowDisabledSpinButtonForGoldBurstOutro();
                                     collectRequested = true;
                                 });
        uiManager?.SetGoldBurstTakeButtonInteractable(false);
        bool hasCollectButton = goldBurstResultCollectButton != null;

        PlayGoldBurstResultPopup();
        float introDuration = PlaySpineAnimation(
            goldBurstResultSkeleton,
            GoldBurstResultIntroAnimation,
            false);
        StartGoldBurstResultCount(totalWin, GoldBurstResultCountDuration);

        float introElapsed = 0f;
        while (!collectRequested && introElapsed < introDuration)
        {
            introElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float loopDuration = PlaySpineAnimation(
            goldBurstResultSkeleton,
            GoldBurstResultLoopAnimation,
            true);
        StartGoldBurstResultHeartbeat(loopDuration);

        float remainingCountDuration = Mathf.Max(
            0f,
            GoldBurstResultCountDuration - introElapsed);
        if (remainingCountDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingCountDuration);
        }
        CompleteGoldBurstResultAmount(totalWin);
        if (goldBurstResultCollectButton != null)
        {
            SetButtonInteractableWithoutAlphaChange(
                goldBurstResultCollectButton,
                true);
        }
        uiManager?.SetGoldBurstTakeButtonInteractable(hasTakeButton);

        if (!hasCollectButton && !hasTakeButton)
        {
            collectRequested = true;
        }
        while (!collectRequested)
        {
            yield return null;
        }

        CompleteGoldBurstResultAmount(totalWin);
        RemoveGoldBurstResultListeners();
        StopGoldBurstResultTweens(true);

        float dissolveDuration = PlaySpineAnimation(
            goldBurstResultSkeleton,
            GoldBurstResultDissolveAnimation,
            false);
        float dismissDuration = PlayGoldBurstResultDismiss(dissolveDuration);
        float exitDuration = Mathf.Max(dissolveDuration, dismissDuration);
        if (exitDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(exitDuration);
        }

        goldBurstResultPanel.SetActive(false);
        activeGoldBurstResultUi = null;
    }

    protected IEnumerator PlayTrolleyManAnimation()
    {
        EnsureTransitionsCached();
        ResetTrolleyMan();
        ApplyTrackAndTrolleyLayout();
        if (track != null) track.SetActive(true);
        if (trolleyMan != null)
        {
            trolleyMan.SetActive(true);
            SkeletonGraphic trolleyGraphic = trolleyMan.GetComponent<SkeletonGraphic>();
            float trolleyDuration = 0f;
            if (trolleyGraphic != null)
            {
                if (trolleyGraphic.SkeletonData == null)
                {
                    trolleyGraphic.Initialize(false);
                }

                Spine.Animation trolleyAnimation =
                    trolleyGraphic.SkeletonData?.FindAnimation("animation");
                if (trolleyAnimation != null && trolleyGraphic.AnimationState != null)
                {
                    trolleyGraphic.freeze = false;
                    trolleyGraphic.AnimationState.ClearTracks();
                    trolleyGraphic.Skeleton?.SetToSetupPose();
                    trolleyGraphic.AnimationState.SetAnimation(
                        0,
                        trolleyAnimation.Name,
                        false);
                    AudioManager.Instance?.PlayGoldMineTrolleyMan();
                    trolleyDuration = trolleyAnimation.Duration;
                }
            }

            if (trolleyDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(trolleyDuration);
            }
        }

        ResetTrolleyMan();
        if (track != null) track.SetActive(false);
    }

    protected void SetDarkBackgroundActive(
        bool showDarkBackground,
        bool showInLandscape = false)
    {
        if (trainJourneyOrientation == null)
        {
            trainJourneyOrientation =
                UnityEngine.Object.FindFirstObjectByType<OrientationChange>();
        }
        bool isPortrait = trainJourneyOrientation != null &&
                          trainJourneyOrientation.CurrentMode ==
                              OrientationChange.OrientationMode.MobilePortrait;
        bool shouldShowDarkBackground = showDarkBackground &&
                                        (showInLandscape || isPortrait);
        if (darkBackground != null)
        {
            darkBackground.SetActive(shouldShowDarkBackground);
        }
        if (landscapeExtraUI != null)
        {
            landscapeExtraUI.SetActive(!showDarkBackground);
        }
    }

    protected float GetSharedPortraitOverlayCompensation()
    {
        EnsureOrientationController();
        return orientationLayoutController != null
            ? orientationLayoutController.GetSharedPortraitOverlayCompensation()
            : 1f;
    }

    protected Vector2 GetSharedPortraitOverlayPosition(Vector2 referencePosition)
    {
        EnsureOrientationController();
        return orientationLayoutController != null
            ? orientationLayoutController.GetSharedPortraitOverlayPosition(
                referencePosition)
            : referencePosition;
    }

    protected static Vector3 ScalePortraitOverlay(
        Vector3 authoredScale,
        float compensation)
    {
        return new Vector3(
            authoredScale.x * compensation,
            authoredScale.y * compensation,
            authoredScale.z);
    }

    protected static float PlaySpineAnimation(
        SkeletonGraphic skeletonGraphic,
        string animationName,
        bool loop)
    {
        if (skeletonGraphic == null || string.IsNullOrEmpty(animationName))
        {
            return 0f;
        }
        if (skeletonGraphic.SkeletonData == null)
        {
            skeletonGraphic.Initialize(false);
        }

        Spine.Animation animation =
            skeletonGraphic.SkeletonData?.FindAnimation(animationName);
        if (animation == null || skeletonGraphic.AnimationState == null)
        {
            return 0f;
        }

        skeletonGraphic.freeze = false;
        skeletonGraphic.AnimationState.ClearTracks();
        skeletonGraphic.Skeleton?.SetToSetupPose();
        skeletonGraphic.AnimationState.SetAnimation(0, animation.Name, loop);
        return animation.Duration /
               Mathf.Max(0.01f, Mathf.Abs(skeletonGraphic.timeScale));
    }

    protected static void ResetSpineAnimation(SkeletonGraphic skeletonGraphic)
    {
        if (skeletonGraphic == null) return;
        if (skeletonGraphic.SkeletonData == null)
        {
            skeletonGraphic.Initialize(false);
        }
        skeletonGraphic.AnimationState?.ClearTracks();
        skeletonGraphic.Skeleton?.SetToSetupPose();
        skeletonGraphic.freeze = true;
    }

    protected static void SetSpriteAmount(TMP_Text target, double amount)
    {
        if (target == null) return;

        string number = Math.Max(0d, amount)
            .ToString("0.00", CultureInfo.InvariantCulture);
        if (target.spriteAsset == null)
        {
            target.text = number;
            return;
        }

        var spriteText = new StringBuilder(number.Length * 10);
        foreach (char character in number)
        {
            if (character >= '0' && character <= '9')
            {
                spriteText.Append("<sprite=")
                    .Append(character - '0')
                    .Append('>');
            }
            else if (character == '.')
            {
                spriteText.Append("<sprite=10>");
            }
            else if (character == ',')
            {
                spriteText.Append("<sprite=11>");
            }
        }
        target.text = spriteText.ToString();
    }

    private void CacheTrainJourneyPresentation()
    {
        trainJourneyTrains.Clear();
        trainJourneyAmountsByType.Clear();

        trainJourneyTrackRect = track != null
            ? track.transform as RectTransform
            : null;
        if (trainJourneyTrackRect != null &&
            !hasCapturedTrainJourneyTrackPosition)
        {
            trainJourneyTrackStartPosition =
                trainJourneyTrackRect.anchoredPosition;
            trainJourneyTrackStartScale = trainJourneyTrackRect.localScale;
            hasCapturedTrainJourneyTrackPosition = true;
        }

        trainJourneyWinBoxImage = trainJourneyWinBox != null
            ? trainJourneyWinBox.GetComponent<Image>()
            : null;
        if (trainJourneyWinBoxImage != null &&
            !hasCapturedTrainJourneyWinBoxColor)
        {
            trainJourneyWinBoxBaseColor = trainJourneyWinBoxImage.color;
            hasCapturedTrainJourneyWinBoxColor = true;
        }
        if (trainJourneyWinBox != null &&
            !hasCapturedTrainJourneyWinBoxTransform)
        {
            trainJourneyWinBoxStartPosition =
                trainJourneyWinBox.anchoredPosition;
            trainJourneyWinBoxStartScale = trainJourneyWinBox.localScale;
            hasCapturedTrainJourneyWinBoxTransform = true;
        }

        trainJourneyLineAnimation = FindTransitionDescendant(
                trainJourneyWinBox,
                "LineAnimation")
            ?.GetComponent<ImageAnimation>();
        trainJourneyBoxAnimation = FindTransitionDescendant(
                trainJourneyWinBox,
                "BoxAnimations")
            ?.GetComponent<ImageAnimation>();

        trainJourneyWagonAmounts = CacheTrainJourneyVisual(
            TrainVisualType.Green,
            trainJourneyGreenTrain,
            trainJourneyWagonAmounts);
        trainJourneyPurpleWagonAmounts = CacheTrainJourneyVisual(
            TrainVisualType.HorizontalPurple,
            trainJourneyPurpleTrain,
            trainJourneyPurpleWagonAmounts);
        if (trainJourneyPurpleTrain != null)
        {
            trainJourneyTrains[TrainVisualType.VerticalPurple] = trainJourneyPurpleTrain;
            trainJourneyAmountsByType[TrainVisualType.VerticalPurple] =
                trainJourneyPurpleWagonAmounts;
        }
        trainJourneyRedWagonAmounts = CacheTrainJourneyVisual(
            TrainVisualType.Red,
            trainJourneyRedTrain,
            trainJourneyRedWagonAmounts);
        trainJourneyGoldenWagonAmounts = CacheTrainJourneyVisual(
            TrainVisualType.Golden,
            trainJourneyGoldenTrain,
            trainJourneyGoldenWagonAmounts);

        if (!hasCapturedTrainJourneyState)
        {
            if (trainJourneyActor != null)
            {
                trainJourneyActorStartPosition =
                    trainJourneyActor.anchoredPosition;
                trainJourneyActorStartSize = trainJourneyActor.sizeDelta;
                trainJourneyActorStartScale = trainJourneyActor.localScale;
            }
            if (trainJourneySecondActor != null)
            {
                trainJourneySecondActorStartPosition =
                    trainJourneySecondActor.anchoredPosition;
                trainJourneySecondActorStartSize =
                    trainJourneySecondActor.sizeDelta;
                trainJourneySecondActorStartScale =
                    trainJourneySecondActor.localScale;
            }
            activeTrainJourneyActorStartPosition =
                trainJourneyActorStartPosition;
            activeTrainJourneySecondActorStartPosition =
                trainJourneySecondActorStartPosition;
            hasCapturedTrainJourneyState = true;
        }

        if (trainJourneyWinAmount == null && trainJourneyWinBox != null)
        {
            trainJourneyWinAmount =
                trainJourneyWinBox.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private TMP_Text[] CacheTrainJourneyVisual(
        TrainVisualType type,
        RectTransform journeyTrain,
        TMP_Text[] configuredAmounts)
    {
        if (journeyTrain == null)
        {
            return configuredAmounts ?? Array.Empty<TMP_Text>();
        }

        TMP_Text[] amounts = configuredAmounts;
        if (amounts == null ||
            amounts.Length == 0 ||
            amounts.All(text => text == null))
        {
            amounts = journeyTrain
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text != null &&
                               text.name.StartsWith(
                                   "Winamount",
                                   StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(text => text.rectTransform.anchoredPosition.x)
                .ToArray();
        }

        trainJourneyTrains[type] = journeyTrain;
        trainJourneyAmountsByType[type] = amounts;

        if (!trainJourneyStartPositions.ContainsKey(journeyTrain))
        {
            trainJourneyStartPositions[journeyTrain] = journeyTrain.anchoredPosition;
            trainJourneyStartScales[journeyTrain] = journeyTrain.localScale;
            ImageAnimation trainAnimation = journeyTrain.GetComponent<ImageAnimation>();
            trainJourneyOriginalLoops[journeyTrain] =
                trainAnimation != null && trainAnimation.doLoopAnimation;
        }

        return amounts;
    }

    private void EnsureTransitionsCached()
    {
        if (!transitionsCached) CacheTransitionSceneObjects();
    }

    private void EnsureOrientationController()
    {
        if (orientationLayoutController == null)
        {
            orientationLayoutController =
                UnityEngine.Object.FindFirstObjectByType<OCController>();
        }
    }

    private void CaptureTrolleyManState()
    {
        if (trolleyMan == null || hasCapturedTrolleyManState) return;

        trolleyManRect = trolleyMan.transform as RectTransform;
        if (trolleyManRect == null) return;

        trolleyManStartPosition = trolleyManRect.anchoredPosition;
        trolleyManStartRotation = trolleyManRect.localRotation;
        trolleyManStartScale = trolleyManRect.localScale;
        hasCapturedTrolleyManState = true;
    }

    private void CaptureTrackState()
    {
        if (track == null || hasCapturedTransitionTrackState) return;

        transitionTrackRect = track.transform as RectTransform;
        if (transitionTrackRect == null) return;

        transitionTrackStartPosition = transitionTrackRect.anchoredPosition;
        transitionTrackStartScale = transitionTrackRect.localScale;
        hasCapturedTransitionTrackState = true;
    }

    private void ResetTrolleyMan()
    {
        if (trolleyMan == null) return;

        CaptureTrolleyManState();
        if (trolleyManRect != null)
        {
            DOTween.Kill(trolleyManRect);
            if (hasCapturedTrolleyManState)
            {
                trolleyManRect.anchoredPosition = trolleyManStartPosition;
                trolleyManRect.localRotation = trolleyManStartRotation;
                trolleyManRect.localScale = trolleyManStartScale;
            }
        }

        ResetSpineAnimation(trolleyMan.GetComponent<SkeletonGraphic>());
        trolleyMan.SetActive(false);
    }

    private void ApplyTrackAndTrolleyLayout()
    {
        bool usePortraitLayout = trainJourneyOrientation != null &&
            trainJourneyOrientation.CurrentMode ==
                OrientationChange.OrientationMode.MobilePortrait;
        float compensation = usePortraitLayout
            ? GetSharedPortraitOverlayCompensation()
            : 1f;

        CaptureTrackState();
        if (transitionTrackRect != null && hasCapturedTransitionTrackState)
        {
            transitionTrackRect.anchoredPosition =
                GetSharedPortraitOverlayPosition(transitionTrackStartPosition);
            transitionTrackRect.localScale = ScalePortraitOverlay(
                transitionTrackStartScale,
                compensation);
        }
        if (trolleyManRect != null && hasCapturedTrolleyManState)
        {
            trolleyManRect.anchoredPosition =
                GetSharedPortraitOverlayPosition(trolleyManStartPosition);
            trolleyManRect.localScale = ScalePortraitOverlay(
                trolleyManStartScale,
                compensation);
        }
    }

    private void CacheGoldBurstResultPresentation()
    {
        goldBurstResultPanel = goldBurstResultPanel != null
            ? goldBurstResultPanel
            : FindTransitionSceneGameObject("FreeGamesEnd");
        if (goldBurstResultPanel == null) return;

        goldBurstResultSkeleton = goldBurstResultPanel.GetComponent<SkeletonGraphic>();
        goldBurstResultPanelRect = goldBurstResultPanel.transform as RectTransform;
        goldBurstResultContainer = goldBurstResultPanel.transform.parent;
        if (goldBurstResultContainer != null)
        {
            goldBurstResultContainerSiblingIndex =
                goldBurstResultContainer.GetSiblingIndex();
            Transform startPanel = FindTransitionDescendant(
                goldBurstResultContainer,
                "FreeGamesStart");
            goldBurstResultStartPanel = startPanel != null
                ? startPanel.gameObject
                : null;
        }
        if (goldBurstResultWinBox == null)
        {
            goldBurstResultWinBox = FindTransitionDescendant(
                goldBurstResultPanel.transform,
                "Winbox") as RectTransform;
        }
        if (goldBurstResultWinAmount == null && goldBurstResultWinBox != null)
        {
            Transform amount = FindTransitionDescendant(
                goldBurstResultWinBox,
                "Winamount");
            goldBurstResultWinAmount = amount != null
                ? amount.GetComponent<TMP_Text>()
                : null;
        }
        if (goldBurstResultCollectButton == null)
        {
            Transform collect = FindTransitionDescendant(
                goldBurstResultPanel.transform,
                "Collect");
            goldBurstResultCollectButton = collect != null
                ? collect.GetComponent<Button>()
                : null;
        }
        goldBurstResultCollectRect = goldBurstResultCollectButton != null
            ? goldBurstResultCollectButton.transform as RectTransform
            : null;

        if (hasCapturedGoldBurstResultState) return;

        if (goldBurstResultPanelRect != null)
        {
            goldBurstResultPanelBasePosition = goldBurstResultPanelRect.anchoredPosition;
            goldBurstResultPanelBaseSize = goldBurstResultPanelRect.sizeDelta;
            goldBurstResultPanelBaseScale = goldBurstResultPanelRect.localScale;
        }
        if (goldBurstResultWinBox != null)
        {
            goldBurstResultWinBoxBasePosition = goldBurstResultWinBox.anchoredPosition;
            goldBurstResultWinBoxBaseSize = goldBurstResultWinBox.sizeDelta;
        }
        goldBurstResultWinBoxBaseScale = goldBurstResultWinBox != null
            ? goldBurstResultWinBox.localScale
            : Vector3.one;
        goldBurstResultWinBoxAuthoredScale = goldBurstResultWinBoxBaseScale;
        if (goldBurstResultWinAmount != null)
        {
            RectTransform amountRect = goldBurstResultWinAmount.rectTransform;
            goldBurstResultAmountBasePosition = amountRect.anchoredPosition;
            goldBurstResultAmountBaseSize = amountRect.sizeDelta;
            goldBurstResultAmountBaseFontSize = goldBurstResultWinAmount.fontSize;
            goldBurstResultAmountBaseFontSizeMax = goldBurstResultWinAmount.fontSizeMax;
            goldBurstResultAmountBaseFontStyle = goldBurstResultWinAmount.fontStyle;
            goldBurstResultAmountBaseAutoSizing =
                goldBurstResultWinAmount.enableAutoSizing;
        }
        if (goldBurstResultCollectRect != null)
        {
            goldBurstResultCollectBasePosition =
                goldBurstResultCollectRect.anchoredPosition;
            goldBurstResultCollectBaseSize = goldBurstResultCollectRect.sizeDelta;
        }
        goldBurstResultCollectBaseScale = goldBurstResultCollectButton != null
            ? goldBurstResultCollectButton.transform.localScale
            : Vector3.one;
        goldBurstResultCollectAuthoredScale = goldBurstResultCollectBaseScale;
        hasCapturedGoldBurstResultState = true;
    }

    private void ApplyGoldBurstResultLayout()
    {
        bool usePortraitLayout = trainJourneyOrientation != null &&
            trainJourneyOrientation.CurrentMode ==
                OrientationChange.OrientationMode.MobilePortrait;
        float overlayCompensation = usePortraitLayout
            ? GetSharedPortraitOverlayCompensation()
            : 1f;

        if (goldBurstResultPanelRect != null)
        {
            goldBurstResultPanelRect.anchoredPosition = usePortraitLayout
                ? portraitGoldBurstResultPanelPosition * overlayCompensation
                : goldBurstResultPanelBasePosition;
            goldBurstResultPanelRect.sizeDelta = usePortraitLayout
                ? portraitGoldBurstResultPanelSize
                : goldBurstResultPanelBaseSize;
            goldBurstResultPanelRect.localScale = usePortraitLayout
                ? ScalePortraitOverlay(
                    portraitGoldBurstResultPanelScale,
                    overlayCompensation)
                : goldBurstResultPanelBaseScale;
        }
        if (goldBurstResultWinBox != null)
        {
            goldBurstResultWinBox.anchoredPosition = usePortraitLayout
                ? portraitGoldBurstResultWinBoxPosition
                : goldBurstResultWinBoxBasePosition;
            goldBurstResultWinBox.sizeDelta = usePortraitLayout
                ? portraitGoldBurstResultWinBoxSize
                : goldBurstResultWinBoxBaseSize;
            goldBurstResultWinBoxBaseScale = usePortraitLayout
                ? portraitGoldBurstResultWinBoxScale
                : goldBurstResultWinBoxAuthoredScale;
            goldBurstResultWinBox.localScale = goldBurstResultWinBoxBaseScale;
        }
        if (goldBurstResultWinAmount != null)
        {
            RectTransform amountRect = goldBurstResultWinAmount.rectTransform;
            amountRect.anchoredPosition = usePortraitLayout
                ? portraitGoldBurstResultAmountPosition
                : goldBurstResultAmountBasePosition;
            amountRect.sizeDelta = usePortraitLayout
                ? portraitGoldBurstResultAmountSize
                : goldBurstResultAmountBaseSize;
            goldBurstResultWinAmount.enableAutoSizing = usePortraitLayout ||
                goldBurstResultAmountBaseAutoSizing;
            goldBurstResultWinAmount.fontSize = usePortraitLayout
                ? portraitGoldBurstResultAmountFontSize
                : goldBurstResultAmountBaseFontSize;
            goldBurstResultWinAmount.fontSizeMax = usePortraitLayout
                ? portraitGoldBurstResultAmountFontSize
                : goldBurstResultAmountBaseFontSizeMax;
            goldBurstResultWinAmount.fontStyle = usePortraitLayout
                ? portraitGoldBurstResultAmountFontStyle
                : goldBurstResultAmountBaseFontStyle;
        }
        if (goldBurstResultCollectRect != null)
        {
            goldBurstResultCollectRect.anchoredPosition = usePortraitLayout
                ? portraitGoldBurstResultCollectPosition
                : goldBurstResultCollectBasePosition;
            goldBurstResultCollectRect.sizeDelta = usePortraitLayout
                ? portraitGoldBurstResultCollectSize
                : goldBurstResultCollectBaseSize;
            goldBurstResultCollectBaseScale = usePortraitLayout
                ? portraitGoldBurstResultCollectScale
                : goldBurstResultCollectAuthoredScale;
            goldBurstResultCollectRect.localScale = goldBurstResultCollectBaseScale;
        }
    }

    private void CacheFreeGamesPresentation()
    {
        freeGamesStartPanel = freeGamesStartPanel != null
            ? freeGamesStartPanel
            : goldBurstResultStartPanel != null
                ? goldBurstResultStartPanel
                : FindTransitionSceneGameObject("FreeGamesStart");
        if (freeGamesStartPanel != null)
        {
            freeGamesStartSkeleton = freeGamesStartPanel.GetComponent<SkeletonGraphic>();
            if (freeGamesStartButton == null)
            {
                Transform startButton = FindTransitionDescendant(
                    freeGamesStartPanel.transform,
                    "Start");
                freeGamesStartButton = startButton != null
                    ? startButton.GetComponent<Button>()
                    : null;
            }
            if (!hasCapturedFreeGamesStartState)
            {
                freeGamesStartButtonBaseScale = freeGamesStartButton != null
                    ? freeGamesStartButton.transform.localScale
                    : Vector3.one;
                hasCapturedFreeGamesStartState = true;
            }
        }

        trainTrackAnimation = trainTrackAnimation != null
            ? trainTrackAnimation
            : FindTransitionSceneGameObject("TrainTrackAnimation");
        if (trainTrackAnimation == null) return;

        trainTrackSkeleton = trainTrackAnimation.GetComponent<SkeletonGraphic>();
        if (!hasCapturedTrainTrackSiblingIndex &&
            trainTrackAnimation.transform.parent != null)
        {
            trainTrackSiblingIndex = trainTrackAnimation.transform.GetSiblingIndex();
            hasCapturedTrainTrackSiblingIndex = true;
        }
    }

    private void ResetFreeGamesStartPresentation()
    {
        freeGamesStartButtonTween?.Kill();
        freeGamesStartButtonTween = null;
        RemoveFreeGamesStartListener();

        if (freeGamesStartButton != null)
        {
            freeGamesStartButton.transform.localScale =
                freeGamesStartButtonBaseScale;
            SetButtonInteractableWithoutAlphaChange(freeGamesStartButton, false);
        }
        ResetSpineAnimation(freeGamesStartSkeleton);
        if (freeGamesStartPanel != null)
        {
            freeGamesStartPanel.SetActive(false);
        }
        RestoreFreeGamesStartHierarchy();
    }

    private void RemoveFreeGamesStartListener()
    {
        if (freeGamesStartButton != null &&
            freeGamesStartButtonListener != null)
        {
            freeGamesStartButton.onClick.RemoveListener(
                freeGamesStartButtonListener);
        }
        freeGamesStartButtonListener = null;
    }

    private void RaiseFreeGamesStartHierarchy()
    {
        if (goldBurstResultContainer == null ||
            isFreeGamesStartHierarchyRaised)
        {
            return;
        }

        freeGamesStartContainerSiblingIndex =
            goldBurstResultContainer.GetSiblingIndex();
        if (goldBurstResultPanel != null)
        {
            freeGamesEndPanelWasActive = goldBurstResultPanel.activeSelf;
            goldBurstResultPanel.SetActive(false);
        }
        goldBurstResultContainer.SetAsLastSibling();
        isFreeGamesStartHierarchyRaised = true;
    }

    private void RestoreFreeGamesStartHierarchy()
    {
        if (!isFreeGamesStartHierarchyRaised) return;

        if (goldBurstResultContainer != null)
        {
            goldBurstResultContainer.SetSiblingIndex(
                freeGamesStartContainerSiblingIndex);
        }
        if (goldBurstResultPanel != null)
        {
            goldBurstResultPanel.SetActive(freeGamesEndPanelWasActive);
        }
        isFreeGamesStartHierarchyRaised = false;
    }

    private IEnumerator PlayFreeGamesTrackAnimation()
    {
        if (trainTrackAnimation == null || trainTrackSkeleton == null)
        {
            yield break;
        }

        bool ownsDarkBackground = darkBackground != null &&
                                  !darkBackground.activeSelf;
        SetDarkBackgroundActive(true, showInLandscape: true);

        Transform trackTransform = trainTrackAnimation.transform;
        Transform trackParent = trackTransform.parent;
        if (trackParent != null)
        {
            trackTransform.SetAsLastSibling();
        }

        trainTrackAnimation.SetActive(true);
        AudioManager.Instance?.PlayGoldMineTrainTransition();
        float duration = PlaySpineAnimation(
            trainTrackSkeleton,
            TrainTrackAnimationName,
            false);
        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }
        else
        {
            yield return null;
        }

        AudioManager.Instance?.StopGoldMineTrainTransition();
        ResetSpineAnimation(trainTrackSkeleton);
        trainTrackAnimation.SetActive(false);
        if (trackParent != null && hasCapturedTrainTrackSiblingIndex)
        {
            trackTransform.SetSiblingIndex(Mathf.Clamp(
                trainTrackSiblingIndex,
                0,
                trackParent.childCount - 1));
        }
        if (ownsDarkBackground)
        {
            SetDarkBackgroundActive(false);
        }
    }

    private void PlayGoldBurstResultPopup()
    {
        goldBurstResultPopupTween?.Kill();
        Sequence popup = DOTween.Sequence().SetUpdate(true);
        if (goldBurstResultWinBox != null)
        {
            popup.Join(goldBurstResultWinBox
                .DOScale(goldBurstResultWinBoxBaseScale,
                    goldBurstResultPopupDuration)
                .SetEase(Ease.OutBack));
        }
        if (goldBurstResultCollectButton != null)
        {
            popup.Join(goldBurstResultCollectButton.transform
                .DOScale(goldBurstResultCollectBaseScale,
                    goldBurstResultPopupDuration)
                .SetEase(Ease.OutBack));
        }
        goldBurstResultPopupTween = popup;
    }

    private float PlayGoldBurstResultDismiss(float dissolveDuration)
    {
        goldBurstResultPopupTween?.Kill();
        float duration = Mathf.Max(0.01f, goldBurstResultPopupDuration);
        if (dissolveDuration > 0f)
        {
            duration = Mathf.Min(duration, dissolveDuration);
        }

        Sequence dismiss = DOTween.Sequence().SetUpdate(true);
        bool hasTarget = false;
        if (goldBurstResultWinBox != null)
        {
            dismiss.Join(goldBurstResultWinBox
                .DOScale(Vector3.zero, duration)
                .SetEase(Ease.InBack));
            hasTarget = true;
        }
        if (goldBurstResultCollectButton != null)
        {
            SetButtonInteractableWithoutAlphaChange(
                goldBurstResultCollectButton,
                false);
            dismiss.Join(goldBurstResultCollectButton.transform
                .DOScale(Vector3.zero, duration)
                .SetEase(Ease.InBack));
            hasTarget = true;
        }
        if (!hasTarget)
        {
            dismiss.Kill();
            return 0f;
        }

        goldBurstResultPopupTween = dismiss;
        return duration;
    }

    private void StartGoldBurstResultCount(double totalWin, float duration)
    {
        goldBurstResultCountTween?.Kill();
        if (goldBurstResultWinAmount == null) return;

        double target = Math.Max(0d, totalWin);
        if (duration <= 0f || target <= 0d)
        {
            SetSpriteAmount(goldBurstResultWinAmount, target);
            return;
        }

        double displayed = 0d;
        goldBurstResultCountTween = DOTween.To(
                () => displayed,
                value =>
                {
                    displayed = value;
                    SetSpriteAmount(goldBurstResultWinAmount, displayed);
                },
                target,
                duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                SetSpriteAmount(goldBurstResultWinAmount, target);
                goldBurstResultCountTween = null;
            });
    }

    private void StartGoldBurstResultHeartbeat(float loopDuration)
    {
        goldBurstResultHeartbeatTween?.Kill();
        if (goldBurstResultWinBox == null || loopDuration <= 0f) return;

        float halfCycle = loopDuration * 0.5f;
        Sequence heartbeat = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(goldBurstResultWinBox);
        heartbeat.Append(goldBurstResultWinBox
            .DOScale(
                goldBurstResultWinBoxBaseScale * goldBurstResultHeartbeatScale,
                halfCycle)
            .SetEase(Ease.InOutSine));
        heartbeat.Append(goldBurstResultWinBox
            .DOScale(goldBurstResultWinBoxBaseScale, halfCycle)
            .SetEase(Ease.InOutSine));
        heartbeat.SetLoops(-1, LoopType.Restart);
        goldBurstResultHeartbeatTween = heartbeat;
    }

    private void CompleteGoldBurstResultAmount(double totalWin)
    {
        goldBurstResultCountTween?.Kill();
        goldBurstResultCountTween = null;
        SetSpriteAmount(goldBurstResultWinAmount, totalWin);
    }

    private void RemoveGoldBurstResultListeners()
    {
        if (goldBurstResultCollectButton != null &&
            goldBurstResultCollectListener != null)
        {
            goldBurstResultCollectButton.onClick.RemoveListener(
                goldBurstResultCollectListener);
        }
        goldBurstResultCollectListener = null;
        activeGoldBurstResultUi?.HideGoldBurstTakeButton();
    }

    private void StopGoldBurstResultTweens(bool restoreScale)
    {
        goldBurstResultPopupTween?.Kill();
        goldBurstResultPopupTween = null;
        goldBurstResultHeartbeatTween?.Kill();
        goldBurstResultHeartbeatTween = null;
        goldBurstResultCountTween?.Kill();
        goldBurstResultCountTween = null;

        if (!restoreScale || !hasCapturedGoldBurstResultState) return;
        if (goldBurstResultWinBox != null)
        {
            goldBurstResultWinBox.localScale = goldBurstResultWinBoxBaseScale;
        }
        if (goldBurstResultCollectButton != null)
        {
            goldBurstResultCollectButton.transform.localScale =
                goldBurstResultCollectBaseScale;
            SetButtonInteractableWithoutAlphaChange(
                goldBurstResultCollectButton,
                false);
        }
    }

    private void ResetGoldBurstResultPresentation()
    {
        StopGoldBurstResultTweens(true);
        RemoveGoldBurstResultListeners();
        activeGoldBurstResultUi = null;
        ResetSpineAnimation(goldBurstResultSkeleton);
        if (goldBurstResultPanel != null)
        {
            goldBurstResultPanel.SetActive(false);
        }
        RestoreGoldBurstResultHierarchy();
    }

    private void RaiseGoldBurstResultHierarchy()
    {
        if (goldBurstResultContainer == null ||
            isGoldBurstResultHierarchyRaised)
        {
            return;
        }

        goldBurstResultContainerSiblingIndex =
            goldBurstResultContainer.GetSiblingIndex();
        if (goldBurstResultStartPanel != null)
        {
            goldBurstResultStartPanelWasActive =
                goldBurstResultStartPanel.activeSelf;
            goldBurstResultStartPanel.SetActive(false);
        }
        goldBurstResultContainer.SetAsLastSibling();
        isGoldBurstResultHierarchyRaised = true;
    }

    private void RestoreGoldBurstResultHierarchy()
    {
        if (!isGoldBurstResultHierarchyRaised) return;

        if (goldBurstResultContainer != null)
        {
            goldBurstResultContainer.SetSiblingIndex(
                goldBurstResultContainerSiblingIndex);
        }
        if (goldBurstResultStartPanel != null)
        {
            goldBurstResultStartPanel.SetActive(
                goldBurstResultStartPanelWasActive);
        }
        isGoldBurstResultHierarchyRaised = false;
    }

    private static void SetButtonInteractableWithoutAlphaChange(
        Button button,
        bool interactable)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        Color disabledColor = colors.disabledColor;
        disabledColor.a = colors.normalColor.a;
        colors.disabledColor = disabledColor;
        button.colors = colors;
        button.interactable = interactable;
    }

    private static Transform FindTransitionDescendant(
        Transform root,
        string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindTransitionDescendant(
                root.GetChild(index),
                objectName);
            if (result != null) return result;
        }
        return null;
    }

    private static GameObject FindTransitionSceneGameObject(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }
        return null;
    }

    protected bool TryStartTrainJourneyPresentation(
        TrainPlacement train,
        TMP_Text destinationAmount,
        Vector3 destinationBaseScale,
        Action onComplete)
    {
        if (!CanPlayTrainJourneyPresentation(train)) return false;

        trainJourneyRoutine = StartCoroutine(
            PlayTrainJourneyPresentation(
                train,
                destinationAmount,
                destinationBaseScale,
                onComplete));
        return true;
    }

    protected void ResetTrainJourneyTransition()
    {
        ResetTrainJourneyPresentation();
    }

    private bool CanPlayTrainJourneyPresentation(TrainPlacement train)
    {
        return train != null &&
               trainJourneyWinBox != null &&
               TryGetTrainJourneyVisual(
                   train.type,
                   out _,
                   out TMP_Text[] wagonAmounts) &&
               wagonAmounts.Any(text => text != null);
    }

    protected static double GetTrainPayout(TrainPlacement train)
    {
        if (train == null) return 0d;
        return train.payout > 0d
            ? train.payout
            : train.trainJourney?.Sum() ?? 0d;
    }

    private bool TryGetTrainJourneyVisual(
        TrainVisualType type,
        out RectTransform journeyTrain,
        out TMP_Text[] wagonAmounts)
    {
        wagonAmounts = null;
        if (!trainJourneyTrains.TryGetValue(type, out journeyTrain) ||
            journeyTrain == null ||
            !trainJourneyAmountsByType.TryGetValue(type, out wagonAmounts) ||
            wagonAmounts == null)
        {
            journeyTrain = null;
            wagonAmounts = null;
            return false;
        }

        return true;
    }

    private IEnumerator PlayTrainJourneyPresentation(
        TrainPlacement train,
        TMP_Text destinationAmount,
        Vector3 destinationBaseScale,
        Action onComplete)
    {
        ResetTrainJourneyVisuals();
        if (!TryGetTrainJourneyVisual(
                train.type,
                out activeTrainJourneyTrain,
                out activeTrainJourneyWagonAmounts))
        {
            trainJourneyRoutine = null;
            onComplete?.Invoke();
            yield break;
        }
        ApplyTrainJourneyLayout();
        AudioManager.Instance?.PlayGoldMineManWithTrain();

        double totalPayout = GetTrainPayout(train);
        SetTrainJourneyWagonAmounts(train.trainJourney);
        SetSpriteAmount(trainJourneyWinAmount, 0d);
        if (trainJourneyWinAmount != null)
        {
            trainJourneyWinAmount.gameObject.SetActive(true);
        }
        RestoreTrainJourneyWinBoxImage();

        if (darkBackground != null && !darkBackground.activeSelf)
        {
            SetDarkBackgroundActive(true, showInLandscape: true);
            trainJourneyOwnsDarkBackground = true;
        }

        trainJourneyWinBox.gameObject.SetActive(true);
        PlayTrainJourneyActor(trainJourneyActor);
        PlayTrainJourneyActor(trainJourneySecondActor);

        ImageAnimation trainAnimation =
            activeTrainJourneyTrain.GetComponent<ImageAnimation>();
        float travelDuration = Mathf.Max(
            0.2f,
            trainJourneyPhaseDuration + trainJourneyExitDuration);
        if (trainAnimation != null)
        {
            trainAnimation.StopAnimation();
            trainAnimation.onLoopComplete = null;
            trainAnimation.doLoopAnimation = true;
            trainAnimation.ClearLoopDuration();
        }

        Vector2 startPosition = trainJourneyStartPositions.TryGetValue(
            activeTrainJourneyTrain,
            out Vector2 capturedStartPosition)
                ? GetSharedPortraitOverlayPosition(capturedStartPosition)
                : activeTrainJourneyTrain.anchoredPosition;
        activeTrainJourneyTrain.anchoredPosition = startPosition;
        if (trainJourneyStartScales.TryGetValue(
                activeTrainJourneyTrain,
                out Vector3 capturedStartScale))
        {
            activeTrainJourneyTrain.localScale = ScalePortraitOverlay(
                capturedStartScale,
                activeTrainJourneyScaleCompensation);
        }

        if (trainJourneySettleDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(trainJourneySettleDelay);
        }

        activeTrainJourneyTrain.gameObject.SetActive(true);
        trainAnimation?.PlayAnimation();

        Tween travelTween = activeTrainJourneyTrain
            .DOAnchorPosX(
                GetSharedPortraitOverlayPosition(
                    new Vector2(trainJourneyExitX, 0f)).x,
                travelDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true);

        yield return AnimateTrainJourneyCollections(
            train.trainJourney,
            totalPayout,
            travelDuration);

        if (travelTween.IsActive() && !travelTween.IsComplete())
        {
            yield return travelTween.WaitForCompletion();
        }

        if (trainJourneyResultHold > 0f)
        {
            yield return new WaitForSecondsRealtime(trainJourneyResultHold);
        }

        yield return PlayTrainJourneyActorJumpAndExit();

        yield return PlayTrainJourneyResultHandoff(
            destinationAmount,
            destinationBaseScale,
            totalPayout);

        ResetTrainJourneyVisuals();
        trainJourneyRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator AnimateTrainJourneyCollections(
        IReadOnlyList<double> amounts,
        double totalPayout,
        float travelDuration)
    {
        int amountCount = Mathf.Min(
            amounts?.Count ?? 0,
            activeTrainJourneyWagonAmounts?.Length ?? 0);
        if (amountCount <= 0)
        {
            yield return CountTrainJourneyWinAmount(
                0d,
                totalPayout,
                travelDuration);
            yield break;
        }

        float collectionWorldX = trainJourneyWinBox
            .TransformPoint(new Vector3(trainJourneyCollectionXOffset, 0f, 0f))
            .x;
        float trainWorldSpeed = GetTrainJourneyWorldSpeed(travelDuration);
        float lineActivationWorldX = collectionWorldX -
                                     trainWorldSpeed * trainJourneyLineActivationLead;
        float collectionTimeout = Time.realtimeSinceStartup + travelDuration + 0.5f;
        double displayedTotal = 0d;
        Coroutine lastCountRoutine = null;

        for (int index = 0; index < amountCount; index++)
        {
            TMP_Text wagonAmount = activeTrainJourneyWagonAmounts[index];
            if (wagonAmount == null) continue;

            RectTransform wagonMarker = wagonAmount.rectTransform;
            while (wagonMarker != null &&
                   wagonMarker.position.x < lineActivationWorldX &&
                   Time.realtimeSinceStartup < collectionTimeout)
            {
                yield return null;
            }

            if (wagonMarker == null || wagonMarker.position.x < lineActivationWorldX)
            {
                continue;
            }

            yield return PlayTrainJourneyCollectionEffect();

            double nextTotal = displayedTotal + Math.Max(0d, amounts[index]);
            if (index == amountCount - 1)
            {
                nextTotal = totalPayout;
            }

            lastCountRoutine = StartCoroutine(
                CountTrainJourneyWinAmountTracked(
                    displayedTotal,
                    nextTotal,
                    trainJourneyCollectionCountDuration));
            trainJourneyCountRoutine = lastCountRoutine;
            displayedTotal = nextTotal;
        }

        if (lastCountRoutine != null)
        {
            yield return lastCountRoutine;
        }

        SetSpriteAmount(trainJourneyWinAmount, totalPayout);
    }

    private IEnumerator CountTrainJourneyWinAmountTracked(
        double startAmount,
        double targetAmount,
        float duration)
    {
        yield return CountTrainJourneyWinAmount(
            startAmount,
            targetAmount,
            duration);
        trainJourneyCountRoutine = null;
    }

    private float GetTrainJourneyWorldSpeed(float travelDuration)
    {
        if (activeTrainJourneyTrain == null ||
            activeTrainJourneyTrain.parent == null ||
            travelDuration <= 0f)
        {
            return 0f;
        }

        Vector3 exitWorldPosition = activeTrainJourneyTrain.parent.TransformPoint(
            new Vector3(
                GetSharedPortraitOverlayPosition(
                    new Vector2(trainJourneyExitX, 0f)).x,
                activeTrainJourneyTrain.anchoredPosition.y,
                activeTrainJourneyTrain.localPosition.z));
        return Mathf.Abs(exitWorldPosition.x - activeTrainJourneyTrain.position.x) /
               travelDuration;
    }

    private IEnumerator PlayTrainJourneyCollectionEffect()
    {
        if (trainJourneyLineAnimation == null)
        {
            PlayTrainJourneyBoxAnimation();
            yield break;
        }

        bool lineComplete = false;
        bool boxAnimationStarted = false;
        PrepareTrainJourneyEffectAnimation(trainJourneyLineAnimation);
        int boxStartFrameIndex = Mathf.Clamp(
            trainJourneyBoxStartAfterLineFrames,
            1,
            trainJourneyLineAnimation.textureArray?.Count ?? 1);
        Action<int> frameDisplayedHandler = displayedFrameIndex =>
        {
            if (boxAnimationStarted || displayedFrameIndex < boxStartFrameIndex) return;

            boxAnimationStarted = true;
            PlayTrainJourneyBoxAnimation();
        };
        trainJourneyLineAnimation.onFrameDisplayed += frameDisplayedHandler;
        trainJourneyLineAnimation.onLoopComplete = completedLoops =>
        {
            if (completedLoops < 1) return;
            lineComplete = true;
        };
        trainJourneyLineAnimation.gameObject.SetActive(true);
        AudioManager.Instance?.PlayGoldMineGoldGoingUpward();
        trainJourneyLineAnimation.PlayAnimation();

        float timeout = Time.realtimeSinceStartup +
                        GetImageAnimationDuration(trainJourneyLineAnimation) + 0.25f;
        while (!lineComplete && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }

        trainJourneyLineAnimation.onFrameDisplayed -= frameDisplayedHandler;
        StopAndHideTrainJourneyEffect(trainJourneyLineAnimation);
        if (!boxAnimationStarted)
        {
            PlayTrainJourneyBoxAnimation();
        }
    }

    private IEnumerator CountTrainJourneyWinAmount(
        double startAmount,
        double targetAmount,
        float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            double displayedAmount = startAmount +
                                     (targetAmount - startAmount) *
                                     EaseOutCubic(progress);
            SetSpriteAmount(trainJourneyWinAmount, displayedAmount);
            yield return null;
        }

        SetSpriteAmount(trainJourneyWinAmount, targetAmount);
    }

    private void PlayTrainJourneyBoxAnimation()
    {
        if (trainJourneyBoxAnimation == null) return;

        PrepareTrainJourneyEffectAnimation(trainJourneyBoxAnimation);
        trainJourneyBoxAnimation.onLoopComplete = completedLoops =>
        {
            if (completedLoops < 1) return;
            StopAndHideTrainJourneyEffect(trainJourneyBoxAnimation);
        };
        trainJourneyBoxAnimation.gameObject.SetActive(true);
        trainJourneyBoxAnimation.PlayAnimation();
    }

    private static void PrepareTrainJourneyEffectAnimation(
        ImageAnimation animation)
    {
        if (animation == null) return;

        animation.onLoopComplete = null;
        animation.onFrameDisplayed = null;
        animation.doLoopAnimation = false;
        animation.StopAnimation();
        animation.ClearLoopDuration();
        animation.RevertToInitialState();
    }

    private static void StopAndHideTrainJourneyEffect(
        ImageAnimation animation)
    {
        if (animation == null) return;

        animation.onLoopComplete = null;
        animation.onFrameDisplayed = null;
        animation.doLoopAnimation = false;
        animation.StopAnimation();
        animation.ClearLoopDuration();
        animation.RevertToInitialState();
        animation.gameObject.SetActive(false);
    }

    private static float GetImageAnimationDuration(ImageAnimation animation)
    {
        int frameCount = animation?.textureArray?.Count ?? 0;
        if (frameCount <= 0) return 0f;

        float animationSpeed = Mathf.Max(0.01f, animation.AnimationSpeed);
        float frameDuration = (1f / 24f) * frameCount / animationSpeed;
        return frameDuration * frameCount;
    }

    private void SetTrainJourneyWagonAmounts(IReadOnlyList<double> amounts)
    {
        if (activeTrainJourneyWagonAmounts == null) return;

        for (int index = 0; index < activeTrainJourneyWagonAmounts.Length; index++)
        {
            TMP_Text amountText = activeTrainJourneyWagonAmounts[index];
            if (amountText == null) continue;

            bool hasAmount = amounts != null && index < amounts.Count;
            amountText.gameObject.SetActive(hasAmount);
            if (hasAmount)
            {
                SetSpriteAmount(amountText, amounts[index]);
            }
        }
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    internal void ShowBigWinActors()
    {
        EnsureTransitionsCached();

        bool usePortraitLayout = trainJourneyOrientation != null &&
            trainJourneyOrientation.CurrentMode ==
                OrientationChange.OrientationMode.MobilePortrait;
        activeTrainJourneyScaleCompensation = usePortraitLayout
            ? GetSharedPortraitOverlayCompensation()
            : 1f;
        ApplyTrainJourneyActorLayout(usePortraitLayout);

        PlayTrainJourneyActor(trainJourneyActor);
        PlayTrainJourneyActor(trainJourneySecondActor);
        areBigWinActorsActive = true;
    }

    internal void HideBigWinActors()
    {
        if (!areBigWinActorsActive) return;

        ResetTrainJourneyActor(
            trainJourneyActor,
            trainJourneyActorStartPosition,
            trainJourneyActorStartSize,
            trainJourneyActorStartScale);
        ResetTrainJourneyActor(
            trainJourneySecondActor,
            trainJourneySecondActorStartPosition,
            trainJourneySecondActorStartSize,
            trainJourneySecondActorStartScale);
        areBigWinActorsActive = false;
    }

    internal IEnumerator PlayBigWinActorsExit()
    {
        if (!areBigWinActorsActive) yield break;

        // Start the downward exit during the final part of the Jump animation
        // so the actors fall naturally instead of waiting for Jump to finish.
        yield return PlayTrainJourneyActorJumpAndExit();

        if (!areBigWinActorsActive) yield break;
        HideBigWinActors();
    }

    private static void PlayTrainJourneyActor(RectTransform actor)
    {
        if (actor == null) return;

        actor.gameObject.SetActive(true);
        SkeletonGraphic actorGraphic =
            actor.GetComponent<SkeletonGraphic>();
        if (actorGraphic == null) return;

        if (actorGraphic.SkeletonData == null)
        {
            actorGraphic.Initialize(false);
        }

        actorGraphic.freeze = false;
        actorGraphic.AnimationState?.ClearTracks();
        Spine.Animation enterAnimation =
            actorGraphic.SkeletonData?.FindAnimation("enter");
        Spine.Animation idleAnimation =
            actorGraphic.SkeletonData?.FindAnimation("Ideal");
        if (enterAnimation != null)
        {
            actorGraphic.AnimationState.SetAnimation(0, enterAnimation.Name, false);
            if (idleAnimation != null)
            {
                actorGraphic.AnimationState.AddAnimation(0, idleAnimation.Name, true, 0f);
            }
        }
        else if (idleAnimation != null)
        {
            actorGraphic.AnimationState.SetAnimation(0, idleAnimation.Name, true);
        }
    }

    private IEnumerator PlayTrainJourneyActorJumpAndExit()
    {
        float donkeyJumpDuration = PlayTrainJourneyActorJump(trainJourneyActor);
        float manJumpDuration = PlayTrainJourneyActorJump(trainJourneySecondActor);
        float jumpDuration = Mathf.Max(donkeyJumpDuration, manJumpDuration);
        float exitDuration = Mathf.Min(
            Mathf.Max(0.01f, trainJourneyActorExitLeadTime),
            jumpDuration > 0f ? jumpDuration : trainJourneyActorExitLeadTime);
        float exitDelay = Mathf.Max(0f, jumpDuration - exitDuration);

        if (exitDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(exitDelay);
        }

        var exitSequence = DOTween.Sequence().SetUpdate(true);
        bool hasExitTween = false;
        hasExitTween |= AppendTrainJourneyActorExit(
            exitSequence,
            trainJourneyActor,
            activeTrainJourneyActorStartPosition,
            exitDuration);
        hasExitTween |= AppendTrainJourneyActorExit(
            exitSequence,
            trainJourneySecondActor,
            activeTrainJourneySecondActorStartPosition,
            exitDuration);

        if (hasExitTween)
        {
            AudioManager.Instance?.PlayGoldMineManDonkeyExit();
            yield return exitSequence.WaitForCompletion();
        }
        else if (exitDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(exitDuration);
        }
    }

    private IEnumerator PlayTrainJourneyResultHandoff(
        TMP_Text destinationAmount,
        Vector3 destinationBaseScale,
        double totalPayout)
    {
        if (trainJourneyWinBox == null || trainJourneyWinAmount == null)
        {
            ShowTrainResultAmountImmediately(destinationAmount, totalPayout);
            yield break;
        }

        RectTransform transferRect = CreateTrainJourneyTransferAmount(totalPayout);
        Tween fadeTween = CreateTrainJourneyWinBoxFade();
        if (fadeTween != null)
        {
            yield return fadeTween.WaitForCompletion();
        }

        trainJourneyWinAmount.gameObject.SetActive(false);
        ReleaseTrainJourneyDarkBackground();

        if (destinationAmount == null)
        {
            FinishTrainJourneyAmountTransfer();
            yield break;
        }

        RectTransform destinationRect = destinationAmount.rectTransform;
        yield return CreateTrainJourneyAmountTransfer(
            transferRect,
            destinationRect).WaitForCompletion();

        FinishTrainJourneyAmountTransfer();
        yield return CreateTrainResultPop(
            destinationAmount,
            destinationBaseScale,
            totalPayout).WaitForCompletion();
    }

    private RectTransform CreateTrainJourneyTransferAmount(double totalPayout)
    {
        DestroyTrainJourneyTransferAmount();
        Transform transferParent = trainJourneyWinBox.parent != null
            ? trainJourneyWinBox.parent
            : transform;
        activeTrainJourneyTransferAmount = Instantiate(
            trainJourneyWinAmount,
            transferParent,
            true);
        activeTrainJourneyTransferAmount.name = "TrainJourneyAmountTransfer";
        activeTrainJourneyTransferAmount.raycastTarget = false;
        activeTrainJourneyTransferAmount.gameObject.SetActive(true);
        activeTrainJourneyTransferAmount.transform.SetAsLastSibling();
        SetSpriteAmount(activeTrainJourneyTransferAmount, totalPayout);

        RectTransform transferRect = activeTrainJourneyTransferAmount.rectTransform;
        RectTransform sourceRect = trainJourneyWinAmount.rectTransform;
        transferRect.position = sourceRect.position;
        transferRect.rotation = sourceRect.rotation;
        return transferRect;
    }

    private Tween CreateTrainJourneyWinBoxFade()
    {
        if (trainJourneyWinBoxImage == null) return null;

        RestoreTrainJourneyWinBoxImage();
        return trainJourneyWinBoxImage
            .DOFade(0f, trainJourneyWinBoxFadeDuration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    private Sequence CreateTrainJourneyAmountTransfer(
        RectTransform transferRect,
        RectTransform destinationRect)
    {
        Vector3 nearDestination = Vector3.Lerp(
            transferRect.position,
            destinationRect.position,
            0.9f);
        Vector3 transferStartScale = transferRect.localScale;
        Sequence sequence = DOTween.Sequence()
            .SetTarget(transferRect)
            .SetUpdate(true);
        sequence.Append(
            transferRect
                .DOMove(nearDestination, trainJourneyAmountTransferDuration)
                .SetEase(Ease.InOutCubic));
        sequence.Join(
            transferRect
                .DOScale(
                    transferStartScale * 0.8f,
                    trainJourneyAmountTransferDuration)
                .SetEase(Ease.InOutSine));
        return sequence;
    }

    private Sequence CreateTrainResultPop(
        TMP_Text destinationAmount,
        Vector3 destinationBaseScale,
        double totalPayout)
    {
        RectTransform destinationRect = destinationAmount.rectTransform;
        DOTween.Kill(destinationRect);
        SetSpriteAmount(destinationAmount, totalPayout);
        destinationRect.localScale = Vector3.zero;
        destinationAmount.gameObject.SetActive(true);

        float growDuration = trainJourneyAmountPopDuration * 0.6f;
        float settleDuration = trainJourneyAmountPopDuration - growDuration;
        Sequence sequence = DOTween.Sequence()
            .SetTarget(destinationRect)
            .SetUpdate(true);
        sequence.Append(
            destinationRect
                .DOScale(
                    destinationBaseScale * trainJourneyAmountPopScale,
                    growDuration)
                .SetEase(Ease.OutBack));
        sequence.Append(
            destinationRect
                .DOScale(destinationBaseScale, settleDuration)
                .SetEase(Ease.OutSine));
        return sequence;
    }

    private static void ShowTrainResultAmountImmediately(
        TMP_Text destinationAmount,
        double totalPayout)
    {
        if (destinationAmount == null) return;

        SetSpriteAmount(destinationAmount, totalPayout);
        destinationAmount.gameObject.SetActive(true);
    }

    private void FinishTrainJourneyAmountTransfer()
    {
        trainJourneyWinBox.gameObject.SetActive(false);
        DestroyTrainJourneyTransferAmount();
    }

    private void ReleaseTrainJourneyDarkBackground()
    {
        if (!trainJourneyOwnsDarkBackground || darkBackground == null) return;

        SetDarkBackgroundActive(false);
        trainJourneyOwnsDarkBackground = false;
    }

    private void DestroyTrainJourneyTransferAmount()
    {
        if (activeTrainJourneyTransferAmount == null) return;

        DOTween.Kill(activeTrainJourneyTransferAmount.rectTransform);
        activeTrainJourneyTransferAmount.gameObject.SetActive(false);
        Destroy(activeTrainJourneyTransferAmount.gameObject);
        activeTrainJourneyTransferAmount = null;
    }

    private void RestoreTrainJourneyWinBoxImage()
    {
        if (trainJourneyWinBoxImage == null) return;

        DOTween.Kill(trainJourneyWinBoxImage);
        if (hasCapturedTrainJourneyWinBoxColor)
        {
            trainJourneyWinBoxImage.color = trainJourneyWinBoxBaseColor;
        }
    }

    private static float PlayTrainJourneyActorJump(
        RectTransform actor,
        bool shouldLoop = false)
    {
        if (actor == null || !actor.gameObject.activeInHierarchy) return 0f;

        SkeletonGraphic actorGraphic = actor.GetComponent<SkeletonGraphic>();
        if (actorGraphic == null) return 0f;

        if (actorGraphic.SkeletonData == null)
        {
            actorGraphic.Initialize(false);
        }

        Spine.Animation jumpAnimation =
            actorGraphic.SkeletonData?.FindAnimation("Jump");
        if (jumpAnimation == null) return 0f;

        actorGraphic.freeze = false;
        actorGraphic.AnimationState.SetAnimation(
            0,
            jumpAnimation.Name,
            shouldLoop);
        return jumpAnimation.Duration;
    }

    private bool AppendTrainJourneyActorExit(
        Sequence sequence,
        RectTransform actor,
        Vector2 startPosition,
        float duration)
    {
        if (sequence == null || actor == null || !actor.gameObject.activeInHierarchy)
        {
            return false;
        }

        DOTween.Kill(actor);
        float exitY = startPosition.y -
            trainJourneyActorExitDistance * activeTrainJourneyScaleCompensation;
        sequence.Join(
            actor.DOAnchorPosY(exitY, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true));
        return true;
    }

    private void ResetTrainJourneyPresentation()
    {
        if (trainJourneyRoutine != null)
        {
            StopCoroutine(trainJourneyRoutine);
            trainJourneyRoutine = null;
        }

        if (trainJourneyCountRoutine != null)
        {
            StopCoroutine(trainJourneyCountRoutine);
            trainJourneyCountRoutine = null;
        }

        ResetTrainJourneyVisuals();
    }

    private void ResetTrainJourneyVisuals()
    {
        DestroyTrainJourneyTransferAmount();

        foreach (RectTransform journeyTrain in trainJourneyTrains.Values
                     .Where(train => train != null)
                     .Distinct())
        {
            DOTween.Kill(journeyTrain);
            ImageAnimation trainAnimation =
                journeyTrain.GetComponent<ImageAnimation>();
            if (trainAnimation != null)
            {
                trainAnimation.onLoopComplete = null;
                trainAnimation.StopAnimation();
                trainAnimation.ClearLoopDuration();
                trainAnimation.RevertToInitialState();
                if (trainJourneyOriginalLoops.TryGetValue(
                        journeyTrain,
                        out bool originalLoop))
                {
                    trainAnimation.doLoopAnimation = originalLoop;
                }
            }

            if (trainJourneyStartPositions.TryGetValue(
                    journeyTrain,
                    out Vector2 startPosition))
            {
                journeyTrain.anchoredPosition = startPosition;
            }
            if (trainJourneyStartScales.TryGetValue(
                    journeyTrain,
                    out Vector3 startScale))
            {
                journeyTrain.localScale = startScale;
            }
            journeyTrain.gameObject.SetActive(false);
        }

        activeTrainJourneyTrain = null;
        activeTrainJourneyWagonAmounts = null;

        ResetTrainJourneyActor(
            trainJourneyActor,
            trainJourneyActorStartPosition,
            trainJourneyActorStartSize,
            trainJourneyActorStartScale);
        ResetTrainJourneyActor(
            trainJourneySecondActor,
            trainJourneySecondActorStartPosition,
            trainJourneySecondActorStartSize,
            trainJourneySecondActorStartScale);
        areBigWinActorsActive = false;

        if (trainJourneyWinBox != null)
        {
            if (hasCapturedTrainJourneyWinBoxTransform)
            {
                trainJourneyWinBox.anchoredPosition =
                    trainJourneyWinBoxStartPosition;
                trainJourneyWinBox.localScale = trainJourneyWinBoxStartScale;
            }
            RestoreTrainJourneyWinBoxImage();
            StopAndHideTrainJourneyEffect(trainJourneyLineAnimation);
            StopAndHideTrainJourneyEffect(trainJourneyBoxAnimation);
            trainJourneyWinBox.gameObject.SetActive(false);
        }

        ReleaseTrainJourneyDarkBackground();
    }

    private void ResetTrainJourneyActor(
        RectTransform actor,
        Vector2 startPosition,
        Vector2 startSize,
        Vector3 startScale)
    {
        if (actor == null) return;

        DOTween.Kill(actor);
        if (hasCapturedTrainJourneyState)
        {
            actor.anchoredPosition = startPosition;
            actor.sizeDelta = startSize;
            actor.localScale = startScale;
        }

        SkeletonGraphic actorGraphic = actor.GetComponent<SkeletonGraphic>();
        if (actorGraphic != null)
        {
            if (actorGraphic.SkeletonData == null)
            {
                actorGraphic.Initialize(false);
            }
            actorGraphic.AnimationState?.ClearTracks();
            actorGraphic.Skeleton?.SetToSetupPose();
            actorGraphic.freeze = true;
        }
        actor.gameObject.SetActive(false);
    }

    private void ApplyTrainJourneyLayout()
    {
        bool usePortraitLayout = trainJourneyOrientation != null &&
            trainJourneyOrientation.CurrentMode ==
                OrientationChange.OrientationMode.MobilePortrait;
        activeTrainJourneyScaleCompensation = usePortraitLayout
            ? GetSharedPortraitOverlayCompensation()
            : 1f;

        ApplyTrainJourneyTrackLayout();

        ApplyTrainJourneyActorLayout(usePortraitLayout);

        if (trainJourneyWinBox != null &&
            hasCapturedTrainJourneyWinBoxTransform)
        {
            trainJourneyWinBox.anchoredPosition =
                GetSharedPortraitOverlayPosition(
                    trainJourneyWinBoxStartPosition);
            trainJourneyWinBox.localScale = ScalePortraitOverlay(
                trainJourneyWinBoxStartScale,
                activeTrainJourneyScaleCompensation);
        }
    }

    private void ApplyTrainJourneyActorLayout(bool usePortraitLayout)
    {
        if (!hasCapturedTrainJourneyState) return;

        // Position and scale are both converted independently into the active
        // SlotBG coordinates so their final screen-space values match 5x3.
        Vector2 actorPosition = usePortraitLayout
            ? portraitTrainJourneyActorPosition
            : trainJourneyActorStartPosition;
        Vector2 secondActorPosition = usePortraitLayout
            ? portraitTrainJourneySecondActorPosition
            : trainJourneySecondActorStartPosition;
        activeTrainJourneyActorStartPosition =
            GetSharedPortraitOverlayPosition(actorPosition);
        activeTrainJourneySecondActorStartPosition =
            GetSharedPortraitOverlayPosition(secondActorPosition);

        if (trainJourneyActor != null)
        {
            trainJourneyActor.anchoredPosition =
                activeTrainJourneyActorStartPosition;
            trainJourneyActor.sizeDelta = usePortraitLayout
                ? portraitTrainJourneyActorSize
                : trainJourneyActorStartSize;
            trainJourneyActor.localScale = usePortraitLayout
                ? ScalePortraitOverlay(
                    portraitTrainJourneyActorScale,
                    activeTrainJourneyScaleCompensation)
                : trainJourneyActorStartScale;
        }

        if (trainJourneySecondActor != null)
        {
            trainJourneySecondActor.anchoredPosition =
                activeTrainJourneySecondActorStartPosition;
            trainJourneySecondActor.sizeDelta = usePortraitLayout
                ? portraitTrainJourneySecondActorSize
                : trainJourneySecondActorStartSize;
            trainJourneySecondActor.localScale = usePortraitLayout
                ? ScalePortraitOverlay(
                    portraitTrainJourneySecondActorScale,
                    activeTrainJourneyScaleCompensation)
                : trainJourneySecondActorStartScale;
        }
    }

    private void ApplyTrainJourneyTrackLayout()
    {
        if (trainJourneyTrackRect == null ||
            !hasCapturedTrainJourneyTrackPosition)
        {
            return;
        }

        trainJourneyTrackRect.anchoredPosition =
            GetSharedPortraitOverlayPosition(trainJourneyTrackStartPosition);
        trainJourneyTrackRect.localScale = ScalePortraitOverlay(
            trainJourneyTrackStartScale,
            activeTrainJourneyScaleCompensation);
    }

}
