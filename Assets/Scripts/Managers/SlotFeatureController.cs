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
/// Owns feature visuals that sit on top of the reel presentation.
/// Handles reusable 2x1 and 3x1 barrels and pooled train visuals.
/// </summary>
public class SlotFeatureController : MonoBehaviour
{
    private const int ReelCount = 5;
    private const int RowCount = 3;
    private const int FirstGoldBurstSymbolId = 11;
    private const int LastGoldBurstSymbolId = 13;
    private const int MaxGreenTrains = 2;
    private const int MaxRedTrains = 1;
    private const int MaxHorizontalPurpleTrains = 1;
    private const int MaxVerticalPurpleTrains = 2;
    private const int MaxGoldenTrains = 1;
    private const int TwoSlotFeatureType = -1;
    private const int ThreeSlotFeatureType = -2;
    private const float ConversionHoldDuration = 0.15f;
    private const float ConversionFadeDuration = 0.3f;
    private const float TrainConversionForwardDistance = 50f;
    private const float TrainConversionMoveDuration = 0.35f;
    private const float TrainActivationLeftOffset = 150f;
    private const string GoldBurstResultIntroAnimation = "Symbol_4";
    private const string GoldBurstResultLoopAnimation = "Symbol_4_Loop";
    private const string GoldBurstResultDissolveAnimation = "dissolved";
    private const float GoldBurstResultCountDuration = 2f;
    private const string FreeGamesStartIntroAnimation = "Symbol_3";
    private const string FreeGamesStartLoopAnimation = "Symbol_3_Loop";
    private const string FreeGamesStartDissolveAnimation = "dissolved";
    private const string TrainTrackAnimationName = "animation";

    [System.Serializable]
    private sealed class AnimationCellReferences
    {
        public RectTransform slot = null;
        public RectTransform winbox = null;
    }

    [System.Serializable]
    private sealed class AnimationColumnReferences
    {
        public RectTransform column = null;
        public AnimationCellReferences[] rows = new AnimationCellReferences[RowCount];
    }

    [System.Serializable]
    private sealed class TrainVisualReferences
    {
        public RectTransform visual = null;
        public GameObject mask = null;
        public RectTransform pressPlay = null;
        [NonSerialized] internal TMP_Text resultAmount;
        [NonSerialized] internal Vector3 resultAmountBaseScale;
        [NonSerialized] internal bool hasResultAmountBaseScale;
    }

    private sealed class GoldBoxRuntime
    {
        internal RectTransform visual;
        internal TMP_Text amountText;
        internal Vector3 baseScale;
    }

    [Header("Feature References")]
    [SerializeField] private RectTransform animationRoot;
    [SerializeField] private RectTransform twoSlotBarrelRoot;
    [SerializeField] private RectTransform threeSlotBarrelRoot;
    [SerializeField] private RectTransform greenTrainRoot;
    [SerializeField] private RectTransform redTrainRoot;
    [SerializeField] private RectTransform horizontalPurpleTrainRoot;
    [SerializeField] private RectTransform verticalPurpleTrainRoot;
    [SerializeField] private RectTransform goldenTrainRoot;

    [Header("Gold Burst Presentation")]
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private GameObject coldBurstRespin;
    [SerializeField] private GameObject megaGoldBurstRespin;
    [SerializeField] private GameObject ultimateGoldBurstRespin;
    [SerializeField] private GameObject track;
    [SerializeField] private GameObject trolleyMan;
    [SerializeField] private RectTransform oneSlotGoldBoxRoot;
    [SerializeField] private RectTransform twoSlotGoldBoxRoot;
    [SerializeField] private RectTransform threeSlotGoldBoxRoot;
    [SerializeField] private PrizeTrailController goldBurstCollectionParticles;
    [SerializeField, Min(1f)] private float goldBurstIntroFramesPerSecond = 29f;
    [SerializeField, Min(0f)] private float goldBurstMergeDelayAfterTrolley = 0.5f;
    [SerializeField, Min(0f)] private float goldBoxHoldWithoutTrain = 1.2f;

    [Header("Gold Burst Result Presentation")]
    [SerializeField] private GameObject goldBurstResultPanel;
    [SerializeField] private RectTransform goldBurstResultWinBox;
    [SerializeField] private TMP_Text goldBurstResultWinAmount;
    [SerializeField] private Button goldBurstResultCollectButton;
    [SerializeField, Min(0.01f)] private float goldBurstResultPopupDuration = 0.35f;
    [SerializeField, Min(1f)] private float goldBurstResultHeartbeatScale = 1.08f;

    [Header("Free Games Presentation")]
    [SerializeField] private GameObject freeGamesStartPanel;
    [SerializeField] private Button freeGamesStartButton;
    [SerializeField] private GameObject trainTrackAnimation;
    [SerializeField, Min(0.01f)] private float freeGamesStartButtonPopupDuration = 0.35f;

    [Header("5x3 Train Visuals")]
    [SerializeField] private TrainVisualReferences[] greenTrainVisuals =
        new TrainVisualReferences[MaxGreenTrains];
    [SerializeField] private TrainVisualReferences[] redTrainVisuals =
        new TrainVisualReferences[MaxRedTrains];
    [SerializeField] private TrainVisualReferences[] horizontalPurpleTrainVisuals =
        new TrainVisualReferences[MaxHorizontalPurpleTrains];
    [SerializeField] private TrainVisualReferences[] verticalPurpleTrainVisuals =
        new TrainVisualReferences[MaxVerticalPurpleTrains];
    [SerializeField] private TrainVisualReferences[] goldenTrainVisuals =
        new TrainVisualReferences[MaxGoldenTrains];

    [Header("Gold Burst Train Journey Presentation")]
    [SerializeField] private RectTransform trainJourneyGreenTrain;
    [SerializeField] private RectTransform trainJourneyPurpleTrain;
    [SerializeField] private RectTransform trainJourneyRedTrain;
    [SerializeField] private RectTransform trainJourneyGoldenTrain;
    [SerializeField] private RectTransform trainJourneyActor;
    [SerializeField] private RectTransform trainJourneySecondActor;
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

    [Header("Press Play Animation")]
    [SerializeField, Min(1f)] private float pressPlayPopupScale = 1.12f;
    [SerializeField, Min(0.01f)] private float pressPlayPopupDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float pressPlaySettleDuration = 0.12f;
    [SerializeField, Range(0.8f, 1f)] private float pressPlayHeartbeatSmallScale = 0.98f;
    [SerializeField, Min(1f)] private float pressPlayHeartbeatLargeScale = 1.04f;
    [SerializeField, Min(0.05f)] private float pressPlayHeartbeatHalfCycle = 0.45f;

    [Header("Animation Grid")]
    [SerializeField] private AnimationColumnReferences[] animationGrid =
        new AnimationColumnReferences[ReelCount];

    [Header("2x1 Barrel Positions")]
    [SerializeField] private float topAndMiddleY = 110f;
    [SerializeField] private float middleAndBottomY = -110f;

    [Header("Reusable Train Positions")]
    [Tooltip("Anchored X positions for a 2x2 Green Train, indexed by its starting column.")]
    [SerializeField] private float[] greenTrainXByStartColumn = { -390f, -119f, 153f, 428f };
    [SerializeField] private float greenTrainTopY = 109f;
    [SerializeField] private float greenTrainBottomY = -112f;

    [Tooltip("Anchored X positions for a 2x4 Red Train, indexed by its starting column.")]
    [SerializeField] private float[] redTrainXByStartColumn = { -116f, 154f };
    [SerializeField] private float redTrainTopY = 105f;
    [SerializeField] private float redTrainBottomY = -114f;

    [Tooltip("Anchored X positions for a 2x3 Horizontal Purple Train, indexed by its starting column.")]
    [SerializeField] private float[] horizontalPurpleTrainXByStartColumn = { -256f, 18f, 288f };
    [SerializeField] private float horizontalPurpleTrainTopY = 106f;
    [SerializeField] private float horizontalPurpleTrainBottomY = -115f;

    [Tooltip("Anchored X positions for a 3x2 Vertical Purple Train, indexed by its starting column.")]
    [SerializeField] private float[] verticalPurpleTrainXByStartColumn = { -395f, -122f, 147f, 421f };
    [SerializeField] private float verticalPurpleTrainY;

    [Tooltip("Anchored X positions for a 3x4 Golden Train, indexed by its starting column.")]
    [SerializeField] private float[] goldenTrainXByStartColumn = { -133f, 142f };
    [SerializeField] private float goldenTrainY;

    private readonly List<RectTransform> twoSlotBarrels = new List<RectTransform>();
    private readonly List<RectTransform> threeSlotBarrels = new List<RectTransform>();
    private readonly Dictionary<GameObject, RectTransform> winboxByAnimationCell =
        new Dictionary<GameObject, RectTransform>();
    private readonly Dictionary<int, int> pendingStartRowByReel = new Dictionary<int, int>();
    private readonly HashSet<int> pendingThreeSlotReels = new HashSet<int>();
    private readonly List<TrainPlacement> pendingTrains = new List<TrainPlacement>();
    private readonly Dictionary<
        (int type, int startRow, int startCol, int rowCount, int columnCount),
        RectTransform> visibleFeatures =
            new Dictionary<
                (int type, int startRow, int startCol, int rowCount, int columnCount),
                RectTransform>();
    private readonly HashSet<
        (int type, int startRow, int startCol, int rowCount, int columnCount)>
        immediateTrainRevealKeys =
            new HashSet<
                (int type, int startRow, int startCol, int rowCount, int columnCount)>();
    private readonly Dictionary<TrainVisualType, RectTransform> trainRoots =
        new Dictionary<TrainVisualType, RectTransform>();
    private readonly Dictionary<TrainVisualType, List<TrainVisualReferences>> trainVisuals =
        new Dictionary<TrainVisualType, List<TrainVisualReferences>>();
    private readonly Dictionary<RectTransform, TrainVisualReferences> trainReferencesByVisual =
        new Dictionary<RectTransform, TrainVisualReferences>();
    private readonly Dictionary<RectTransform, TrainPlacement> trainPlacementsByVisual =
        new Dictionary<RectTransform, TrainPlacement>();
    private readonly Dictionary<RectTransform, Vector3> pressPlayBaseScales =
        new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Vector2> trainVisualRestingPositions =
        new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector2> trainAnimationRestingPositions =
        new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<Button, UnityAction> pressPlayListeners =
        new Dictionary<Button, UnityAction>();
    private readonly HashSet<TrainPlacement> completedTrainJourneys =
        new HashSet<TrainPlacement>();
    private readonly HashSet<GameObject> hiddenAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<GameObject> activeWinAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<GameObject> activeTrainAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<GameObject> activeGoldBurstAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<CanvasGroup> conversionCanvasGroups = new HashSet<CanvasGroup>();
    private readonly List<GoldBoxRuntime> allGoldBoxes = new List<GoldBoxRuntime>();
    private readonly List<GoldBoxRuntime>[] oneSlotGoldBoxesByReel =
        new List<GoldBoxRuntime>[ReelCount];
    private readonly List<GoldBoxRuntime> twoSlotGoldBoxes = new List<GoldBoxRuntime>();
    private readonly List<GoldBoxRuntime> threeSlotGoldBoxes = new List<GoldBoxRuntime>();
    private readonly HashSet<CanvasGroup> hiddenGoldBurstSources = new HashSet<CanvasGroup>();
    private readonly HashSet<RectTransform> hiddenGoldBurstFeatures = new HashSet<RectTransform>();
    private readonly Dictionary<RectTransform, int> animationColumnSiblingIndices =
        new Dictionary<RectTransform, int>();
    private readonly Dictionary<TrainVisualType, RectTransform> trainJourneyTrains =
        new Dictionary<TrainVisualType, RectTransform>();
    private readonly Dictionary<TrainVisualType, TMP_Text[]> trainJourneyAmountsByType =
        new Dictionary<TrainVisualType, TMP_Text[]>();
    private readonly Dictionary<RectTransform, Vector2> trainJourneyStartPositions =
        new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, bool> trainJourneyOriginalLoops =
        new Dictionary<RectTransform, bool>();

    private Transform searchRoot;
    private IReadOnlyList<ReelResultSlots> conversionSourceSlots;
    private bool isInitialized;
    private bool configurationWarningShown;
    private bool isGoldBurstPresentationActive;
    private bool deferGoldBurstTriggerBarrelMerge;
    private bool isTrainJourneyPlaying;
    private Action goldBurstPressPlayCallback;
    private Coroutine trainJourneyRoutine;
    private Coroutine trainJourneyCountRoutine;
    private Coroutine featureRevealRoutine;
    private RectTransform activeTrainJourneyTrain;
    private TMP_Text[] activeTrainJourneyWagonAmounts;
    private ImageAnimation trainJourneyLineAnimation;
    private ImageAnimation trainJourneyBoxAnimation;
    private Image trainJourneyWinBoxImage;
    private Color trainJourneyWinBoxBaseColor;
    private bool hasCapturedTrainJourneyWinBoxColor;
    private TMP_Text activeTrainJourneyTransferAmount;
    private Vector2 trainJourneyActorStartPosition;
    private Vector2 trainJourneySecondActorStartPosition;
    private bool hasCapturedTrainJourneyState;
    private bool trainJourneyOwnsDarkBackground;
    private RectTransform trolleyManRect;
    private Vector2 trolleyManStartPosition;
    private Quaternion trolleyManStartRotation;
    private Vector3 trolleyManStartScale;
    private bool hasCapturedTrolleyManState;
    private SkeletonGraphic goldBurstResultSkeleton;
    private Vector3 goldBurstResultWinBoxBaseScale;
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

    internal void Initialize(Transform slotRoot, IReadOnlyList<ReelResultSlots> resultSlotsByReel)
    {
        searchRoot = slotRoot != null ? slotRoot : searchRoot;
        conversionSourceSlots = resultSlotsByReel;
        CacheSceneObjects();
        ResetFeatures();
    }

    internal void PrepareTwoSlotBarrels(IReadOnlyList<TwoSlotBarrelPlacement> placements)
    {
        EnsureInitialized();
        pendingStartRowByReel.Clear();

        if (placements == null) return;

        foreach (TwoSlotBarrelPlacement placement in placements)
        {
            if (placement == null ||
                placement.reelIndex < 0 || placement.reelIndex >= ReelCount ||
                placement.startRow < 0 || placement.startRow >= RowCount - 1)
            {
                continue;
            }

            pendingStartRowByReel[placement.reelIndex] = placement.startRow;
        }
    }

    internal void PrepareThreeSlotBarrels(IReadOnlyList<ThreeSlotBarrelPlacement> placements)
    {
        EnsureInitialized();
        pendingThreeSlotReels.Clear();

        if (placements == null) return;

        foreach (ThreeSlotBarrelPlacement placement in placements)
        {
            if (placement == null || placement.reelIndex < 0 || placement.reelIndex >= ReelCount)
            {
                continue;
            }

            pendingThreeSlotReels.Add(placement.reelIndex);
        }
    }

    internal void PrepareTrains(IReadOnlyList<TrainPlacement> placements)
    {
        EnsureInitialized();
        pendingTrains.Clear();

        if (placements == null) return;

        foreach (TrainPlacement placement in placements)
        {
            if (placement == null ||
                placement.startRow < 0 || placement.startCol < 0 ||
                placement.startRow + placement.rowCount > RowCount ||
                placement.startCol + placement.columnCount > ReelCount ||
                pendingTrains.Any(existing =>
                    existing.type == placement.type &&
                    existing.startRow == placement.startRow &&
                    existing.startCol == placement.startCol &&
                    existing.rowCount == placement.rowCount &&
                    existing.columnCount == placement.columnCount))
            {
                continue;
            }

            pendingTrains.Add(placement);
        }

        // A train can remain visible across Gold Burst respins while the server
        // refreshes its payout journey on the final response. Keep the reusable
        // visual bound to the newest placement data even when it is not revealed again.
        foreach (TrainPlacement placement in pendingTrains)
        {
            var key = ((int)placement.type, placement.startRow, placement.startCol,
                placement.rowCount, placement.columnCount);
            if (visibleFeatures.TryGetValue(key, out RectTransform visibleTrain) &&
                visibleTrain != null)
            {
                trainPlacementsByVisual[visibleTrain] = placement;
            }
        }
    }

    internal void ConfigureGoldBurstTriggerBarrelMerge(bool shouldDefer)
    {
        EnsureInitialized();
        deferGoldBurstTriggerBarrelMerge = shouldDefer;
    }

    internal void RevealFeaturesForReel(int reelIndex)
    {
        EnsureInitialized();

        if (!deferGoldBurstTriggerBarrelMerge)
        {
            RevealBarrelForReel(reelIndex);
        }

        foreach (TrainPlacement train in pendingTrains)
        {
            int lastCoveredReel = train.startCol + train.columnCount - 1;
            var key = ((int)train.type, train.startRow, train.startCol, train.rowCount, train.columnCount);
            if (lastCoveredReel == reelIndex && !visibleFeatures.ContainsKey(key))
            {
                RevealTrain(train);
            }
        }
    }

    private void RevealBarrelForReel(int reelIndex)
    {
        if (pendingThreeSlotReels.Contains(reelIndex))
        {
            if (!visibleFeatures.ContainsKey((ThreeSlotFeatureType, 0, reelIndex, RowCount, 1)))
            {
                RevealThreeSlotBarrel(reelIndex);
            }
        }
        else if (pendingStartRowByReel.TryGetValue(reelIndex, out int startRow))
        {
            if (!visibleFeatures.ContainsKey((TwoSlotFeatureType, startRow, reelIndex, 2, 1)))
            {
                RevealTwoSlotBarrel(reelIndex, startRow);
            }
        }
    }

    private void RevealTwoSlotBarrel(int reelIndex, int startRow)
    {
        if (!HasTwoSlotConfiguration()) return;

        RectTransform barrel = twoSlotBarrels[reelIndex];
        Vector2 barrelPosition = barrel.anchoredPosition;
        barrelPosition.y = startRow == 0 ? topAndMiddleY : middleAndBottomY;
        barrel.anchoredPosition = barrelPosition;

        PlayConversionFade(reelIndex, 1, startRow, 2, barrel);
        visibleFeatures[(TwoSlotFeatureType, startRow, reelIndex, 2, 1)] = barrel;
    }

    private void RevealThreeSlotBarrel(int reelIndex)
    {
        if (!HasThreeSlotConfiguration()) return;

        PlayConversionFade(
            reelIndex,
            1,
            0,
            RowCount,
            threeSlotBarrels[reelIndex]);
        visibleFeatures[(ThreeSlotFeatureType, 0, reelIndex, RowCount, 1)] =
            threeSlotBarrels[reelIndex];
    }

    private void RevealTrain(TrainPlacement train)
    {
        RectTransform trainRoot = GetTrainRoot(train.type);
        TrainVisualReferences trainReferences = GetAvailableTrainVisual(train.type);
        RectTransform trainVisual = trainReferences?.visual;
        if (trainVisual == null || trainRoot == null ||
            !TryGetTrainPosition(train, out Vector2 position) ||
            !HasCompleteAnimationGrid())
        {
            ReportConfigurationWarning(
                $"No reusable {train.type} train visual or position is available for " +
                $"row {train.startRow}, column {train.startCol}.");
            return;
        }

        PrepareTrainVisualForReveal(trainReferences);
        trainVisual.anchoredPosition = position;
        trainVisualRestingPositions[trainVisual] = position;
        var trainKey = ((int)train.type, train.startRow, train.startCol,
            train.rowCount, train.columnCount);
        PlayConversionFade(
            train.startCol,
            train.columnCount,
            train.startRow,
            train.rowCount,
            trainVisual,
            true,
            immediateTrainRevealKeys.Contains(trainKey));
        visibleFeatures[trainKey] = trainVisual;
        trainPlacementsByVisual[trainVisual] = train;
    }

    internal void RevealAllFeatures()
    {
        StopFeatureRevealRoutine();
        DOTween.Complete(this);
        immediateTrainRevealKeys.Clear();
        bool hasTrainConversion = RemoveFeaturesMissingFromResult();

        RevealPreparedFeatures(!hasTrainConversion);
        if (hasTrainConversion)
        {
            featureRevealRoutine = StartCoroutine(RevealAfterTrainConversion());
        }
    }

    private IEnumerator RevealAfterTrainConversion()
    {
        yield return new WaitForSecondsRealtime(TrainConversionMoveDuration);
        yield return null;
        featureRevealRoutine = null;
        RevealPreparedFeatures();
        immediateTrainRevealKeys.Clear();
    }

    private void RevealPreparedFeatures(bool showPressPlay = true)
    {

        for (int reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            RevealFeaturesForReel(reelIndex);
        }

        if (showPressPlay && !isGoldBurstPresentationActive)
        {
            ShowPressPlayButtons();
        }
    }

    private void StopFeatureRevealRoutine()
    {
        if (featureRevealRoutine == null) return;

        StopCoroutine(featureRevealRoutine);
        featureRevealRoutine = null;
        immediateTrainRevealKeys.Clear();
    }

    internal void BeginSpinPresentation()
    {
        EnsureInitialized();
        ResetGoldBurstPresentation();
        ClearVisibleFeatures();
    }

    internal void ResetFeatures()
    {
        pendingStartRowByReel.Clear();
        pendingThreeSlotReels.Clear();
        pendingTrains.Clear();
        ClearVisibleFeatures();
        ResetGoldBurstPresentation();
        ResetFreeGamesStartPresentation();
        ResetSpineAnimation(trainTrackSkeleton);
        if (trainTrackAnimation != null)
        {
            trainTrackAnimation.SetActive(false);
        }
    }

    internal void BeginGoldBurstTriggerPresentation()
    {
        EnsureInitialized();
        DOTween.Complete(this);
        ResetGoldBurstPresentation();
        ClearAllVisibleTrains();
        deferGoldBurstTriggerBarrelMerge = true;
        isGoldBurstPresentationActive = true;
    }

    internal IEnumerator PlayGoldBurstTriggerPresentation(GoldBurstTier tier)
    {
        EnsureInitialized();
        if (!isGoldBurstPresentationActive)
        {
            BeginGoldBurstTriggerPresentation();
        }

        if (darkBackground != null) darkBackground.SetActive(true);

        GameObject intro = tier switch
        {
            GoldBurstTier.Mega => megaGoldBurstRespin,
            GoldBurstTier.Ultimate => ultimateGoldBurstRespin,
            _ => coldBurstRespin
        };
        if (intro != null)
        {
            yield return PlayOneShotImageAnimation(intro);
        }

        yield return PlayTrolleyManAnimation();
        if (darkBackground != null) darkBackground.SetActive(false);

        if (goldBurstMergeDelayAfterTrolley > 0f)
        {
            yield return new WaitForSecondsRealtime(goldBurstMergeDelayAfterTrolley);
        }

        deferGoldBurstTriggerBarrelMerge = false;
        bool hasPreparedBarrelMerge = pendingThreeSlotReels.Count > 0 ||
                                      pendingStartRowByReel.Count > 0;
        for (int reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            RevealBarrelForReel(reelIndex);
        }

        if (hasPreparedBarrelMerge)
        {
            yield return new WaitForSecondsRealtime(
                ConversionHoldDuration + ConversionFadeDuration);
        }
    }

    internal IEnumerator PlayGoldBurstOutroPresentation()
    {
        EnsureInitialized();
        if (darkBackground != null) darkBackground.SetActive(true);
        yield return PlayTrolleyManAnimation();
        if (darkBackground != null) darkBackground.SetActive(false);
    }

    internal IEnumerator PlayFreeGamesStartPresentation(UIManager uiManager)
    {
        EnsureInitialized();
        ResetFreeGamesStartPresentation();

        if (darkBackground != null) darkBackground.SetActive(true);
        if (freeGamesStartPanel == null || freeGamesStartSkeleton == null)
        {
            yield return PlayFreeGamesTrackAnimation();
            if (darkBackground != null) darkBackground.SetActive(false);
            yield break;
        }

        RaiseFreeGamesStartHierarchy();
        freeGamesStartPanel.SetActive(true);

        bool startRequested = false;
        Action requestStart = () =>
        {
            if (startRequested) return;
            startRequested = true;
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
            freeGamesStartButtonTween = freeGamesStartButton.transform
                .DOScale(
                    freeGamesStartButtonBaseScale,
                    freeGamesStartButtonPopupDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        bool hasBottomStartButton = uiManager != null &&
                                    uiManager.ShowFreeGamesStartButton(requestStart);
        float introDuration = PlaySpineAnimation(
            freeGamesStartSkeleton,
            FreeGamesStartIntroAnimation,
            false);
        if (introDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(introDuration);
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

        freeGamesStartPanel.SetActive(false);
        RestoreFreeGamesStartHierarchy();
        yield return PlayFreeGamesTrackAnimation();
        if (darkBackground != null) darkBackground.SetActive(false);
    }

    internal IEnumerator PlayFreeGamesEndPresentation(
        double totalWin,
        UIManager uiManager)
    {
        yield return PlayGoldBurstResultPresentation(totalWin, uiManager);
        yield return PlayFreeGamesTrackAnimation();
        RestoreGoldBurstResultHierarchy();
        if (darkBackground != null) darkBackground.SetActive(false);
    }

    private IEnumerator PlayTrolleyManAnimation()
    {
        ResetTrolleyMan();
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
                    trolleyGraphic.AnimationState.SetAnimation(0, trolleyAnimation.Name, false);
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

    internal List<GoldBurstPrizePlacement> GetGoldBurstTriggerSingleSlotBarrels(
        IReadOnlyList<List<int>> matrix)
    {
        EnsureInitialized();
        var placements = new List<GoldBurstPrizePlacement>();
        var coveredCells = new bool[ReelCount, RowCount];

        foreach (int reelIndex in pendingThreeSlotReels.OrderBy(index => index))
        {
            if (reelIndex < 0 || reelIndex >= ReelCount) continue;

            for (int row = 0; row < RowCount; row++)
            {
                placements.Add(new GoldBurstPrizePlacement
                {
                    startCol = reelIndex,
                    startRow = row,
                    rowCount = 1,
                    columnCount = 1
                });
                coveredCells[reelIndex, row] = true;
            }
        }

        foreach (KeyValuePair<int, int> barrel in pendingStartRowByReel
                     .OrderBy(entry => entry.Key))
        {
            int reelIndex = barrel.Key;
            int startRow = barrel.Value;
            if (reelIndex < 0 || reelIndex >= ReelCount ||
                startRow < 0 || startRow + 1 >= RowCount ||
                coveredCells[reelIndex, startRow] ||
                coveredCells[reelIndex, startRow + 1])
            {
                continue;
            }

            for (int row = startRow; row < startRow + 2; row++)
            {
                placements.Add(new GoldBurstPrizePlacement
                {
                    startCol = reelIndex,
                    startRow = row,
                    rowCount = 1,
                    columnCount = 1
                });
                coveredCells[reelIndex, row] = true;
            }
        }

        if (matrix == null) return placements;

        for (int reelIndex = 0;
             reelIndex < Mathf.Min(ReelCount, matrix.Count);
             reelIndex++)
        {
            List<int> column = matrix[reelIndex];
            if (column == null) continue;

            for (int row = 0; row < Mathf.Min(RowCount, column.Count); row++)
            {
                int symbolId = column[row];
                if (coveredCells[reelIndex, row] ||
                    symbolId < FirstGoldBurstSymbolId ||
                    symbolId > LastGoldBurstSymbolId)
                {
                    continue;
                }

                placements.Add(new GoldBurstPrizePlacement
                {
                    startCol = reelIndex,
                    startRow = row,
                    rowCount = 1,
                    columnCount = 1
                });
            }
        }

        return placements;
    }

    internal void BeginGoldBurstPrizeReveal()
    {
        EnsureInitialized();
        goldBurstCollectionParticles?.ResetParticles();
        HideAllTrainPressPlayButtons();
        HideAllGoldBoxes();
    }

    internal void RevealGoldBurstPrize(GoldBurstPrizePlacement prize)
    {
        EnsureInitialized();
        if (prize == null || prize.amount < 0d ||
            !TryGetGoldBox(prize, out GoldBoxRuntime goldBox))
        {
            return;
        }

        HideGoldBurstSource(prize, false);
        SetGoldBoxRootActive(prize.rowCount, true);
        if (goldBox.visual.parent != null)
        {
            goldBox.visual.parent.gameObject.SetActive(true);
        }

        goldBox.amountText.text = prize.amount.ToString("0.00", CultureInfo.InvariantCulture);
        DOTween.Kill(goldBox.visual);
        goldBox.visual.localScale = goldBox.baseScale;
        goldBox.visual.gameObject.SetActive(true);
        RefreshAnimationHierarchy();
    }

    internal void CompleteGoldBurstPrizeReveal(GoldBurstPrizePlacement prize)
    {
        if (prize == null) return;
        HideGoldBurstSource(prize);
        RefreshAnimationHierarchy();
    }

    internal bool ShowGoldBurstPressPlay(Action onPressed)
    {
        completedTrainJourneys.Clear();
        isTrainJourneyPlaying = false;
        goldBurstPressPlayCallback = onPressed;
        HideAllTrainPressPlayButtons();

        bool showedButton = ShowRemainingGoldBurstPressPlayButtons();
        if (!showedButton)
        {
            goldBurstPressPlayCallback = null;
        }

        return showedButton;
    }

    private bool ShowRemainingGoldBurstPressPlayButtons()
    {
        bool showedButton = false;

        foreach (RectTransform visual in visibleFeatures.Values.Distinct())
        {
            if (visual == null || !visual.gameObject.activeSelf ||
                !trainReferencesByVisual.TryGetValue(
                    visual,
                    out TrainVisualReferences trainReferences) ||
                trainReferences?.pressPlay == null)
            {
                continue;
            }

            Button button = trainReferences.pressPlay.GetComponent<Button>();
            if (button == null ||
                !trainPlacementsByVisual.TryGetValue(
                    trainReferences.visual,
                    out TrainPlacement selectedTrain))
            {
                continue;
            }
            if (completedTrainJourneys.Contains(selectedTrain)) continue;

            RemovePressPlayListener(button);
            UnityAction listener = () => OnGoldBurstPressPlayClicked(
                selectedTrain,
                trainReferences);
            pressPlayListeners[button] = listener;
            button.onClick.AddListener(listener);
            button.interactable = true;
            PlayPressPlayAnimation(trainReferences);
            showedButton = true;
        }

        return showedButton;
    }

    internal float GoldBurstHoldWithoutTrain => goldBoxHoldWithoutTrain;

    internal IEnumerator PlayGoldBurstCollectionParticles(
        IReadOnlyList<GoldBurstPrizePlacement> prizes,
        Action<double> onCollectedAmountChanged)
    {
        EnsureInitialized();

        HashSet<RectTransform> uniqueSources = new HashSet<RectTransform>();
        Dictionary<RectTransform, double> amountsBySource =
            new Dictionary<RectTransform, double>();
        if (prizes != null)
        {
            foreach (GoldBurstPrizePlacement prize in prizes)
            {
                if (prize == null ||
                    !TryGetGoldBox(prize, out GoldBoxRuntime goldBox) ||
                    goldBox?.visual == null)
                {
                    continue;
                }

                amountsBySource[goldBox.visual] = Math.Max(0d, prize.amount);
            }
        }

        foreach (GoldBoxRuntime goldBox in allGoldBoxes)
        {
            if (goldBox?.visual != null && goldBox.visual.gameObject.activeInHierarchy)
            {
                uniqueSources.Add(goldBox.visual);
            }
        }

        foreach (RectTransform feature in visibleFeatures.Values)
        {
            if (feature != null &&
                feature.gameObject.activeInHierarchy &&
                trainReferencesByVisual.ContainsKey(feature) &&
                trainPlacementsByVisual.TryGetValue(
                    feature,
                    out TrainPlacement train))
            {
                uniqueSources.Add(feature);
                amountsBySource[feature] = GetTrainPayout(train);
            }
        }

        List<RectTransform> orderedSources = uniqueSources
            .OrderBy(source => source.position.x)
            .ThenByDescending(source => source.position.y)
            .ToList();
        double collectedAmount = 0d;
        onCollectedAmountChanged?.Invoke(collectedAmount);
        if (goldBurstCollectionParticles == null) yield break;

        yield return goldBurstCollectionParticles.PlaySequentially(
            orderedSources,
            arrivedSourceIndex =>
            {
                if (arrivedSourceIndex < 0 ||
                    arrivedSourceIndex >= orderedSources.Count)
                {
                    return;
                }

                RectTransform arrivedSource = orderedSources[arrivedSourceIndex];
                if (arrivedSource != null &&
                    amountsBySource.TryGetValue(arrivedSource, out double amount))
                {
                    collectedAmount += Math.Max(0d, amount);
                }

                onCollectedAmountChanged?.Invoke(collectedAmount);
            });
    }

    internal IEnumerator PlayGoldBurstResultPresentation(
        double totalWin,
        UIManager uiManager)
    {
        EnsureInitialized();
        if (goldBurstResultPanel == null || goldBurstResultSkeleton == null)
        {
            yield break;
        }

        ResetGoldBurstResultPresentation();
        activeGoldBurstResultUi = uiManager;
        if (darkBackground != null) darkBackground.SetActive(true);
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
        float introDuration = PlayGoldBurstResultSpineAnimation(
            GoldBurstResultIntroAnimation,
            false);

        float introElapsed = 0f;
        while (!collectRequested && introElapsed < introDuration)
        {
            introElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float loopDuration = PlayGoldBurstResultSpineAnimation(
            GoldBurstResultLoopAnimation,
            true);
        StartGoldBurstResultHeartbeat(loopDuration);
        StartGoldBurstResultCount(
            totalWin,
            GoldBurstResultCountDuration);

        yield return new WaitForSecondsRealtime(GoldBurstResultCountDuration);
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

        float dissolveDuration = PlayGoldBurstResultSpineAnimation(
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

    private void PlayGoldBurstResultPopup()
    {
        goldBurstResultPopupTween?.Kill();
        Sequence popup = DOTween.Sequence().SetUpdate(true);
        if (goldBurstResultWinBox != null)
        {
            popup.Join(
                goldBurstResultWinBox
                    .DOScale(goldBurstResultWinBoxBaseScale,
                        goldBurstResultPopupDuration)
                    .SetEase(Ease.OutBack));
        }
        if (goldBurstResultCollectButton != null)
        {
            popup.Join(
                goldBurstResultCollectButton.transform
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
            dismiss.Join(
                goldBurstResultWinBox
                    .DOScale(Vector3.zero, duration)
                    .SetEase(Ease.InBack));
            hasTarget = true;
        }
        if (goldBurstResultCollectButton != null)
        {
            SetButtonInteractableWithoutAlphaChange(
                goldBurstResultCollectButton,
                false);
            dismiss.Join(
                goldBurstResultCollectButton.transform
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
        heartbeat.Append(
            goldBurstResultWinBox
                .DOScale(
                    goldBurstResultWinBoxBaseScale * goldBurstResultHeartbeatScale,
                    halfCycle)
                .SetEase(Ease.InOutSine));
        heartbeat.Append(
            goldBurstResultWinBox
                .DOScale(goldBurstResultWinBoxBaseScale, halfCycle)
                .SetEase(Ease.InOutSine));
        heartbeat.SetLoops(-1, LoopType.Restart);
        goldBurstResultHeartbeatTween = heartbeat;
    }

    private float PlayGoldBurstResultSpineAnimation(string animationName, bool loop)
    {
        if (goldBurstResultSkeleton == null || string.IsNullOrEmpty(animationName))
        {
            return 0f;
        }

        if (goldBurstResultSkeleton.SkeletonData == null)
        {
            goldBurstResultSkeleton.Initialize(false);
        }

        Spine.Animation animation =
            goldBurstResultSkeleton.SkeletonData?.FindAnimation(animationName);
        if (animation == null || goldBurstResultSkeleton.AnimationState == null)
        {
            return 0f;
        }

        goldBurstResultSkeleton.freeze = false;
        goldBurstResultSkeleton.AnimationState.ClearTracks();
        goldBurstResultSkeleton.Skeleton?.SetToSetupPose();
        goldBurstResultSkeleton.AnimationState.SetAnimation(
            0,
            animation.Name,
            loop);
        return animation.Duration /
               Mathf.Max(0.01f, Mathf.Abs(goldBurstResultSkeleton.timeScale));
    }

    private void CompleteGoldBurstResultAmount(double totalWin)
    {
        goldBurstResultCountTween?.Kill();
        goldBurstResultCountTween = null;
        SetSpriteAmount(goldBurstResultWinAmount, totalWin);
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

    internal void EndGoldBurstPresentation()
    {
        ResetGoldBurstPresentation();
        ClearVisibleFeatures();
        RefreshAnimationHierarchy();
    }

    internal bool TryAcquireGoldBurstAnimationCell(
        int reelIndex,
        int row,
        out RectTransform animationCell)
    {
        EnsureInitialized();
        if (!TryGetUsableAnimationCell(
                reelIndex,
                row,
                false,
                out animationCell,
                out RectTransform column) ||
            !animationCell.IsChildOf(animationRoot))
        {
            return false;
        }

        activeGoldBurstAnimationCells.Add(animationCell.gameObject);
        SetWinboxActive(animationCell, false);
        animationRoot.gameObject.SetActive(true);
        column.gameObject.SetActive(true);
        column.SetAsLastSibling();
        animationCell.gameObject.SetActive(true);
        return true;
    }

    internal bool TryGetGoldBurstBarrelAnimationTarget(
        int reelIndex,
        int startRow,
        int rowCount,
        out RectTransform barrel)
    {
        EnsureInitialized();
        barrel = null;

        if (reelIndex < 0 || reelIndex >= ReelCount ||
            startRow < 0 || startRow + rowCount > RowCount)
        {
            return false;
        }

        if (rowCount == 2)
        {
            visibleFeatures.TryGetValue(
                (TwoSlotFeatureType, startRow, reelIndex, 2, 1),
                out barrel);
        }
        else if (rowCount == 3 && startRow == 0)
        {
            visibleFeatures.TryGetValue(
                (ThreeSlotFeatureType, 0, reelIndex, 3, 1),
                out barrel);
        }

        return barrel != null &&
               barrel.gameObject.activeInHierarchy &&
               barrel.GetComponent<Image>() != null;
    }

    internal bool IsGoldBurstCellCoveredByFeature(int reelIndex, int row)
    {
        EnsureInitialized();
        return reelIndex >= 0 && reelIndex < ReelCount &&
               row >= 0 && row < RowCount &&
               IsCellCoveredByPendingFeature(reelIndex, row);
    }

    internal void ReleaseGoldBurstAnimationCell(RectTransform animationCell)
    {
        if (animationCell == null) return;

        activeGoldBurstAnimationCells.Remove(animationCell.gameObject);
        bool keepCellActive = activeWinAnimationCells.Contains(animationCell.gameObject) ||
                              activeTrainAnimationCells.Contains(animationCell.gameObject);
        SetWinboxActive(
            animationCell,
            activeWinAnimationCells.Contains(animationCell.gameObject));
        animationCell.gameObject.SetActive(keepCellActive);

        Transform columnTransform = animationCell.parent;
        if (columnTransform is RectTransform column &&
            !activeGoldBurstAnimationCells.Any(cell => cell.transform.parent == column) &&
            animationColumnSiblingIndices.TryGetValue(column, out int siblingIndex))
        {
            column.SetSiblingIndex(siblingIndex);
        }

        RefreshAnimationHierarchy();
    }

    internal bool TryGetGoldBurstAnimationCenter(
        int reelIndex,
        int startRow,
        int rowCount,
        out Vector3 worldCenter)
    {
        EnsureInitialized();
        worldCenter = Vector3.zero;

        int endRow = startRow + rowCount - 1;
        if (!HasCompleteAnimationGrid() ||
            reelIndex < 0 || reelIndex >= ReelCount ||
            startRow < 0 || endRow < startRow || endRow >= RowCount)
        {
            return false;
        }

        RectTransform firstCell = GetAnimationCell(reelIndex, startRow);
        RectTransform lastCell = GetAnimationCell(reelIndex, endRow);
        if (firstCell == null || lastCell == null) return false;

        worldCenter = (firstCell.position + lastCell.position) * 0.5f;
        return true;
    }

    internal bool TryAcquireWinAnimationCell(
        int reelIndex,
        int row,
        out RectTransform animationCell)
    {
        EnsureInitialized();
        if (!TryGetUsableAnimationCell(
                reelIndex,
                row,
                true,
                out animationCell,
                out RectTransform column))
        {
            return false;
        }

        activeWinAnimationCells.Add(animationCell.gameObject);
        SetWinboxActive(animationCell, true);
        animationRoot.gameObject.SetActive(true);
        column.gameObject.SetActive(true);
        animationCell.gameObject.SetActive(true);
        return true;
    }

    internal void ReleaseWinAnimationCell(RectTransform animationCell)
    {
        if (animationCell == null) return;

        activeWinAnimationCells.Remove(animationCell.gameObject);
        bool keepCellActive = activeTrainAnimationCells.Contains(animationCell.gameObject);
        SetWinboxActive(animationCell, false);
        animationCell.gameObject.SetActive(keepCellActive);
        RefreshAnimationHierarchy();
    }

    internal bool TryAcquireTrainAnimationCell(
        int reelIndex,
        int row,
        out RectTransform animationCell)
    {
        EnsureInitialized();
        if (!TryGetUsableAnimationCell(
                reelIndex,
                row,
                true,
                out animationCell,
                out RectTransform column))
        {
            return false;
        }

        activeTrainAnimationCells.Add(animationCell.gameObject);
        SetWinboxActive(animationCell, true);
        if (!activeWinAnimationCells.Contains(animationCell.gameObject))
        {
            Image trainImage = animationCell.GetComponent<Image>();
            if (trainImage != null) trainImage.enabled = false;
        }

        animationRoot.gameObject.SetActive(true);
        column.gameObject.SetActive(true);
        animationCell.gameObject.SetActive(true);
        return true;
    }

    private bool TryGetUsableAnimationCell(
        int reelIndex,
        int row,
        bool rejectCoveredFeature,
        out RectTransform animationCell,
        out RectTransform column)
    {
        animationCell = null;
        column = null;
        if (animationRoot == null ||
            !HasCompleteAnimationGrid() ||
            reelIndex < 0 || reelIndex >= ReelCount ||
            row < 0 || row >= RowCount ||
            (rejectCoveredFeature &&
             IsCellCoveredByPendingFeature(reelIndex, row)))
        {
            return false;
        }

        animationCell = GetAnimationCell(reelIndex, row);
        column = GetAnimationColumn(reelIndex);
        return animationCell != null && column != null;
    }

    internal void ReleaseTrainAnimationCell(RectTransform animationCell)
    {
        if (animationCell == null) return;

        activeTrainAnimationCells.Remove(animationCell.gameObject);
        bool keepCellActive = activeWinAnimationCells.Contains(animationCell.gameObject);
        SetWinboxActive(animationCell, keepCellActive);
        animationCell.gameObject.SetActive(keepCellActive);
        RefreshAnimationHierarchy();
    }

    private void ClearVisibleFeatures()
    {
        StopFeatureRevealRoutine();
        DOTween.Kill(this);

        foreach (CanvasGroup canvasGroup in conversionCanvasGroups)
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        foreach (GameObject hiddenCell in hiddenAnimationCells)
        {
            if (hiddenCell != null)
            {
                bool isActive = IsAnimationCellActive(hiddenCell);
                hiddenCell.SetActive(isActive);
                SetWinboxActive(
                    hiddenCell,
                    isActive && activeWinAnimationCells.Contains(hiddenCell));
            }
        }
        hiddenAnimationCells.Clear();

        foreach (RectTransform barrel in twoSlotBarrels)
        {
            if (barrel != null) barrel.gameObject.SetActive(false);
        }

        foreach (RectTransform barrel in threeSlotBarrels)
        {
            if (barrel != null) barrel.gameObject.SetActive(false);
        }

        if (twoSlotBarrelRoot != null)
        {
            twoSlotBarrelRoot.gameObject.SetActive(false);
        }

        if (threeSlotBarrelRoot != null)
        {
            threeSlotBarrelRoot.gameObject.SetActive(false);
        }

        foreach (KeyValuePair<TrainVisualType, List<TrainVisualReferences>> entry in trainVisuals)
        {
            foreach (TrainVisualReferences trainReferences in entry.Value)
            {
                ResetTrainPresentation(trainReferences);
                if (trainReferences?.visual != null)
                {
                    trainReferences.visual.gameObject.SetActive(false);
                }
            }
        }

        foreach (KeyValuePair<TrainVisualType, RectTransform> entry in trainRoots)
        {
            if (entry.Value != null) entry.Value.gameObject.SetActive(false);
        }

        visibleFeatures.Clear();
        trainPlacementsByVisual.Clear();
        RefreshAnimationHierarchy();
    }

    private void CacheSceneObjects()
    {
        if (animationRoot == null)
        {
            animationRoot = FindDescendant(searchRoot, "5x3Animation") as RectTransform;
        }

        if (animationRoot == null)
        {
            Transform sceneAnimationRoot = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.name == "5x3Animation");
            animationRoot = sceneAnimationRoot as RectTransform;
        }

        if (animationRoot == null)
        {
            ReportConfigurationWarning("Could not find the 5x3Animation object.");
            isInitialized = true;
            return;
        }

        if (twoSlotBarrelRoot == null)
        {
            twoSlotBarrelRoot = FindDescendant(animationRoot, "2SlotBarrels") as RectTransform;
        }

        if (threeSlotBarrelRoot == null)
        {
            threeSlotBarrelRoot = FindDescendant(animationRoot, "3SlotBarrels") as RectTransform;
        }

        if (greenTrainRoot == null)
        {
            greenTrainRoot = FindDescendant(animationRoot, "GreenTrains") as RectTransform;
        }
        if (redTrainRoot == null)
        {
            redTrainRoot = FindDescendant(animationRoot, "RedTrains") as RectTransform;
        }
        if (horizontalPurpleTrainRoot == null)
        {
            horizontalPurpleTrainRoot = FindDescendant(animationRoot, "HorizontalPurpleTrains") as RectTransform;
        }
        if (verticalPurpleTrainRoot == null)
        {
            verticalPurpleTrainRoot = FindDescendant(animationRoot, "VerticalPurpleTrains") as RectTransform;
        }
        if (goldenTrainRoot == null)
        {
            goldenTrainRoot = FindDescendant(animationRoot, "GoldenTrains") as RectTransform;
        }

        CacheGoldBurstSceneObjects();

        twoSlotBarrels.Clear();
        if (twoSlotBarrelRoot != null)
        {
            twoSlotBarrels.AddRange(
                GetSortedChildren(twoSlotBarrelRoot, true));
            CaptureAuthoredVerticalPositions();
        }

        threeSlotBarrels.Clear();
        if (threeSlotBarrelRoot != null)
        {
            threeSlotBarrels.AddRange(
                GetSortedChildren(threeSlotBarrelRoot, true));
        }

        CacheTrainVisuals();
        CacheTrainJourneyPresentation();

        CacheAnimationGrid();
        CacheGoldBoxPools();

        isInitialized = true;
        HasTwoSlotConfiguration();
        HasThreeSlotConfiguration();
    }

    private void CacheGoldBurstSceneObjects()
    {
        darkBackground = darkBackground != null
            ? darkBackground
            : FindSceneGameObject("DarkBackground");
        coldBurstRespin = coldBurstRespin != null
            ? coldBurstRespin
            : FindSceneGameObject("ColdBurstRespin");
        megaGoldBurstRespin = megaGoldBurstRespin != null
            ? megaGoldBurstRespin
            : FindSceneGameObject("MegaGoldBurstRespin");
        ultimateGoldBurstRespin = ultimateGoldBurstRespin != null
            ? ultimateGoldBurstRespin
            : FindSceneGameObject("UltimateGoldBurstRespin");
        track = track != null ? track : FindSceneGameObject("Track");
        trolleyMan = trolleyMan != null ? trolleyMan : FindSceneGameObject("TrolleyMan");
        CaptureTrolleyManState();
        CacheGoldBurstResultPresentation();
        CacheFreeGamesPresentation();

        if (oneSlotGoldBoxRoot == null)
        {
            oneSlotGoldBoxRoot = FindDescendant(animationRoot, "1SlotGoldBox") as RectTransform;
        }
        if (twoSlotGoldBoxRoot == null)
        {
            twoSlotGoldBoxRoot = FindDescendant(animationRoot, "2SlotGoldBox") as RectTransform;
        }
        if (threeSlotGoldBoxRoot == null)
        {
            threeSlotGoldBoxRoot = FindDescendant(animationRoot, "3SlotGoldBox") as RectTransform;
        }
        if (goldBurstCollectionParticles == null)
        {
            GameObject particlePool = FindSceneGameObject("ParticlePool");
            if (particlePool != null)
            {
                goldBurstCollectionParticles =
                    particlePool.GetComponent<PrizeTrailController>();
            }
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

        SkeletonGraphic trolleyGraphic = trolleyMan.GetComponent<SkeletonGraphic>();
        if (trolleyGraphic != null)
        {
            if (trolleyGraphic.SkeletonData == null)
            {
                trolleyGraphic.Initialize(false);
            }

            trolleyGraphic.AnimationState?.ClearTracks();
            trolleyGraphic.Skeleton?.SetToSetupPose();
            trolleyGraphic.freeze = true;
        }

        trolleyMan.SetActive(false);
    }

    private void CacheGoldBurstResultPresentation()
    {
        goldBurstResultPanel = goldBurstResultPanel != null
            ? goldBurstResultPanel
            : FindSceneGameObject("FreeGamesEnd");
        if (goldBurstResultPanel == null) return;

        goldBurstResultSkeleton =
            goldBurstResultPanel.GetComponent<SkeletonGraphic>();
        goldBurstResultContainer = goldBurstResultPanel.transform.parent;
        if (goldBurstResultContainer != null)
        {
            goldBurstResultContainerSiblingIndex =
                goldBurstResultContainer.GetSiblingIndex();
            Transform startPanel = FindDescendant(
                goldBurstResultContainer,
                "FreeGamesStart");
            goldBurstResultStartPanel = startPanel != null
                ? startPanel.gameObject
                : null;
        }
        if (goldBurstResultWinBox == null)
        {
            goldBurstResultWinBox = FindDescendant(
                goldBurstResultPanel.transform,
                "Winbox") as RectTransform;
        }
        if (goldBurstResultWinAmount == null && goldBurstResultWinBox != null)
        {
            Transform amount = FindDescendant(
                goldBurstResultWinBox,
                "Winamount");
            goldBurstResultWinAmount = amount != null
                ? amount.GetComponent<TMP_Text>()
                : null;
        }
        if (goldBurstResultCollectButton == null)
        {
            Transform collect = FindDescendant(
                goldBurstResultPanel.transform,
                "Collect");
            goldBurstResultCollectButton = collect != null
                ? collect.GetComponent<Button>()
                : null;
        }

        if (!hasCapturedGoldBurstResultState)
        {
            goldBurstResultWinBoxBaseScale = goldBurstResultWinBox != null
                ? goldBurstResultWinBox.localScale
                : Vector3.one;
            goldBurstResultCollectBaseScale = goldBurstResultCollectButton != null
                ? goldBurstResultCollectButton.transform.localScale
                : Vector3.one;
            hasCapturedGoldBurstResultState = true;
        }
    }

    private void CacheFreeGamesPresentation()
    {
        freeGamesStartPanel = freeGamesStartPanel != null
            ? freeGamesStartPanel
            : goldBurstResultStartPanel != null
                ? goldBurstResultStartPanel
                : FindSceneGameObject("FreeGamesStart");
        if (freeGamesStartPanel != null)
        {
            freeGamesStartSkeleton =
                freeGamesStartPanel.GetComponent<SkeletonGraphic>();
            if (freeGamesStartButton == null)
            {
                Transform startButton = FindDescendant(
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
            : FindSceneGameObject("TrainTrackAnimation");
        if (trainTrackAnimation != null)
        {
            trainTrackSkeleton =
                trainTrackAnimation.GetComponent<SkeletonGraphic>();
            if (!hasCapturedTrainTrackSiblingIndex &&
                trainTrackAnimation.transform.parent != null)
            {
                trainTrackSiblingIndex =
                    trainTrackAnimation.transform.GetSiblingIndex();
                hasCapturedTrainTrackSiblingIndex = true;
            }
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
            SetButtonInteractableWithoutAlphaChange(
                freeGamesStartButton,
                false);
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

        Transform trackTransform = trainTrackAnimation.transform;
        Transform trackParent = trackTransform.parent;
        if (trackParent != null)
        {
            trackTransform.SetAsLastSibling();
        }

        trainTrackAnimation.SetActive(true);
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

        ResetSpineAnimation(trainTrackSkeleton);
        trainTrackAnimation.SetActive(false);
        if (trackParent != null && hasCapturedTrainTrackSiblingIndex)
        {
            trackTransform.SetSiblingIndex(Mathf.Clamp(
                trainTrackSiblingIndex,
                0,
                trackParent.childCount - 1));
        }
    }

    private static float PlaySpineAnimation(
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
        skeletonGraphic.AnimationState.SetAnimation(
            0,
            animation.Name,
            loop);
        return animation.Duration /
               Mathf.Max(0.01f, Mathf.Abs(skeletonGraphic.timeScale));
    }

    private static void ResetSpineAnimation(SkeletonGraphic skeletonGraphic)
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

    private void ResetGoldBurstResultPresentation()
    {
        StopGoldBurstResultTweens(true);
        RemoveGoldBurstResultListeners();
        activeGoldBurstResultUi = null;

        if (goldBurstResultSkeleton != null)
        {
            if (goldBurstResultSkeleton.SkeletonData == null)
            {
                goldBurstResultSkeleton.Initialize(false);
            }
            goldBurstResultSkeleton.AnimationState?.ClearTracks();
            goldBurstResultSkeleton.Skeleton?.SetToSetupPose();
            goldBurstResultSkeleton.freeze = true;
        }

        if (goldBurstResultPanel != null)
        {
            goldBurstResultPanel.SetActive(false);
        }

        RestoreGoldBurstResultHierarchy();
    }

    private void RaiseGoldBurstResultHierarchy()
    {
        if (goldBurstResultContainer == null || isGoldBurstResultHierarchyRaised)
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

    private void CacheGoldBoxPools()
    {
        allGoldBoxes.Clear();
        twoSlotGoldBoxes.Clear();
        threeSlotGoldBoxes.Clear();
        for (int reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            oneSlotGoldBoxesByReel[reelIndex] = new List<GoldBoxRuntime>();
        }

        if (oneSlotGoldBoxRoot != null)
        {
            List<RectTransform> reelColumns = GetSortedChildren(oneSlotGoldBoxRoot, true);
            for (int reelIndex = 0;
                 reelIndex < Mathf.Min(ReelCount, reelColumns.Count);
                 reelIndex++)
            {
                List<RectTransform> rowVisuals = GetSortedChildren(reelColumns[reelIndex], false);
                foreach (RectTransform rowVisual in rowVisuals.Take(RowCount))
                {
                    GoldBoxRuntime runtime = CreateGoldBoxRuntime(rowVisual);
                    if (runtime == null) continue;

                    oneSlotGoldBoxesByReel[reelIndex].Add(runtime);
                    allGoldBoxes.Add(runtime);
                }
            }
        }

        CacheLinearGoldBoxPool(twoSlotGoldBoxRoot, twoSlotGoldBoxes);
        CacheLinearGoldBoxPool(threeSlotGoldBoxRoot, threeSlotGoldBoxes);
        HideAllGoldBoxes();
    }

    private void CacheLinearGoldBoxPool(
        RectTransform root,
        List<GoldBoxRuntime> destination)
    {
        if (root == null) return;

        foreach (RectTransform child in GetSortedChildren(root, true).Take(ReelCount))
        {
            GoldBoxRuntime runtime = CreateGoldBoxRuntime(child);
            if (runtime == null) continue;

            destination.Add(runtime);
            allGoldBoxes.Add(runtime);
        }
    }

    private static List<RectTransform> GetSortedChildren(
        RectTransform root,
        bool sortLeftToRight)
    {
        if (root == null) return new List<RectTransform>();

        var children = new List<RectTransform>();
        for (int index = 0; index < root.childCount; index++)
        {
            if (root.GetChild(index) is RectTransform child)
            {
                children.Add(child);
            }
        }

        return sortLeftToRight
            ? children.OrderBy(child => child.anchoredPosition.x).ToList()
            : children.OrderByDescending(child => child.anchoredPosition.y).ToList();
    }

    private static GoldBoxRuntime CreateGoldBoxRuntime(RectTransform visual)
    {
        if (visual == null) return null;

        TMP_Text amountText = visual.GetComponentInChildren<TMP_Text>(true);
        return amountText == null
            ? null
            : new GoldBoxRuntime
            {
                visual = visual,
                amountText = amountText,
                baseScale = visual.localScale
            };
    }

    private void CacheAnimationGrid()
    {
        winboxByAnimationCell.Clear();
        animationColumnSiblingIndices.Clear();
        if (animationGrid == null) return;

        foreach (AnimationColumnReferences columnReferences in animationGrid)
        {
            if (columnReferences == null) continue;

            if (columnReferences.column != null)
            {
                animationColumnSiblingIndices[columnReferences.column] =
                    columnReferences.column.GetSiblingIndex();
                columnReferences.column.gameObject.SetActive(false);
            }

            if (columnReferences.rows == null) continue;

            foreach (AnimationCellReferences cellReferences in columnReferences.rows)
            {
                if (cellReferences == null) continue;

                if (cellReferences.slot != null)
                {
                    cellReferences.slot.gameObject.SetActive(false);
                }

                if (cellReferences.winbox == null) continue;

                cellReferences.winbox.gameObject.SetActive(false);

                if (cellReferences.slot != null)
                {
                    winboxByAnimationCell[cellReferences.slot.gameObject] =
                        cellReferences.winbox;
                }
            }
        }
    }

    private RectTransform GetAnimationColumn(int reelIndex)
    {
        return animationGrid != null &&
               reelIndex >= 0 && reelIndex < animationGrid.Length
            ? animationGrid[reelIndex]?.column
            : null;
    }

    private RectTransform GetAnimationCell(int reelIndex, int row)
    {
        if (animationGrid == null ||
            reelIndex < 0 || reelIndex >= animationGrid.Length ||
            animationGrid[reelIndex]?.rows == null ||
            row < 0 || row >= animationGrid[reelIndex].rows.Length)
        {
            return null;
        }

        return animationGrid[reelIndex].rows[row]?.slot;
    }

    private void CacheTrainVisuals()
    {
        trainRoots.Clear();
        trainVisuals.Clear();
        trainReferencesByVisual.Clear();
        pressPlayBaseScales.Clear();
        trainVisualRestingPositions.Clear();
        trainAnimationRestingPositions.Clear();

        CacheTrainVisuals(TrainVisualType.Green, greenTrainRoot, greenTrainVisuals);
        CacheTrainVisuals(TrainVisualType.Red, redTrainRoot, redTrainVisuals);
        CacheTrainVisuals(
            TrainVisualType.HorizontalPurple,
            horizontalPurpleTrainRoot,
            horizontalPurpleTrainVisuals);
        CacheTrainVisuals(
            TrainVisualType.VerticalPurple,
            verticalPurpleTrainRoot,
            verticalPurpleTrainVisuals);
        CacheTrainVisuals(TrainVisualType.Golden, goldenTrainRoot, goldenTrainVisuals);

        ValidateTrainVisualPool(TrainVisualType.Green);
        ValidateTrainVisualPool(TrainVisualType.Red);
        ValidateTrainVisualPool(TrainVisualType.HorizontalPurple);
        ValidateTrainVisualPool(TrainVisualType.VerticalPurple);
        ValidateTrainVisualPool(TrainVisualType.Golden);
    }

    private bool RemoveFeaturesMissingFromResult()
    {
        var pendingFeatures = new HashSet<
            (int type, int startRow, int startCol, int rowCount, int columnCount)>();

        foreach (KeyValuePair<int, int> barrel in pendingStartRowByReel)
        {
            pendingFeatures.Add((TwoSlotFeatureType, barrel.Value, barrel.Key, 2, 1));
        }

        foreach (int reelIndex in pendingThreeSlotReels)
        {
            pendingFeatures.Add((ThreeSlotFeatureType, 0, reelIndex, RowCount, 1));
        }

        foreach (TrainPlacement train in pendingTrains)
        {
            pendingFeatures.Add(((int)train.type, train.startRow, train.startCol,
                train.rowCount, train.columnCount));
        }

        bool hasTrainConversion = false;
        foreach (KeyValuePair<
                     (int type, int startRow, int startCol, int rowCount, int columnCount),
                     RectTransform> visibleFeature in visibleFeatures.ToList())
        {
            if (pendingFeatures.Contains(visibleFeature.Key)) continue;

            bool isTrain = visibleFeature.Key.type >= 0;
            List<TrainPlacement> replacementTrains = isTrain
                ? GetNewOverlappingTrains(visibleFeature.Key)
                : new List<TrainPlacement>();
            bool isTrainConversion = replacementTrains.Count > 0;

            if (isTrainConversion &&
                PlayTrainConversionOut(visibleFeature.Key, visibleFeature.Value))
            {
                foreach (TrainPlacement replacement in replacementTrains)
                {
                    immediateTrainRevealKeys.Add(
                        ((int)replacement.type, replacement.startRow,
                            replacement.startCol, replacement.rowCount,
                            replacement.columnCount));
                }
                hasTrainConversion = true;
            }
            else if (isTrain)
            {
                HideRemovedFeatureImmediately(visibleFeature.Key, visibleFeature.Value);
            }
            else
            {
                FadeOutRemovedFeature(visibleFeature.Key, visibleFeature.Value);
            }

            visibleFeatures.Remove(visibleFeature.Key);
        }

        return hasTrainConversion;
    }

    private List<TrainPlacement> GetNewOverlappingTrains(
        (int type, int startRow, int startCol, int rowCount, int columnCount) outgoing)
    {
        return pendingTrains
            .Where(incoming =>
            {
                var incomingKey = ((int)incoming.type, incoming.startRow,
                    incoming.startCol, incoming.rowCount, incoming.columnCount);
                return !visibleFeatures.ContainsKey(incomingKey) &&
                       outgoing.startCol < incoming.startCol + incoming.columnCount &&
                       outgoing.startCol + outgoing.columnCount > incoming.startCol &&
                       outgoing.startRow < incoming.startRow + incoming.rowCount &&
                       outgoing.startRow + outgoing.rowCount > incoming.startRow;
            })
            .ToList();
    }

    private bool PlayTrainConversionOut(
        (int type, int startRow, int startCol, int rowCount, int columnCount) feature,
        RectTransform visual)
    {
        if (visual == null ||
            !trainReferencesByVisual.TryGetValue(
                visual,
                out TrainVisualReferences trainReferences))
        {
            return false;
        }

        ResetPressPlay(trainReferences);
        CanvasGroup visualGroup = GetConversionCanvasGroup(visual.gameObject);
        visualGroup.alpha = 1f;
        ImageAnimation trainAnimation = GetTrainVisualAnimation(trainReferences);
        RectTransform animatedTrain = trainAnimation != null
            ? trainAnimation.transform as RectTransform
            : null;
        if (animatedTrain == null)
        {
            return false;
        }

        Vector2 startPosition = animatedTrain.anchoredPosition;
        DOTween.Kill(animatedTrain);

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        sequence.AppendCallback(() =>
            EnsureTrainVisualAnimationPlaying(trainReferences));
        sequence.Join(
            animatedTrain.DOAnchorPosX(
                    startPosition.x + TrainConversionForwardDistance,
                    TrainConversionMoveDuration)
                .SetEase(Ease.InOutSine));
        sequence.Join(
            visualGroup.DOFade(0f, TrainConversionMoveDuration)
                .SetEase(Ease.Linear));
        sequence.AppendCallback(() =>
            FinalizeRemovedFeature(feature, visual));
        return true;
    }

    private void HideRemovedFeatureImmediately(
        (int type, int startRow, int startCol, int rowCount, int columnCount) feature,
        RectTransform visual)
    {
        FinalizeRemovedFeature(feature, visual);
    }

    private void ClearAllVisibleTrains()
    {
        foreach (KeyValuePair<
                     (int type, int startRow, int startCol, int rowCount, int columnCount),
                     RectTransform> visibleFeature in visibleFeatures.ToList())
        {
            if (visibleFeature.Key.type < 0) continue;

            FinalizeRemovedFeature(
                visibleFeature.Key,
                visibleFeature.Value,
                true);
            visibleFeatures.Remove(visibleFeature.Key);
        }

        foreach (List<TrainVisualReferences> references in trainVisuals.Values)
        {
            foreach (TrainVisualReferences trainReferences in references)
            {
                if (trainReferences?.visual == null) continue;

                ResetTrainPresentation(trainReferences);
                trainReferences.visual.gameObject.SetActive(false);
                GetConversionCanvasGroup(trainReferences.visual.gameObject).alpha = 1f;
                trainPlacementsByVisual.Remove(trainReferences.visual);
            }
        }

        RefreshAnimationHierarchy();
    }

    private void FadeOutRemovedFeature(
        (int type, int startRow, int startCol, int rowCount, int columnCount) feature,
        RectTransform visual)
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        sequence.AppendInterval(ConversionHoldDuration);

        CanvasGroup visualGroup = GetConversionCanvasGroup(visual.gameObject);
        sequence.Insert(
            ConversionHoldDuration,
            visualGroup.DOFade(0f, ConversionFadeDuration).SetEase(Ease.Linear));

        FadeInUncoveredSourceCells(sequence, feature);
        sequence.AppendCallback(() => FinalizeRemovedFeature(feature, visual));
    }

    private void FadeInUncoveredSourceCells(
        Sequence sequence,
        (int type, int startRow, int startCol, int rowCount, int columnCount) feature)
    {
        for (int reelIndex = feature.startCol;
             reelIndex < feature.startCol + feature.columnCount;
             reelIndex++)
        {
            for (int row = feature.startRow; row < feature.startRow + feature.rowCount; row++)
            {
                if (IsCellCoveredByPendingFeature(reelIndex, row)) continue;

                RectTransform sourceCell = GetConversionSourceCell(reelIndex, row);
                if (sourceCell == null) continue;

                CanvasGroup sourceGroup = GetConversionCanvasGroup(sourceCell.gameObject);
                sequence.Insert(
                    ConversionHoldDuration,
                    sourceGroup.DOFade(1f, ConversionFadeDuration).SetEase(Ease.Linear));
            }
        }
    }

    private void FinalizeRemovedFeature(
        (int type, int startRow, int startCol, int rowCount, int columnCount) feature,
        RectTransform visual,
        bool restoreAllCells = false)
    {
        if (visual == null) return;

        if (trainReferencesByVisual.TryGetValue(
                visual,
                out TrainVisualReferences trainReferences))
        {
            ResetTrainPresentation(trainReferences);
            trainPlacementsByVisual.Remove(visual);
        }

        visual.gameObject.SetActive(false);
        CanvasGroup visualGroup = GetConversionCanvasGroup(visual.gameObject);
        visualGroup.alpha = 1f;

        for (int reelIndex = feature.startCol;
             reelIndex < feature.startCol + feature.columnCount;
             reelIndex++)
        {
            for (int row = feature.startRow; row < feature.startRow + feature.rowCount; row++)
            {
                if (!restoreAllCells && IsCellCoveredByPendingFeature(reelIndex, row)) continue;

                RectTransform sourceCell = GetConversionSourceCell(reelIndex, row);
                if (sourceCell != null)
                {
                    GetConversionCanvasGroup(sourceCell.gameObject).alpha = 1f;
                }

                RectTransform animationCell = GetAnimationCell(reelIndex, row);
                if (animationCell == null) continue;

                GameObject animationCellObject = animationCell.gameObject;
                hiddenAnimationCells.Remove(animationCellObject);
                bool isActive = IsAnimationCellActive(animationCellObject);
                animationCellObject.SetActive(isActive);
                SetWinboxActive(
                    animationCellObject,
                    isActive && activeWinAnimationCells.Contains(animationCellObject));
            }
        }

        RefreshAnimationHierarchy();
    }

    private void PlayConversionFade(
        int startReel,
        int reelCount,
        int startRow,
        int rowCount,
        RectTransform visual,
        bool revealVisualAfterSourcesHide = false,
        bool revealImmediately = false)
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        if (!revealImmediately)
        {
            sequence.AppendInterval(ConversionHoldDuration);
        }

        for (int reelIndex = startReel; reelIndex < startReel + reelCount; reelIndex++)
        {
            for (int row = startRow; row < startRow + rowCount; row++)
            {
                RectTransform sourceCell = GetConversionSourceCell(reelIndex, row);
                if (sourceCell == null) continue;

                CanvasGroup sourceGroup = GetConversionCanvasGroup(sourceCell.gameObject);
                if (revealImmediately)
                {
                    sourceGroup.alpha = 0f;
                }
                else
                {
                    sequence.Insert(
                        ConversionHoldDuration,
                        sourceGroup.DOFade(0f, ConversionFadeDuration).SetEase(Ease.Linear));
                }
            }
        }

        CanvasGroup visualGroup = GetConversionCanvasGroup(visual.gameObject);
        visualGroup.alpha = 0f;
        animationRoot.gameObject.SetActive(true);
        visual.parent.gameObject.SetActive(true);
        visual.gameObject.SetActive(true);

        if (revealImmediately)
        {
            for (int reelIndex = startReel; reelIndex < startReel + reelCount; reelIndex++)
            {
                HideAnimationCells(reelIndex, startRow, rowCount);
            }

            if (trainReferencesByVisual.TryGetValue(
                    visual,
                    out TrainVisualReferences immediateTrainReferences))
            {
                StartTrainVisualEntry(immediateTrainReferences);
            }

            sequence.Append(
                visualGroup.DOFade(1f, TrainConversionMoveDuration)
                    .SetEase(Ease.Linear));
            return;
        }

        if (!revealVisualAfterSourcesHide)
        {
            sequence.Insert(
                ConversionHoldDuration,
                visualGroup.DOFade(1f, ConversionFadeDuration).SetEase(Ease.Linear));
        }

        sequence.AppendCallback(() =>
        {
            for (int reelIndex = startReel; reelIndex < startReel + reelCount; reelIndex++)
            {
                HideAnimationCells(reelIndex, startRow, rowCount);
            }

            if (revealVisualAfterSourcesHide &&
                trainReferencesByVisual.TryGetValue(
                    visual,
                    out TrainVisualReferences trainReferences))
            {
                StartTrainVisualEntry(trainReferences);
            }
        });

        if (revealVisualAfterSourcesHide)
        {
            sequence.Append(
                visualGroup.DOFade(1f, ConversionFadeDuration).SetEase(Ease.Linear));
        }
    }

    private bool IsCellCoveredByPendingFeature(int reelIndex, int row)
    {
        if (pendingThreeSlotReels.Contains(reelIndex)) return true;

        if (pendingStartRowByReel.TryGetValue(reelIndex, out int barrelStartRow) &&
            row >= barrelStartRow && row < barrelStartRow + 2)
        {
            return true;
        }

        return pendingTrains.Any(train =>
            reelIndex >= train.startCol && reelIndex < train.startCol + train.columnCount &&
            row >= train.startRow && row < train.startRow + train.rowCount);
    }

    private RectTransform GetConversionSourceCell(int reelIndex, int row)
    {
        return conversionSourceSlots != null &&
               reelIndex >= 0 && reelIndex < conversionSourceSlots.Count
            ? conversionSourceSlots[reelIndex]?.Get(row)?.rectTransform
            : null;
    }

    private CanvasGroup GetConversionCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        conversionCanvasGroups.Add(canvasGroup);
        return canvasGroup;
    }

    private void CacheTrainJourneyPresentation()
    {
        trainJourneyTrains.Clear();
        trainJourneyAmountsByType.Clear();

        trainJourneyWinBoxImage = trainJourneyWinBox != null
            ? trainJourneyWinBox.GetComponent<Image>()
            : null;
        if (trainJourneyWinBoxImage != null &&
            !hasCapturedTrainJourneyWinBoxColor)
        {
            trainJourneyWinBoxBaseColor = trainJourneyWinBoxImage.color;
            hasCapturedTrainJourneyWinBoxColor = true;
        }

        trainJourneyLineAnimation = FindDescendant(
                trainJourneyWinBox,
                "LineAnimation")
            ?.GetComponent<ImageAnimation>();
        trainJourneyBoxAnimation = FindDescendant(
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
            }
            if (trainJourneySecondActor != null)
            {
                trainJourneySecondActorStartPosition =
                    trainJourneySecondActor.anchoredPosition;
            }
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
            ImageAnimation trainAnimation = journeyTrain.GetComponent<ImageAnimation>();
            trainJourneyOriginalLoops[journeyTrain] =
                trainAnimation != null && trainAnimation.doLoopAnimation;
        }

        return amounts;
    }

    private void CacheTrainVisuals(
        TrainVisualType type,
        RectTransform root,
        TrainVisualReferences[] references)
    {
        trainRoots[type] = root;
        var visuals = new List<TrainVisualReferences>();

        if (references != null)
        {
            foreach (TrainVisualReferences trainReferences in references)
            {
                if (trainReferences?.visual == null) continue;

                visuals.Add(trainReferences);
                trainReferencesByVisual[trainReferences.visual] = trainReferences;
                trainVisualRestingPositions[trainReferences.visual] =
                    trainReferences.visual.anchoredPosition;
                ImageAnimation trainAnimation =
                    GetTrainVisualAnimation(trainReferences);
                if (trainAnimation != null &&
                    trainAnimation.transform is RectTransform animatedTrain)
                {
                    trainAnimationRestingPositions[animatedTrain] =
                        animatedTrain.anchoredPosition;
                }

                if (trainReferences.pressPlay != null)
                {
                    pressPlayBaseScales[trainReferences.pressPlay] =
                        trainReferences.pressPlay.localScale;
                }

                GetTrainResultAmount(trainReferences);

                ResetTrainPresentation(trainReferences);
            }
        }

        trainVisuals[type] = visuals;
    }

    private TrainVisualReferences GetAvailableTrainVisual(TrainVisualType type)
    {
        if (!trainVisuals.TryGetValue(type, out List<TrainVisualReferences> visuals))
        {
            return null;
        }

        int reusableCount = Mathf.Min(GetMaximumVisibleTrainCount(type), visuals.Count);
        for (int index = 0; index < reusableCount; index++)
        {
            TrainVisualReferences trainReferences = visuals[index];
            if (trainReferences?.visual != null &&
                !trainReferences.visual.gameObject.activeSelf)
            {
                return trainReferences;
            }
        }

        return null;
    }

    private void PrepareTrainVisualForReveal(TrainVisualReferences trainReferences)
    {
        if (trainReferences == null) return;

        ResetPressPlay(trainReferences);
        if (trainReferences.mask != null)
        {
            trainReferences.mask.SetActive(true);
        }
        ResetTrainVisualAnimation(trainReferences);
    }

    private void ShowPressPlayButtons()
    {
        foreach (RectTransform visual in visibleFeatures.Values.Distinct())
        {
            if (visual == null || !visual.gameObject.activeSelf ||
                !trainReferencesByVisual.TryGetValue(
                    visual,
                    out TrainVisualReferences trainReferences))
            {
                continue;
            }

            PlayPressPlayAnimation(trainReferences);
        }
    }

    private void PlayPressPlayAnimation(TrainVisualReferences trainReferences)
    {
        RectTransform pressPlay = trainReferences?.pressPlay;
        if (pressPlay == null) return;

        DOTween.Kill(pressPlay);
        Vector3 baseScale = GetPressPlayBaseScale(pressPlay);
        pressPlay.localScale = Vector3.zero;
        pressPlay.gameObject.SetActive(true);

        Sequence popup = DOTween.Sequence().SetUpdate(true).SetTarget(pressPlay);
        popup.Append(
            pressPlay.DOScale(baseScale * pressPlayPopupScale, pressPlayPopupDuration)
                .SetEase(Ease.OutCubic));
        popup.Append(
            pressPlay.DOScale(baseScale, pressPlaySettleDuration)
                .SetEase(Ease.OutSine));
        popup.OnComplete(() => StartPressPlayHeartbeat(pressPlay, baseScale));
    }

    private void StartPressPlayHeartbeat(RectTransform pressPlay, Vector3 baseScale)
    {
        if (pressPlay == null || !pressPlay.gameObject.activeInHierarchy) return;

        Sequence heartbeat = DOTween.Sequence().SetUpdate(true).SetTarget(pressPlay);
        heartbeat.Append(
            pressPlay.DOScale(
                    baseScale * pressPlayHeartbeatLargeScale,
                    pressPlayHeartbeatHalfCycle)
                .SetEase(Ease.InOutSine));
        heartbeat.Append(
            pressPlay.DOScale(
                    baseScale * pressPlayHeartbeatSmallScale,
                    pressPlayHeartbeatHalfCycle)
                .SetEase(Ease.InOutSine));
        heartbeat.SetLoops(-1, LoopType.Restart);
    }

    private void ResetTrainPresentation(TrainVisualReferences trainReferences)
    {
        if (trainReferences == null) return;

        ResetPressPlay(trainReferences);
        ResetTrainResultAmount(trainReferences);
        ResetTrainVisualAnimation(trainReferences);
        if (trainReferences.mask != null)
        {
            trainReferences.mask.SetActive(false);
        }

        if (trainReferences.visual != null &&
            trainVisualRestingPositions.TryGetValue(
                trainReferences.visual,
                out Vector2 restingPosition))
        {
            trainReferences.visual.anchoredPosition = restingPosition;
        }
    }

    private static TMP_Text GetTrainResultAmount(
        TrainVisualReferences trainReferences)
    {
        if (trainReferences?.visual == null) return null;

        if (trainReferences.resultAmount == null)
        {
            trainReferences.resultAmount = trainReferences.visual
                .GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text =>
                    text != null &&
                    text.name.StartsWith(
                        "Winamount",
                        StringComparison.OrdinalIgnoreCase));
        }

        if (trainReferences.resultAmount != null &&
            !trainReferences.hasResultAmountBaseScale)
        {
            trainReferences.resultAmountBaseScale =
                trainReferences.resultAmount.rectTransform.localScale;
            trainReferences.hasResultAmountBaseScale = true;
        }

        return trainReferences.resultAmount;
    }

    private void ResetTrainResultAmount(
        TrainVisualReferences trainReferences)
    {
        TMP_Text resultAmount = GetTrainResultAmount(trainReferences);
        if (resultAmount == null) return;

        RectTransform amountRect = resultAmount.rectTransform;
        DOTween.Kill(amountRect);
        if (trainReferences.hasResultAmountBaseScale)
        {
            amountRect.localScale = trainReferences.resultAmountBaseScale;
        }
        resultAmount.gameObject.SetActive(false);
    }

    private static ImageAnimation GetTrainVisualAnimation(
        TrainVisualReferences trainReferences)
    {
        return trainReferences?.mask != null
            ? trainReferences.mask.GetComponentInChildren<ImageAnimation>(true)
            : null;
    }

    private void ResetTrainVisualAnimation(
        TrainVisualReferences trainReferences)
    {
        ImageAnimation animation = GetTrainVisualAnimation(trainReferences);
        if (animation == null) return;

        if (animation.transform is RectTransform animatedTrain)
        {
            DOTween.Kill(animatedTrain);
            if (trainAnimationRestingPositions.TryGetValue(
                    animatedTrain,
                    out Vector2 restingPosition))
            {
                animatedTrain.anchoredPosition = restingPosition;
            }
        }

        animation.onLoopComplete = null;
        animation.StopAnimation();
        animation.ClearLoopDuration();
        animation.RevertToInitialState();
        animation.enabled = false;
    }

    private void StartTrainVisualEntry(
        TrainVisualReferences trainReferences)
    {
        ImageAnimation animation = GetTrainVisualAnimation(trainReferences);
        if (animation == null) return;

        if (animation.transform is RectTransform animatedTrain)
        {
            DOTween.Kill(animatedTrain);
            Vector2 restingPosition = trainAnimationRestingPositions.TryGetValue(
                animatedTrain,
                out Vector2 capturedPosition)
                ? capturedPosition
                : animatedTrain.anchoredPosition;
            trainAnimationRestingPositions[animatedTrain] = restingPosition;
            animatedTrain.anchoredPosition = restingPosition +
                                             Vector2.left * TrainActivationLeftOffset;
            animatedTrain
                .DOAnchorPos(restingPosition, ConversionFadeDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        EnsureTrainVisualAnimationPlaying(trainReferences);
    }

    private static void EnsureTrainVisualAnimationPlaying(
        TrainVisualReferences trainReferences)
    {
        ImageAnimation animation = GetTrainVisualAnimation(trainReferences);
        if (animation == null) return;

        animation.doLoopAnimation = true;
        animation.enabled = true;
        if (animation.currentAnimationState != ImageAnimation.ImageState.PLAYING)
        {
            animation.StartAnimation();
        }
    }

    private void ResetPressPlay(TrainVisualReferences trainReferences)
    {
        RectTransform pressPlay = trainReferences?.pressPlay;
        if (pressPlay == null) return;

        Button button = pressPlay.GetComponent<Button>();
        if (button != null)
        {
            RemovePressPlayListener(button);
            button.interactable = true;
        }

        DOTween.Kill(pressPlay);
        pressPlay.localScale = GetPressPlayBaseScale(pressPlay);
        pressPlay.gameObject.SetActive(false);
    }

    private void HideAllTrainPressPlayButtons()
    {
        foreach (List<TrainVisualReferences> references in trainVisuals.Values)
        {
            foreach (TrainVisualReferences trainReferences in references)
            {
                ResetPressPlay(trainReferences);
            }
        }
    }

    private void RemovePressPlayListener(Button button)
    {
        if (button == null ||
            !pressPlayListeners.TryGetValue(button, out UnityAction listener))
        {
            return;
        }

        button.onClick.RemoveListener(listener);
        pressPlayListeners.Remove(button);
    }

    private Vector3 GetPressPlayBaseScale(RectTransform pressPlay)
    {
        return pressPlay != null &&
               pressPlayBaseScales.TryGetValue(pressPlay, out Vector3 baseScale)
            ? baseScale
            : Vector3.one;
    }

    private IEnumerator PlayOneShotImageAnimation(GameObject animationObject)
    {
        if (animationObject == null) yield break;

        ImageAnimation imageAnimation = animationObject.GetComponent<ImageAnimation>();
        if (imageAnimation == null ||
            imageAnimation.textureArray == null ||
            imageAnimation.textureArray.Count == 0)
        {
            animationObject.SetActive(true);
            yield return null;
            animationObject.SetActive(false);
            yield break;
        }

        bool completed = false;
        Action<int> completionHandler = _ => completed = true;
        imageAnimation.onLoopComplete += completionHandler;
        imageAnimation.doLoopAnimation = false;

        float duration = imageAnimation.textureArray.Count /
                         Mathf.Max(1f, goldBurstIntroFramesPerSecond);
        imageAnimation.SetLoopDuration(duration);
        animationObject.SetActive(true);
        imageAnimation.StartAnimation();

        float elapsed = 0f;
        float timeout = duration + 0.5f;
        while (!completed && elapsed < timeout && animationObject.activeInHierarchy)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        imageAnimation.onLoopComplete -= completionHandler;
        imageAnimation.StopAnimation();
        imageAnimation.ClearLoopDuration();
        animationObject.SetActive(false);
    }

    private bool TryGetGoldBox(
        GoldBurstPrizePlacement prize,
        out GoldBoxRuntime goldBox)
    {
        goldBox = null;
        if (prize == null || prize.columnCount != 1 ||
            prize.startCol < 0 || prize.startCol >= ReelCount ||
            prize.startRow < 0 || prize.startRow >= RowCount)
        {
            return false;
        }

        switch (prize.rowCount)
        {
            case 1:
                List<GoldBoxRuntime> reelBoxes = oneSlotGoldBoxesByReel[prize.startCol];
                if (reelBoxes == null || prize.startRow >= reelBoxes.Count) return false;
                goldBox = reelBoxes[prize.startRow];
                break;

            case 2:
                if (prize.startCol >= twoSlotGoldBoxes.Count || prize.startRow >= RowCount - 1)
                {
                    return false;
                }

                goldBox = twoSlotGoldBoxes[prize.startCol];
                Vector2 twoSlotPosition = goldBox.visual.anchoredPosition;
                twoSlotPosition.y = prize.startRow == 0 ? topAndMiddleY : middleAndBottomY;
                goldBox.visual.anchoredPosition = twoSlotPosition;
                break;

            case 3:
                if (prize.startRow != 0 || prize.startCol >= threeSlotGoldBoxes.Count)
                {
                    return false;
                }
                goldBox = threeSlotGoldBoxes[prize.startCol];
                break;
        }

        return goldBox?.visual != null && goldBox.amountText != null;
    }

    private void HideGoldBurstSource(
        GoldBurstPrizePlacement prize,
        bool hideFeature = true)
    {
        for (int reelIndex = prize.startCol;
             reelIndex < prize.startCol + prize.columnCount;
             reelIndex++)
        {
            for (int row = prize.startRow; row < prize.startRow + prize.rowCount; row++)
            {
                RectTransform source = GetConversionSourceCell(reelIndex, row);
                if (source == null) continue;

                CanvasGroup sourceGroup = GetConversionCanvasGroup(source.gameObject);
                sourceGroup.alpha = 0f;
                hiddenGoldBurstSources.Add(sourceGroup);
            }
        }

        RectTransform feature = null;
        if (prize.columnCount == 1 && prize.rowCount == 2)
        {
            visibleFeatures.TryGetValue(
                (TwoSlotFeatureType, prize.startRow, prize.startCol, 2, 1),
                out feature);
        }
        else if (prize.columnCount == 1 && prize.rowCount == 3)
        {
            visibleFeatures.TryGetValue(
                (ThreeSlotFeatureType, 0, prize.startCol, 3, 1),
                out feature);
        }

        if (hideFeature && feature != null)
        {
            feature.gameObject.SetActive(false);
            hiddenGoldBurstFeatures.Add(feature);
        }
    }

    private void SetGoldBoxRootActive(int rowCount, bool isActive)
    {
        RectTransform root = rowCount switch
        {
            2 => twoSlotGoldBoxRoot,
            3 => threeSlotGoldBoxRoot,
            _ => oneSlotGoldBoxRoot
        };
        if (root != null) root.gameObject.SetActive(isActive);
    }

    private void HideAllGoldBoxes()
    {
        foreach (GoldBoxRuntime goldBox in allGoldBoxes)
        {
            if (goldBox?.visual == null) continue;

            DOTween.Kill(goldBox.visual);
            goldBox.visual.localScale = goldBox.baseScale;
            goldBox.visual.gameObject.SetActive(false);
        }

        if (oneSlotGoldBoxRoot != null)
        {
            for (int index = 0; index < oneSlotGoldBoxRoot.childCount; index++)
            {
                oneSlotGoldBoxRoot.GetChild(index).gameObject.SetActive(false);
            }
            oneSlotGoldBoxRoot.gameObject.SetActive(false);
        }
        if (twoSlotGoldBoxRoot != null) twoSlotGoldBoxRoot.gameObject.SetActive(false);
        if (threeSlotGoldBoxRoot != null) threeSlotGoldBoxRoot.gameObject.SetActive(false);
    }

    private bool HasVisibleGoldBox()
    {
        return allGoldBoxes.Any(goldBox =>
            goldBox?.visual != null && goldBox.visual.gameObject.activeInHierarchy);
    }

    private void ResetGoldBurstPresentation(bool preserveGoldBoxes = false)
    {
        ResetTrainJourneyPresentation();
        ResetGoldBurstResultPresentation();
        goldBurstCollectionParticles?.ResetParticles();
        isGoldBurstPresentationActive = false;
        deferGoldBurstTriggerBarrelMerge = false;
        isTrainJourneyPlaying = false;
        completedTrainJourneys.Clear();
        goldBurstPressPlayCallback = null;

        StopAndHideImageAnimation(coldBurstRespin);
        StopAndHideImageAnimation(megaGoldBurstRespin);
        StopAndHideImageAnimation(ultimateGoldBurstRespin);
        if (darkBackground != null) darkBackground.SetActive(false);
        if (track != null) track.SetActive(false);
        ResetTrolleyMan();

        HideAllTrainPressPlayButtons();

        if (!preserveGoldBoxes)
        {
            HideAllGoldBoxes();
        }

        foreach (CanvasGroup sourceGroup in hiddenGoldBurstSources)
        {
            if (sourceGroup != null) sourceGroup.alpha = 1f;
        }
        hiddenGoldBurstSources.Clear();

        foreach (RectTransform feature in hiddenGoldBurstFeatures)
        {
            if (feature != null && visibleFeatures.Values.Contains(feature))
            {
                feature.gameObject.SetActive(true);
            }
        }
        hiddenGoldBurstFeatures.Clear();

        foreach (GameObject cell in activeGoldBurstAnimationCells.ToList())
        {
            if (cell != null)
            {
                SetWinboxActive(cell, activeWinAnimationCells.Contains(cell));
                cell.SetActive(activeWinAnimationCells.Contains(cell) ||
                               activeTrainAnimationCells.Contains(cell));
            }
        }
        activeGoldBurstAnimationCells.Clear();

        foreach (KeyValuePair<RectTransform, int> entry in animationColumnSiblingIndices)
        {
            if (entry.Key != null) entry.Key.SetSiblingIndex(entry.Value);
        }
    }

    private static void StopAndHideImageAnimation(GameObject animationObject)
    {
        if (animationObject == null) return;

        ImageAnimation imageAnimation = animationObject.GetComponent<ImageAnimation>();
        if (imageAnimation != null)
        {
            imageAnimation.onLoopComplete = null;
            imageAnimation.StopAnimation();
            imageAnimation.ClearLoopDuration();
        }
        animationObject.SetActive(false);
    }

    private void OnGoldBurstPressPlayClicked(
        TrainPlacement selectedTrain,
        TrainVisualReferences selectedTrainReferences)
    {
        if (isTrainJourneyPlaying ||
            selectedTrain == null ||
            completedTrainJourneys.Contains(selectedTrain))
        {
            return;
        }

        isTrainJourneyPlaying = true;
        if (track != null) track.SetActive(true);
        ResetPressPlay(selectedTrainReferences);
        TMP_Text selectedTrainResultAmount =
            GetTrainResultAmount(selectedTrainReferences);
        if (selectedTrainResultAmount != null)
        {
            DOTween.Kill(selectedTrainResultAmount.rectTransform);
            selectedTrainResultAmount.gameObject.SetActive(false);
        }
        if (CanPlayTrainJourneyPresentation(selectedTrain))
        {
            trainJourneyRoutine = StartCoroutine(
                PlayTrainJourneyPresentation(
                    selectedTrain,
                    selectedTrainReferences,
                    () => OnTrainJourneyComplete(selectedTrain)));
            return;
        }

        OnTrainJourneyComplete(selectedTrain);
    }

    private void OnTrainJourneyComplete(TrainPlacement completedTrain)
    {
        if (completedTrain != null)
        {
            completedTrainJourneys.Add(completedTrain);
        }
        isTrainJourneyPlaying = false;
        if (track != null) track.SetActive(false);

        if (ShowRemainingGoldBurstPressPlayButtons()) return;

        Action callback = goldBurstPressPlayCallback;
        goldBurstPressPlayCallback = null;
        callback?.Invoke();
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

    private static double GetTrainPayout(TrainPlacement train)
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
        TrainVisualReferences selectedTrainReferences,
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
            darkBackground.SetActive(true);
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
                ? capturedStartPosition
                : activeTrainJourneyTrain.anchoredPosition;
        activeTrainJourneyTrain.anchoredPosition = startPosition;

        if (trainJourneySettleDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(trainJourneySettleDelay);
        }

        activeTrainJourneyTrain.gameObject.SetActive(true);
        trainAnimation?.PlayAnimation();

        Tween travelTween = activeTrainJourneyTrain
            .DOAnchorPosX(trainJourneyExitX, travelDuration)
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
            selectedTrainReferences,
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
                trainJourneyExitX,
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

    private static void SetSpriteAmount(TMP_Text target, double amount)
    {
        if (target == null) return;

        string number = Math.Max(0d, amount).ToString("0.00", CultureInfo.InvariantCulture);
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

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
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
            trainJourneyActorStartPosition,
            exitDuration);
        hasExitTween |= AppendTrainJourneyActorExit(
            exitSequence,
            trainJourneySecondActor,
            trainJourneySecondActorStartPosition,
            exitDuration);

        if (hasExitTween)
        {
            yield return exitSequence.WaitForCompletion();
        }
        else if (exitDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(exitDuration);
        }
    }

    private IEnumerator PlayTrainJourneyResultHandoff(
        TrainVisualReferences selectedTrainReferences,
        double totalPayout)
    {
        TMP_Text destinationAmount =
            GetTrainResultAmount(selectedTrainReferences);
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
            selectedTrainReferences,
            destinationAmount,
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
        TrainVisualReferences trainReferences,
        TMP_Text destinationAmount,
        double totalPayout)
    {
        RectTransform destinationRect = destinationAmount.rectTransform;
        Vector3 destinationBaseScale = trainReferences.hasResultAmountBaseScale
            ? trainReferences.resultAmountBaseScale
            : destinationRect.localScale;
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

        darkBackground.SetActive(false);
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

    private static float PlayTrainJourneyActorJump(RectTransform actor)
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
        actorGraphic.AnimationState.SetAnimation(0, jumpAnimation.Name, false);
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
        float exitY = startPosition.y - trainJourneyActorExitDistance;
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
            journeyTrain.gameObject.SetActive(false);
        }

        activeTrainJourneyTrain = null;
        activeTrainJourneyWagonAmounts = null;

        ResetTrainJourneyActor(
            trainJourneyActor,
            trainJourneyActorStartPosition);
        ResetTrainJourneyActor(
            trainJourneySecondActor,
            trainJourneySecondActorStartPosition);

        if (trainJourneyWinBox != null)
        {
            RestoreTrainJourneyWinBoxImage();
            StopAndHideTrainJourneyEffect(trainJourneyLineAnimation);
            StopAndHideTrainJourneyEffect(trainJourneyBoxAnimation);
            trainJourneyWinBox.gameObject.SetActive(false);
        }

        ReleaseTrainJourneyDarkBackground();
    }

    private void ResetTrainJourneyActor(
        RectTransform actor,
        Vector2 startPosition)
    {
        if (actor == null) return;

        DOTween.Kill(actor);
        if (hasCapturedTrainJourneyState)
        {
            actor.anchoredPosition = startPosition;
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

    private bool TryGetTrainPosition(TrainPlacement train, out Vector2 position)
    {
        position = default;
        float[] xPositions;
        float y;

        switch (train.type)
        {
            case TrainVisualType.Green:
                if (train.rowCount != 2 || train.columnCount != 2) return false;
                xPositions = greenTrainXByStartColumn;
                y = train.startRow == 0 ? greenTrainTopY : greenTrainBottomY;
                break;

            case TrainVisualType.Red:
                if (train.rowCount != 2 || train.columnCount != 4) return false;
                xPositions = redTrainXByStartColumn;
                y = train.startRow == 0 ? redTrainTopY : redTrainBottomY;
                break;

            case TrainVisualType.HorizontalPurple:
                if (train.rowCount != 2 || train.columnCount != 3) return false;
                xPositions = horizontalPurpleTrainXByStartColumn;
                y = train.startRow == 0
                    ? horizontalPurpleTrainTopY
                    : horizontalPurpleTrainBottomY;
                break;

            case TrainVisualType.VerticalPurple:
                if (train.rowCount != 3 || train.columnCount != 2 || train.startRow != 0) return false;
                xPositions = verticalPurpleTrainXByStartColumn;
                y = verticalPurpleTrainY;
                break;

            case TrainVisualType.Golden:
                if (train.rowCount != 3 || train.columnCount != 4 || train.startRow != 0) return false;
                xPositions = goldenTrainXByStartColumn;
                y = goldenTrainY;
                break;

            default:
                return false;
        }

        if (xPositions == null ||
            train.startCol < 0 || train.startCol >= xPositions.Length)
        {
            return false;
        }

        position = new Vector2(xPositions[train.startCol], y);
        return true;
    }

    private void ValidateTrainVisualPool(TrainVisualType type)
    {
        int availableCount = trainVisuals.TryGetValue(
            type,
            out List<TrainVisualReferences> visuals)
            ? visuals.Count
            : 0;
        int requiredCount = GetMaximumVisibleTrainCount(type);
        if (availableCount < requiredCount)
        {
            ReportConfigurationWarning(
                $"The {type} train setup requires {requiredCount} reusable visual" +
                $"{(requiredCount == 1 ? string.Empty : "s")}, but only {availableCount} were found.");
        }
    }

    private static int GetMaximumVisibleTrainCount(TrainVisualType type)
    {
        switch (type)
        {
            case TrainVisualType.Green:
                return MaxGreenTrains;
            case TrainVisualType.Red:
                return MaxRedTrains;
            case TrainVisualType.HorizontalPurple:
                return MaxHorizontalPurpleTrains;
            case TrainVisualType.VerticalPurple:
                return MaxVerticalPurpleTrains;
            case TrainVisualType.Golden:
                return MaxGoldenTrains;
            default:
                return 0;
        }
    }

    private RectTransform GetTrainRoot(TrainVisualType type)
    {
        trainRoots.TryGetValue(type, out RectTransform root);
        return root;
    }

    private void CaptureAuthoredVerticalPositions()
    {
        if (twoSlotBarrels.Count == 0) return;

        float lowestY = twoSlotBarrels.Min(barrel => barrel.anchoredPosition.y);
        float highestY = twoSlotBarrels.Max(barrel => barrel.anchoredPosition.y);

        if (highestY - lowestY > 1f)
        {
            topAndMiddleY = highestY;
            middleAndBottomY = lowestY;
        }
    }

    private void HideAnimationCells(int reelIndex, int startRow, int count)
    {
        for (int row = startRow; row < startRow + count; row++)
        {
            RectTransform animationCell = GetAnimationCell(reelIndex, row);
            GameObject coveredCell = animationCell.gameObject;
            SetWinboxActive(animationCell, false);
            coveredCell.SetActive(false);
            hiddenAnimationCells.Add(coveredCell);
        }
    }

    private void RefreshAnimationHierarchy()
    {
        if (animationRoot == null) return;

        bool hasActiveAnimationCell = false;
        for (int reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            RectTransform column = GetAnimationColumn(reelIndex);
            bool hasActiveCell = false;
            for (int row = 0; row < RowCount; row++)
            {
                RectTransform cell = GetAnimationCell(reelIndex, row);
                hasActiveCell |= cell != null &&
                                 IsAnimationCellActive(cell.gameObject) &&
                                 cell.gameObject.activeSelf;
            }

            if (column != null) column.gameObject.SetActive(hasActiveCell);
            hasActiveAnimationCell |= hasActiveCell;
        }

        bool hasActiveFeature = visibleFeatures.Values.Any(visual =>
            visual != null && visual.gameObject.activeSelf);
        animationRoot.gameObject.SetActive(
            hasActiveAnimationCell || hasActiveFeature || HasVisibleGoldBox());
    }

    private bool IsAnimationCellActive(GameObject animationCell)
    {
        return animationCell != null &&
               (activeWinAnimationCells.Contains(animationCell) ||
                activeTrainAnimationCells.Contains(animationCell) ||
                activeGoldBurstAnimationCells.Contains(animationCell));
    }

    private void SetWinboxActive(RectTransform animationCell, bool isActive)
    {
        SetWinboxActive(animationCell != null ? animationCell.gameObject : null, isActive);
    }

    private void SetWinboxActive(GameObject animationCell, bool isActive)
    {
        if (animationCell != null &&
            winboxByAnimationCell.TryGetValue(animationCell, out RectTransform winbox) &&
            winbox != null)
        {
            winbox.gameObject.SetActive(isActive);
        }
    }

    private bool HasTwoSlotConfiguration()
    {
        bool isComplete = twoSlotBarrelRoot != null &&
                          twoSlotBarrels.Count == ReelCount &&
                          HasCompleteAnimationGrid();

        if (!isComplete)
        {
            ReportConfigurationWarning(
                "The 2x1 barrel setup requires five barrels and five animation columns with three cells each.");
        }

        return isComplete;
    }

    private bool HasThreeSlotConfiguration()
    {
        bool isComplete = threeSlotBarrelRoot != null &&
                          threeSlotBarrels.Count == ReelCount &&
                          HasCompleteAnimationGrid();

        if (!isComplete)
        {
            ReportConfigurationWarning(
                "The 3x1 barrel setup requires five barrels and five animation columns with three cells each.");
        }

        return isComplete;
    }

    private bool HasCompleteAnimationGrid()
    {
        return animationGrid != null &&
               animationGrid.Length == ReelCount &&
               animationGrid.All(column =>
                   column != null &&
                   column.column != null &&
                   column.rows != null &&
                   column.rows.Length == RowCount &&
                   column.rows.All(cell =>
                       cell != null && cell.slot != null && cell.winbox != null));
    }

    private void EnsureInitialized()
    {
        if (!isInitialized) CacheSceneObjects();
    }

    private void ReportConfigurationWarning(string message)
    {
        if (configurationWarningShown) return;

        configurationWarningShown = true;
        Debug.LogWarning($"[SlotFeatureController] {message}", this);
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindDescendant(root.GetChild(index), objectName);
            if (result != null) return result;
        }

        return null;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        Transform match = Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.name == objectName);
        return match != null ? match.gameObject : null;
    }
}
