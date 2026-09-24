using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Owns feature visuals that sit on top of the reel presentation.
/// Handles reusable 2x1 and 3x1 barrels and pooled train visuals.
/// </summary>
public class SlotFeatureVisualController : MonoBehaviour
{
    private const int ReelCount = 5;
    private const int RowCount = 3;
    private const int MaxGreenTrains = 2;
    private const int MaxRedTrains = 1;
    private const int MaxHorizontalPurpleTrains = 1;
    private const int MaxVerticalPurpleTrains = 2;
    private const int MaxGoldenTrains = 1;
    private const int TwoSlotFeatureType = -1;
    private const int ThreeSlotFeatureType = -2;
    private const float ConversionHoldDuration = 0.15f;
    private const float ConversionFadeDuration = 0.3f;

    [Header("Feature References")]
    [SerializeField] private RectTransform animationRoot;
    [SerializeField] private RectTransform twoSlotBarrelRoot;
    [SerializeField] private RectTransform threeSlotBarrelRoot;
    [SerializeField] private RectTransform greenTrainRoot;
    [SerializeField] private RectTransform redTrainRoot;
    [SerializeField] private RectTransform horizontalPurpleTrainRoot;
    [SerializeField] private RectTransform verticalPurpleTrainRoot;
    [SerializeField] private RectTransform goldenTrainRoot;

    [Header("Animation Grid")]
    [SerializeField, Min(0.01f)] private float winboxScale = 0.79f;

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
    private readonly List<RectTransform> animationColumns = new List<RectTransform>();
    private readonly List<List<RectTransform>> animationCellsByReel = new List<List<RectTransform>>();
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
    private readonly Dictionary<TrainVisualType, List<RectTransform>> trainVisuals =
        new Dictionary<TrainVisualType, List<RectTransform>>();
    private readonly HashSet<GameObject> hiddenAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<GameObject> activeWinAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<GameObject> activeTrainAnimationCells = new HashSet<GameObject>();
    private readonly HashSet<CanvasGroup> conversionCanvasGroups = new HashSet<CanvasGroup>();

    private Transform searchRoot;
    private IReadOnlyList<ReelResultSlots> conversionSourceSlots;
    private bool isInitialized;
    private bool configurationWarningShown;

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
    }

    internal void RevealFeaturesForReel(int reelIndex)
    {
        EnsureInitialized();

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
        RectTransform trainVisual = GetAvailableTrainVisual(train.type);
        if (trainVisual == null || trainRoot == null ||
            !TryGetTrainPosition(train, out Vector2 position) ||
            !HasCompleteAnimationGrid())
        {
            ReportConfigurationWarning(
                $"No reusable {train.type} train visual or position is available for " +
                $"row {train.startRow}, column {train.startCol}.");
            return;
        }

        trainVisual.anchoredPosition = position;
        PlayConversionFade(
            train.startCol,
            train.columnCount,
            train.startRow,
            train.rowCount,
            trainVisual);
        visibleFeatures[((int)train.type, train.startRow, train.startCol,
            train.rowCount, train.columnCount)] = trainVisual;
    }

    internal void RevealAllFeatures()
    {
        DOTween.Complete(this);
        RemoveFeaturesMissingFromResult();

        for (int reelIndex = 0; reelIndex < ReelCount; reelIndex++)
        {
            RevealFeaturesForReel(reelIndex);
        }
    }

    internal void ResetFeatures()
    {
        pendingStartRowByReel.Clear();
        pendingThreeSlotReels.Clear();
        pendingTrains.Clear();
        ClearVisibleFeatures();
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

        animationCell = animationCellsByReel[reelIndex][row];
        if (animationCell == null) return false;

        activeWinAnimationCells.Add(animationCell.gameObject);
        SetWinboxActive(animationCell, true);
        animationRoot.gameObject.SetActive(true);
        animationColumns[reelIndex].gameObject.SetActive(true);
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

        animationCell = animationCellsByReel[reelIndex][row];
        if (animationCell == null) return false;

        activeTrainAnimationCells.Add(animationCell.gameObject);
        if (!activeWinAnimationCells.Contains(animationCell.gameObject))
        {
            SetWinboxActive(animationCell, false);
        }

        animationRoot.gameObject.SetActive(true);
        animationColumns[reelIndex].gameObject.SetActive(true);
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
                hiddenCell.SetActive(IsAnimationCellActive(hiddenCell));
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

        foreach (KeyValuePair<TrainVisualType, List<RectTransform>> entry in trainVisuals)
        {
            foreach (RectTransform visual in entry.Value)
            {
                if (visual != null) visual.gameObject.SetActive(false);
            }
        }

        foreach (KeyValuePair<TrainVisualType, RectTransform> entry in trainRoots)
        {
            if (entry.Value != null) entry.Value.gameObject.SetActive(false);
        }

        visibleFeatures.Clear();
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

        animationCellsByReel.Clear();
        animationColumns.Clear();
        for (int index = 0; index < animationRoot.childCount; index++)
        {
            if (!(animationRoot.GetChild(index) is RectTransform child)) continue;
            if (!child.name.StartsWith("Slot") || child.childCount < RowCount) continue;

            animationColumns.Add(child);
        }

        animationColumns.Sort((left, right) =>
            left.anchoredPosition.x.CompareTo(right.anchoredPosition.x));

        foreach (RectTransform column in animationColumns)
        {
            column.gameObject.SetActive(false);

            var cells = new List<RectTransform>();
            for (int row = 0; row < column.childCount; row++)
            {
                if (column.GetChild(row) is RectTransform cell)
                {
                    cells.Add(cell);
                }
            }

            cells.Sort((top, bottom) =>
                bottom.anchoredPosition.y.CompareTo(top.anchoredPosition.y));
            foreach (RectTransform cell in cells)
            {
                SetWinboxScale(cell);
                cell.gameObject.SetActive(false);
            }
            animationCellsByReel.Add(cells);
        }

        isInitialized = true;
        HasTwoSlotConfiguration();
        HasThreeSlotConfiguration();
    }

    private void CacheTrainVisuals()
    {
        trainRoots.Clear();
        trainVisuals.Clear();

        CacheTrainVisuals(TrainVisualType.Green, greenTrainRoot);
        CacheTrainVisuals(TrainVisualType.Red, redTrainRoot);
        CacheTrainVisuals(TrainVisualType.HorizontalPurple, horizontalPurpleTrainRoot);
        CacheTrainVisuals(TrainVisualType.VerticalPurple, verticalPurpleTrainRoot);
        CacheTrainVisuals(TrainVisualType.Golden, goldenTrainRoot);

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
                animationCellsToRestore.Add(animationCellsByReel[reelIndex][row].gameObject);
            }
        }

        sequence.AppendCallback(() =>
        {
            visual.gameObject.SetActive(false);
            visualGroup.alpha = 1f;

            foreach (GameObject animationCell in animationCellsToRestore)
            {
                hiddenAnimationCells.Remove(animationCell);
                animationCell.SetActive(IsAnimationCellActive(animationCell));
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

    private void CacheTrainVisuals(TrainVisualType type, RectTransform root)
    {
        trainRoots[type] = root;
        var visuals = new List<RectTransform>();

        if (root != null)
        {
            for (int index = 0; index < root.childCount; index++)
            {
                if (root.GetChild(index) is RectTransform visual)
                {
                    visuals.Add(visual);
                }
            }
        }

        trainVisuals[type] = visuals;
    }

    private RectTransform GetAvailableTrainVisual(TrainVisualType type)
    {
        if (!trainVisuals.TryGetValue(type, out List<RectTransform> visuals))
        {
            return null;
        }

        int reusableCount = Mathf.Min(GetMaximumVisibleTrainCount(type), visuals.Count);
        for (int index = 0; index < reusableCount; index++)
        {
            RectTransform visual = visuals[index];
            if (visual != null && !visual.gameObject.activeSelf)
            {
                return visual;
            }
        }

        return null;
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
        int availableCount = trainVisuals.TryGetValue(type, out List<RectTransform> visuals)
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
        List<RectTransform> reelCells = animationCellsByReel[reelIndex];
        for (int row = startRow; row < startRow + count; row++)
        {
            GameObject coveredCell = reelCells[row].gameObject;
            coveredCell.SetActive(false);
            hiddenAnimationCells.Add(coveredCell);
        }
    }

    private void RefreshAnimationHierarchy()
    {
        if (animationRoot == null) return;

        bool hasActiveAnimationCell = false;
        for (int reelIndex = 0; reelIndex < animationColumns.Count; reelIndex++)
        {
            RectTransform column = animationColumns[reelIndex];
            bool hasActiveCell = reelIndex < animationCellsByReel.Count &&
                                 animationCellsByReel[reelIndex].Any(cell =>
                                     cell != null &&
                                     IsAnimationCellActive(cell.gameObject) &&
                                     cell.gameObject.activeSelf);

            column.gameObject.SetActive(hasActiveCell);
            hasActiveAnimationCell |= hasActiveCell;
        }

        bool hasActiveFeature = visibleFeatures.Values.Any(visual =>
            visual != null && visual.gameObject.activeSelf);
        animationRoot.gameObject.SetActive(hasActiveAnimationCell || hasActiveFeature);
    }

    private bool IsAnimationCellActive(GameObject animationCell)
    {
        return animationCell != null &&
               (activeWinAnimationCells.Contains(animationCell) ||
                activeTrainAnimationCells.Contains(animationCell));
    }

    private void SetWinboxScale(RectTransform animationCell)
    {
        Transform winbox = FindDirectWinbox(animationCell);
        if (winbox != null)
        {
            winbox.localScale = Vector3.one * winboxScale;
        }
    }

    private static void SetWinboxActive(RectTransform animationCell, bool isActive)
    {
        Transform winbox = FindDirectWinbox(animationCell);
        if (winbox != null)
        {
            winbox.gameObject.SetActive(isActive);
        }
    }

    private static Transform FindDirectWinbox(RectTransform animationCell)
    {
        if (animationCell == null) return null;

        for (int childIndex = 0; childIndex < animationCell.childCount; childIndex++)
        {
            Transform child = animationCell.GetChild(childIndex);
            if (child.name.StartsWith("Winbox")) return child;
        }

        return null;
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
        return animationCellsByReel.Count == ReelCount &&
               animationCellsByReel.All(cells => cells.Count >= RowCount);
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
}
