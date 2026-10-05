using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal interface IGoldBurstRespinController
{
    GoldBurstTier Tier { get; }
    int ReelCount { get; }
    bool PreserveAnimationHierarchyOrder { get; }
    bool UsesDeterministicTrainVisualMapping { get; }
    IEnumerator PlayIntro();
    void ResetIntro();
    Action GetLayoutActivation(SlotView slotView);
    int GetTrainVisualIndex(TrainPlacement placement, int visualCount);
    bool ContainsPlacement(int startColumn, int columnCount);
}

/// <summary>
/// Owns feature visuals that sit on top of the reel presentation.
/// Handles reusable 2x1 and 3x1 barrels and pooled train visuals.
/// </summary>
public class SlotFeatureController : TransitionController
{
    private const int BaseReelCount = 5;
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
    [SerializeField] private ColdGoldBurstRespinController coldBurstController;
    [SerializeField] private MegaGoldBurstRespinController megaGoldBurstController;
    [SerializeField] private UltimateGoldBurstRespinController ultimateGoldBurstController;
    [SerializeField] private RectTransform oneSlotGoldBoxRoot;
    [SerializeField] private RectTransform twoSlotGoldBoxRoot;
    [SerializeField] private RectTransform threeSlotGoldBoxRoot;
    [SerializeField] private PrizeTrailController goldBurstCollectionParticles;
    [SerializeField, Min(0f)] private float goldBurstMergeDelayAfterTrolley = 0.5f;
    [SerializeField, Min(0f)] private float goldBoxHoldWithoutTrain = 1.2f;

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

    [Header("Press Play Animation")]
    [SerializeField, Min(1f)] private float pressPlayPopupScale = 1.12f;
    [SerializeField, Min(0.01f)] private float pressPlayPopupDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float pressPlaySettleDuration = 0.12f;
    [SerializeField, Range(0.8f, 1f)] private float pressPlayHeartbeatSmallScale = 0.98f;
    [SerializeField, Min(1f)] private float pressPlayHeartbeatLargeScale = 1.04f;
    [SerializeField, Min(0.05f)] private float pressPlayHeartbeatHalfCycle = 0.45f;

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
    private List<GoldBoxRuntime>[] oneSlotGoldBoxesByReel =
        new List<GoldBoxRuntime>[BaseReelCount];
    private readonly List<GoldBoxRuntime> twoSlotGoldBoxes = new List<GoldBoxRuntime>();
    private readonly List<GoldBoxRuntime> threeSlotGoldBoxes = new List<GoldBoxRuntime>();
    private readonly HashSet<CanvasGroup> hiddenGoldBurstSources = new HashSet<CanvasGroup>();
    private readonly HashSet<RectTransform> hiddenGoldBurstFeatures = new HashSet<RectTransform>();
    private readonly HashSet<RectTransform> goldBurstSourceAnimationCells =
        new HashSet<RectTransform>();
    private readonly Dictionary<Image, bool> animationCellImageEnabledStates =
        new Dictionary<Image, bool>();
    private readonly Dictionary<Image, Color> animationCellImageColors =
        new Dictionary<Image, Color>();
    private AnimationColumnReferences[] animationGrid =
        Array.Empty<AnimationColumnReferences>();
    private Transform searchRoot;
    private IReadOnlyList<ReelResultSlots> conversionSourceSlots;
    private int activeReelCount = BaseReelCount;
    private bool isInitialized;
    private bool configurationWarningShown;
    private bool isGoldBurstPresentationActive;
    private bool deferGoldBurstTriggerBarrelMerge;
    private bool isTrainJourneyPlaying;
    private Action goldBurstPressPlayCallback;
    private Coroutine featureRevealRoutine;
    internal void Initialize(
        Transform slotRoot,
        IReadOnlyList<ReelResultSlots> resultSlotsByReel,
        int reelCount = BaseReelCount,
        RectTransform configuredAnimationRoot = null)
    {
        if (isInitialized)
        {
            ResetFeatures();
        }

        searchRoot = slotRoot != null ? slotRoot : searchRoot;
        conversionSourceSlots = resultSlotsByReel;
        activeReelCount = Mathf.Max(1, reelCount);
        oneSlotGoldBoxesByReel = new List<GoldBoxRuntime>[activeReelCount];
        if (configuredAnimationRoot != null)
        {
            ConfigureLayoutReferences(configuredAnimationRoot);
        }

        isInitialized = false;
        configurationWarningShown = false;
        CacheSceneObjects();
        ResetFeatures();
    }

    private void ConfigureLayoutReferences(RectTransform configuredAnimationRoot)
    {
        animationRoot = configuredAnimationRoot;
        twoSlotBarrelRoot = FindDescendant(animationRoot, "2SlotBarrels") as RectTransform;
        threeSlotBarrelRoot = FindDescendant(animationRoot, "3SlotBarrels") as RectTransform;
        greenTrainRoot = FindDescendant(animationRoot, "GreenTrains") as RectTransform;
        redTrainRoot = FindDescendant(animationRoot, "RedTrains") as RectTransform;
        horizontalPurpleTrainRoot =
            FindDescendant(animationRoot, "HorizontalPurpleTrains") as RectTransform;
        verticalPurpleTrainRoot =
            FindDescendant(animationRoot, "VerticalPurpleTrains") as RectTransform;
        goldenTrainRoot = FindDescendant(animationRoot, "GoldenTrains") as RectTransform;
        oneSlotGoldBoxRoot =
            FindDescendantStartingWith(animationRoot, "1SlotGoldBox") as RectTransform;
        twoSlotGoldBoxRoot =
            FindDescendantStartingWith(animationRoot, "2SlotGoldBox") as RectTransform;
        threeSlotGoldBoxRoot =
            FindDescendantStartingWith(animationRoot, "3SlotGoldBox") as RectTransform;

        animationGrid = BuildConfiguredAnimationGrid(
            conversionSourceSlots,
            activeReelCount);
        greenTrainVisuals = DiscoverTrainVisualReferences(greenTrainRoot);
        redTrainVisuals = DiscoverTrainVisualReferences(redTrainRoot);
        horizontalPurpleTrainVisuals =
            DiscoverTrainVisualReferences(horizontalPurpleTrainRoot);
        verticalPurpleTrainVisuals =
            DiscoverTrainVisualReferences(verticalPurpleTrainRoot);
        goldenTrainVisuals = DiscoverTrainVisualReferences(goldenTrainRoot);
    }

    private static AnimationColumnReferences[] BuildConfiguredAnimationGrid(
        IReadOnlyList<ReelResultSlots> configuredSlots,
        int reelCount)
    {
        if (configuredSlots == null || configuredSlots.Count < reelCount)
        {
            return Array.Empty<AnimationColumnReferences>();
        }

        var configuredGrid = new AnimationColumnReferences[reelCount];
        for (int reelIndex = 0; reelIndex < reelCount; reelIndex++)
        {
            ReelResultSlots reelSlots = configuredSlots[reelIndex];
            var rows = new AnimationCellReferences[RowCount];
            for (int row = 0; row < RowCount; row++)
            {
                rows[row] = new AnimationCellReferences
                {
                    slot = reelSlots?.GetAnimationSlot(row),
                    winbox = reelSlots?.GetAnimationWinbox(row)
                };
            }

            configuredGrid[reelIndex] = new AnimationColumnReferences
            {
                column = rows[0].slot?.parent as RectTransform,
                rows = rows
            };
        }

        return configuredGrid;
    }

    private TrainVisualReferences[] DiscoverTrainVisualReferences(
        RectTransform root)
    {
        if (root == null) return Array.Empty<TrainVisualReferences>();

        return GetLayoutOrderedChildren(root)
            .Select(visual =>
            {
                Transform[] descendants =
                    visual.GetComponentsInChildren<Transform>(true);
                Transform mask = descendants.FirstOrDefault(candidate =>
                    candidate != visual &&
                    candidate.name.Trim().StartsWith(
                        "Mask",
                        StringComparison.OrdinalIgnoreCase));
                Transform pressPlay = descendants.FirstOrDefault(candidate =>
                    candidate != visual &&
                    candidate.name.Replace(" ", string.Empty).StartsWith(
                        "PressPlay",
                        StringComparison.OrdinalIgnoreCase));
                return new TrainVisualReferences
                {
                    visual = visual,
                    mask = mask != null ? mask.gameObject : null,
                    pressPlay = pressPlay as RectTransform
                };
            })
            .ToArray();
    }

    internal void PrepareTwoSlotBarrels(IReadOnlyList<TwoSlotBarrelPlacement> placements)
    {
        EnsureInitialized();
        pendingStartRowByReel.Clear();

        if (placements == null) return;

        foreach (TwoSlotBarrelPlacement placement in placements)
        {
            if (placement == null ||
                placement.reelIndex < 0 || placement.reelIndex >= activeReelCount ||
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
            if (placement == null || placement.reelIndex < 0 || placement.reelIndex >= activeReelCount)
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
                placement.startCol + placement.columnCount > activeReelCount ||
                !IsPlacementWithinOneExpandedBoard(
                    placement.startCol,
                    placement.columnCount) ||
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

    internal bool RevealFeaturesForReel(int reelIndex)
    {
        EnsureInitialized();
        bool revealedTrain = false;

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
                revealedTrain |= RevealTrain(train);
            }
        }

        return revealedTrain;
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
        if (activeReelCount > BaseReelCount)
        {
            TryPlaceVisualAtConversionCenter(barrel, reelIndex, 1, startRow, 2);
        }
        else
        {
            Vector2 barrelPosition = barrel.anchoredPosition;
            barrelPosition.y = startRow == 0 ? topAndMiddleY : middleAndBottomY;
            barrel.anchoredPosition = barrelPosition;
        }

        AudioManager.Instance?.PlayGoldMineBarrelsMerge();
        PlayConversionFade(reelIndex, 1, startRow, 2, barrel);
        visibleFeatures[(TwoSlotFeatureType, startRow, reelIndex, 2, 1)] = barrel;
    }

    private void RevealThreeSlotBarrel(int reelIndex)
    {
        if (!HasThreeSlotConfiguration()) return;

        RectTransform barrel = threeSlotBarrels[reelIndex];
        if (activeReelCount > BaseReelCount)
        {
            TryPlaceVisualAtConversionCenter(barrel, reelIndex, 1, 0, RowCount);
        }
        AudioManager.Instance?.PlayGoldMineBarrelsMerge();
        PlayConversionFade(
            reelIndex,
            1,
            0,
            RowCount,
            barrel);
        visibleFeatures[(ThreeSlotFeatureType, 0, reelIndex, RowCount, 1)] =
            barrel;
    }

    private bool RevealTrain(TrainPlacement train)
    {
        RectTransform trainRoot = GetTrainRoot(train.type);
        TrainVisualReferences trainReferences = GetAvailableTrainVisual(train);
        RectTransform trainVisual = trainReferences?.visual;
        bool hasPosition = trainVisual != null &&
                           (activeReelCount > BaseReelCount
                               ? TryPlaceVisualAtConversionCenter(
                                   trainVisual,
                                   train.startCol,
                                   train.columnCount,
                                   train.startRow,
                                   train.rowCount)
                               : TryGetTrainPosition(train, out _));
        if (trainVisual == null || trainRoot == null || !hasPosition ||
            !HasCompleteConversionSourceGrid())
        {
            ReportConfigurationWarning(
                $"No reusable {train.type} train visual or position is available for " +
                $"row {train.startRow}, column {train.startCol}.");
            return false;
        }

        PrepareTrainVisualForReveal(trainReferences);
        if (activeReelCount > BaseReelCount)
        {
            TryPlaceVisualAtConversionCenter(
                trainVisual,
                train.startCol,
                train.columnCount,
                train.startRow,
                train.rowCount);
        }
        else if (TryGetTrainPosition(train, out Vector2 position))
        {
            trainVisual.anchoredPosition = position;
        }
        trainVisualRestingPositions[trainVisual] = trainVisual.anchoredPosition;
        var trainKey = ((int)train.type, train.startRow, train.startCol,
            train.rowCount, train.columnCount);
        AudioManager.Instance?.PlayGoldMineSlotTrainConversion();
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
        return true;
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

    internal bool IsFeatureMergeInProgress()
    {
        return featureRevealRoutine != null || DOTween.IsTweening(this);
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
        for (int reelIndex = 0; reelIndex < activeReelCount; reelIndex++)
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
        ResetFreeGamesTransition();
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

    internal IEnumerator PlayGoldBurstTriggerPresentation(
        GoldBurstTier tier,
        Action afterTrolley,
        Func<bool> applyEntryMatrix)
    {
        EnsureInitialized();
        if (!isGoldBurstPresentationActive)
        {
            BeginGoldBurstTriggerPresentation();
        }

        IGoldBurstRespinController respinController =
            GetGoldBurstRespinController(tier);
        if (respinController != null)
        {
            // In portrait, fully hide the reels while the respin intro plays.
            // The following transition call restores the authored background alpha.
            SetDarkBackgroundActive(true, forceOpaque: true);
            AudioManager.Instance?.PlayGoldMineRespinBg();
            yield return respinController.PlayIntro();
        }

        SetDarkBackgroundActive(true, showInLandscape: true);
        yield return PlayTrolleyManAnimation();

        afterTrolley?.Invoke();
        if (applyEntryMatrix != null && !applyEntryMatrix())
        {
            Debug.LogError(
                "[SlotFeatureController] Server-provided Gold Burst entry matrix could not be applied.",
                this);
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        SetDarkBackgroundActive(false);

        if (goldBurstMergeDelayAfterTrolley > 0f)
        {
            yield return new WaitForSecondsRealtime(goldBurstMergeDelayAfterTrolley);
        }

        deferGoldBurstTriggerBarrelMerge = false;
        bool hasPreparedFeatureMerge = pendingThreeSlotReels.Count > 0 ||
                                       pendingStartRowByReel.Count > 0 ||
                                       pendingTrains.Count > 0;
        RevealAllFeatures();

        if (hasPreparedFeatureMerge)
        {
            while (IsFeatureMergeInProgress())
            {
                yield return null;
            }
        }
    }

    internal Action GetGoldBurstLayoutActivation(
        GoldBurstTier tier,
        SlotView slotView)
    {
        return GetGoldBurstRespinController(tier)
            ?.GetLayoutActivation(slotView);
    }

    internal List<GoldBurstPrizePlacement> GetGoldBurstTriggerSingleSlotBarrels(
        IReadOnlyList<List<int>> matrix)
    {
        EnsureInitialized();
        var placements = new List<GoldBurstPrizePlacement>();
        var coveredCells = new bool[activeReelCount, RowCount];

        foreach (int reelIndex in pendingThreeSlotReels.OrderBy(index => index))
        {
            if (reelIndex < 0 || reelIndex >= activeReelCount) continue;

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
            if (reelIndex < 0 || reelIndex >= activeReelCount ||
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
             reelIndex < Mathf.Min(activeReelCount, matrix.Count);
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
        if (goldBurstCollectionParticles != null)
        {
            goldBurstCollectionParticles.ResetParticles();
        }
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
        HashSet<RectTransform> goldBoxSources = new HashSet<RectTransform>();
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
                goldBoxSources.Add(goldBox.visual);
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
            },
            startedSourceIndex =>
            {
                if (startedSourceIndex < 0 ||
                    startedSourceIndex >= orderedSources.Count ||
                    !goldBoxSources.Contains(orderedSources[startedSourceIndex]))
                {
                    return;
                }

                AudioManager.Instance?.PlayGoldMineSparkle();
            });
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
        if (TryGetUsableAnimationCell(
                reelIndex,
                row,
                false,
                out animationCell,
                out RectTransform column) &&
            animationCell.IsChildOf(animationRoot))
        {
            activeGoldBurstAnimationCells.Add(animationCell.gameObject);
            SetWinboxActive(animationCell, false);
            animationRoot.gameObject.SetActive(true);
            column.gameObject.SetActive(true);
            animationCell.gameObject.SetActive(true);
            return true;
        }

        animationCell = GetConversionSourceCell(reelIndex, row);
        if (animationCell == null)
        {
            return false;
        }

        goldBurstSourceAnimationCells.Add(animationCell);
        animationCell.gameObject.SetActive(true);
        return true;
    }

    internal bool IsGoldBurstSourceAnimationCell(RectTransform animationCell)
    {
        return animationCell != null &&
               goldBurstSourceAnimationCells.Contains(animationCell);
    }

    internal bool TryGetGoldBurstBarrelAnimationTarget(
        int reelIndex,
        int startRow,
        int rowCount,
        out RectTransform barrel)
    {
        EnsureInitialized();
        barrel = null;

        if (reelIndex < 0 || reelIndex >= activeReelCount ||
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
        return reelIndex >= 0 && reelIndex < activeReelCount &&
               row >= 0 && row < RowCount &&
               IsCellCoveredByPendingFeature(reelIndex, row);
    }

    internal void ReleaseGoldBurstAnimationCell(RectTransform animationCell)
    {
        if (animationCell == null) return;
        if (goldBurstSourceAnimationCells.Remove(animationCell))
        {
            animationCell.gameObject.SetActive(true);
            return;
        }

        activeGoldBurstAnimationCells.Remove(animationCell.gameObject);
        bool keepCellActive = activeWinAnimationCells.Contains(animationCell.gameObject) ||
                              activeTrainAnimationCells.Contains(animationCell.gameObject);
        if (!keepCellActive)
        {
            RestoreAnimationCellVisualState(animationCell);
        }
        SetWinboxActive(
            animationCell,
            activeWinAnimationCells.Contains(animationCell.gameObject));
        animationCell.gameObject.SetActive(keepCellActive);

        RefreshAnimationHierarchy();
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
        if (!keepCellActive)
        {
            RestoreAnimationCellVisualState(animationCell);
        }
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
        SetWinboxActive(animationCell, false);
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
            reelIndex < 0 || reelIndex >= activeReelCount ||
            row < 0 || row >= RowCount ||
            (rejectCoveredFeature &&
             IsCellCoveredByPendingFeature(reelIndex, row)))
        {
            return false;
        }

        animationCell = GetAnimationCell(reelIndex, row);
        column = GetAnimationColumn(reelIndex);
        if (animationCell != null && !IsAnimationCellActive(animationCell.gameObject))
        {
            RestoreAnimationCellVisualState(animationCell);
        }
        return animationCell != null && column != null;
    }

    internal void ReleaseTrainAnimationCell(RectTransform animationCell)
    {
        if (animationCell == null) return;

        activeTrainAnimationCells.Remove(animationCell.gameObject);
        bool keepCellActive = activeWinAnimationCells.Contains(animationCell.gameObject);
        if (!keepCellActive)
        {
            RestoreAnimationCellVisualState(animationCell);
        }
        SetWinboxActive(animationCell, keepCellActive);
        animationCell.gameObject.SetActive(keepCellActive);
        RefreshAnimationHierarchy();
    }

    internal void ResetAnimationPoolForGoldBurstRespin()
    {
        EnsureInitialized();

        activeWinAnimationCells.Clear();
        activeTrainAnimationCells.Clear();
        activeGoldBurstAnimationCells.Clear();
        goldBurstSourceAnimationCells.Clear();

        if (animationGrid != null)
        {
            foreach (AnimationColumnReferences columnReferences in animationGrid)
            {
                if (columnReferences?.rows == null) continue;

                foreach (AnimationCellReferences cellReferences in columnReferences.rows)
                {
                    RectTransform cell = cellReferences?.slot;
                    if (cell == null) continue;

                    ImageAnimation animation = cell.GetComponent<ImageAnimation>();
                    if (animation != null)
                    {
                        animation.onLoopComplete = null;
                        animation.onFrameDisplayed = null;
                        animation.doLoopAnimation = false;
                        animation.StopAnimation();
                        animation.ClearLoopDuration();
                    }

                    RestoreAnimationCellVisualState(cell);
                    SetWinboxActive(cell, false);
                    cell.gameObject.SetActive(false);
                }
            }
        }

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
                GetLayoutOrderedChildren(twoSlotBarrelRoot));
            foreach (RectTransform barrel in twoSlotBarrels)
            {
                if (barrel != null) barrel.gameObject.SetActive(false);
            }
            twoSlotBarrelRoot.gameObject.SetActive(false);
            CaptureAuthoredVerticalPositions();
        }

        threeSlotBarrels.Clear();
        if (threeSlotBarrelRoot != null)
        {
            threeSlotBarrels.AddRange(
                GetLayoutOrderedChildren(threeSlotBarrelRoot));
            foreach (RectTransform barrel in threeSlotBarrels)
            {
                if (barrel != null) barrel.gameObject.SetActive(false);
            }
            threeSlotBarrelRoot.gameObject.SetActive(false);
        }

        CacheTrainVisuals();

        CacheAnimationGrid();
        CacheGoldBoxPools();

        isInitialized = true;
        HasTwoSlotConfiguration();
        HasThreeSlotConfiguration();
    }

    private void CacheGoldBurstSceneObjects()
    {
        CacheTransitionSceneObjects();
        coldBurstController = FindGoldBurstRespinController(
            coldBurstController,
            "ColdBurstRespin");
        megaGoldBurstController = FindGoldBurstRespinController(
            megaGoldBurstController,
            "MegaGoldBurstRespin");
        ultimateGoldBurstController = FindGoldBurstRespinController(
            ultimateGoldBurstController,
            "UltimateGoldBurstRespin");

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

    private T FindGoldBurstRespinController<T>(
        T controller,
        string presentationObjectName)
        where T : Component
    {
        if (controller != null) return controller;

        GameObject presentationObject =
            FindSceneGameObject(presentationObjectName);
        if (presentationObject == null) return null;

        return presentationObject.GetComponent<T>();
    }

    private IGoldBurstRespinController GetGoldBurstRespinController(
        GoldBurstTier tier)
    {
        return tier switch
        {
            GoldBurstTier.Mega => megaGoldBurstController,
            GoldBurstTier.Ultimate => ultimateGoldBurstController,
            _ => coldBurstController
        };
    }

    private IGoldBurstRespinController GetActiveGoldBurstRespinController()
    {
        if (ultimateGoldBurstController != null &&
            activeReelCount == ultimateGoldBurstController.ReelCount)
        {
            return ultimateGoldBurstController;
        }
        if (megaGoldBurstController != null &&
            activeReelCount == megaGoldBurstController.ReelCount)
        {
            return megaGoldBurstController;
        }
        return coldBurstController;
    }

    private void CacheGoldBoxPools()
    {
        allGoldBoxes.Clear();
        twoSlotGoldBoxes.Clear();
        threeSlotGoldBoxes.Clear();
        for (int reelIndex = 0; reelIndex < activeReelCount; reelIndex++)
        {
            oneSlotGoldBoxesByReel[reelIndex] = new List<GoldBoxRuntime>();
        }

        if (oneSlotGoldBoxRoot != null)
        {
            List<RectTransform> reelColumns =
                GetLayoutOrderedChildren(oneSlotGoldBoxRoot);
            for (int reelIndex = 0;
                 reelIndex < Mathf.Min(activeReelCount, reelColumns.Count);
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

        foreach (RectTransform child in
                 GetLayoutOrderedChildren(root).Take(activeReelCount))
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
        List<RectTransform> children = GetChildrenInHierarchyOrder(root);

        return sortLeftToRight
            ? children.OrderBy(child => child.anchoredPosition.x).ToList()
            : children.OrderByDescending(child => child.anchoredPosition.y).ToList();
    }

    private static List<RectTransform> GetChildrenInHierarchyOrder(
        RectTransform root)
    {
        var children = new List<RectTransform>();
        if (root == null) return children;

        for (int index = 0; index < root.childCount; index++)
        {
            if (root.GetChild(index) is RectTransform child)
            {
                children.Add(child);
            }
        }

        return children;
    }

    private List<RectTransform> GetLayoutOrderedChildren(RectTransform root)
    {
        if (root == null) return new List<RectTransform>();

        List<RectTransform> children = GetChildrenInHierarchyOrder(root);

        if (GetActiveGoldBurstRespinController()
            ?.PreserveAnimationHierarchyOrder == true)
        {
            // The authored Two7x3Animation hierarchy is intentional: its first
            // half belongs to the top board and its second half to the bottom.
            return children;
        }

        return children
            .OrderBy(child => child.anchoredPosition.x)
            .ToList();
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
        if (animationGrid == null) return;

        foreach (AnimationColumnReferences columnReferences in animationGrid)
        {
            if (columnReferences == null) continue;

            if (columnReferences.column != null)
            {
                columnReferences.column.gameObject.SetActive(false);
            }

            if (columnReferences.rows == null) continue;

            foreach (AnimationCellReferences cellReferences in columnReferences.rows)
            {
                if (cellReferences == null) continue;

                if (cellReferences.slot != null)
                {
                    CacheAnimationCellState(cellReferences.slot);
                    RestoreAnimationCellVisualState(cellReferences.slot);
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

    private void CacheAnimationCellState(RectTransform cell)
    {
        if (cell == null) return;

        Image image = cell.GetComponent<Image>();
        if (image == null || animationCellImageEnabledStates.ContainsKey(image)) return;

        animationCellImageEnabledStates[image] = image.enabled;
        animationCellImageColors[image] = image.color;
    }

    private void RestoreAnimationCellVisualState(RectTransform cell)
    {
        if (cell == null) return;

        Image image = cell.GetComponent<Image>();
        if (image == null) return;

        if (animationCellImageEnabledStates.TryGetValue(image, out bool imageEnabled))
        {
            image.enabled = imageEnabled;
        }
        if (animationCellImageColors.TryGetValue(image, out Color imageColor))
        {
            image.color = imageColor;
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

                trainReferences.visual.gameObject.SetActive(false);
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

    private TrainVisualReferences GetAvailableTrainVisual(TrainPlacement placement)
    {
        if (placement == null ||
            !trainVisuals.TryGetValue(
                placement.type,
                out List<TrainVisualReferences> visuals))
        {
            return null;
        }

        IGoldBurstRespinController layoutController =
            GetActiveGoldBurstRespinController();
        int mappedVisualIndex = layoutController?.GetTrainVisualIndex(
            placement,
            visuals.Count) ?? -1;
        if (layoutController?.UsesDeterministicTrainVisualMapping == true)
        {
            if (mappedVisualIndex < 0 || mappedVisualIndex >= visuals.Count)
            {
                return null;
            }
            TrainVisualReferences mappedVisual = visuals[mappedVisualIndex];
            return mappedVisual?.visual != null &&
                   !mappedVisual.visual.gameObject.activeSelf
                ? mappedVisual
                : null;
        }

        for (int index = 0; index < visuals.Count; index++)
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

    private bool IsPlacementWithinOneExpandedBoard(
        int startColumn,
        int columnCount)
    {
        return GetActiveGoldBurstRespinController()
                   ?.ContainsPlacement(startColumn, columnCount) ?? true;
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

    private bool TryGetGoldBox(
        GoldBurstPrizePlacement prize,
        out GoldBoxRuntime goldBox)
    {
        goldBox = null;
        if (prize == null || prize.columnCount != 1 ||
            prize.startCol < 0 || prize.startCol >= activeReelCount ||
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
                if (activeReelCount > BaseReelCount)
                {
                    TryPlaceVisualAtConversionCenter(
                        goldBox.visual,
                        prize.startCol,
                        1,
                        prize.startRow,
                        2);
                }
                else
                {
                    Vector2 twoSlotPosition = goldBox.visual.anchoredPosition;
                    twoSlotPosition.y = prize.startRow == 0
                        ? topAndMiddleY
                        : middleAndBottomY;
                    goldBox.visual.anchoredPosition = twoSlotPosition;
                }
                break;

            case 3:
                if (prize.startRow != 0 || prize.startCol >= threeSlotGoldBoxes.Count)
                {
                    return false;
                }
                goldBox = threeSlotGoldBoxes[prize.startCol];
                if (activeReelCount > BaseReelCount)
                {
                    TryPlaceVisualAtConversionCenter(
                        goldBox.visual,
                        prize.startCol,
                        1,
                        0,
                        RowCount);
                }
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
        ResetTrainJourneyTransition();
        ResetGoldBurstSharedTransitions();
        if (goldBurstCollectionParticles != null)
        {
            goldBurstCollectionParticles.ResetParticles();
        }
        isGoldBurstPresentationActive = false;
        deferGoldBurstTriggerBarrelMerge = false;
        isTrainJourneyPlaying = false;
        completedTrainJourneys.Clear();
        goldBurstPressPlayCallback = null;

        coldBurstController?.ResetIntro();
        megaGoldBurstController?.ResetIntro();
        ultimateGoldBurstController?.ResetIntro();

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
        goldBurstSourceAnimationCells.Clear();

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
        Vector3 resultAmountBaseScale =
            selectedTrainReferences.hasResultAmountBaseScale
                ? selectedTrainReferences.resultAmountBaseScale
                : selectedTrainResultAmount != null
                    ? selectedTrainResultAmount.rectTransform.localScale
                    : Vector3.one;
        if (TryStartTrainJourneyPresentation(
                selectedTrain,
                selectedTrainResultAmount,
                resultAmountBaseScale,
                () => OnTrainJourneyComplete(selectedTrain)))
        {
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

    private bool TryPlaceVisualAtConversionCenter(
        RectTransform visual,
        int startReel,
        int reelCount,
        int startRow,
        int rowCount)
    {
        if (visual == null || reelCount <= 0 || rowCount <= 0) return false;

        Vector3 worldPositionTotal = Vector3.zero;
        int sourceCount = 0;
        for (int reelIndex = startReel;
             reelIndex < startReel + reelCount;
             reelIndex++)
        {
            for (int row = startRow; row < startRow + rowCount; row++)
            {
                RectTransform source = GetConversionSourceCell(reelIndex, row);
                if (source == null) continue;
                worldPositionTotal += source.position;
                sourceCount++;
            }
        }

        if (sourceCount == 0) return false;
        visual.position = worldPositionTotal / sourceCount;
        return true;
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
            if (animationCell == null) continue;
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
        for (int reelIndex = 0; reelIndex < activeReelCount; reelIndex++)
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
                          twoSlotBarrels.Count >= activeReelCount &&
                          HasCompleteConversionSourceGrid();

        if (!isComplete)
        {
            ReportConfigurationWarning(
                $"The 2x1 barrel setup requires {activeReelCount} barrels and three result cells per reel.");
        }

        return isComplete;
    }

    private bool HasThreeSlotConfiguration()
    {
        bool isComplete = threeSlotBarrelRoot != null &&
                            threeSlotBarrels.Count >= activeReelCount &&
                            HasCompleteConversionSourceGrid();

        if (!isComplete)
        {
            ReportConfigurationWarning(
                $"The 3x1 barrel setup requires {activeReelCount} barrels and three result cells per reel.");
        }

        return isComplete;
    }

    private bool HasCompleteAnimationGrid()
    {
        return animationGrid != null &&
               animationGrid.Length == activeReelCount &&
               animationGrid.All(column =>
                   column != null &&
                   column.column != null &&
                    column.rows != null &&
                    column.rows.Length == RowCount &&
                    column.rows.All(cell =>
                        cell != null &&
                        cell.slot != null &&
                        cell.slot.parent == column.column));
    }

    private bool HasCompleteConversionSourceGrid()
    {
        if (conversionSourceSlots == null ||
            conversionSourceSlots.Count < activeReelCount)
        {
            return false;
        }

        for (int reelIndex = 0; reelIndex < activeReelCount; reelIndex++)
        {
            ReelResultSlots slots = conversionSourceSlots[reelIndex];
            if (slots == null) return false;
            for (int row = 0; row < RowCount; row++)
            {
                if (slots.Get(row) == null) return false;
            }
        }

        return true;
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

    private static Transform FindDescendantStartingWith(
        Transform root,
        string objectNamePrefix)
    {
        if (root == null) return null;
        if (root.name.StartsWith(
                objectNamePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindDescendantStartingWith(
                root.GetChild(index),
                objectNamePrefix);
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
