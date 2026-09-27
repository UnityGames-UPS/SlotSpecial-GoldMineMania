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
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns feature visuals that sit on top of the reel presentation.
/// Handles reusable 2x1 and 3x1 barrels and pooled train visuals.
/// </summary>
public class SlotFeatureVisualController : MonoBehaviour
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
    [SerializeField, Min(1f)] private float goldBurstIntroFramesPerSecond = 29f;
    [SerializeField, Min(0f)] private float goldBurstMergeDelayAfterTrolley = 0.5f;
    [SerializeField, Min(0f)] private float goldBoxRevealStagger = 0.12f;
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

    [Header("Gold Burst Train Journey Presentation")]
    [SerializeField] private RectTransform trainJourneyGreenTrain;
    [SerializeField] private RectTransform trainJourneyActor;
    [SerializeField] private RectTransform trainJourneySecondActor;
    [SerializeField] private RectTransform trainJourneyWinBox;
    [SerializeField] private TMP_Text[] trainJourneyWagonAmounts = new TMP_Text[4];
    [SerializeField] private TMP_Text trainJourneyWinAmount;
    [SerializeField, Min(0.1f)] private float trainJourneyPhaseDuration = 1.25f;
    [SerializeField, Min(0f)] private float trainJourneySettleDelay = 0.2f;
    [SerializeField, Min(0f)] private float trainJourneyResultHold = 0.3f;
    [SerializeField, Min(0.1f)] private float trainJourneyExitDuration = 1f;
    [SerializeField] private float trainJourneyCenterX;
    [SerializeField] private float trainJourneyExitX = 2813f;
    [SerializeField] private float trainJourneyCollectionXOffset;
    [SerializeField, Min(0.01f)] private float trainJourneyCollectionCountDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float trainJourneyActorExitLeadTime = 0.3f;
    [SerializeField, Min(0f)] private float trainJourneyActorExitDistance = 900f;

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

    private Transform searchRoot;
    private IReadOnlyList<ReelResultSlots> conversionSourceSlots;
    private bool isInitialized;
    private bool configurationWarningShown;
    private bool isGoldBurstPresentationActive;
    private bool deferGoldBurstTriggerBarrelMerge;
    private bool goldBurstPressPlayHandled;
    private Action goldBurstPressPlayCallback;
    private Coroutine trainJourneyRoutine;
    private Vector2 trainJourneyGreenTrainStartPosition;
    private Vector2 trainJourneyActorStartPosition;
    private Vector2 trainJourneySecondActorStartPosition;
    private bool trainJourneyOriginalLoop;
    private bool hasCapturedTrainJourneyState;
    private bool trainJourneyOwnsDarkBackground;

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
        PlayConversionFade(
            train.startCol,
            train.columnCount,
            train.startRow,
            train.rowCount,
            trainVisual);
        visibleFeatures[((int)train.type, train.startRow, train.startCol,
            train.rowCount, train.columnCount)] = trainVisual;
        trainPlacementsByVisual[trainVisual] = train;
    }

    internal void RevealAllFeatures()
    {
        DOTween.Complete(this);
        RemoveFeaturesMissingFromResult();

        for (int reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            RevealFeaturesForReel(reelIndex);
        }

        if (!isGoldBurstPresentationActive)
        {
            ShowPressPlayButtons();
        }
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
    }

    internal void BeginGoldBurstTriggerPresentation()
    {
        EnsureInitialized();
        DOTween.Complete(this);
        ResetGoldBurstPresentation();
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
                    trolleyGraphic.AnimationState.SetAnimation(0, trolleyAnimation.Name, false);
                    trolleyDuration = trolleyAnimation.Duration;
                }
            }

            if (trolleyDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(trolleyDuration);
            }
        }

        if (trolleyMan != null) trolleyMan.SetActive(false);
        if (track != null) track.SetActive(false);
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
        HideAllGoldBoxes();
    }

    internal IEnumerator RevealGoldBurstPrize(GoldBurstPrizePlacement prize)
    {
        EnsureInitialized();
        if (prize == null || prize.amount < 0d ||
            !TryGetGoldBox(prize, out GoldBoxRuntime goldBox))
        {
            yield break;
        }

        HideGoldBurstSource(prize);
        SetGoldBoxRootActive(prize.rowCount, true);
        if (goldBox.visual.parent != null)
        {
            goldBox.visual.parent.gameObject.SetActive(true);
        }

        goldBox.amountText.text = prize.amount.ToString("0.00", CultureInfo.InvariantCulture);
        DOTween.Kill(goldBox.visual);
        goldBox.visual.localScale = Vector3.zero;
        goldBox.visual.gameObject.SetActive(true);
        RefreshAnimationHierarchy();

        Sequence reveal = DOTween.Sequence().SetUpdate(true).SetTarget(goldBox.visual);
        reveal.Append(goldBox.visual
            .DOScale(goldBox.baseScale * 1.08f, 0.2f)
            .SetEase(Ease.OutBack));
        reveal.Append(goldBox.visual
            .DOScale(goldBox.baseScale, 0.12f)
            .SetEase(Ease.OutSine));
        yield return reveal.WaitForCompletion();

        if (goldBoxRevealStagger > 0f)
        {
            yield return new WaitForSecondsRealtime(goldBoxRevealStagger);
        }

        RefreshAnimationHierarchy();
    }

    internal bool ShowGoldBurstPressPlay(Action onPressed)
    {
        goldBurstPressPlayHandled = false;
        goldBurstPressPlayCallback = onPressed;
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
            if (button == null) continue;

            button.onClick.RemoveListener(OnGoldBurstPressPlayClicked);
            button.onClick.AddListener(OnGoldBurstPressPlayClicked);
            button.interactable = true;
            PlayPressPlayAnimation(trainReferences);
            showedButton = true;
        }

        if (!showedButton)
        {
            goldBurstPressPlayCallback = null;
        }

        return showedButton;
    }

    internal float GoldBurstHoldWithoutTrain => goldBoxHoldWithoutTrain;

    internal void EndGoldBurstPresentation()
    {
        ResetGoldBurstPresentation();
        RefreshAnimationHierarchy();
    }

    internal bool TryAcquireGoldBurstAnimationCell(
        int reelIndex,
        int row,
        out RectTransform animationCell)
    {
        EnsureInitialized();
        animationCell = null;

        if (!HasCompleteAnimationGrid() ||
            reelIndex < 0 || reelIndex >= ReelCount ||
            row < 0 || row >= RowCount)
        {
            return false;
        }

        animationCell = GetAnimationCell(reelIndex, row);
        RectTransform column = GetAnimationColumn(reelIndex);
        if (animationCell == null || column == null || animationRoot == null ||
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
        animationCell = null;

        if (!HasCompleteAnimationGrid() ||
            reelIndex < 0 || reelIndex >= ReelCount ||
            row < 0 || row >= RowCount ||
            IsCellCoveredByPendingFeature(reelIndex, row))
        {
            return false;
        }

        animationCell = GetAnimationCell(reelIndex, row);
        if (animationCell == null) return false;

        activeWinAnimationCells.Add(animationCell.gameObject);
        SetWinboxActive(animationCell, true);
        animationRoot.gameObject.SetActive(true);
        GetAnimationColumn(reelIndex).gameObject.SetActive(true);
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
        animationCell = null;

        if (!HasCompleteAnimationGrid() ||
            reelIndex < 0 || reelIndex >= ReelCount ||
            row < 0 || row >= RowCount ||
            IsCellCoveredByPendingFeature(reelIndex, row))
        {
            return false;
        }

        animationCell = GetAnimationCell(reelIndex, row);
        if (animationCell == null) return false;

        activeTrainAnimationCells.Add(animationCell.gameObject);
        SetWinboxActive(animationCell, true);
        if (!activeWinAnimationCells.Contains(animationCell.gameObject))
        {
            Image trainImage = animationCell.GetComponent<Image>();
            if (trainImage != null) trainImage.enabled = false;
        }

        animationRoot.gameObject.SetActive(true);
        GetAnimationColumn(reelIndex).gameObject.SetActive(true);
        animationCell.gameObject.SetActive(true);
        return true;
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
            for (int index = 0; index < twoSlotBarrelRoot.childCount; index++)
            {
                if (twoSlotBarrelRoot.GetChild(index) is RectTransform barrel)
                {
                    twoSlotBarrels.Add(barrel);
                }
            }

            twoSlotBarrels.Sort((left, right) =>
                left.anchoredPosition.x.CompareTo(right.anchoredPosition.x));
            CaptureAuthoredVerticalPositions();
        }

        threeSlotBarrels.Clear();
        if (threeSlotBarrelRoot != null)
        {
            for (int index = 0; index < threeSlotBarrelRoot.childCount; index++)
            {
                if (threeSlotBarrelRoot.GetChild(index) is RectTransform barrel)
                {
                    threeSlotBarrels.Add(barrel);
                }
            }

            threeSlotBarrels.Sort((left, right) =>
                left.anchoredPosition.x.CompareTo(right.anchoredPosition.x));
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

    private void RemoveFeaturesMissingFromResult()
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

        foreach (KeyValuePair<
                     (int type, int startRow, int startCol, int rowCount, int columnCount),
                     RectTransform> visibleFeature in visibleFeatures.ToList())
        {
            if (pendingFeatures.Contains(visibleFeature.Key)) continue;

            FadeOutRemovedFeature(visibleFeature.Key, visibleFeature.Value);
            visibleFeatures.Remove(visibleFeature.Key);
        }
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

        var animationCellsToRestore = new List<GameObject>();
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
                animationCellsToRestore.Add(GetAnimationCell(reelIndex, row).gameObject);
            }
        }

        sequence.AppendCallback(() =>
        {
            if (trainReferencesByVisual.TryGetValue(
                    visual,
                    out TrainVisualReferences trainReferences))
            {
                ResetTrainPresentation(trainReferences);
            }

            visual.gameObject.SetActive(false);
            visualGroup.alpha = 1f;

            foreach (GameObject animationCell in animationCellsToRestore)
            {
                hiddenAnimationCells.Remove(animationCell);
                bool isActive = IsAnimationCellActive(animationCell);
                animationCell.SetActive(isActive);
                SetWinboxActive(
                    animationCell,
                    isActive && activeWinAnimationCells.Contains(animationCell));
            }

            RefreshAnimationHierarchy();
        });
    }

    private void PlayConversionFade(
        int startReel,
        int reelCount,
        int startRow,
        int rowCount,
        RectTransform visual)
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        sequence.AppendInterval(ConversionHoldDuration);

        for (int reelIndex = startReel; reelIndex < startReel + reelCount; reelIndex++)
        {
            for (int row = startRow; row < startRow + rowCount; row++)
            {
                RectTransform sourceCell = GetConversionSourceCell(reelIndex, row);
                if (sourceCell == null) continue;

                CanvasGroup sourceGroup = GetConversionCanvasGroup(sourceCell.gameObject);
                sequence.Insert(
                    ConversionHoldDuration,
                    sourceGroup.DOFade(0f, ConversionFadeDuration).SetEase(Ease.Linear));
            }
        }

        CanvasGroup visualGroup = GetConversionCanvasGroup(visual.gameObject);
        visualGroup.alpha = 0f;
        animationRoot.gameObject.SetActive(true);
        visual.parent.gameObject.SetActive(true);
        visual.gameObject.SetActive(true);

        sequence.Insert(
            ConversionHoldDuration,
            visualGroup.DOFade(1f, ConversionFadeDuration).SetEase(Ease.Linear));

        sequence.AppendCallback(() =>
        {
            for (int reelIndex = startReel; reelIndex < startReel + reelCount; reelIndex++)
            {
                HideAnimationCells(reelIndex, startRow, rowCount);
            }
        });
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
        if (trainJourneyGreenTrain != null)
        {
            TMP_Text[] discoveredAmounts = trainJourneyGreenTrain
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text != null &&
                               text.name.StartsWith("Winamount", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(text => text.rectTransform.anchoredPosition.x)
                .ToArray();

            if (trainJourneyWagonAmounts == null ||
                trainJourneyWagonAmounts.Length == 0 ||
                trainJourneyWagonAmounts.All(text => text == null))
            {
                trainJourneyWagonAmounts = discoveredAmounts;
            }

            if (!hasCapturedTrainJourneyState)
            {
                trainJourneyGreenTrainStartPosition =
                    trainJourneyGreenTrain.anchoredPosition;
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
                ImageAnimation trainAnimation =
                    trainJourneyGreenTrain.GetComponent<ImageAnimation>();
                trainJourneyOriginalLoop = trainAnimation != null &&
                                           trainAnimation.doLoopAnimation;
                hasCapturedTrainJourneyState = true;
            }
        }

        if (trainJourneyWinAmount == null && trainJourneyWinBox != null)
        {
            trainJourneyWinAmount =
                trainJourneyWinBox.GetComponentInChildren<TMP_Text>(true);
        }
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

                if (trainReferences.pressPlay != null)
                {
                    pressPlayBaseScales[trainReferences.pressPlay] =
                        trainReferences.pressPlay.localScale;
                }

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
        if (trainReferences.mask != null)
        {
            trainReferences.mask.SetActive(false);
        }
    }

    private void ResetPressPlay(TrainVisualReferences trainReferences)
    {
        RectTransform pressPlay = trainReferences?.pressPlay;
        if (pressPlay == null) return;

        Button button = pressPlay.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(OnGoldBurstPressPlayClicked);
            button.interactable = true;
        }

        DOTween.Kill(pressPlay);
        pressPlay.localScale = GetPressPlayBaseScale(pressPlay);
        pressPlay.gameObject.SetActive(false);
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

    private void HideGoldBurstSource(GoldBurstPrizePlacement prize)
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

        if (feature != null)
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

    private void ResetGoldBurstPresentation()
    {
        ResetTrainJourneyPresentation();
        isGoldBurstPresentationActive = false;
        deferGoldBurstTriggerBarrelMerge = false;
        goldBurstPressPlayHandled = false;
        goldBurstPressPlayCallback = null;

        StopAndHideImageAnimation(coldBurstRespin);
        StopAndHideImageAnimation(megaGoldBurstRespin);
        StopAndHideImageAnimation(ultimateGoldBurstRespin);
        if (darkBackground != null) darkBackground.SetActive(false);
        if (track != null) track.SetActive(false);
        if (trolleyMan != null) trolleyMan.SetActive(false);

        foreach (List<TrainVisualReferences> references in trainVisuals.Values)
        {
            foreach (TrainVisualReferences trainReferences in references)
            {
                ResetPressPlay(trainReferences);
            }
        }

        HideAllGoldBoxes();

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

    private void OnGoldBurstPressPlayClicked()
    {
        if (goldBurstPressPlayHandled) return;
        TrainPlacement selectedTrain = GetSelectedTrainPlacement();
        goldBurstPressPlayHandled = true;

        foreach (List<TrainVisualReferences> references in trainVisuals.Values)
        {
            foreach (TrainVisualReferences trainReferences in references)
            {
                ResetPressPlay(trainReferences);
            }
        }

        Action callback = goldBurstPressPlayCallback;
        goldBurstPressPlayCallback = null;
        if (CanPlayTrainJourneyPresentation(selectedTrain))
        {
            trainJourneyRoutine = StartCoroutine(
                PlayTrainJourneyPresentation(selectedTrain, callback));
            return;
        }

        callback?.Invoke();
    }

    private TrainPlacement GetSelectedTrainPlacement()
    {
        GameObject selectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        RectTransform fallbackVisual = null;

        foreach (List<TrainVisualReferences> references in trainVisuals.Values)
        {
            foreach (TrainVisualReferences trainReferences in references)
            {
                if (trainReferences?.visual == null ||
                    trainReferences.pressPlay == null ||
                    !trainReferences.pressPlay.gameObject.activeInHierarchy)
                {
                    continue;
                }

                fallbackVisual ??= trainReferences.visual;
                if (selectedObject == trainReferences.pressPlay.gameObject ||
                    (selectedObject != null &&
                     selectedObject.transform.IsChildOf(trainReferences.pressPlay)))
                {
                    return trainPlacementsByVisual.TryGetValue(
                        trainReferences.visual,
                        out TrainPlacement selectedTrain)
                        ? selectedTrain
                        : null;
                }
            }
        }

        return fallbackVisual != null &&
               trainPlacementsByVisual.TryGetValue(fallbackVisual, out TrainPlacement fallbackTrain)
            ? fallbackTrain
            : null;
    }

    private bool CanPlayTrainJourneyPresentation(TrainPlacement train)
    {
        return train != null &&
               train.type == TrainVisualType.Green &&
               trainJourneyGreenTrain != null &&
               trainJourneyWinBox != null &&
               trainJourneyWagonAmounts != null &&
               trainJourneyWagonAmounts.Any(text => text != null);
    }

    private IEnumerator PlayTrainJourneyPresentation(
        TrainPlacement train,
        Action onComplete)
    {
        ResetTrainJourneyVisuals();

        double totalPayout = train.payout > 0d
            ? train.payout
            : train.trainJourney?.Sum() ?? 0d;
        SetTrainJourneyWagonAmounts(train.trainJourney);
        SetSpriteAmount(trainJourneyWinAmount, 0d);

        if (darkBackground != null && !darkBackground.activeSelf)
        {
            darkBackground.SetActive(true);
            trainJourneyOwnsDarkBackground = true;
        }

        trainJourneyWinBox.gameObject.SetActive(true);
        PlayTrainJourneyActor(trainJourneyActor);
        PlayTrainJourneyActor(trainJourneySecondActor);

        ImageAnimation trainAnimation =
            trainJourneyGreenTrain.GetComponent<ImageAnimation>();
        float travelDuration = Mathf.Max(
            0.2f,
            trainJourneyPhaseDuration + trainJourneyExitDuration);
        if (trainAnimation != null)
        {
            trainAnimation.StopAnimation();
            trainAnimation.onLoopComplete = null;
            trainAnimation.doLoopAnimation = false;
            int animationPhaseCount =
                trainAnimation.secondaryTextureArray != null &&
                trainAnimation.secondaryTextureArray.Count > 0
                    ? 2
                    : 1;
            trainAnimation.SetLoopDuration(travelDuration / animationPhaseCount);
        }

        Vector2 startPosition = hasCapturedTrainJourneyState
            ? trainJourneyGreenTrainStartPosition
            : trainJourneyGreenTrain.anchoredPosition;
        trainJourneyGreenTrain.anchoredPosition = startPosition;

        if (trainJourneySettleDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(trainJourneySettleDelay);
        }

        trainJourneyGreenTrain.gameObject.SetActive(true);
        trainAnimation?.PlayAnimation();

        Tween travelTween = trainJourneyGreenTrain
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
            trainJourneyWagonAmounts?.Length ?? 0);
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
        float collectionTimeout = Time.realtimeSinceStartup + travelDuration + 0.5f;
        double displayedTotal = 0d;

        for (int index = 0; index < amountCount; index++)
        {
            TMP_Text wagonAmount = trainJourneyWagonAmounts[index];
            if (wagonAmount == null) continue;

            RectTransform wagonMarker = wagonAmount.rectTransform;
            while (wagonMarker != null &&
                   wagonMarker.position.x < collectionWorldX &&
                   Time.realtimeSinceStartup < collectionTimeout)
            {
                yield return null;
            }

            if (wagonMarker == null || wagonMarker.position.x < collectionWorldX)
            {
                continue;
            }

            double nextTotal = displayedTotal + Math.Max(0d, amounts[index]);
            if (index == amountCount - 1)
            {
                nextTotal = totalPayout;
            }

            PlayTrainJourneyWinBoxAnimation();
            yield return CountTrainJourneyWinAmount(
                displayedTotal,
                nextTotal,
                trainJourneyCollectionCountDuration);
            displayedTotal = nextTotal;
        }

        SetSpriteAmount(trainJourneyWinAmount, totalPayout);
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

    private void PlayTrainJourneyWinBoxAnimation()
    {
        if (trainJourneyWinBox == null) return;

        ImageAnimation winBoxAnimation =
            trainJourneyWinBox.GetComponentInChildren<ImageAnimation>(true);
        if (winBoxAnimation == null) return;

        winBoxAnimation.StopAnimation();
        winBoxAnimation.PlayAnimation();
    }

    private void SetTrainJourneyWagonAmounts(IReadOnlyList<double> amounts)
    {
        for (int index = 0; index < trainJourneyWagonAmounts.Length; index++)
        {
            TMP_Text amountText = trainJourneyWagonAmounts[index];
            if (amountText == null) continue;

            bool hasAmount = amounts != null && index < amounts.Count;
            amountText.gameObject.SetActive(hasAmount);
            if (hasAmount)
            {
                SetSpriteAmount(amountText, amounts[index]);
            }
        }
    }

    private void SetTrainJourneyWagonAmountsVisible(bool visible)
    {
        if (trainJourneyWagonAmounts == null) return;

        foreach (TMP_Text amountText in trainJourneyWagonAmounts)
        {
            if (amountText != null) amountText.gameObject.SetActive(visible);
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

        ResetTrainJourneyVisuals();
    }

    private void ResetTrainJourneyVisuals()
    {
        if (trainJourneyGreenTrain != null)
        {
            DOTween.Kill(trainJourneyGreenTrain);
            ImageAnimation trainAnimation =
                trainJourneyGreenTrain.GetComponent<ImageAnimation>();
            if (trainAnimation != null)
            {
                trainAnimation.onLoopComplete = null;
                trainAnimation.StopAnimation();
                trainAnimation.ClearLoopDuration();
                trainAnimation.doLoopAnimation = trainJourneyOriginalLoop;
            }

            if (hasCapturedTrainJourneyState)
            {
                trainJourneyGreenTrain.anchoredPosition =
                    trainJourneyGreenTrainStartPosition;
            }
            trainJourneyGreenTrain.gameObject.SetActive(false);
        }

        ResetTrainJourneyActor(
            trainJourneyActor,
            trainJourneyActorStartPosition);
        ResetTrainJourneyActor(
            trainJourneySecondActor,
            trainJourneySecondActorStartPosition);

        if (trainJourneyWinBox != null)
        {
            ImageAnimation winBoxAnimation =
                trainJourneyWinBox.GetComponentInChildren<ImageAnimation>(true);
            if (winBoxAnimation != null)
            {
                winBoxAnimation.onLoopComplete = null;
                winBoxAnimation.StopAnimation();
                winBoxAnimation.ClearLoopDuration();
            }
            trainJourneyWinBox.gameObject.SetActive(false);
        }

        if (trainJourneyOwnsDarkBackground && darkBackground != null)
        {
            darkBackground.SetActive(false);
            trainJourneyOwnsDarkBackground = false;
        }
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
            actorGraphic.AnimationState?.ClearTracks();
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
        Debug.LogWarning($"[SlotFeatureVisualController] {message}", this);
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
