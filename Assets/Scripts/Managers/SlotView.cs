using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Owns the server-authoritative 5x3 reel presentation for Gold Mine Mania.
/// Feature outcomes are never generated here; this class only presents the
/// matrix and wins supplied by the server.
/// </summary>
public class SlotView : MonoBehaviour
{
    private const int DefaultReelCount = 5;
    private const int DefaultRowCount = 3;
    private const int FirstGoldBurstLockedSymbolId = 11;
    private const int LastGoldBurstLockedSymbolId = 13;
    private const float TrainAnimationFramesPerSecond = 30f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SymbolInfoCard symbolInfoCard;
    [SerializeField] private SlotFeatureVisualController featureVisualController;

    [Header("Symbol Sprites - Assign by Init Name")]
    [SerializeField] private Sprite spriteMiner;                       // 0
    [SerializeField] private Sprite spriteDonkey;                      // 1
    [SerializeField] private Sprite spriteGoldHelmet;                  // 2
    [SerializeField] private Sprite spriteBoots;                       // 3
    [SerializeField] private Sprite spriteLantern;                     // 4
    [SerializeField] private Sprite spriteA;                           // 5
    [SerializeField] private Sprite spriteK;                           // 6
    [SerializeField] private Sprite spriteQ;                           // 7
    [SerializeField] private Sprite spriteJ;                           // 8
    [SerializeField] private Sprite spriteWild;                        // 9
    [SerializeField] private Sprite spriteFreeGameScatter;             // 10
    [SerializeField] private Sprite spriteGoldBurstScatter;            // 11
    [SerializeField] private Sprite spriteMegaGoldBurstScatter;        // 12
    [SerializeField] private Sprite spriteUltimateGoldBurstScatter;    // 13
    [SerializeField] private Sprite spriteGoldMineJourney;             // 14

    [Header("Winning Symbol Animations")]
    [SerializeField, Min(0.1f)] private float winSymbolLoopDuration = 2f;
    [SerializeField] private List<Sprite> animSpritesMiner = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesDonkey = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesGoldHelmet = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesBoots = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesLantern = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesA = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesK = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesQ = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesJ = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesWild = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesFreeGameScatter = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesFreeGameTrigger = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesGoldBurstScatter = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesMegaGoldBurstScatter = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesUltimateGoldBurstScatter = new List<Sprite>();

    [Header("Free Games Train Animation")]
    [SerializeField, Min(0.01f)] private float trainSymbolScale = 1.3f;

    [Header("Reels")]
    [Tooltip("Optional. If empty, 5x3SlotHolder (or Slots) is discovered automatically.")]
    [SerializeField] private Transform reelRoot;
    [SerializeField] private Transform[] reelTransforms = Array.Empty<Transform>();

    [Header("Visible Result Slots")]
    [Tooltip("Assign the top, middle and bottom result Image for each reel, from left to right.")]
    [SerializeField] private ReelResultSlots[] resultSlotsByReel =
        new ReelResultSlots[DefaultReelCount];

    [Header("Spin Timing")]
    [SerializeField, Min(1)] private int minSpinCyclesBeforeStop = 3;
    [SerializeField, Min(0f)] private float reelStartStagger = 0.08f;
    [FormerlySerializedAs("reelStopStagger")]
    [SerializeField, Min(0f)] private float normalReelStopInterval = 0.6f;
    [SerializeField, Min(0f)] private float turboReelStopInterval = 0.06f;
    [FormerlySerializedAs("quickStopStagger")]
    [SerializeField, Min(0f)] private float quickReelStopInterval = 0.03f;
    [FormerlySerializedAs("spinSpeed")]
    [SerializeField, Min(100f)] private float normalReelSpeed = 4700f;
    [SerializeField, Min(100f)] private float fastReelSpeed = 6000f;
    [FormerlySerializedAs("anticipationUpDistance")]
    [SerializeField, Min(0f)] private float stopAnticipationDistance = 20f;
    [SerializeField, Min(0f)] private float stopOvershootDistance = 1f;
    [SerializeField, Min(0.01f)] private float stopOvershootDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float stopSettleDuration = 0.3f;

    [Header("Free Game Scatter Anticipation Timing")]
    [SerializeField, Min(0f)] private float scatterAnticipationDuration = 1.5f;
    [SerializeField, Min(1f)] private float scatterAnticipationSpeedMultiplier = 1.15f;

    internal List<List<int>> currentDisplayMatrix;

    private readonly List<ReelRuntime> reels = new List<ReelRuntime>();
    private readonly List<GoldBurstCellRuntime> goldBurstCells = new List<GoldBurstCellRuntime>();
    private readonly List<Tween> activeTweens = new List<Tween>();
    private readonly Dictionary<int, Sprite> spritesByServerId = new Dictionary<int, Sprite>();
    private readonly Dictionary<int, List<Sprite>> winAnimationFramesByServerId =
        new Dictionary<int, List<Sprite>>();
    private readonly Dictionary<Image, Vector3> originalResultSlotScales =
        new Dictionary<Image, Vector3>();
    private readonly List<int> mappedServerSymbolIds = new List<int>();
    private readonly HashSet<int> reportedUnknownSymbolIds = new HashSet<int>();
    private readonly HashSet<string> reportedResultSlotIssues = new HashSet<string>();

    private Coroutine reelStartRoutine;
    private Coroutine reelStopRoutine;
    private bool isSpinning;
    private bool quickStopRequested;
    private readonly List<WinAnimationRuntime> activeWinAnimations =
        new List<WinAnimationRuntime>();
    private readonly List<TrainSymbolAnimationRuntime> activeTrainAnimations =
        new List<TrainSymbolAnimationRuntime>();
    private int winAnimationSession;
    private int requiredWinAnimationLoops;
    private bool firstWinAnimationLoopReported;
    private Action firstWinAnimationLoopCallback;
    private Action winAnimationCompleteCallback;

    private sealed class ReelRuntime
    {
        internal RectTransform transform;
        internal readonly List<Image> symbols = new List<Image>();
        internal Vector2 restingPosition;
        internal float symbolPitch;
        internal Tween motionTween;
        internal float motionBasePixelsPerSecond;
        internal Tween stopTween;
        internal bool isAnticipating;
        internal int completedCycles;
    }

    private sealed class GoldBurstCellRuntime
    {
        internal int reelIndex;
        internal int row;
        internal Image symbolImage;
        internal Mask mask;
        internal RectTransform spinner;
        internal Image firstSpinnerImage;
        internal Image lastSpinnerImage;
        internal Vector2 restingPosition;
        internal Tween motionTween;
    }

    private sealed class WinAnimationRuntime
    {
        internal Image baseImage;
        internal bool baseImageWasEnabled;
        internal RectTransform animationCell;
        internal ImageAnimation animation;
        internal int completedLoops;
    }

    private sealed class TrainSymbolAnimationRuntime
    {
        internal int reelIndex;
        internal int row;
        internal Image baseImage;
        internal bool baseImageWasEnabled;
        internal Vector3 baseImageOriginalScale;
        internal RectTransform animationCell;
        internal Vector3 animationCellOriginalScale;
        internal Image animationCellImage;
        internal bool animationCellImageWasEnabled;
        internal ImageAnimation animation;
    }

    private void Awake()
    {
        gameManager = gameManager != null ? gameManager : FindSceneComponent<GameManager>();
        symbolInfoCard = symbolInfoCard != null
            ? symbolInfoCard
            : FindSceneComponent<SymbolInfoCard>();

        BuildReelCache();

        featureVisualController = featureVisualController != null
            ? featureVisualController
            : GetComponent<SlotFeatureVisualController>();
        if (featureVisualController == null)
        {
            featureVisualController = gameObject.AddComponent<SlotFeatureVisualController>();
        }

        Transform featureSearchRoot = reelRoot != null ? reelRoot.parent : null;
        featureVisualController.Initialize(featureSearchRoot, resultSlotsByReel);
        symbolInfoCard?.HideCard();
    }

    private void Start()
    {
        EnsureConfiguration();
    }

    private void OnDisable()
    {
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopViewCoroutines();
        KillAllTweens();
        featureVisualController?.ResetFeatures();
        isSpinning = false;
    }

    private void OnDestroy()
    {
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopViewCoroutines();
        KillAllTweens();
    }

    private void EnsureConfiguration()
    {
        if (gameManager == null)
        {
            gameManager = FindSceneComponent<GameManager>();
        }

        if (gameManager?.gameConfig != null)
        {
            BuildServerSymbolMapping(gameManager.gameConfig.symbols);
        }
    }

    #region Reel and symbol setup

    private void BuildReelCache()
    {
        reels.Clear();
        reportedResultSlotIssues.Clear();
        EnsureResultSlotArraySize();

        Transform discoveredRoot = reelRoot != null
            ? reelRoot
            : FindSceneTransform("5x3SlotHolder") ?? FindSceneTransform("Slots");

        List<RectTransform> candidates = new List<RectTransform>();
        if (discoveredRoot != null)
        {
            for (int i = 0; i < discoveredRoot.childCount; i++)
            {
                if (discoveredRoot.GetChild(i) is RectTransform rect && HasDirectSymbolImages(rect))
                {
                    candidates.Add(rect);
                }
            }
        }

        if (candidates.Count == 0 && reelTransforms != null)
        {
            candidates.AddRange(reelTransforms
                .OfType<RectTransform>()
                .Where(HasDirectSymbolImages));
        }

        foreach (RectTransform reelTransform in candidates.Take(DefaultReelCount))
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(reelTransform);

            ReelRuntime reel = new ReelRuntime
            {
                transform = reelTransform,
                restingPosition = reelTransform.anchoredPosition,
                symbolPitch = CalculateSymbolPitch(reelTransform)
            };

            for (int symbolIndex = 0; symbolIndex < reelTransform.childCount; symbolIndex++)
            {
                Transform symbolTransform = reelTransform.GetChild(symbolIndex);
                Image symbolImage = symbolTransform.GetComponent<Image>() ??
                                    symbolTransform.GetComponentInChildren<Image>(true);
                if (symbolImage != null)
                {
                    reel.symbols.Add(symbolImage);
                }
            }

            reels.Add(reel);
        }

        if (reels.Count == 0)
        {
            Debug.LogError("[SlotView] No reel strips with symbol Images were found.", this);
            return;
        }

        if (reels.Count != DefaultReelCount)
        {
            Debug.LogError($"[SlotView] Found {reels.Count} reels; expected {DefaultReelCount}.", this);
        }

        reelRoot = discoveredRoot;
        reelTransforms = reels.Select(reel => (Transform)reel.transform).ToArray();

        SetupSymbolButtons(GetRowCount());
        BuildGoldBurstCellCache();
    }

    private void EnsureResultSlotArraySize()
    {
        if (resultSlotsByReel != null && resultSlotsByReel.Length == DefaultReelCount) return;

        ReelResultSlots[] previous = resultSlotsByReel;
        resultSlotsByReel = new ReelResultSlots[DefaultReelCount];
        if (previous != null)
        {
            Array.Copy(previous, resultSlotsByReel, Mathf.Min(previous.Length, resultSlotsByReel.Length));
        }
    }

    private static bool HasDirectSymbolImages(RectTransform candidate)
    {
        if (candidate == null) return false;

        for (int i = 0; i < candidate.childCount; i++)
        {
            Transform child = candidate.GetChild(i);
            if (child.GetComponent<Image>() != null || child.GetComponentInChildren<Image>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static float CalculateSymbolPitch(RectTransform reelTransform)
    {
        float height = 200f;
        float spacing = 0f;

        if (reelTransform.childCount > 0 && reelTransform.GetChild(0) is RectTransform firstSymbol)
        {
            height = Mathf.Max(1f, firstSymbol.rect.height);
        }

        VerticalLayoutGroup layout = reelTransform.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            spacing = layout.spacing;
        }

        return Mathf.Max(1f, height + spacing);
    }

    private void SetupSymbolButtons(int rowCount)
    {
        int safeRows = Mathf.Min(DefaultRowCount, Mathf.Max(1, rowCount));
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            for (int row = 0; row < safeRows; row++)
            {
                Image symbolImage = GetResultSlotImage(reelIndex, row);
                if (symbolImage == null) continue;

                SymbolButtonHandler handler = symbolImage.GetComponent<SymbolButtonHandler>();
                if (handler == null)
                {
                    handler = symbolImage.gameObject.AddComponent<SymbolButtonHandler>();
                }

                handler.Init(reelIndex, row, this);
            }
        }
    }

    private void BuildGoldBurstCellCache()
    {
        goldBurstCells.Clear();

        int rowCount = Mathf.Min(DefaultRowCount, GetRowCount());
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            for (int row = 0; row < rowCount; row++)
            {
                Image symbolImage = GetResultSlotImage(reelIndex, row);
                if (symbolImage == null) continue;

                Transform maskTransform = symbolImage.transform.Find("Mask");
                RectTransform spinner = maskTransform?.Find("SpinningSlot") as RectTransform;
                if (maskTransform == null || spinner == null || spinner.childCount == 0) continue;

                Mask mask = maskTransform.GetComponent<Mask>();
                Image firstSpinnerImage = spinner.GetChild(0).GetComponent<Image>() ??
                                          spinner.GetChild(0).GetComponentInChildren<Image>(true);
                Image lastSpinnerImage = spinner.GetChild(spinner.childCount - 1).GetComponent<Image>() ??
                                         spinner.GetChild(spinner.childCount - 1).GetComponentInChildren<Image>(true);
                if (mask == null || firstSpinnerImage == null || lastSpinnerImage == null) continue;

                symbolImage.enabled = true;
                mask.gameObject.SetActive(false);
                spinner.gameObject.SetActive(false);
                goldBurstCells.Add(new GoldBurstCellRuntime
                {
                    reelIndex = reelIndex,
                    row = row,
                    symbolImage = symbolImage,
                    mask = mask,
                    spinner = spinner,
                    firstSpinnerImage = firstSpinnerImage,
                    lastSpinnerImage = lastSpinnerImage,
                    restingPosition = spinner.anchoredPosition
                });
            }
        }
    }

    private Image GetResultSlotImage(int reelIndex, int row)
    {
        if (reelIndex < 0 || reelIndex >= reels.Count || row < 0 || row >= DefaultRowCount)
        {
            return null;
        }

        ReelResultSlots slots = resultSlotsByReel != null && reelIndex < resultSlotsByReel.Length
            ? resultSlotsByReel[reelIndex]
            : null;
        Image image = slots?.Get(row);

        if (image == null)
        {
            ReportResultSlotIssue(
                $"missing-{reelIndex}-{row}",
                $"[SlotView] Assign reel {reelIndex + 1} result slot '{GetResultRowName(row)}' in the Inspector.");
            return null;
        }

        if (!reels[reelIndex].symbols.Contains(image))
        {
            ReportResultSlotIssue(
                $"wrong-reel-{reelIndex}-{row}",
                $"[SlotView] Reel {reelIndex + 1} result slot '{GetResultRowName(row)}' is not an Image in that reel strip.");
            return null;
        }

        return image;
    }

    private int GetTravelSymbolCount(int reelIndex)
    {
        if (reelIndex < 0 || reelIndex >= reels.Count) return DefaultRowCount;

        ReelRuntime reel = reels[reelIndex];
        Image top = GetResultSlotImage(reelIndex, 0);
        if (top == null) return DefaultRowCount;

        int topIndex = reel.symbols.IndexOf(top);
        Image middle = GetResultSlotImage(reelIndex, 1);
        Image bottom = GetResultSlotImage(reelIndex, 2);
        int middleIndex = reel.symbols.IndexOf(middle);
        int bottomIndex = reel.symbols.IndexOf(bottom);

        if (middleIndex != topIndex + 1 || bottomIndex != topIndex + 2)
        {
            ReportResultSlotIssue(
                $"order-{reelIndex}",
                $"[SlotView] Reel {reelIndex + 1} result Images must be consecutive in top, middle, bottom order.");
        }

        return Mathf.Max(DefaultRowCount, topIndex);
    }

    private void ReportResultSlotIssue(string key, string message)
    {
        if (reportedResultSlotIssues.Add(key)) Debug.LogError(message, this);
    }

    private static string GetResultRowName(int row)
    {
        switch (row)
        {
            case 0: return "Top";
            case 1: return "Middle";
            case 2: return "Bottom";
            default: return row.ToString();
        }
    }

    private bool BuildServerSymbolMapping(List<SymbolInfo> symbols)
    {
        spritesByServerId.Clear();
        winAnimationFramesByServerId.Clear();
        mappedServerSymbolIds.Clear();
        reportedUnknownSymbolIds.Clear();

        if (symbols == null || symbols.Count == 0)
        {
            Debug.LogWarning("[SlotView] Init did not provide symbol definitions.", this);
            return false;
        }

        bool complete = true;
        HashSet<int> ids = new HashSet<int>();
        foreach (SymbolInfo symbol in symbols)
        {
            if (symbol == null || !ids.Add(symbol.id))
            {
                complete = false;
                Debug.LogError("[SlotView] Init contains a null or duplicate symbol entry.", this);
                continue;
            }

            if (!TryResolveNamedSymbol(NormalizeSymbolName(symbol.name), out Sprite sprite) || sprite == null)
            {
                complete = false;
                Debug.LogError(
                    $"[SlotView] Assign a sprite for init symbol {symbol.id} ('{symbol.name}').",
                    this);
                continue;
            }

            spritesByServerId.Add(symbol.id, sprite);
            mappedServerSymbolIds.Add(symbol.id);

            if (TryResolveNamedWinAnimation(
                    NormalizeSymbolName(symbol.name),
                    out List<Sprite> animationFrames) &&
                animationFrames != null && animationFrames.Count > 0)
            {
                winAnimationFramesByServerId[symbol.id] = animationFrames;
            }
        }

        if (complete)
        {
            Debug.Log($"[SlotView] Mapped all {mappedServerSymbolIds.Count} init symbols.", this);
        }

        return complete;
    }

    private bool TryResolveNamedSymbol(string normalizedName, out Sprite sprite)
    {
        sprite = null;
        switch (normalizedName)
        {
            case "miner": sprite = spriteMiner; break;
            case "donkey": sprite = spriteDonkey; break;
            case "goldhelmet":
            case "helmet": sprite = spriteGoldHelmet; break;
            case "boots":
            case "boot": sprite = spriteBoots; break;
            case "lantern": sprite = spriteLantern; break;
            case "a":
            case "ace": sprite = spriteA; break;
            case "k":
            case "king": sprite = spriteK; break;
            case "q":
            case "queen": sprite = spriteQ; break;
            case "j":
            case "jack": sprite = spriteJ; break;
            case "wild": sprite = spriteWild; break;
            case "freegamescatter":
            case "freegame": sprite = spriteFreeGameScatter; break;
            case "goldburstscatter":
            case "goldburst": sprite = spriteGoldBurstScatter; break;
            case "megagoldburstscatter":
            case "megagoldburst": sprite = spriteMegaGoldBurstScatter; break;
            case "ultimategoldburstscatter":
            case "ultimategoldburst": sprite = spriteUltimateGoldBurstScatter; break;
            case "goldminejourney": sprite = spriteGoldMineJourney; break;
            default: return false;
        }

        return true;
    }

    private bool TryResolveNamedWinAnimation(
        string normalizedName,
        out List<Sprite> animationFrames)
    {
        animationFrames = null;
        switch (normalizedName)
        {
            case "miner": animationFrames = animSpritesMiner; break;
            case "donkey": animationFrames = animSpritesDonkey; break;
            case "goldhelmet":
            case "helmet": animationFrames = animSpritesGoldHelmet; break;
            case "boots":
            case "boot": animationFrames = animSpritesBoots; break;
            case "lantern": animationFrames = animSpritesLantern; break;
            case "a":
            case "ace": animationFrames = animSpritesA; break;
            case "k":
            case "king": animationFrames = animSpritesK; break;
            case "q":
            case "queen": animationFrames = animSpritesQ; break;
            case "j":
            case "jack": animationFrames = animSpritesJ; break;
            case "wild": animationFrames = animSpritesWild; break;
            case "freegamescatter":
            case "freegame": animationFrames = animSpritesFreeGameScatter; break;
            case "goldburstscatter":
            case "goldburst": animationFrames = animSpritesGoldBurstScatter; break;
            case "megagoldburstscatter":
            case "megagoldburst": animationFrames = animSpritesMegaGoldBurstScatter; break;
            case "ultimategoldburstscatter":
            case "ultimategoldburst": animationFrames = animSpritesUltimateGoldBurstScatter; break;
            default: return false;
        }

        return true;
    }

    private static string NormalizeSymbolName(string symbolName)
    {
        return string.IsNullOrWhiteSpace(symbolName)
            ? string.Empty
            : new string(symbolName
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private Sprite GetSymbolSprite(int symbolId)
    {
        if (spritesByServerId.TryGetValue(symbolId, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        if (reportedUnknownSymbolIds.Add(symbolId))
        {
            Debug.LogError($"[SlotView] Symbol id {symbolId} is missing a sprite mapping.", this);
        }

        return spritesByServerId.Values.FirstOrDefault(candidate => candidate != null);
    }

    #endregion

    #region Public slot flow

    internal void SetInitialMatrix(List<List<int>> matrix)
    {
        if (reels.Count == 0) BuildReelCache();

        EnsureConfiguration();
        SetupSymbolButtons(GetRowCount());

        if (!IsValidMatrix(matrix))
        {
            Debug.LogWarning("[SlotView] Ignored an invalid initial matrix.", this);
            return;
        }

        ApplyMatrix(matrix);
    }

    internal void PrepareTwoSlotBarrels(IReadOnlyList<TwoSlotBarrelPlacement> placements)
    {
        featureVisualController?.PrepareTwoSlotBarrels(placements);
    }

    internal void PrepareThreeSlotBarrels(IReadOnlyList<ThreeSlotBarrelPlacement> placements)
    {
        featureVisualController?.PrepareThreeSlotBarrels(placements);
    }

    internal void PrepareTrains(IReadOnlyList<TrainPlacement> placements)
    {
        featureVisualController?.PrepareTrains(placements);
    }

    internal void ShowWinningSymbolAnimations(
        IReadOnlyList<WinLine> winLines,
        int requiredLoops,
        Action onFirstLoopComplete,
        Action onRequiredLoopsComplete)
    {
        StopWinningSymbolAnimations();
        EnsureConfiguration();

        requiredWinAnimationLoops = Mathf.Max(0, requiredLoops);
        firstWinAnimationLoopCallback = onFirstLoopComplete;
        winAnimationCompleteCallback = onRequiredLoopsComplete;
        int session = winAnimationSession;

        var winningPositions = new HashSet<int>();
        if (winLines != null)
        {
            foreach (WinLine winLine in winLines)
            {
                if (winLine?.positions == null) continue;
                foreach (int position in winLine.positions)
                {
                    winningPositions.Add(position);
                }
            }
        }

        foreach (int flatPosition in winningPositions)
        {
            int row = flatPosition / DefaultReelCount;
            int reelIndex = flatPosition % DefaultReelCount;
            if (flatPosition < 0 ||
                reelIndex < 0 || reelIndex >= DefaultReelCount ||
                row < 0 || row >= DefaultRowCount ||
                currentDisplayMatrix == null ||
                reelIndex >= currentDisplayMatrix.Count ||
                currentDisplayMatrix[reelIndex] == null ||
                row >= currentDisplayMatrix[reelIndex].Count)
            {
                continue;
            }

            int symbolId = currentDisplayMatrix[reelIndex][row];
            if (symbolId == GetFreeGameScatterId()) continue;

            if (!winAnimationFramesByServerId.TryGetValue(
                    symbolId,
                    out List<Sprite> animationFrames) ||
                animationFrames == null || animationFrames.Count == 0)
            {
                continue;
            }

            Image baseImage = GetResultSlotImage(reelIndex, row);
            if (baseImage == null ||
                featureVisualController == null ||
                !featureVisualController.TryAcquireWinAnimationCell(
                    reelIndex,
                    row,
                    out RectTransform animationCell))
            {
                continue;
            }

            Image slotAnimationImage = animationCell.GetComponent<Image>();
            if (slotAnimationImage == null)
            {
                featureVisualController.ReleaseWinAnimationCell(animationCell);
                continue;
            }

            ImageAnimation animation = animationCell.GetComponent<ImageAnimation>();
            if (animation == null)
            {
                animation = animationCell.gameObject.AddComponent<ImageAnimation>();
            }

            animation.StopAnimation();
            animation.textureArray = animationFrames;
            animation.rendererDelegate = slotAnimationImage;
            animation.doLoopAnimation = true;
            animation.delayBetweenLoop = 0f;
            animation.SetLoopDuration(winSymbolLoopDuration);

            if (animation.rendererDelegate != null)
            {
                Color animationColor = animation.rendererDelegate.color;
                animation.rendererDelegate.color = new Color(
                    animationColor.r,
                    animationColor.g,
                    animationColor.b,
                    1f);
                animation.rendererDelegate.enabled = true;
            }

            var runtime = new WinAnimationRuntime
            {
                baseImage = baseImage,
                baseImageWasEnabled = baseImage.enabled,
                animationCell = animationCell,
                animation = animation
            };
            activeWinAnimations.Add(runtime);
            baseImage.enabled = false;

            animation.onLoopComplete = loopCount =>
                HandleWinAnimationLoop(session, runtime, loopCount);
        }

        if (activeWinAnimations.Count == 0)
        {
            CompleteUnavailableWinAnimation();
            return;
        }

        foreach (WinAnimationRuntime runtime in activeWinAnimations)
        {
            runtime.animation.StartAnimation();
        }
    }

    internal void StopWinningSymbolAnimations()
    {
        winAnimationSession++;

        foreach (WinAnimationRuntime runtime in activeWinAnimations)
        {
            if (runtime.animation != null)
            {
                runtime.animation.onLoopComplete = null;
                runtime.animation.doLoopAnimation = false;
                runtime.animation.StopAnimation();
                runtime.animation.ClearLoopDuration();
            }

            if (runtime.baseImage != null)
            {
                runtime.baseImage.enabled = runtime.baseImageWasEnabled;
            }

            featureVisualController?.ReleaseWinAnimationCell(runtime.animationCell);
        }

        activeWinAnimations.Clear();
        firstWinAnimationLoopCallback = null;
        winAnimationCompleteCallback = null;
        requiredWinAnimationLoops = 0;
        firstWinAnimationLoopReported = false;
    }

    private void HandleWinAnimationLoop(
        int session,
        WinAnimationRuntime runtime,
        int loopCount)
    {
        if (session != winAnimationSession || runtime == null) return;

        runtime.completedLoops = Mathf.Max(runtime.completedLoops, loopCount);
        if (!firstWinAnimationLoopReported &&
            activeWinAnimations.All(active => active.completedLoops >= 1))
        {
            firstWinAnimationLoopReported = true;
            Action firstLoopCallback = firstWinAnimationLoopCallback;
            firstWinAnimationLoopCallback = null;
            firstLoopCallback?.Invoke();
        }

        if (session != winAnimationSession || requiredWinAnimationLoops <= 0 ||
            !activeWinAnimations.All(active =>
                active.completedLoops >= requiredWinAnimationLoops))
        {
            return;
        }

        Action completionCallback = winAnimationCompleteCallback;
        StopWinningSymbolAnimations();
        completionCallback?.Invoke();
    }

    private void CompleteUnavailableWinAnimation()
    {
        Action firstLoopCallback = firstWinAnimationLoopCallback;
        Action completionCallback = winAnimationCompleteCallback;
        int requiredLoops = requiredWinAnimationLoops;

        firstWinAnimationLoopCallback = null;
        winAnimationCompleteCallback = null;
        requiredWinAnimationLoops = 0;
        firstWinAnimationLoopReported = true;

        firstLoopCallback?.Invoke();
        if (requiredLoops > 0) completionCallback?.Invoke();
    }

    private void StartTrainLandingAnimationsForReel(
        int reelIndex,
        IReadOnlyList<int> resultColumn)
    {
        if (resultColumn == null) return;

        int rowCount = Mathf.Min(DefaultRowCount, resultColumn.Count);
        int trainSymbolId = GetFreeGameScatterId();
        for (int row = 0; row < rowCount; row++)
        {
            if (resultColumn[row] == trainSymbolId)
            {
                StartTrainLandingAnimation(reelIndex, row);
            }
        }
    }

    private void StartTrainLandingAnimation(int reelIndex, int row)
    {
        if (animSpritesFreeGameScatter == null ||
            animSpritesFreeGameScatter.Count == 0 ||
            activeTrainAnimations.Any(active =>
                active.reelIndex == reelIndex && active.row == row))
        {
            return;
        }

        Image baseImage = GetResultSlotImage(reelIndex, row);
        if (baseImage == null ||
            featureVisualController == null ||
            !featureVisualController.TryAcquireTrainAnimationCell(
                reelIndex,
                row,
                out RectTransform animationCell))
        {
            return;
        }

        Image animationCellImage = animationCell.GetComponent<Image>();
        if (animationCellImage == null)
        {
            featureVisualController.ReleaseTrainAnimationCell(animationCell);
            return;
        }

        var runtime = new TrainSymbolAnimationRuntime
        {
            reelIndex = reelIndex,
            row = row,
            baseImage = baseImage,
            baseImageWasEnabled = baseImage.enabled,
            baseImageOriginalScale = GetOriginalResultSlotScale(baseImage),
            animationCell = animationCell,
            animationCellOriginalScale = animationCell.localScale,
            animationCellImage = animationCellImage,
            animationCellImageWasEnabled = animationCellImage.enabled,
            animation = animationCell.GetComponent<ImageAnimation>() ??
                        animationCell.gameObject.AddComponent<ImageAnimation>()
        };

        baseImage.rectTransform.localScale =
            runtime.baseImageOriginalScale * trainSymbolScale;
        animationCell.localScale =
            runtime.animationCellOriginalScale * trainSymbolScale;
        animationCellImage.enabled = true;

        baseImage.enabled = false;
        activeTrainAnimations.Add(runtime);
        StartTrainSpriteAnimation(runtime, animSpritesFreeGameScatter, true);
    }

    internal IEnumerator PlayFreeGameTrainTriggerAnimation()
    {
        List<TrainSymbolAnimationRuntime> activeAnimations = activeTrainAnimations
            .Where(active => active?.animation != null)
            .ToList();
        if (activeAnimations.Count == 0 ||
            animSpritesFreeGameTrigger == null ||
            animSpritesFreeGameTrigger.Count == 0)
        {
            yield break;
        }

        float triggerDuration =
            animSpritesFreeGameTrigger.Count / TrainAnimationFramesPerSecond;
        foreach (TrainSymbolAnimationRuntime active in activeAnimations)
        {
            StartTrainSpriteAnimation(active, animSpritesFreeGameTrigger, false);
        }

        yield return new WaitForSeconds(triggerDuration);
    }

    private static void StartTrainSpriteAnimation(
        TrainSymbolAnimationRuntime runtime,
        List<Sprite> frames,
        bool shouldLoop)
    {
        if (runtime?.animation == null ||
            runtime.animationCellImage == null ||
            frames == null || frames.Count == 0)
        {
            return;
        }

        runtime.animation.StopAnimation();
        runtime.animation.textureArray = frames;
        runtime.animation.rendererDelegate = runtime.animationCellImage;
        runtime.animation.doLoopAnimation = shouldLoop;
        runtime.animation.delayBetweenLoop = 0f;
        runtime.animation.SetLoopDuration(
            frames.Count / TrainAnimationFramesPerSecond);
        runtime.animation.onLoopComplete = null;
        runtime.animation.StartAnimation();
    }

    private void StopTrainSymbolAnimations()
    {
        foreach (TrainSymbolAnimationRuntime runtime in activeTrainAnimations)
        {
            if (runtime?.animation != null)
            {
                runtime.animation.onLoopComplete = null;
                runtime.animation.doLoopAnimation = false;
                runtime.animation.StopAnimation();
                runtime.animation.ClearLoopDuration();
            }

            RestoreTrainAnimationRuntime(runtime);
        }

        activeTrainAnimations.Clear();
    }

    private void RestoreTrainAnimationRuntime(TrainSymbolAnimationRuntime runtime)
    {
        if (runtime == null) return;

        if (runtime.baseImage != null)
        {
            runtime.baseImage.enabled = runtime.baseImageWasEnabled;
            runtime.baseImage.rectTransform.localScale = runtime.baseImageOriginalScale;
        }

        if (runtime.animationCellImage != null)
        {
            runtime.animationCellImage.enabled = runtime.animationCellImageWasEnabled;
        }

        if (runtime.animationCell != null)
        {
            runtime.animationCell.localScale = runtime.animationCellOriginalScale;
        }

        featureVisualController?.ReleaseTrainAnimationCell(runtime.animationCell);
    }

    internal void StartSpin()
    {
        if (isSpinning || reels.Count == 0) return;

        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        featureVisualController?.BeginSpinPresentation();
        EnsureConfiguration();
        HideSymbolInfoCard();
        KillReelTweens(true);

        quickStopRequested = false;
        isSpinning = true;
        reelStartRoutine = StartCoroutine(StartReelsSequentially());
    }

    internal void StartGoldBurstRespin()
    {
        if (isSpinning || reels.Count == 0) return;

        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        HideSymbolInfoCard();
        KillReelTweens(true);
        KillGoldBurstCellTweens(true);

        quickStopRequested = false;
        isSpinning = true;

        foreach (GoldBurstCellRuntime cell in goldBurstCells)
        {
            bool isLocked = currentDisplayMatrix != null &&
                            cell.reelIndex < currentDisplayMatrix.Count &&
                            currentDisplayMatrix[cell.reelIndex] != null &&
                            cell.row < currentDisplayMatrix[cell.reelIndex].Count &&
                            currentDisplayMatrix[cell.reelIndex][cell.row] >= FirstGoldBurstLockedSymbolId &&
                            currentDisplayMatrix[cell.reelIndex][cell.row] <= LastGoldBurstLockedSymbolId;

            if (isLocked)
            {
                cell.symbolImage.enabled = true;
                cell.mask.gameObject.SetActive(false);
                cell.spinner.gameObject.SetActive(false);
                continue;
            }

            cell.firstSpinnerImage.sprite = cell.symbolImage.sprite;
            cell.lastSpinnerImage.sprite = cell.symbolImage.sprite;
            cell.mask.gameObject.SetActive(true);
            cell.spinner.gameObject.SetActive(true);
            cell.symbolImage.enabled = false;
            StartGoldBurstCellMotion(cell);
        }
    }

    internal void StopGoldBurstRespin(List<List<int>> resultMatrix, Action onComplete)
    {
        if (!IsValidMatrix(resultMatrix))
        {
            Debug.LogError("[SlotView] Gold Burst result matrix does not match the visible slots.", this);
            KillGoldBurstCellTweens(true);
            CompleteGoldBurstRespin(onComplete);
            return;
        }

        ApplyMatrix(resultMatrix);

        Sequence settleSequence = DOTween.Sequence().SetUpdate(true);
        bool hasSpinningCells = false;

        foreach (GoldBurstCellRuntime cell in goldBurstCells)
        {
            cell.motionTween?.Kill();
            cell.motionTween = null;

            if (!cell.mask.gameObject.activeSelf || !cell.spinner.gameObject.activeSelf) continue;

            hasSpinningCells = true;
            cell.firstSpinnerImage.sprite = cell.symbolImage.sprite;
            cell.lastSpinnerImage.sprite = cell.symbolImage.sprite;

            float travelDistance = CalculateSymbolPitch(cell.spinner) *
                                   Mathf.Max(1, cell.spinner.childCount - 1);
            float targetY = cell.restingPosition.y;
            float remainingDistance = Mathf.Max(0f, cell.spinner.anchoredPosition.y - targetY);
            float settleDuration = Mathf.Max(0.08f, remainingDistance / normalReelSpeed);

            settleSequence.Join(cell.spinner
                .DOAnchorPosY(targetY, settleDuration)
                .SetEase(Ease.OutCubic));
        }

        if (!hasSpinningCells)
        {
            settleSequence.Kill();
            CompleteGoldBurstRespin(onComplete);
            return;
        }

        settleSequence.OnComplete(() => CompleteGoldBurstRespin(onComplete));
        activeTweens.Add(settleSequence);
    }

    private void CompleteGoldBurstRespin(Action onComplete)
    {
        foreach (GoldBurstCellRuntime cell in goldBurstCells)
        {
            cell.spinner.anchoredPosition = cell.restingPosition;
            cell.symbolImage.enabled = true;
            cell.mask.gameObject.SetActive(false);
            cell.spinner.gameObject.SetActive(false);
        }

        activeTweens.RemoveAll(tween => tween == null || !tween.IsActive());
        isSpinning = false;
        featureVisualController?.RevealAllFeatures();
        AudioManager.Instance?.PlayReelStop();
        onComplete?.Invoke();
    }

    internal void StopSpin(List<List<int>> resultMatrix, Action onComplete)
    {
        BeginStop(resultMatrix, false, onComplete);
    }

    internal void QuickStop(List<List<int>> resultMatrix, Action onComplete = null)
    {
        BeginStop(resultMatrix, true, onComplete);
    }

    internal void ApplySpinSpeed(SpinSpeed speed)
    {
        if (gameManager != null) gameManager.currentSpinSpeed = speed;
        foreach (ReelRuntime reel in reels) ApplyReelMotionSpeed(reel);
    }

    internal List<List<int>> GetCurrentDisplayMatrix()
    {
        return CloneMatrix(currentDisplayMatrix);
    }

    internal bool IsSpinning()
    {
        return isSpinning;
    }

    internal void HideSymbolInfoCard()
    {
        symbolInfoCard?.HideCard();
    }

    internal void OnBetChanged()
    {
        HideSymbolInfoCard();
    }

    internal void OnSymbolClicked(int column, int row, RectTransform symbolRect)
    {
        if (isSpinning)
        {
            HideSymbolInfoCard();
            return;
        }

        if (currentDisplayMatrix == null || column < 0 || column >= currentDisplayMatrix.Count ||
            currentDisplayMatrix[column] == null || row < 0 || row >= currentDisplayMatrix[column].Count)
        {
            return;
        }

        symbolInfoCard?.ShowCard(currentDisplayMatrix[column][row], column, row, symbolRect, gameManager);
    }

    #endregion

    #region Reel motion

    private void StartGoldBurstCellMotion(GoldBurstCellRuntime cell)
    {
        cell.motionTween?.Kill();

        float symbolPitch = CalculateSymbolPitch(cell.spinner);
        float travelDistance = symbolPitch * Mathf.Max(1, cell.spinner.childCount - 1);
        float duration = Mathf.Max(0.08f, travelDistance / normalReelSpeed);

        cell.spinner.anchoredPosition = cell.restingPosition + Vector2.up * travelDistance;
        cell.motionTween = cell.spinner
            .DOAnchorPosY(cell.restingPosition.y, duration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);

        activeTweens.Add(cell.motionTween);
    }

    private IEnumerator StartReelsSequentially()
    {
        SpinSpeed speed = GetSpinSpeed();
        for (int reelIndex = 0; reelIndex < reels.Count && isSpinning; reelIndex++)
        {
            StartReelMotion(reelIndex);

            if (speed == SpinSpeed.Normal && reelIndex < reels.Count - 1 && reelStartStagger > 0f)
            {
                float remaining = reelStartStagger;
                while (remaining > 0f && isSpinning)
                {
                    remaining -= Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        reelStartRoutine = null;
    }

    private void StartReelMotion(int reelIndex)
    {
        ReelRuntime reel = reels[reelIndex];
        reel.motionTween?.Kill();
        reel.stopTween?.Kill();
        reel.stopTween = null;
        reel.transform.anchoredPosition = reel.restingPosition;
        reel.isAnticipating = false;
        reel.completedCycles = 0;

        int travelSymbols = GetTravelSymbolCount(reelIndex);
        float travelDistance = reel.symbolPitch * travelSymbols;
        float pixelsPerSecond = GetSpinSpeed() == SpinSpeed.Normal ? normalReelSpeed : fastReelSpeed;
        float duration = Mathf.Max(0.08f, travelDistance / pixelsPerSecond);
        reel.motionBasePixelsPerSecond = pixelsPerSecond;

        reel.motionTween = reel.transform
            .DOAnchorPosY(reel.restingPosition.y - travelDistance, duration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .OnStepComplete(() => reel.completedCycles++)
            .SetUpdate(true);

        activeTweens.Add(reel.motionTween);
    }

    private void BeginStop(List<List<int>> resultMatrix, bool quickStop, Action onComplete)
    {
        if (!isSpinning || reelStopRoutine != null) return;

        if (!IsValidMatrix(resultMatrix))
        {
            Debug.LogError("[SlotView] Server result matrix does not match the visible reels.", this);
            KillReelTweens(true);
            isSpinning = false;
            onComplete?.Invoke();
            return;
        }

        quickStopRequested = quickStop;
        if (reelStartRoutine != null)
        {
            StopCoroutine(reelStartRoutine);
            reelStartRoutine = null;
        }

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            ReelRuntime reel = reels[reelIndex];
            if (reel.motionTween == null || !reel.motionTween.IsActive()) StartReelMotion(reelIndex);
        }

        reelStopRoutine = StartCoroutine(StopReelsAndApplyMatrix(resultMatrix, quickStop, onComplete));
    }

    private IEnumerator StopReelsAndApplyMatrix(
        List<List<int>> resultMatrix,
        bool forceQuickStop,
        Action onComplete)
    {
        SpinSpeed requestedSpeed = GetSpinSpeed();
        bool quickStop = forceQuickStop || requestedSpeed == SpinSpeed.QuickSpin;

        if (!quickStop && requestedSpeed == SpinSpeed.Normal)
        {
            while (reels.Any(reel => reel.completedCycles < minSpinCyclesBeforeStop))
            {
                yield return null;
            }
        }

        currentDisplayMatrix = CloneMatrix(resultMatrix);

        SpinSpeed scheduledSpeed = quickStop
            ? SpinSpeed.QuickSpin
            : requestedSpeed == SpinSpeed.Turbo ? SpinSpeed.Turbo : SpinSpeed.Normal;
        float timingScale = GetStopTimingScale(scheduledSpeed);
        float stopInterval = GetReelStopInterval(scheduledSpeed);
        float overshoot = stopOvershootDistance * timingScale;
        float overshootDuration = stopOvershootDuration * timingScale;
        float settleDuration = stopSettleDuration * timingScale;

        int completedStops = 0;
        int stoppedFreeGameScatters = 0;
        float nextReelDelay = 0f;
        int anticipationThreshold = Mathf.Max(1, GetFreeGameMinTrigger() - 1);

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            if (reelIndex > 0) nextReelDelay += stopInterval;

            bool shouldAnticipate = !quickStop && stoppedFreeGameScatters == anticipationThreshold;
            float anticipation = shouldAnticipate
                ? scatterAnticipationDuration * (scheduledSpeed == SpinSpeed.Turbo ? timingScale : 1f)
                : 0f;

            int capturedReelIndex = reelIndex;
            StartCoroutine(StopSingleReel(
                capturedReelIndex,
                resultMatrix[capturedReelIndex],
                nextReelDelay,
                scheduledSpeed,
                anticipation,
                quickStop,
                overshoot,
                overshootDuration,
                settleDuration,
                () =>
                {
                    StartTrainLandingAnimationsForReel(
                        capturedReelIndex,
                        resultMatrix[capturedReelIndex]);
                    completedStops++;
                }));

            nextReelDelay += anticipation;
            stoppedFreeGameScatters += CountFreeGameScatters(resultMatrix[reelIndex]);
        }

        while (completedStops < reels.Count) yield return null;

        foreach (ReelRuntime reel in reels)
        {
            reel.transform.anchoredPosition = reel.restingPosition;
        }

        activeTweens.RemoveAll(tween => tween == null || !tween.IsActive());
        isSpinning = false;
        quickStopRequested = false;
        reelStopRoutine = null;
        featureVisualController?.RevealAllFeatures();
        onComplete?.Invoke();
    }

    private IEnumerator StopSingleReel(
        int reelIndex,
        List<int> resultColumn,
        float delay,
        SpinSpeed scheduledSpeed,
        float anticipationDuration,
        bool quickStop,
        float overshoot,
        float overshootDuration,
        float settleDuration,
        Action onComplete)
    {
        if (delay > 0f) yield return WaitForSpeedAdjustedStopDelay(delay, scheduledSpeed);

        ReelRuntime reel = reels[reelIndex];
        if (anticipationDuration > 0f)
        {
            yield return PlayScatterAnticipation(reelIndex, anticipationDuration, scheduledSpeed);
        }

        reel.isAnticipating = false;
        reel.motionTween?.Kill();
        reel.motionTween = null;

        float landingDistance = Mathf.Max(
            stopAnticipationDistance,
            reel.symbolPitch * (quickStop ? 0.75f : 2f));
        reel.transform.anchoredPosition = reel.restingPosition + Vector2.up * landingDistance;
        ApplyMatrixColumn(reelIndex, resultColumn);

        Sequence stopSequence = DOTween.Sequence().SetUpdate(true);
        stopSequence.Append(
            reel.transform
                .DOAnchorPosY(reel.restingPosition.y - overshoot, overshootDuration)
                .SetEase(Ease.OutQuad));
        stopSequence.Append(
            reel.transform
                .DOAnchorPos(reel.restingPosition, settleDuration)
                .SetEase(Ease.InOutQuad));

        reel.stopTween = stopSequence;
        activeTweens.Add(stopSequence);
        yield return stopSequence.WaitForCompletion();
        reel.stopTween = null;

        AudioManager.Instance?.PlayReelStop();
        onComplete?.Invoke();
    }

    private IEnumerator WaitForSpeedAdjustedStopDelay(float delay, SpinSpeed scheduledSpeed)
    {
        float remainingDelay = Mathf.Max(0f, delay);
        float scheduledInterval = GetReelStopInterval(scheduledSpeed);

        while (remainingDelay > 0f)
        {
            SpinSpeed effectiveSpeed = quickStopRequested ? SpinSpeed.QuickSpin : GetSpinSpeed();
            float currentInterval = GetReelStopInterval(effectiveSpeed);
            if (currentInterval <= 0f) yield break;

            float progressMultiplier = scheduledInterval > 0f
                ? scheduledInterval / currentInterval
                : GetStopTimingScale(scheduledSpeed) / GetStopTimingScale(effectiveSpeed);
            remainingDelay -= Time.unscaledDeltaTime * Mathf.Max(0.01f, progressMultiplier);
            yield return null;
        }
    }

    private void ApplyReelMotionSpeed(ReelRuntime reel)
    {
        if (reel?.motionTween == null || !reel.motionTween.IsActive() ||
            reel.motionBasePixelsPerSecond <= 0f)
        {
            return;
        }

        float targetSpeed = GetSpinSpeed() == SpinSpeed.Normal ? normalReelSpeed : fastReelSpeed;
        float anticipationMultiplier = reel.isAnticipating
            ? Mathf.Max(1f, scatterAnticipationSpeedMultiplier)
            : 1f;
        reel.motionTween.timeScale = Mathf.Max(
            0.01f,
            targetSpeed / reel.motionBasePixelsPerSecond * anticipationMultiplier);
    }

    private SpinSpeed GetSpinSpeed()
    {
        return gameManager != null ? gameManager.currentSpinSpeed : SpinSpeed.Normal;
    }

    private static float GetStopTimingScale(SpinSpeed speed)
    {
        switch (speed)
        {
            case SpinSpeed.QuickSpin: return 0.35f;
            case SpinSpeed.Turbo: return 0.55f;
            default: return 1f;
        }
    }

    private float GetReelStopInterval(SpinSpeed speed)
    {
        switch (speed)
        {
            case SpinSpeed.QuickSpin: return quickReelStopInterval;
            case SpinSpeed.Turbo: return turboReelStopInterval;
            default: return normalReelStopInterval;
        }
    }

    #endregion

    #region Free-game scatter anticipation

    private int CountFreeGameScatters(IReadOnlyList<int> resultColumn)
    {
        if (resultColumn == null) return 0;

        int count = 0;
        int rows = Mathf.Min(GetRowCount(), resultColumn.Count);
        int scatterId = GetFreeGameScatterId();
        for (int row = 0; row < rows; row++)
        {
            if (resultColumn[row] == scatterId) count++;
        }

        return count;
    }

    private IEnumerator PlayScatterAnticipation(
        int reelIndex,
        float duration,
        SpinSpeed scheduledSpeed)
    {
        if (duration <= 0f || reelIndex <= 0 || reelIndex >= reels.Count) yield break;

        ReelRuntime reel = reels[reelIndex];
        reel.isAnticipating = true;
        ApplyReelMotionSpeed(reel);

        float remaining = duration;
        float initialScale = GetStopTimingScale(scheduledSpeed);
        while (remaining > 0f && !quickStopRequested)
        {
            SpinSpeed effective = GetSpinSpeed();
            float currentScale = GetStopTimingScale(effective);
            remaining -= Time.unscaledDeltaTime * Mathf.Max(0.01f, initialScale / currentScale);
            yield return null;
        }

        reel.isAnticipating = false;
        ApplyReelMotionSpeed(reel);
    }

    #endregion

    #region Matrix application

    private void ApplyMatrix(List<List<int>> matrix)
    {
        if (!IsValidMatrix(matrix)) return;

        currentDisplayMatrix = CloneMatrix(matrix);
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            ApplyMatrixColumn(reelIndex, matrix[reelIndex]);
            reels[reelIndex].transform.anchoredPosition = reels[reelIndex].restingPosition;
        }
    }

    private void ApplyMatrixColumn(int reelIndex, List<int> column)
    {
        int rowCount = Mathf.Min(GetRowCount(), column.Count);

        for (int row = 0; row < rowCount; row++)
        {
            Image resultImage = GetResultSlotImage(reelIndex, row);
            if (resultImage == null) continue;

            Vector3 originalScale = GetOriginalResultSlotScale(resultImage);
            resultImage.rectTransform.localScale = column[row] == GetFreeGameScatterId()
                ? originalScale * trainSymbolScale
                : originalScale;

            Sprite sprite = GetSymbolSprite(column[row]);
            if (sprite != null) resultImage.sprite = sprite;
        }
    }

    private Vector3 GetOriginalResultSlotScale(Image resultImage)
    {
        if (resultImage == null) return Vector3.one;

        if (!originalResultSlotScales.TryGetValue(resultImage, out Vector3 originalScale))
        {
            originalScale = resultImage.rectTransform.localScale;
            originalResultSlotScales[resultImage] = originalScale;
        }

        return originalScale;
    }

    private bool IsValidMatrix(List<List<int>> matrix)
    {
        if (matrix == null || matrix.Count < reels.Count || reels.Count == 0) return false;

        int rowCount = GetRowCount();
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            if (matrix[reelIndex] == null || matrix[reelIndex].Count < rowCount) return false;
        }

        return true;
    }

    private static List<List<int>> CloneMatrix(List<List<int>> matrix)
    {
        return matrix?.Select(column => column != null
            ? new List<int>(column)
            : new List<int>()).ToList();
    }

    #endregion

    #region Helpers and cleanup

    private int GetRowCount()
    {
        return gameManager?.gameConfig != null && gameManager.gameConfig.rowCount > 0
            ? gameManager.gameConfig.rowCount
            : DefaultRowCount;
    }

    private int GetFreeGameScatterId()
    {
        return gameManager?.gameConfig != null ? gameManager.gameConfig.scatterSymbolId : 10;
    }

    private int GetFreeGameMinTrigger()
    {
        return gameManager?.gameConfig != null ? gameManager.gameConfig.freeGameMinTrigger : 3;
    }

    private void KillReelTweens(bool restorePositions)
    {
        foreach (ReelRuntime reel in reels)
        {
            reel.motionTween?.Kill();
            reel.motionTween = null;
            reel.stopTween?.Kill();
            reel.stopTween = null;
            reel.isAnticipating = false;

            if (restorePositions && reel.transform != null)
            {
                reel.transform.anchoredPosition = reel.restingPosition;
            }
        }
    }

    private void KillGoldBurstCellTweens(bool restoreVisuals)
    {
        foreach (GoldBurstCellRuntime cell in goldBurstCells)
        {
            cell.motionTween?.Kill();
            cell.motionTween = null;

            if (cell.spinner != null)
            {
                cell.spinner.anchoredPosition = cell.restingPosition;
                cell.spinner.gameObject.SetActive(false);
            }

            if (cell.mask != null)
            {
                cell.mask.gameObject.SetActive(false);
            }

            if (restoreVisuals && cell.symbolImage != null)
            {
                cell.symbolImage.enabled = true;
            }
        }
    }

    private void KillAllTweens()
    {
        KillReelTweens(true);
        KillGoldBurstCellTweens(true);
        foreach (Tween tween in activeTweens) tween?.Kill();
        activeTweens.Clear();
    }

    private void StopViewCoroutines()
    {
        StopAllCoroutines();
        reelStartRoutine = null;
        reelStopRoutine = null;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.name == objectName);
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate =>
                candidate != null && candidate.gameObject.scene.IsValid());
    }

    #endregion
}

[Serializable]
public class ReelResultSlots
{
    [SerializeField] private Image top;
    [SerializeField] private Image middle;
    [SerializeField] private Image bottom;

    public Image Get(int row)
    {
        switch (row)
        {
            case 0: return top;
            case 1: return middle;
            case 2: return bottom;
            default: return null;
        }
    }
}
