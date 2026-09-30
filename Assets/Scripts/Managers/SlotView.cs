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
    private const int MegaGoldBurstReelCount = 7;
    private const int UltimateGoldBurstReelCount = MegaGoldBurstReelCount * 2;
    private const int DefaultRowCount = 3;
    private const int FirstGoldBurstLockedSymbolId = 11;
    private const int LastGoldBurstLockedSymbolId = 13;
    private const float TrainAnimationFramesPerSecond = 30f;
    private const float BarrelBlastSourceFramesPerSecond = 30f;
    private const int GoldBoxRevealFramesBeforeBlastEnd = 5;
    private const float BarrelIdleSourceFramesPerSecond = 30f;
    private const int DefaultTrainLandingLoopsBeforeWins = 1;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SymbolInfoCard symbolInfoCard;
    [SerializeField] private SlotFeatureController featureVisualController;

    [Header("Landscape 5x3-only UI")]
    [SerializeField] private GameObject landscapeExtraUICanDeactivate;

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

    [Header("Free Games Train Animations")]
    [SerializeField] private List<Sprite> animSpritesTrainLanding = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesFreeGameTrigger = new List<Sprite>();

    [Header("Gold Burst Winning Symbol Animations")]
    [SerializeField] private List<Sprite> animSpritesGoldBurstScatter = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesMegaGoldBurstScatter = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesUltimateGoldBurstScatter = new List<Sprite>();

    [Header("Gold Burst Barrel Blast Animations")]
    [Tooltip("Optional dedicated animation used only when a 1-slot barrel converts into its gold box.")]
    [SerializeField] private List<Sprite> singleSlotBarrelBlastFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> redSingleSlotBarrelBlastFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> yellowSingleSlotBarrelBlastFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> doubleYellowSingleSlotBarrelBlastFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> twoSlotBarrelBlastFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> threeSlotBarrelBlastFrames = new List<Sprite>();
    [SerializeField, Range(0.1f, 1f)] private float barrelBlastPlaybackSpeed = 0.8f;

    [Header("Gold Burst Barrel Idle Animations")]
    [SerializeField] private List<Sprite> singleSlotBarrelIdleFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> twoSlotBarrelIdleFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> threeSlotBarrelIdleFrames = new List<Sprite>();
    [SerializeField, Range(0.1f, 3f)] private float barrelIdlePlaybackSpeed = 1f;

    [Header("Free Games Train Animation")]
    [SerializeField, Min(0.01f)] private float trainSymbolScale = 1.3f;
    [SerializeField, Min(0f)] private float trainBoxLeadInDelay = 0.5f;

    [Header("Reels")]
    [Tooltip("Optional. If empty, 5x3SlotHolder (or Slots) is discovered automatically.")]
    [SerializeField] private Transform reelRoot;
    [Tooltip("Optional. If empty, 7x3SlotHolder is discovered automatically.")]
    [SerializeField] private Transform megaGoldBurstReelRoot;
    [Tooltip("Optional. First authored reel holder inside Two7x3Slot.")]
    [SerializeField] private Transform ultimateGoldBurstFirstReelRoot;
    [Tooltip("Optional. Second authored reel holder inside Two7x3Slot.")]
    [SerializeField] private Transform ultimateGoldBurstSecondReelRoot;
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

    [Header("Reel Anticipation Timing")]
    [SerializeField, Min(0f)] private float scatterAnticipationDuration = 1.5f;
    [SerializeField, Min(1f)] private float scatterAnticipationSpeedMultiplier = 1.15f;

    internal List<List<int>> currentDisplayMatrix;

    private readonly List<ReelRuntime> reels = new List<ReelRuntime>();
    private readonly List<GoldBurstCellRuntime> goldBurstCells = new List<GoldBurstCellRuntime>();
    private readonly Dictionary<int, ImageAnimation> anticipationAnimationsByReel =
        new Dictionary<int, ImageAnimation>();
    private RectTransform anticipationAnimationRoot;
    private bool anticipationOwnsAnimationRoot;
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
    private readonly List<GoldBurstConversionRuntime> activeGoldBurstConversions =
        new List<GoldBurstConversionRuntime>();
    private readonly List<BarrelIdleAnimationRuntime> activeBarrelIdleAnimations =
        new List<BarrelIdleAnimationRuntime>();
    private readonly Dictionary<Image, bool> barrelIdleSourceImageStates =
        new Dictionary<Image, bool>();
    private readonly List<TwoSlotBarrelPlacement> preparedTwoSlotBarrels =
        new List<TwoSlotBarrelPlacement>();
    private readonly List<ThreeSlotBarrelPlacement> preparedThreeSlotBarrels =
        new List<ThreeSlotBarrelPlacement>();
    private readonly List<TrainPlacement> preparedTrains =
        new List<TrainPlacement>();
    private Transform baseGameReelRoot;
    private GameObject baseGameLayoutRoot;
    private GameObject megaGoldBurstLayoutRoot;
    private GameObject ultimateGoldBurstLayoutRoot;
    private bool isUsingMegaGoldBurstLayout;
    private bool isUsingUltimateGoldBurstLayout;
    private int winAnimationSession;
    private int requiredWinAnimationLoops;
    private bool firstWinAnimationLoopReported;
    private Action firstWinAnimationLoopCallback;
    private Action winAnimationCompleteCallback;
    private bool stopTrainLandingAnimationsBeforeWins;
    private bool trainLandingAnimationsEnabled = true;
    private int requiredTrainLandingLoopsBeforeWins =
        DefaultTrainLandingLoopsBeforeWins;
    private bool deferTrainLandingAnimationsUntilReelsStop;
    private bool trainLandingAnimationsReleased = true;

    internal Transform MegaGoldBurstReelRoot => megaGoldBurstReelRoot;
    internal Transform UltimateGoldBurstFirstReelRoot => ultimateGoldBurstFirstReelRoot;
    internal Transform UltimateGoldBurstSecondReelRoot => ultimateGoldBurstSecondReelRoot;
    internal GameObject BaseGameLayoutRoot => baseGameLayoutRoot;
    internal GameObject MegaGoldBurstLayoutRoot => megaGoldBurstLayoutRoot;
    internal GameObject UltimateGoldBurstLayoutRoot => ultimateGoldBurstLayoutRoot;
    internal List<List<int>> CurrentDisplayMatrix => currentDisplayMatrix;
    internal int ActiveRowCount => GetRowCount();
    internal bool IsUsingMegaGoldBurstLayout => isUsingMegaGoldBurstLayout;
    internal bool IsUsingUltimateGoldBurstLayout => isUsingUltimateGoldBurstLayout;

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
        internal int completedLandingLoops;
        internal Image baseImage;
        internal bool baseImageWasEnabled;
        internal Vector3 baseImageOriginalScale;
        internal RectTransform animationCell;
        internal Vector3 animationCellOriginalScale;
        internal Image animationCellImage;
        internal bool animationCellImageWasEnabled;
        internal ImageAnimation animation;
        internal Coroutine delayedStartRoutine;
        internal bool animationStarted;
    }

    private sealed class GoldBurstConversionRuntime
    {
        internal Image baseImage;
        internal bool baseImageWasEnabled;
        internal bool baseImageHidden;
        internal RectTransform animationCell;
        internal bool usesBarrelVisual;
        internal Vector3 originalScale;
        internal Vector3 originalPosition;
        internal Image animationImage;
        internal bool animationImageWasEnabled;
        internal Sprite originalSprite;
        internal Color originalColor;
        internal ImageAnimation animation;
        internal bool completed;
    }

    private sealed class BarrelIdleAnimationRuntime
    {
        internal int reelIndex;
        internal int startRow;
        internal int rowCount;
        internal Image image;
        internal RectTransform pooledAnimationCell;
        internal Vector3 originalPosition;
        internal ImageAnimation animation;
        internal Sprite originalSprite;
        internal List<Sprite> originalFrames;
        internal List<Sprite> originalSecondaryFrames;
        internal Image originalRenderer;
        internal bool originalLoop;
        internal float originalLoopDelay;
        internal Action<int> originalLoopComplete;
    }

    private void Awake()
    {
        gameManager = gameManager != null ? gameManager : FindSceneComponent<GameManager>();
        symbolInfoCard = symbolInfoCard != null
            ? symbolInfoCard
            : FindSceneComponent<SymbolInfoCard>();

        CacheGoldBurstLayouts();
        ActivateBaseGameLayout();
        BuildReelCache();

        featureVisualController = featureVisualController != null
            ? featureVisualController
            : GetComponent<SlotFeatureController>();
        if (featureVisualController == null)
        {
            featureVisualController = gameObject.AddComponent<SlotFeatureController>();
        }

        InitializeFeatureLayout();
        symbolInfoCard?.HideCard();
    }

    private void Start()
    {
        EnsureConfiguration();
    }

    private void CacheGoldBurstLayouts()
    {
        baseGameReelRoot = reelRoot != null
            ? reelRoot
            : FindSceneTransform("5x3SlotHolder") ?? FindSceneTransform("Slots");
        megaGoldBurstReelRoot = megaGoldBurstReelRoot != null
            ? megaGoldBurstReelRoot
            : FindSceneTransform("7x3SlotHolder");

        Transform ultimateSlotContainer =
            FindSceneTransformWithDirectChildren(
                "Two7x3Slot",
                "Two7x3SlotHolder",
                "Two7x3SlotHolder (1)");
        ultimateGoldBurstFirstReelRoot = ultimateGoldBurstFirstReelRoot != null
            ? ultimateGoldBurstFirstReelRoot
            : ultimateSlotContainer?.Find("Two7x3SlotHolder");
        ultimateGoldBurstSecondReelRoot = ultimateGoldBurstSecondReelRoot != null
            ? ultimateGoldBurstSecondReelRoot
            : ultimateSlotContainer?.Find("Two7x3SlotHolder (1)");

        baseGameLayoutRoot = FindLayoutRoot(baseGameReelRoot, "5x3Slot");
        megaGoldBurstLayoutRoot = FindLayoutRoot(
            megaGoldBurstReelRoot,
            "7x3Slot");
        ultimateGoldBurstLayoutRoot = FindLayoutRoot(
            ultimateGoldBurstFirstReelRoot,
            "Two7x3Slot");
    }

    private void ActivateBaseGameLayout()
    {
        if (baseGameLayoutRoot != null) baseGameLayoutRoot.SetActive(true);
        if (megaGoldBurstLayoutRoot != null) megaGoldBurstLayoutRoot.SetActive(false);
        if (ultimateGoldBurstLayoutRoot != null) ultimateGoldBurstLayoutRoot.SetActive(false);
        SetLandscapeExtraUIForBaseLayout(true);
        reelRoot = baseGameReelRoot;
        isUsingMegaGoldBurstLayout = false;
        isUsingUltimateGoldBurstLayout = false;
    }

    internal void PrepareForGoldBurstLayoutSwitch()
    {
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopGoldBurstConversionAnimations();
        StopGoldBurstBarrelIdleAnimations();
        KillAllTweens();
        featureVisualController?.ResetFeatures();
    }

    internal void SetGoldBurstLayout(
        Transform activeReelRoot,
        bool useMegaLayout,
        bool useUltimateLayout)
    {
        reelRoot = activeReelRoot;
        isUsingMegaGoldBurstLayout = useMegaLayout;
        isUsingUltimateGoldBurstLayout = useUltimateLayout;
    }

    internal void RebuildGoldBurstLayout()
    {
        BuildReelCache(true);
        InitializeFeatureLayout();
    }

    internal void CompleteGoldBurstLayoutSwitch()
    {
        featureVisualController?.BeginGoldBurstTriggerPresentation();
        ReapplyPreparedFeatures();
    }

    internal void SetLandscapeExtraUIForBaseLayout(bool isBaseLayoutActive)
    {
        if (landscapeExtraUICanDeactivate != null)
        {
            landscapeExtraUICanDeactivate.SetActive(isBaseLayoutActive);
        }

    }

    internal void InitializeFeatureLayout()
    {
        if (featureVisualController == null) return;

        Transform featureSearchRoot = reelRoot != null ? reelRoot.parent : null;
        featureVisualController.Initialize(
            featureSearchRoot,
            resultSlotsByReel,
            Mathf.Max(1, reels.Count),
            FindLayoutAnimationRoot(reelRoot));
    }

    private void ReapplyPreparedFeatures()
    {
        featureVisualController?.PrepareTwoSlotBarrels(preparedTwoSlotBarrels);
        featureVisualController?.PrepareThreeSlotBarrels(preparedThreeSlotBarrels);
        featureVisualController?.PrepareTrains(preparedTrains);
    }

    private static GameObject FindLayoutRoot(Transform holder, string layoutName)
    {
        GameObject layoutRoot = null;
        for (Transform current = holder; current != null; current = current.parent)
        {
            if (current.name == layoutName)
            {
                layoutRoot = current.gameObject;
            }
        }

        return layoutRoot;
    }

    private static RectTransform FindLayoutAnimationRoot(Transform holder)
    {
        Transform container = holder != null ? holder.parent : null;
        if (container == null) return null;

        for (int childIndex = 0; childIndex < container.childCount; childIndex++)
        {
            Transform child = container.GetChild(childIndex);
            if (child is RectTransform rect &&
                child.name.EndsWith("Animation", StringComparison.OrdinalIgnoreCase))
            {
                return rect;
            }
        }

        return null;
    }

    private void OnDisable()
    {
        AudioManager.Instance?.StopGoldMineReelSpinning();
        AudioManager.Instance?.StopGoldMineTension();
        AudioManager.Instance?.StopGoldMineTrainFreeSpins();
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopGoldBurstConversionAnimations();
        StopGoldBurstBarrelIdleAnimations();
        StopViewCoroutines();
        KillAllTweens();
        if (featureVisualController != null)
        {
            featureVisualController.ResetFeatures();
        }
        isSpinning = false;
    }

    private void OnDestroy()
    {
        AudioManager.Instance?.StopGoldMineReelSpinning();
        AudioManager.Instance?.StopGoldMineTension();
        AudioManager.Instance?.StopGoldMineTrainFreeSpins();
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopGoldBurstConversionAnimations();
        StopGoldBurstBarrelIdleAnimations();
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

    private void BuildReelCache(bool forceDiscoverResultSlots = false)
    {
        StopAnticipationAnimations();
        anticipationAnimationsByReel.Clear();
        anticipationAnimationRoot = null;
        anticipationOwnsAnimationRoot = false;
        reels.Clear();
        reportedResultSlotIssues.Clear();

        Transform discoveredRoot = reelRoot != null
            ? reelRoot
            : FindSceneTransform("5x3SlotHolder") ?? FindSceneTransform("Slots");

        List<RectTransform> candidates = new List<RectTransform>();
        AddReelCandidates(discoveredRoot, candidates);
        if (isUsingUltimateGoldBurstLayout)
        {
            AddReelCandidates(ultimateGoldBurstSecondReelRoot, candidates);
        }

        if (candidates.Count == 0 && reelTransforms != null)
        {
            candidates.AddRange(reelTransforms
                .OfType<RectTransform>()
                .Where(HasDirectSymbolImages));
        }

        int expectedReelCount = isUsingUltimateGoldBurstLayout
            ? UltimateGoldBurstReelCount
            : isUsingMegaGoldBurstLayout
                ? MegaGoldBurstReelCount
                : DefaultReelCount;
        if (forceDiscoverResultSlots ||
            !ResultSlotsMatchReels(resultSlotsByReel, candidates))
        {
            resultSlotsByReel = DiscoverResultSlots(candidates);
        }
        else
        {
            EnsureResultSlotArraySize(candidates.Count);
        }

        foreach (RectTransform reelTransform in candidates)
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

        if (reels.Count != expectedReelCount)
        {
            Debug.LogError(
                $"[SlotView] Found {reels.Count} reels; expected {expectedReelCount}.",
                this);
        }

        reelRoot = discoveredRoot;
        reelTransforms = reels.Select(reel => (Transform)reel.transform).ToArray();

        SetupSymbolButtons(GetRowCount());
        BuildGoldBurstCellCache();
        CacheAnticipationAnimations();
    }

    private static void AddReelCandidates(
        Transform root,
        ICollection<RectTransform> candidates)
    {
        if (root == null || candidates == null) return;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            if (root.GetChild(childIndex) is RectTransform rect &&
                HasDirectSymbolImages(rect))
            {
                candidates.Add(rect);
            }
        }
    }

    private void EnsureResultSlotArraySize(int reelCount)
    {
        reelCount = Mathf.Max(0, reelCount);
        if (resultSlotsByReel != null && resultSlotsByReel.Length == reelCount) return;

        ReelResultSlots[] previous = resultSlotsByReel;
        resultSlotsByReel = new ReelResultSlots[reelCount];
        if (previous != null)
        {
            Array.Copy(previous, resultSlotsByReel, Mathf.Min(previous.Length, resultSlotsByReel.Length));
        }
    }

    private static bool ResultSlotsMatchReels(
        IReadOnlyList<ReelResultSlots> configuredSlots,
        IReadOnlyList<RectTransform> reelCandidates)
    {
        if (configuredSlots == null || reelCandidates == null ||
            configuredSlots.Count != reelCandidates.Count)
        {
            return false;
        }

        for (int reelIndex = 0; reelIndex < reelCandidates.Count; reelIndex++)
        {
            ReelResultSlots slots = configuredSlots[reelIndex];
            RectTransform reel = reelCandidates[reelIndex];
            if (slots == null || reel == null)
            {
                return false;
            }

            for (int row = 0; row < DefaultRowCount; row++)
            {
                Image image = slots.Get(row);
                if (image == null || !image.transform.IsChildOf(reel))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ReelResultSlots[] DiscoverResultSlots(
        IReadOnlyList<RectTransform> reelCandidates)
    {
        if (reelCandidates == null) return Array.Empty<ReelResultSlots>();

        var discovered = new ReelResultSlots[reelCandidates.Count];
        for (int reelIndex = 0; reelIndex < reelCandidates.Count; reelIndex++)
        {
            RectTransform reel = reelCandidates[reelIndex];
            if (reel == null) continue;

            var directImages = new List<Image>();
            var maskedImages = new List<Image>();
            for (int childIndex = 0; childIndex < reel.childCount; childIndex++)
            {
                Transform child = reel.GetChild(childIndex);
                Image image = child.GetComponent<Image>();
                if (image == null) continue;

                directImages.Add(image);
                if (child.GetComponentInChildren<Mask>(true) != null)
                {
                    maskedImages.Add(image);
                }
            }

            List<Image> resultImages = maskedImages.Count >= DefaultRowCount
                ? maskedImages
                : directImages
                    .Skip(Mathf.Max(0, directImages.Count - DefaultRowCount))
                    .ToList();
            resultImages = resultImages
                .OrderByDescending(image => image.rectTransform.anchoredPosition.y)
                .Take(DefaultRowCount)
                .ToList();

            if (resultImages.Count == DefaultRowCount)
            {
                discovered[reelIndex] = new ReelResultSlots(
                    resultImages[0],
                    resultImages[1],
                    resultImages[2]);
            }
        }

        return discovered;
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

                Mask mask = symbolImage.GetComponentInChildren<Mask>(true);
                RectTransform spinner = mask != null
                    ? mask.transform.Find("SpinningSlot") as RectTransform
                    : null;
                if (mask == null || spinner == null || spinner.childCount == 0) continue;

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

    private void CacheAnticipationAnimations()
    {
        RectTransform animationRoot = FindLayoutAnimationRoot(reelRoot);
        if (animationRoot == null) return;
        anticipationAnimationRoot = animationRoot;

        List<ImageAnimation> anticipationAnimations = animationRoot
            .GetComponentsInChildren<ImageAnimation>(true)
            .Where(animation =>
                animation != null &&
                animation.name.StartsWith(
                    "Anticipation",
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(animation =>
                animation.transform is RectTransform rect
                    ? rect.anchoredPosition.x
                    : animation.transform.localPosition.x)
            .ToList();

        foreach (ImageAnimation animation in anticipationAnimations)
        {
            animation.StopAnimation();
            animation.gameObject.SetActive(false);
        }

        int mappedCount = Mathf.Min(reels.Count, anticipationAnimations.Count);
        int firstMappedReel = Mathf.Max(0, reels.Count - mappedCount);
        int firstAnimation = anticipationAnimations.Count - mappedCount;
        for (int index = 0; index < mappedCount; index++)
        {
            anticipationAnimationsByReel[firstMappedReel + index] =
                anticipationAnimations[firstAnimation + index];
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
        preparedTwoSlotBarrels.Clear();
        if (placements != null) preparedTwoSlotBarrels.AddRange(placements.Where(item => item != null));
        featureVisualController?.PrepareTwoSlotBarrels(placements);
    }

    internal void PrepareThreeSlotBarrels(IReadOnlyList<ThreeSlotBarrelPlacement> placements)
    {
        preparedThreeSlotBarrels.Clear();
        if (placements != null) preparedThreeSlotBarrels.AddRange(placements.Where(item => item != null));
        featureVisualController?.PrepareThreeSlotBarrels(placements);
    }

    internal void PrepareTrains(IReadOnlyList<TrainPlacement> placements)
    {
        preparedTrains.Clear();
        if (placements != null) preparedTrains.AddRange(placements.Where(item => item != null));
        featureVisualController?.PrepareTrains(placements);
    }

    internal void StageGoldBurstRespinFeatures(
        IReadOnlyList<TwoSlotBarrelPlacement> twoSlotBarrels,
        IReadOnlyList<ThreeSlotBarrelPlacement> threeSlotBarrels,
        IReadOnlyList<TrainPlacement> trains)
    {
        preparedTwoSlotBarrels.Clear();
        if (twoSlotBarrels != null)
        {
            preparedTwoSlotBarrels.AddRange(
                twoSlotBarrels.Where(item => item != null));
        }

        preparedThreeSlotBarrels.Clear();
        if (threeSlotBarrels != null)
        {
            preparedThreeSlotBarrels.AddRange(
                threeSlotBarrels.Where(item => item != null));
        }

        preparedTrains.Clear();
        if (trains != null)
        {
            preparedTrains.AddRange(trains.Where(item => item != null));
        }
    }

    internal void ConfigureGoldBurstTriggerBarrelMerge(bool shouldDefer)
    {
        featureVisualController?.ConfigureGoldBurstTriggerBarrelMerge(shouldDefer);
    }

    internal void ConfigureTrainLandingWinSequence(
        bool shouldWait,
        int requiredLoops = DefaultTrainLandingLoopsBeforeWins,
        bool deferUntilReelsStop = false)
    {
        stopTrainLandingAnimationsBeforeWins = shouldWait;
        requiredTrainLandingLoopsBeforeWins = Mathf.Max(1, requiredLoops);
        deferTrainLandingAnimationsUntilReelsStop = deferUntilReelsStop;
        trainLandingAnimationsReleased = !deferUntilReelsStop;
    }

    internal void ReleaseTrainLandingAnimationsAfterReelsStop()
    {
        trainLandingAnimationsReleased = true;
    }

    internal void ConfigureTrainLandingAnimations(bool shouldPlay)
    {
        trainLandingAnimationsEnabled = shouldPlay;
        if (!shouldPlay)
        {
            StopTrainSymbolAnimations();
        }
    }

    internal IEnumerator WaitForTrainLandingAnimationsBeforeWins()
    {
        if (!stopTrainLandingAnimationsBeforeWins) yield break;

        while (activeTrainAnimations.Any(active =>
                   active?.animation != null &&
                   active.completedLandingLoops <
                       requiredTrainLandingLoopsBeforeWins))
        {
            yield return null;
        }
    }

    internal bool HasActiveTrainLandingAnimations()
    {
        return activeTrainAnimations.Any(active =>
            active != null && active.animationStarted);
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
            int matrixReelCount = Mathf.Max(
                1,
                currentDisplayMatrix != null && currentDisplayMatrix.Count > 0
                    ? currentDisplayMatrix.Count
                    : reels.Count);
            int row = flatPosition / matrixReelCount;
            int reelIndex = flatPosition % matrixReelCount;
            if (flatPosition < 0 ||
                reelIndex < 0 || reelIndex >= reels.Count ||
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
            Image overlayImage = runtime.animation.rendererDelegate;
            if (IsAnimationOverlayReady(runtime.animation, overlayImage) &&
                runtime.baseImage != null &&
                runtime.baseImage != overlayImage)
            {
                runtime.baseImage.enabled = false;
            }
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
        if (!trainLandingAnimationsEnabled || resultColumn == null) return;

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
        if (animSpritesTrainLanding == null ||
            animSpritesTrainLanding.Count == 0 ||
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

        activeTrainAnimations.Add(runtime);
        animationCellImage.enabled = false;
        runtime.delayedStartRoutine = StartCoroutine(
            StartTrainLandingAnimationAfterBox(runtime));
    }

    private IEnumerator StartTrainLandingAnimationAfterBox(
        TrainSymbolAnimationRuntime runtime)
    {
        while (deferTrainLandingAnimationsUntilReelsStop &&
               !trainLandingAnimationsReleased)
        {
            if (runtime == null || !activeTrainAnimations.Contains(runtime))
            {
                yield break;
            }
            yield return null;
        }

        if (trainBoxLeadInDelay > 0f)
        {
            yield return new WaitForSeconds(trainBoxLeadInDelay);
        }

        if (runtime == null || !activeTrainAnimations.Contains(runtime) ||
            runtime.animationCellImage == null)
        {
            yield break;
        }

        runtime.delayedStartRoutine = null;
        runtime.animationCellImage.enabled = true;
        runtime.animationStarted = StartTrainSpriteAnimation(
            runtime,
            animSpritesTrainLanding,
            true,
            loopCount => HandleTrainLandingAnimationLoop(runtime, loopCount));
        if (!runtime.animationStarted)
        {
            activeTrainAnimations.Remove(runtime);
            RestoreTrainAnimationRuntime(runtime);
            yield break;
        }

        AudioManager.Instance?.PlayGoldMineTrainIcon();

        if (runtime.baseImage != null &&
            runtime.baseImage != runtime.animationCellImage)
        {
            runtime.baseImage.enabled = false;
        }
    }

    private void HandleTrainLandingAnimationLoop(
        TrainSymbolAnimationRuntime runtime,
        int loopCount)
    {
        if (runtime?.animation == null) return;

        runtime.completedLandingLoops = Mathf.Max(
            runtime.completedLandingLoops,
            loopCount);

        if (stopTrainLandingAnimationsBeforeWins &&
            runtime.completedLandingLoops >=
                requiredTrainLandingLoopsBeforeWins)
        {
            runtime.animation.doLoopAnimation = false;
        }
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

        while (activeAnimations.Any(active =>
                   activeTrainAnimations.Contains(active) &&
                   !active.animationStarted))
        {
            yield return null;
        }

        activeAnimations = activeAnimations
            .Where(active => activeTrainAnimations.Contains(active) &&
                             active.animationStarted)
            .ToList();
        if (activeAnimations.Count == 0) yield break;

        float triggerDuration =
            animSpritesFreeGameTrigger.Count / TrainAnimationFramesPerSecond;
        bool startedAnyAnimation = false;
        foreach (TrainSymbolAnimationRuntime active in activeAnimations)
        {
            startedAnyAnimation |= StartTrainSpriteAnimation(
                active,
                animSpritesFreeGameTrigger,
                false);
        }

        if (!startedAnyAnimation) yield break;

        AudioManager.Instance?.PlayGoldMineTrainFreeSpins();
        yield return new WaitForSeconds(triggerDuration);
        AudioManager.Instance?.StopGoldMineTrainFreeSpins();
    }

    private static bool StartTrainSpriteAnimation(
        TrainSymbolAnimationRuntime runtime,
        List<Sprite> frames,
        bool shouldLoop,
        Action<int> onLoopComplete = null)
    {
        if (runtime?.animation == null ||
            runtime.animationCellImage == null ||
            frames == null || frames.Count == 0)
        {
            return false;
        }

        runtime.animation.StopAnimation();
        runtime.animation.textureArray = frames;
        runtime.animation.rendererDelegate = runtime.animationCellImage;
        runtime.animation.doLoopAnimation = shouldLoop;
        runtime.animation.delayBetweenLoop = 0f;
        runtime.animation.SetLoopDuration(
            frames.Count / TrainAnimationFramesPerSecond);
        runtime.animation.onLoopComplete = onLoopComplete;
        runtime.animation.StartAnimation();
        return IsAnimationOverlayReady(
            runtime.animation,
            runtime.animationCellImage);
    }

    private void StopTrainSymbolAnimations()
    {
        AudioManager.Instance?.StopGoldMineTrainFreeSpins();

        foreach (TrainSymbolAnimationRuntime runtime in activeTrainAnimations)
        {
            if (runtime?.delayedStartRoutine != null)
            {
                StopCoroutine(runtime.delayedStartRoutine);
                runtime.delayedStartRoutine = null;
            }

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
        stopTrainLandingAnimationsBeforeWins = false;
        requiredTrainLandingLoopsBeforeWins =
            DefaultTrainLandingLoopsBeforeWins;
        deferTrainLandingAnimationsUntilReelsStop = false;
        trainLandingAnimationsReleased = true;
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

    internal IEnumerator PlayGoldBurstTriggerPresentation(
        GoldBurstTier tier,
        Action beginHiddenRespin,
        Func<bool> isHiddenRespinReady,
        Func<bool> applyHiddenRespinResult)
    {
        StopWinningSymbolAnimations();
        ConfigureTrainLandingAnimations(false);
        StopGoldBurstConversionAnimations();
        if (featureVisualController != null)
        {
            featureVisualController.BeginGoldBurstTriggerPresentation();
            List<GoldBurstPrizePlacement> triggerBarrels =
                featureVisualController.GetGoldBurstTriggerSingleSlotBarrels(
                    currentDisplayMatrix);
            yield return PlayGoldBurstConversionAnimations(
                triggerBarrels,
                2,
                false);
            Action activateExpandedLayout =
                featureVisualController.GetGoldBurstLayoutActivation(
                    tier,
                    this);
            yield return featureVisualController.PlayGoldBurstTriggerPresentation(
                tier,
                activateExpandedLayout,
                beginHiddenRespin,
                isHiddenRespinReady,
                applyHiddenRespinResult);
            StartGoldBurstBarrelIdleAnimations();
        }
    }

    internal IEnumerator PlayFreeGamesStartPresentation(UIManager uiManager)
    {
        StopWinningSymbolAnimations();
        ConfigureTrainLandingAnimations(false);
        if (featureVisualController != null)
        {
            yield return featureVisualController.PlayFreeGamesStartPresentation(
                uiManager);
        }
    }

    internal IEnumerator PlayFreeGamesEndPresentation(
        double totalWin,
        UIManager uiManager)
    {
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        if (featureVisualController != null)
        {
            yield return featureVisualController.PlayFreeGamesEndPresentation(
                totalWin,
                uiManager);
        }
    }

    internal IEnumerator PlayGoldBurstFinalPresentation(
        IReadOnlyList<GoldBurstPrizePlacement> prizes,
        double totalWin)
    {
        StopWinningSymbolAnimations();
        if (featureVisualController == null) yield break;

        // Freeze every barrel before converting them. Leaving the idle animations
        // running makes several barrels appear to blast while the actual prize
        // conversion is being played one at a time.
        StopGoldBurstBarrelIdleAnimations();
        featureVisualController.BeginGoldBurstPrizeReveal();
        List<GoldBurstPrizePlacement> orderedPrizes = prizes == null
            ? new List<GoldBurstPrizePlacement>()
            : prizes
                .Where(IsValidGoldBurstPrize)
                // Ultimate uses two authored 7x3 boards. Finish the complete top
                // board before beginning the bottom board, then reveal each board
                // from left to right and top to bottom.
                .OrderBy(prize => isUsingUltimateGoldBurstLayout
                    ? prize.startCol / MegaGoldBurstReelCount
                    : 0)
                .ThenBy(prize => isUsingUltimateGoldBurstLayout
                    ? prize.startCol % MegaGoldBurstReelCount
                    : prize.startCol)
                .ThenBy(prize => prize.startRow)
                .ToList();

        foreach (GoldBurstPrizePlacement prize in orderedPrizes)
        {
            yield return PlayGoldBurstConversionAnimations(
                new[] { prize },
                1,
                true,
                () => featureVisualController.RevealGoldBurstPrize(prize));
            featureVisualController.CompleteGoldBurstPrizeReveal(prize);
        }

        bool pressPlayPressed = false;
        bool isWaitingForPressPlay = featureVisualController.ShowGoldBurstPressPlay(
            () => pressPlayPressed = true);
        if (isWaitingForPressPlay)
        {
            while (!pressPlayPressed)
            {
                yield return null;
            }
        }
        else if (featureVisualController.GoldBurstHoldWithoutTrain > 0f)
        {
            yield return new WaitForSecondsRealtime(
                featureVisualController.GoldBurstHoldWithoutTrain);
        }

        UIManager uiManager = gameManager != null ? gameManager.uiManager : null;
        double collectedGoldBurstWin = 0d;
        yield return featureVisualController.PlayGoldBurstCollectionParticles(
            orderedPrizes,
            collectedAmount =>
            {
                collectedGoldBurstWin = collectedAmount;
                uiManager?.UpdateGoldBurstCollectionWinDisplay(collectedAmount);
            });
        double resultWin = collectedGoldBurstWin > 0d
            ? collectedGoldBurstWin
            : Math.Max(0d, totalWin);
        yield return featureVisualController.PlayGoldBurstResultPresentation(
            resultWin,
            uiManager,
            showDarkBackgroundInLandscape: true);
        yield return featureVisualController.PlayGoldBurstOutroPresentation();
    }

    internal void EndGoldBurstPresentation()
    {
        StopTrainSymbolAnimations();
        StopGoldBurstConversionAnimations();
        StopGoldBurstBarrelIdleAnimations();
        featureVisualController?.EndGoldBurstPresentation();
        if (isUsingMegaGoldBurstLayout || isUsingUltimateGoldBurstLayout)
        {
            ActivateBaseGameLayout();
            BuildReelCache(true);
            InitializeFeatureLayout();
            if (IsValidMatrix(currentDisplayMatrix)) ApplyMatrix(currentDisplayMatrix);
        }
    }

    private IEnumerator PlayGoldBurstConversionAnimations(
        IReadOnlyList<GoldBurstPrizePlacement> prizes,
        int loopCount,
        bool useDedicatedSingleSlotBlast,
        Action revealBeforeCompletion = null)
    {
        StopGoldBurstConversionAnimations();
        EnsureConfiguration();
        loopCount = Mathf.Max(1, loopCount);

        if (prizes == null || featureVisualController == null) yield break;

        float longestDuration = 0f;
        foreach (GoldBurstPrizePlacement prize in prizes)
        {
            if (!IsValidGoldBurstPrize(prize))
            {
                continue;
            }

            bool usesBarrelVisual = prize.rowCount > 1;
            RectTransform animationCell;
            bool acquiredTarget = usesBarrelVisual
                ? featureVisualController.TryGetGoldBurstBarrelAnimationTarget(
                    prize.startCol,
                    prize.startRow,
                    prize.rowCount,
                    out animationCell)
                : featureVisualController.TryAcquireGoldBurstAnimationCell(
                    prize.startCol,
                    prize.startRow,
                    out animationCell);
            if (!acquiredTarget)
            {
                continue;
            }

            Image animationImage = animationCell.GetComponent<Image>();
            if (animationImage == null)
            {
                if (!usesBarrelVisual)
                {
                    featureVisualController.ReleaseGoldBurstAnimationCell(animationCell);
                }
                continue;
            }

            var runtime = new GoldBurstConversionRuntime
            {
                baseImage = prize.rowCount == 1
                    ? GetResultSlotImage(prize.startCol, prize.startRow)
                    : null,
                animationCell = animationCell,
                usesBarrelVisual = usesBarrelVisual,
                originalScale = animationCell.localScale,
                originalPosition = animationCell.position,
                animationImage = animationImage,
                animationImageWasEnabled = animationImage.enabled,
                originalSprite = animationImage.sprite,
                originalColor = animationImage.color
            };

            int symbolId = FindGoldBurstSymbolId(prize);
            if (!TryStartGoldBurstBlast(
                    prize,
                    symbolId,
                    runtime,
                    loopCount,
                    useDedicatedSingleSlotBlast,
                    out float duration))
            {
                RestoreGoldBurstConversionRuntime(runtime);
                continue;
            }

            activeGoldBurstConversions.Add(runtime);
            longestDuration = Mathf.Max(longestDuration, duration);
        }

        if (activeGoldBurstConversions.Count == 0)
        {
            revealBeforeCompletion?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        float timeout = longestDuration + 0.5f;
        float playbackFramesPerSecond = BarrelBlastSourceFramesPerSecond *
                                        Mathf.Max(0.1f, barrelBlastPlaybackSpeed);
        float revealLeadDuration = GoldBoxRevealFramesBeforeBlastEnd /
                                   playbackFramesPerSecond;
        float revealTime = Mathf.Max(0f, longestDuration - revealLeadDuration);
        bool hasRevealed = false;
        while (activeGoldBurstConversions.Any(runtime => !runtime.completed) &&
               elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            if (!hasRevealed && revealBeforeCompletion != null &&
                elapsed >= revealTime)
            {
                revealBeforeCompletion();
                hasRevealed = true;
            }
            yield return null;
        }

        if (!hasRevealed)
        {
            revealBeforeCompletion?.Invoke();
        }

        StopGoldBurstConversionAnimations();
    }

    private bool IsValidGoldBurstPrize(GoldBurstPrizePlacement prize)
    {
        return prize != null &&
               prize.amount >= 0d &&
               prize.columnCount == 1 &&
               prize.rowCount >= 1 && prize.rowCount <= DefaultRowCount &&
               prize.startCol >= 0 && prize.startCol < reels.Count &&
               prize.startRow >= 0 &&
               prize.startRow + prize.rowCount <= DefaultRowCount;
    }

    private bool TryStartGoldBurstBlast(
        GoldBurstPrizePlacement prize,
        int symbolId,
        GoldBurstConversionRuntime runtime,
        int loopCount,
        bool useDedicatedSingleSlotBlast,
        out float duration)
    {
        duration = 0f;
        if (prize == null || runtime?.animationCell == null ||
            runtime.animationImage == null)
        {
            return false;
        }

        List<Sprite> frames;
        switch (prize.rowCount)
        {
            case 1:
                frames = GetSingleSlotBarrelBlastFrames(
                    symbolId,
                    useDedicatedSingleSlotBlast);
                break;
            case 2:
                frames = twoSlotBarrelBlastFrames;
                break;
            default:
                frames = threeSlotBarrelBlastFrames;
                break;
        }

        if (frames == null || frames.Count == 0) return false;

        ImageAnimation animation =
            runtime.animationCell.GetComponent<ImageAnimation>();
        if (animation == null) return false;
        runtime.animation = animation;

        animation.StopAnimation();
        animation.textureArray = frames;
        animation.rendererDelegate = runtime.animationImage;
        animation.doLoopAnimation = loopCount > 1;
        animation.delayBetweenLoop = 0f;
        float playbackSpeed = Mathf.Max(0.1f, barrelBlastPlaybackSpeed);
        float loopDuration = frames.Count /
                             (BarrelBlastSourceFramesPerSecond * playbackSpeed);
        duration = loopDuration * loopCount;
        animation.SetLoopDuration(loopDuration);
        animation.onLoopComplete = completedLoops =>
        {
            if (completedLoops < loopCount)
            {
                AudioManager.Instance?.PlayGoldMineExplosionBlast();
                return;
            }

            animation.doLoopAnimation = false;
            runtime.completed = true;
        };

        if (!runtime.usesBarrelVisual)
        {
            runtime.animationCell.localScale = new Vector3(
                runtime.originalScale.x,
                runtime.originalScale.y * prize.rowCount,
                runtime.originalScale.z);
        }
        runtime.animationImage.color = new Color(
            runtime.originalColor.r,
            runtime.originalColor.g,
            runtime.originalColor.b,
            1f);
        runtime.animationImage.enabled = true;
        animation.StartAnimation();
        if (!IsAnimationOverlayReady(animation, runtime.animationImage))
        {
            return false;
        }

        AudioManager.Instance?.PlayGoldMineExplosionBlast();

        if (!runtime.usesBarrelVisual &&
            runtime.baseImage != null &&
            runtime.baseImage != runtime.animationImage)
        {
            runtime.baseImageWasEnabled = runtime.baseImage.enabled;
            runtime.baseImage.enabled = false;
            runtime.baseImageHidden = true;
        }
        return true;
    }

    private List<Sprite> GetSingleSlotBarrelBlastFrames(
        int symbolId,
        bool useDedicatedSingleSlotBlast)
    {
        if (useDedicatedSingleSlotBlast &&
            singleSlotBarrelBlastFrames != null &&
            singleSlotBarrelBlastFrames.Count > 0)
        {
            return singleSlotBarrelBlastFrames;
        }

        switch (symbolId)
        {
            case 12:
                return yellowSingleSlotBarrelBlastFrames;
            case 13:
                return doubleYellowSingleSlotBarrelBlastFrames;
            default:
                return redSingleSlotBarrelBlastFrames;
        }
    }

    private int FindGoldBurstSymbolId(GoldBurstPrizePlacement prize)
    {
        if (currentDisplayMatrix != null &&
            prize.startCol >= 0 && prize.startCol < currentDisplayMatrix.Count &&
            currentDisplayMatrix[prize.startCol] != null)
        {
            int endRow = Mathf.Min(
                currentDisplayMatrix[prize.startCol].Count,
                prize.startRow + Mathf.Max(1, prize.rowCount));
            for (int row = Mathf.Max(0, prize.startRow); row < endRow; row++)
            {
                int symbolId = currentDisplayMatrix[prize.startCol][row];
                if (symbolId >= FirstGoldBurstLockedSymbolId &&
                    symbolId <= LastGoldBurstLockedSymbolId)
                {
                    return symbolId;
                }
            }
        }

        return FirstGoldBurstLockedSymbolId;
    }

    private void StopGoldBurstConversionAnimations()
    {
        foreach (GoldBurstConversionRuntime runtime in activeGoldBurstConversions)
        {
            if (runtime == null) continue;

            if (runtime.animation != null)
            {
                runtime.animation.onLoopComplete = null;
                runtime.animation.doLoopAnimation = false;
                runtime.animation.StopAnimation();
                runtime.animation.ClearLoopDuration();
            }

            if (runtime.animationImage != null)
            {
                runtime.animationImage.sprite = runtime.originalSprite;
                runtime.animationImage.color = runtime.originalColor;
                runtime.animationImage.enabled = runtime.animationImageWasEnabled;
            }

            RestoreGoldBurstConversionBaseImage(runtime);

            if (runtime.animationCell != null)
            {
                runtime.animationCell.position = runtime.originalPosition;
                runtime.animationCell.localScale = runtime.originalScale;
            }

            if (!runtime.usesBarrelVisual)
            {
                featureVisualController?.ReleaseGoldBurstAnimationCell(runtime.animationCell);
            }
        }

        activeGoldBurstConversions.Clear();
    }

    private void RestoreGoldBurstConversionRuntime(GoldBurstConversionRuntime runtime)
    {
        if (runtime == null) return;

        if (runtime.animation != null)
        {
            runtime.animation.onLoopComplete = null;
            runtime.animation.doLoopAnimation = false;
            runtime.animation.StopAnimation();
            runtime.animation.ClearLoopDuration();
        }

        if (runtime.animationImage != null)
        {
            runtime.animationImage.sprite = runtime.originalSprite;
            runtime.animationImage.color = runtime.originalColor;
            runtime.animationImage.enabled = runtime.animationImageWasEnabled;
        }

        RestoreGoldBurstConversionBaseImage(runtime);

        if (runtime.animationCell != null)
        {
            runtime.animationCell.position = runtime.originalPosition;
            runtime.animationCell.localScale = runtime.originalScale;
        }

        if (!runtime.usesBarrelVisual)
        {
            featureVisualController?.ReleaseGoldBurstAnimationCell(runtime.animationCell);
        }
    }

    private static void RestoreGoldBurstConversionBaseImage(
        GoldBurstConversionRuntime runtime)
    {
        if (runtime?.baseImage == null || !runtime.baseImageHidden) return;

        runtime.baseImage.enabled = runtime.baseImageWasEnabled;
        runtime.baseImageHidden = false;
    }

    private void StartGoldBurstBarrelIdleAnimations()
    {
        StopGoldBurstBarrelIdleAnimations();
        if (featureVisualController == null || currentDisplayMatrix == null) return;

        var animatedImages = new HashSet<Image>();
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            if (featureVisualController.TryGetGoldBurstBarrelAnimationTarget(
                    reelIndex,
                    0,
                    3,
                    out RectTransform threeSlotBarrel))
            {
                TryStartBarrelIdleAnimation(
                        threeSlotBarrel.GetComponent<Image>(),
                        threeSlotBarrelIdleFrames,
                        animatedImages,
                        reelIndex: reelIndex,
                        startRow: 0,
                        rowCount: 3);
            }
            else
            {
                for (int startRow = 0; startRow < DefaultRowCount - 1; startRow++)
                {
                    if (!featureVisualController.TryGetGoldBurstBarrelAnimationTarget(
                            reelIndex,
                            startRow,
                            2,
                            out RectTransform twoSlotBarrel))
                    {
                        continue;
                    }

                    TryStartBarrelIdleAnimation(
                            twoSlotBarrel.GetComponent<Image>(),
                            twoSlotBarrelIdleFrames,
                            animatedImages,
                            reelIndex: reelIndex,
                            startRow: startRow,
                            rowCount: 2);
                    break;
                }
            }
        }

        foreach (GoldBurstCellRuntime cell in goldBurstCells)
        {
            if (cell?.symbolImage == null ||
                cell.reelIndex < 0 || cell.reelIndex >= currentDisplayMatrix.Count ||
                currentDisplayMatrix[cell.reelIndex] == null ||
                cell.row < 0 || cell.row >= currentDisplayMatrix[cell.reelIndex].Count)
            {
                continue;
            }

            int symbolId = currentDisplayMatrix[cell.reelIndex][cell.row];
            bool isSingleSlotBarrel = symbolId >= FirstGoldBurstLockedSymbolId &&
                                      symbolId <= LastGoldBurstLockedSymbolId &&
                                      !featureVisualController.IsGoldBurstCellCoveredByFeature(
                                          cell.reelIndex,
                                          cell.row);
            if (!isSingleSlotBarrel) continue;

            if (singleSlotBarrelIdleFrames == null ||
                singleSlotBarrelIdleFrames.Count == 0 ||
                !featureVisualController.TryAcquireGoldBurstAnimationCell(
                    cell.reelIndex,
                    cell.row,
                    out RectTransform animationCell))
            {
                continue;
            }

            Image animationImage = animationCell.GetComponent<Image>();
            Vector3 animationCellOriginalPosition = animationCell.position;

            if (!TryStartBarrelIdleAnimation(
                    animationImage,
                    singleSlotBarrelIdleFrames,
                    animatedImages,
                    animationCell,
                    animationCellOriginalPosition,
                    cell.reelIndex,
                    cell.row,
                    1))
            {
                featureVisualController.ReleaseGoldBurstAnimationCell(animationCell);
            }
        }
    }

    private bool TryStartBarrelIdleAnimation(
        Image image,
        List<Sprite> frames,
        ISet<Image> animatedImages,
        RectTransform pooledAnimationCell = null,
        Vector3? originalPosition = null,
        int reelIndex = -1,
        int startRow = -1,
        int rowCount = 0)
    {
        if (image == null || frames == null || frames.Count == 0 ||
            animatedImages == null || !animatedImages.Add(image))
        {
            return false;
        }

        ImageAnimation animation = image.GetComponent<ImageAnimation>();
        if (animation == null) return false;
        var runtime = new BarrelIdleAnimationRuntime
        {
            reelIndex = reelIndex,
            startRow = startRow,
            rowCount = rowCount,
            image = image,
            pooledAnimationCell = pooledAnimationCell,
            originalPosition = originalPosition ?? image.rectTransform.position,
            animation = animation,
            originalSprite = image.sprite,
            originalFrames = animation.textureArray,
            originalSecondaryFrames = animation.secondaryTextureArray,
            originalRenderer = animation.rendererDelegate,
            originalLoop = animation.doLoopAnimation,
            originalLoopDelay = animation.delayBetweenLoop,
            originalLoopComplete = animation.onLoopComplete
        };
        activeBarrelIdleAnimations.Add(runtime);

        animation.StopAnimation();
        animation.textureArray = frames;
        animation.secondaryTextureArray = null;
        animation.rendererDelegate = image;
        animation.doLoopAnimation = true;
        animation.delayBetweenLoop = 0f;
        animation.onLoopComplete = null;
        animation.SetLoopDuration(
            frames.Count /
            (BarrelIdleSourceFramesPerSecond * Mathf.Max(0.1f, barrelIdlePlaybackSpeed)));
        animation.StartAnimation();
        Image sourceImage = rowCount == 1 &&
                            IsAnimationOverlayReady(animation, image)
            ? GetResultSlotImage(reelIndex, startRow)
            : null;
        if (sourceImage != null && sourceImage != image)
        {
            DisableBarrelIdleSourceImages(reelIndex, startRow, 1);
        }
        return true;
    }

    private static bool IsAnimationOverlayReady(
        ImageAnimation animation,
        Image overlayImage)
    {
        return animation != null &&
               animation.currentAnimationState == ImageAnimation.ImageState.PLAYING &&
               overlayImage != null &&
               overlayImage.isActiveAndEnabled &&
               overlayImage.gameObject.activeInHierarchy;
    }

    private void DisableBarrelIdleSourceImages(
        int reelIndex,
        int startRow,
        int rowCount)
    {
        for (int row = startRow; row < startRow + rowCount; row++)
        {
            Image sourceImage = GetResultSlotImage(reelIndex, row);
            if (sourceImage == null) continue;

            if (!barrelIdleSourceImageStates.ContainsKey(sourceImage))
            {
                barrelIdleSourceImageStates[sourceImage] = sourceImage.enabled;
            }
            sourceImage.enabled = false;
        }
    }

    private void StopGoldBurstBarrelIdleAnimation(GoldBurstPrizePlacement prize)
    {
        if (prize == null) return;

        List<BarrelIdleAnimationRuntime> matchingAnimations =
            activeBarrelIdleAnimations
                .Where(runtime => runtime != null &&
                                  runtime.reelIndex == prize.startCol &&
                                  runtime.startRow == prize.startRow &&
                                  runtime.rowCount == prize.rowCount)
                .ToList();

        foreach (BarrelIdleAnimationRuntime runtime in matchingAnimations)
        {
            StopBarrelIdleAnimationRuntime(runtime);
            activeBarrelIdleAnimations.Remove(runtime);
        }
    }

    private void StopGoldBurstBarrelIdleAnimations()
    {
        foreach (BarrelIdleAnimationRuntime runtime in activeBarrelIdleAnimations.ToList())
        {
            StopBarrelIdleAnimationRuntime(runtime);
        }
        activeBarrelIdleAnimations.Clear();

        foreach (KeyValuePair<Image, bool> sourceState in barrelIdleSourceImageStates)
        {
            if (sourceState.Key != null)
            {
                sourceState.Key.enabled = sourceState.Value;
            }
        }
        barrelIdleSourceImageStates.Clear();
    }

    private void StopBarrelIdleAnimationRuntime(BarrelIdleAnimationRuntime runtime)
    {
        if (runtime == null) return;

        if (runtime.animation != null)
        {
            runtime.animation.onLoopComplete = null;
            runtime.animation.doLoopAnimation = false;
            runtime.animation.StopAnimation();
            runtime.animation.ClearLoopDuration();
            runtime.animation.textureArray = runtime.originalFrames;
            runtime.animation.secondaryTextureArray = runtime.originalSecondaryFrames;
            runtime.animation.rendererDelegate = runtime.originalRenderer;
            runtime.animation.doLoopAnimation = runtime.originalLoop;
            runtime.animation.delayBetweenLoop = runtime.originalLoopDelay;
            runtime.animation.onLoopComplete = runtime.originalLoopComplete;
        }

        if (runtime.image != null)
        {
            runtime.image.sprite = runtime.originalSprite;
            runtime.image.rectTransform.position = runtime.originalPosition;
        }

        if (runtime.pooledAnimationCell != null)
        {
            featureVisualController?.ReleaseGoldBurstAnimationCell(
                runtime.pooledAnimationCell);
        }
    }

    internal void StartSpin()
    {
        if (isSpinning || reels.Count == 0) return;

        AudioManager.Instance?.StopGoldMineWin();
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopGoldBurstConversionAnimations();
        StopGoldBurstBarrelIdleAnimations();
        featureVisualController?.BeginSpinPresentation();
        EnsureConfiguration();
        HideSymbolInfoCard();
        KillReelTweens(true);

        quickStopRequested = false;
        isSpinning = true;
        AudioManager.Instance?.PlayGoldMineReelSpinning();
        reelStartRoutine = StartCoroutine(StartReelsSequentially());
    }

    internal void StartGoldBurstRespin()
    {
        if (isSpinning || reels.Count == 0) return;

        AudioManager.Instance?.StopGoldMineWin();
        StopWinningSymbolAnimations();
        StopTrainSymbolAnimations();
        StopGoldBurstConversionAnimations();
        HideSymbolInfoCard();
        KillReelTweens(true);
        KillGoldBurstCellTweens(true);

        quickStopRequested = false;
        isSpinning = true;
        AudioManager.Instance?.PlayGoldMineReelSpinning();

        if (activeBarrelIdleAnimations.Count == 0)
        {
            StartGoldBurstBarrelIdleAnimations();
        }

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
                cell.symbolImage.enabled =
                    !barrelIdleSourceImageStates.ContainsKey(cell.symbolImage);
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
        AudioManager.Instance?.StopGoldMineReelSpinning();
        featureVisualController?.RevealAllFeatures();
        StartGoldBurstBarrelIdleAnimations();
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

    internal bool IsGoldBurstMergeInProgress()
    {
        return featureVisualController != null &&
               featureVisualController.IsFeatureMergeInProgress();
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
            AudioManager.Instance?.StopGoldMineReelSpinning();
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
        int stoppedGoldBurstBarrels = 0;
        float nextReelDelay = 0f;
        int freeGameAnticipationThreshold =
            Mathf.Max(1, GetFreeGameMinTrigger() - 1);
        int goldBurstAnticipationThreshold =
            Mathf.Max(1, GetGoldBurstMinTrigger() - 1);

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            if (reelIndex > 0) nextReelDelay += stopInterval;

            bool shouldAnticipateFreeGames =
                stoppedFreeGameScatters >= freeGameAnticipationThreshold;
            bool shouldAnticipateGoldBurst =
                stoppedGoldBurstBarrels >= goldBurstAnticipationThreshold;
            bool shouldAnticipate =
                !quickStop &&
                (shouldAnticipateFreeGames || shouldAnticipateGoldBurst);
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
            stoppedGoldBurstBarrels += CountGoldBurstBarrels(resultMatrix[reelIndex]);
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
        AudioManager.Instance?.StopGoldMineReelSpinning();
        StopAnticipationAnimations();
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
            yield return PlayReelAnticipation(reelIndex, anticipationDuration, scheduledSpeed);
        }

        SetAnticipationAnimationActive(reelIndex, false);
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

    private int CountGoldBurstBarrels(IReadOnlyList<int> resultColumn)
    {
        if (resultColumn == null) return 0;

        int count = 0;
        int rows = Mathf.Min(GetRowCount(), resultColumn.Count);
        for (int row = 0; row < rows; row++)
        {
            if (IsGoldBurstTriggerSymbol(resultColumn[row])) count++;
        }

        return count;
    }

    private bool IsGoldBurstTriggerSymbol(int symbolId)
    {
        IReadOnlyList<int> configuredIds =
            gameManager?.gameConfig?.goldBurstTriggerSymbolIds;
        if (configuredIds != null && configuredIds.Count > 0)
        {
            return configuredIds.Contains(symbolId);
        }

        return symbolId >= FirstGoldBurstLockedSymbolId &&
               symbolId <= LastGoldBurstLockedSymbolId;
    }

    private IEnumerator PlayReelAnticipation(
        int reelIndex,
        float duration,
        SpinSpeed scheduledSpeed)
    {
        if (duration <= 0f || reelIndex <= 0 || reelIndex >= reels.Count) yield break;

        ReelRuntime reel = reels[reelIndex];
        SetAnticipationAnimationActive(reelIndex, true);
        reel.isAnticipating = true;
        ApplyReelMotionSpeed(reel);
        AudioManager.Instance?.PlayGoldMineTension();

        float remaining = duration;
        float initialScale = GetStopTimingScale(scheduledSpeed);
        while (remaining > 0f && !quickStopRequested)
        {
            SpinSpeed effective = GetSpinSpeed();
            float currentScale = GetStopTimingScale(effective);
            remaining -= Time.unscaledDeltaTime * Mathf.Max(0.01f, initialScale / currentScale);
            yield return null;
        }

        SetAnticipationAnimationActive(reelIndex, false);
        reel.isAnticipating = false;
        ApplyReelMotionSpeed(reel);
        AudioManager.Instance?.StopGoldMineTension();
    }

    private void SetAnticipationAnimationActive(int reelIndex, bool active)
    {
        if (!anticipationAnimationsByReel.TryGetValue(
                reelIndex,
                out ImageAnimation animation) ||
            animation == null)
        {
            return;
        }

        if (active)
        {
            ActivateAnticipationAnimationRoot();
            animation.gameObject.SetActive(true);
            animation.PlayAnimation();
            return;
        }

        animation.StopAnimation();
        animation.gameObject.SetActive(false);
        ReleaseAnticipationAnimationRoot();
    }

    private void StopAnticipationAnimations()
    {
        AudioManager.Instance?.StopGoldMineTension();

        foreach (ImageAnimation animation in anticipationAnimationsByReel.Values)
        {
            if (animation == null) continue;

            animation.StopAnimation();
            animation.gameObject.SetActive(false);
        }

        ReleaseAnticipationAnimationRoot();
    }

    private void ActivateAnticipationAnimationRoot()
    {
        if (anticipationAnimationRoot == null ||
            anticipationAnimationRoot.gameObject.activeSelf)
        {
            return;
        }

        // Animation columns can retain activeSelf while their root is hidden.
        // Suppress them before enabling the root so anticipation cannot reveal
        // unrelated slot animations for a frame.
        for (int index = 0; index < anticipationAnimationRoot.childCount; index++)
        {
            Transform child = anticipationAnimationRoot.GetChild(index);
            if (!IsSlotAnimationColumn(child)) continue;

            foreach (ImageAnimation animation in
                     child.GetComponentsInChildren<ImageAnimation>(true))
            {
                animation.StopAnimation();
            }
            child.gameObject.SetActive(false);
        }

        anticipationAnimationRoot.gameObject.SetActive(true);
        anticipationOwnsAnimationRoot = true;
    }

    private void ReleaseAnticipationAnimationRoot()
    {
        if (!anticipationOwnsAnimationRoot ||
            anticipationAnimationRoot == null ||
            anticipationAnimationsByReel.Values.Any(animation =>
                animation != null && animation.gameObject.activeSelf))
        {
            return;
        }

        bool hasActiveSibling = false;
        for (int index = 0; index < anticipationAnimationRoot.childCount; index++)
        {
            GameObject child = anticipationAnimationRoot.GetChild(index).gameObject;
            bool isAnticipation = anticipationAnimationsByReel.Values.Any(animation =>
                animation != null && animation.gameObject == child);
            if (!isAnticipation && child.activeSelf)
            {
                hasActiveSibling = true;
                break;
            }
        }

        if (!hasActiveSibling)
        {
            anticipationAnimationRoot.gameObject.SetActive(false);
        }
        anticipationOwnsAnimationRoot = false;
    }

    private static bool IsSlotAnimationColumn(Transform candidate)
    {
        return candidate != null &&
               (string.Equals(
                    candidate.name,
                    "Slot",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.name.StartsWith(
                    "Slot (",
                    StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Matrix application

    internal void ApplyMatrix(List<List<int>> matrix)
    {
        if (!IsValidMatrix(matrix)) return;

        currentDisplayMatrix = CloneMatrix(matrix);
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            ApplyMatrixColumn(reelIndex, matrix[reelIndex]);
            reels[reelIndex].transform.anchoredPosition = reels[reelIndex].restingPosition;
        }
    }

    internal bool ApplyHiddenGoldBurstRespinResult(List<List<int>> matrix)
    {
        if (!IsValidMatrix(matrix))
        {
            Debug.LogError(
                "[SlotView] Hidden Gold Burst result matrix does not match the activated layout.",
                this);
            return false;
        }

        ApplyMatrix(matrix);
        Canvas.ForceUpdateCanvases();
        return true;
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

    internal bool IsValidMatrix(List<List<int>> matrix)
    {
        return reels.Count > 0 &&
               HasMatrixDimensions(matrix, reels.Count, GetRowCount());
    }

    internal static bool HasMatrixDimensions(
        List<List<int>> matrix,
        int reelCount,
        int rowCount)
    {
        if (matrix == null || matrix.Count < reelCount) return false;

        for (int reelIndex = 0; reelIndex < reelCount; reelIndex++)
        {
            if (matrix[reelIndex] == null || matrix[reelIndex].Count < rowCount)
            {
                return false;
            }
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

    private int GetGoldBurstMinTrigger()
    {
        return gameManager?.gameConfig != null
            ? gameManager.gameConfig.goldBurstMinTrigger
            : 6;
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

        StopAnticipationAnimations();
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

    private static Transform FindSceneTransformWithDirectChildren(
        string objectName,
        params string[] childNames)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.name == objectName &&
                childNames.All(childName => candidate.Find(childName) != null));
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

    public ReelResultSlots()
    {
    }

    public ReelResultSlots(Image top, Image middle, Image bottom)
    {
        this.top = top;
        this.middle = middle;
        this.bottom = bottom;
    }

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
