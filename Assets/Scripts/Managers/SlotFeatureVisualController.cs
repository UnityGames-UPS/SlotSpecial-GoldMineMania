using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns feature visuals that sit on top of the reel presentation.
/// Currently this handles the reusable 2x1 and 3x1 barrel visuals.
/// </summary>
public class SlotFeatureVisualController : MonoBehaviour
{
    private const int ReelCount = 5;
    private const int RowCount = 3;

    [Header("Barrel References")]
    [SerializeField] private RectTransform animationRoot;
    [SerializeField] private RectTransform twoSlotBarrelRoot;
    [SerializeField] private RectTransform threeSlotBarrelRoot;
    [SerializeField] private RectTransform greenTrainRoot;
    [SerializeField] private RectTransform redTrainRoot;
    [SerializeField] private RectTransform horizontalPurpleTrainRoot;
    [SerializeField] private RectTransform verticalPurpleTrainRoot;
    [SerializeField] private RectTransform goldenTrainRoot;

    [Header("2x1 Barrel Positions")]
    [SerializeField] private float topAndMiddleY = 110f;
    [SerializeField] private float middleAndBottomY = -110f;

    private readonly List<RectTransform> twoSlotBarrels = new List<RectTransform>();
    private readonly List<RectTransform> threeSlotBarrels = new List<RectTransform>();
    private readonly List<RectTransform> animationColumns = new List<RectTransform>();
    private readonly List<List<RectTransform>> animationCellsByReel = new List<List<RectTransform>>();
    private readonly Dictionary<int, int> pendingStartRowByReel = new Dictionary<int, int>();
    private readonly HashSet<int> pendingThreeSlotReels = new HashSet<int>();
    private readonly List<TrainPlacement> pendingTrains = new List<TrainPlacement>();
    private readonly HashSet<TrainPlacement> revealedTrains = new HashSet<TrainPlacement>();
    private readonly Dictionary<TrainVisualType, RectTransform> trainRoots =
        new Dictionary<TrainVisualType, RectTransform>();
    private readonly Dictionary<TrainVisualType, List<RectTransform>> trainVisuals =
        new Dictionary<TrainVisualType, List<RectTransform>>();
    private readonly HashSet<GameObject> hiddenAnimationCells = new HashSet<GameObject>();

    private Transform searchRoot;
    private bool isInitialized;
    private bool configurationWarningShown;

    internal void Initialize(Transform slotRoot)
    {
        searchRoot = slotRoot != null ? slotRoot : searchRoot;
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
                placement.startCol + placement.columnCount > ReelCount)
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
            RevealThreeSlotBarrel(reelIndex);
        }
        else if (pendingStartRowByReel.TryGetValue(reelIndex, out int startRow))
        {
            RevealTwoSlotBarrel(reelIndex, startRow);
        }

        foreach (TrainPlacement train in pendingTrains)
        {
            int lastCoveredReel = train.startCol + train.columnCount - 1;
            if (lastCoveredReel == reelIndex && !revealedTrains.Contains(train))
            {
                RevealTrain(train);
            }
        }
    }

    private void RevealTwoSlotBarrel(int reelIndex, int startRow)
    {
        if (!HasTwoSlotConfiguration()) return;

        HideAnimationCells(reelIndex, startRow, 2);

        RectTransform barrel = twoSlotBarrels[reelIndex];
        Vector2 barrelPosition = barrel.anchoredPosition;
        barrelPosition.y = startRow == 0 ? topAndMiddleY : middleAndBottomY;
        barrel.anchoredPosition = barrelPosition;

        twoSlotBarrelRoot.gameObject.SetActive(true);
        barrel.gameObject.SetActive(true);
    }

    private void RevealThreeSlotBarrel(int reelIndex)
    {
        if (!HasThreeSlotConfiguration()) return;

        HideAnimationCells(reelIndex, 0, RowCount);
        threeSlotBarrelRoot.gameObject.SetActive(true);
        threeSlotBarrels[reelIndex].gameObject.SetActive(true);
    }

    private void RevealTrain(TrainPlacement train)
    {
        RectTransform trainVisual = FindTrainVisual(train);
        RectTransform trainRoot = GetTrainRoot(train.type);
        if (trainVisual == null || trainRoot == null)
        {
            ReportConfigurationWarning(
                $"No {train.type} train visual is available for row {train.startRow}, column {train.startCol}.");
            return;
        }

        for (int reelIndex = train.startCol;
             reelIndex < train.startCol + train.columnCount;
             reelIndex++)
        {
            HideAnimationCells(reelIndex, train.startRow, train.rowCount);
        }

        trainRoot.gameObject.SetActive(true);
        trainVisual.gameObject.SetActive(true);
        revealedTrains.Add(train);
    }

    internal void RevealAllFeatures()
    {
        ClearVisibleFeatures();
        revealedTrains.Clear();

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
        revealedTrains.Clear();
        ClearVisibleFeatures();
    }

    private void ClearVisibleFeatures()
    {
        foreach (GameObject hiddenCell in hiddenAnimationCells)
        {
            if (hiddenCell != null) hiddenCell.SetActive(true);
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

    private RectTransform FindTrainVisual(TrainPlacement train)
    {
        if (!trainVisuals.TryGetValue(train.type, out List<RectTransform> visuals) ||
            visuals.Count == 0 || animationColumns.Count != ReelCount)
        {
            return null;
        }

        float targetX = 0f;
        for (int column = train.startCol;
             column < train.startCol + train.columnCount;
             column++)
        {
            targetX += animationColumns[column].anchoredPosition.x;
        }
        targetX /= train.columnCount;

        float targetY = train.rowCount == RowCount
            ? 0f
            : train.startRow == 0 ? topAndMiddleY : middleAndBottomY;
        Vector2 targetPosition = new Vector2(targetX, targetY);

        RectTransform closestVisual = null;
        float closestDistance = float.MaxValue;
        RectTransform root = GetTrainRoot(train.type);
        Vector2 rootOffset = root != null ? root.anchoredPosition : Vector2.zero;

        foreach (RectTransform visual in visuals)
        {
            float distance = (visual.anchoredPosition + rootOffset - targetPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestVisual = visual;
            }
        }

        return closestDistance <= 60f * 60f ? closestVisual : null;
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
