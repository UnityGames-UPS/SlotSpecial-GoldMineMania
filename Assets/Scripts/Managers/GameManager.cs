using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private const int FirstGoldBurstSymbolId = 11;
    private const int LastGoldBurstSymbolId = 13;
    private const int ExpandedGoldBurstReelCount = 7;
    private const int GoldBurstRowCount = 3;

    [Header("References")]
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] internal UIManager uiManager;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private SlotView slotView;

    [Header("Spin Settings")]
    [SerializeField] private float normalSpinDuration = 2.0f;
    [SerializeField] private float turboSpinDuration = 1.0f;
    [SerializeField] private float quickSpinCycleDuration = 0.1f;

    [Header("Win Settings")]
    [SerializeField] private double bigWinMultiplierThreshold = 5.0;
    public double BigWinMultiplierThreshold => bigWinMultiplierThreshold;

    internal GameConfig gameConfig;
    internal PlayerData playerData;
    internal SpinResult lastResult;

    internal GameState currentState;
    internal SpinSpeed currentSpinSpeed;

    internal int currentBetIndex;
    internal double currentBetAmount;

    internal bool isAutoPlaying;
    internal int autoPlayTotalRounds;
    internal int autoPlayRemainingRounds;
    internal bool wasAutoPlayingBeforeFreeSpins;
    internal int savedAutoPlayRemainingRounds;
    internal int savedAutoPlayTotalRounds;

    internal bool isInFreeSpins;
    internal int freeSpinsRemaining;
    internal int freeSpinsUsed;
    internal bool waitingForFreeSpinStart;

    internal bool isInGoldBurstRespins;
    private GoldBurstTier activeGoldBurstTier = GoldBurstTier.Cold;
    private int pendingFreeSpins;
    private bool isCompletingGoldBurstRespins;
    private bool isCompletingFreeSpins;

    internal bool isInitialized;
    internal bool initializationFailed;

    private Coroutine spinCoroutine;
    private bool stopRequested;
    private bool waitingForSpecialWin;

    #region Initialization

    private void Start()
    {
        currentState = GameState.Initializing;
        currentSpinSpeed = SpinSpeed.Normal;
        waitingForFreeSpinStart = false;
        isInitialized = false;
        initializationFailed = false;
    }

    internal void OnInitDataReceived(GameConfig config, PlayerData player, List<List<int>> initialMatrix)
    {
        gameConfig = config;
        playerData = player;
        currentBetIndex = playerData.currentBetIndex;
        UpdateBetAmount();

        if (initialMatrix != null && slotView != null)
        {
            slotView.SetInitialMatrix(initialMatrix);
        }

        isInitialized = true;
        currentState = GameState.Idle;

        uiManager.OnGameInitialized();
    }

    #endregion

    #region Bet Management

    internal void IncreaseBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying) return;
        if (gameConfig == null || gameConfig.availableBets == null || gameConfig.availableBets.Count == 0) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex + 1;
        if (nextIndex > maxIndex)
        {
            nextIndex = 0;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void DecreaseBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying) return;
        if (gameConfig == null || gameConfig.availableBets == null || gameConfig.availableBets.Count == 0) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex - 1;
        if (nextIndex < 0)
        {
            nextIndex = maxIndex;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void SetBetIndex(int index)
    {
        if (isInGoldBurstRespins) return;

        currentBetIndex = index;
        UpdateBetAmount();
        uiManager.UpdateBetDisplay();
        if (slotView != null) slotView.OnBetChanged();
    }

    private void UpdateBetAmount()
    {
        currentBetAmount = gameConfig.availableBets[currentBetIndex];
    }

    #endregion

    #region Spin Control
    
    internal void RequestSpin()
    {
        if (waitingForFreeSpinStart) return;

        if (currentState != GameState.Idle) return;
        if (!socketManager.isConnected) return;

        double totalPay = GetTotalPay();
        if (!isInFreeSpins && !isInGoldBurstRespins && playerData.balance < totalPay)
        {
            if (popupManager != null)
            {
                popupManager.ShowInsufficientFundsError();
            }
            return;
        }

        StartSpin();
    }

    internal void RequestStop()
    {
        if (currentState == GameState.Spinning)
        {
            if (isAutoPlaying)
            {
                StopAutoPlay();
            }
            else if (!isInFreeSpins && !isInGoldBurstRespins)
            {
                stopRequested = true;
                uiManager.DisableSpinButtonDuringStop();
            }
        }
    }

    private void StartSpin()
    {
        if (lastResult != null)
        {
            ProcessSpinResult();
        }

        lastResult = null;
        currentState = GameState.Spinning;
        stopRequested = false;

        // Deduct total pay from balance on spin start (except in free spins)
        if (!isInFreeSpins && !isInGoldBurstRespins)
        {
            playerData.balance -= GetTotalPay();
            if (playerData.balance < 0) playerData.balance = 0;
        }

        uiManager.OnSpinStarted();

        if (slotView != null)
        {
            if (isInGoldBurstRespins)
            {
                slotView.StartGoldBurstRespin();
            }
            else
            {
                slotView.StartSpin();
            }
        }

        socketManager.SendSpinRequest(currentBetIndex, isInFreeSpins || isInGoldBurstRespins);

        if (spinCoroutine != null)
            StopCoroutine(spinCoroutine);
        spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        float spinDuration = GetSpinDuration();
        float elapsed = 0f;

        while (elapsed < spinDuration && !stopRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Player pressed Stop manually — hold for 0.5s so the reels keep
        // spinning briefly before snapping, giving clear visual feedback.
        if (stopRequested)
        {
            yield return new WaitForSeconds(0.5f);
        }

        while (lastResult == null)
        {
            yield return null;
        }

        currentState = GameState.Stopping;

        if (slotView != null && lastResult.resultMatrix != null)
        {
            if (isInGoldBurstRespins)
            {
                slotView.StopGoldBurstRespin(lastResult.resultMatrix, OnReelsStoppedComplete);
            }
            else if (currentSpinSpeed == SpinSpeed.QuickSpin || stopRequested)
            {
                slotView.QuickStop(lastResult.resultMatrix);

                // Wait for the snap animation to settle before processing result
                float quickStopWaitTime = 0.5f;
                yield return new WaitForSeconds(quickStopWaitTime);

                OnReelsStoppedComplete();
            }
            else
            {
                slotView.StopSpin(lastResult.resultMatrix, OnReelsStoppedComplete);
            }
        }
        else
        {
            OnReelsStoppedComplete();
        }
    }

    private void OnReelsStoppedComplete()
    {
        slotView?.ReleaseTrainLandingAnimationsAfterReelsStop();

        if (lastResult != null)
        {
            playerData = new PlayerData
            {
                balance = lastResult.playerData != null ? lastResult.playerData.balance : 0,
                currentBetIndex = lastResult.playerData != null ? lastResult.playerData.currentBetIndex : currentBetIndex
            };

            // Keep the previous count visible while the reels are spinning. Apply the
            // server-authoritative count only after every reel has finished stopping.
            if (isInFreeSpins && lastResult.serverSpinsRemaining >= 0)
            {
                freeSpinsRemaining = lastResult.serverSpinsRemaining;
                freeSpinsUsed = lastResult.serverSpinsUsed;
                uiManager.UpdateFreeSpinCount(freeSpinsRemaining);
            }

            if (isInGoldBurstRespins && lastResult.goldBurstData != null)
            {
                uiManager.UpdateFreeSpinCount(
                    Mathf.Max(0, lastResult.goldBurstData.remainingRespins));
            }
        }

        if (lastResult != null && lastResult.winAmount > 0 && lastResult.winLines != null && lastResult.winLines.Count > 0)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;
            bool isBigWin = multiplier >= bigWinMultiplierThreshold;
            bool isAutomaticRound =
                isAutoPlaying || isInFreeSpins || isInGoldBurstRespins;
            SpinResult animationResult = lastResult;

            uiManager.OnSpinStopping(lastResult);
            uiManager.DisableControlsDuringWinAnimation();

            if (isBigWin)
            {
                StartCoroutine(TriggerWinPopupWithDelay(1.5f, lastResult));
            }

            if (slotView != null)
            {
                StartCoroutine(ShowWinningAnimationsAfterTrainLanding(
                    animationResult,
                    isAutomaticRound,
                    isBigWin));
            }
            else
            {
                PlayGoldMineResultWinSound(animationResult, isBigWin);
                OnFirstWinAnimationLoopComplete(
                    animationResult,
                    isAutomaticRound,
                    isBigWin);
                if (isAutomaticRound)
                {
                    OnAutomaticWinAnimationComplete(animationResult);
                }
            }
        }
        else
        {
            uiManager.OnSpinStopping(lastResult);
            if (slotView != null)
            {
                uiManager.DisableControlsDuringWinAnimation();
                StartCoroutine(
                    ContinueAfterTrainLandingWithoutWins(lastResult));
            }
            else
            {
                currentState = GameState.Idle;
                OnWinAnimationComplete();
            }
        }
    }

    private IEnumerator ContinueAfterTrainLandingWithoutWins(
        SpinResult animationResult)
    {
        yield return slotView.WaitForTrainLandingAnimationsBeforeWins();
        if (lastResult != animationResult) yield break;

        if (!IsFreeSpinsTriggered(animationResult))
        {
            currentState = GameState.Idle;
        }
        OnWinAnimationComplete();
    }

    private IEnumerator ShowWinningAnimationsAfterTrainLanding(
        SpinResult animationResult,
        bool isAutomaticRound,
        bool isBigWin)
    {
        yield return slotView.WaitForTrainLandingAnimationsBeforeWins();
        if (lastResult != animationResult) yield break;

        PlayGoldMineResultWinSound(animationResult, isBigWin);

        bool hasTrainLandingAnimations =
            slotView.HasActiveTrainLandingAnimations();
        bool isFreeGameTrigger = IsFreeSpinsTriggered(animationResult);
        if (isFreeGameTrigger)
        {
            if (!isInFreeSpins && !isInGoldBurstRespins)
            {
                uiManager.StartWinAmountCount(
                    animationResult.winAmount,
                    () => CompleteWinSequence(
                        animationResult,
                        isAutomaticRound,
                        isBigWin));
            }
            else if (isInFreeSpins)
            {
                uiManager.StartFreeSpinWinAmountCount(
                    animationResult,
                    () => CompleteWinSequence(
                        animationResult,
                        isAutomaticRound,
                        isBigWin));
            }
            else
            {
                CompleteWinSequence(
                    animationResult,
                    isAutomaticRound,
                    isBigWin);
            }
            yield break;
        }

        if (!isInFreeSpins && !isInGoldBurstRespins)
        {
            uiManager.StartWinAmountCount(animationResult.winAmount);
        }
        else if (isInFreeSpins)
        {
            uiManager.StartFreeSpinWinAmountCount(animationResult);
        }

        if (hasTrainLandingAnimations)
        {
            slotView.ShowWinningSymbolAnimations(
                animationResult.winLines,
                2,
                null,
                () => CompleteWinSequence(
                    animationResult,
                    isAutomaticRound,
                    isBigWin));
            yield break;
        }

        slotView.ShowWinningSymbolAnimations(
            animationResult.winLines,
            isAutomaticRound ? 2 : 0,
            () => OnFirstWinAnimationLoopComplete(
                animationResult,
                isAutomaticRound,
                isBigWin),
            isAutomaticRound
                ? () => OnAutomaticWinAnimationComplete(animationResult)
                : null);
    }

    private void CompleteWinSequence(
        SpinResult animationResult,
        bool isAutomaticRound,
        bool isBigWin)
    {
        if (lastResult != animationResult) return;

        OnFirstWinAnimationLoopComplete(
            animationResult,
            isAutomaticRound,
            isBigWin);
        if (isAutomaticRound)
        {
            OnAutomaticWinAnimationComplete(animationResult);
        }
    }

    private void OnFirstWinAnimationLoopComplete(
        SpinResult animationResult,
        bool isAutomaticRound,
        bool isBigWin)
    {
        if (lastResult != animationResult) return;

        bool isFreeGameTrigger = IsFreeSpinsTriggered(animationResult);
        if (!isFreeGameTrigger)
        {
            currentState = GameState.Idle;
            if (!isBigWin)
            {
                uiManager.EnableControlsAfterWinAnimation();
            }
        }

        if (!isAutomaticRound)
        {
            OnWinAnimationComplete();
        }
    }

    private void OnAutomaticWinAnimationComplete(SpinResult animationResult)
    {
        if (lastResult != animationResult) return;
        OnWinAnimationComplete();
    }

    private IEnumerator TriggerWinPopupWithDelay(float delay, SpinResult result)
    {
        double totalPay = GetTotalPay();
        double multiplier = totalPay > 0 ? (result.winAmount / totalPay) : 0;
        if (multiplier < bigWinMultiplierThreshold)
        {
            waitingForSpecialWin = false;
            yield break;
        }

        waitingForSpecialWin = true;

        yield return new WaitForSeconds(delay);

        if (lastResult == result && multiplier >= bigWinMultiplierThreshold)
        {
            uiManager.TriggerBigWinPopup(result, () =>
            {
                waitingForSpecialWin = false;
            });
        }
        else
        {
            waitingForSpecialWin = false;
        }
    }

    private void OnWinAnimationComplete()
    {
        StartCoroutine(ProcessSpecialFeaturesAfterWin());
    }

    private IEnumerator ProcessSpecialFeaturesAfterWin()
    {
        // Wait for special win popup to finish before starting special features
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        if (IsFreeSpinsTriggered(lastResult))
        {
            if (slotView != null)
            {
                yield return slotView.PlayFreeGameTrainTriggerAnimation();
            }

            ProcessSpinResult();
            yield break;
        }

        ResumeAfterSpecialFeature();
    }

    private bool IsFreeSpinsTriggered(SpinResult result)
    {
        return result != null &&
               result.freeSpinData != null &&
               result.freeSpinData.isTriggered &&
               !isInGoldBurstRespins;
    }

    private void PlayGoldMineResultWinSound(
        SpinResult result,
        bool isBigWin)
    {
        if (isBigWin || result?.winLines == null ||
            result.winLines.Count == 0)
        {
            return;
        }

        var winningSymbolIds = new HashSet<int>();
        int reelCount = result.resultMatrix != null
            ? result.resultMatrix.Count
            : 0;

        foreach (WinLine winLine in result.winLines)
        {
            if (winLine == null) continue;

            winningSymbolIds.Add(winLine.symbolId);
            if (winLine.positions == null || reelCount <= 0) continue;

            foreach (int flatPosition in winLine.positions)
            {
                if (flatPosition < 0) continue;

                int row = flatPosition / reelCount;
                int reelIndex = flatPosition % reelCount;
                if (reelIndex < 0 || reelIndex >= reelCount ||
                    result.resultMatrix[reelIndex] == null ||
                    row < 0 || row >= result.resultMatrix[reelIndex].Count)
                {
                    continue;
                }

                winningSymbolIds.Add(result.resultMatrix[reelIndex][row]);
            }
        }

        bool hasWild = false;
        bool hasDonkey = false;
        bool hasBoots = false;
        bool containsOnlyWildAndLowSymbols = true;
        bool containsOnlyDonkeyAndLowSymbols = true;
        bool containsOnlyBootsAndLowSymbols = true;

        foreach (int symbolId in winningSymbolIds)
        {
            SymbolInfo symbol = FindSymbolInfo(symbolId);
            string normalizedName = NormalizeWinSymbolName(symbol?.name);
            bool isWild = (gameConfig != null &&
                           symbolId == gameConfig.wildSymbolId) ||
                          (symbol != null && symbol.isWild) ||
                          normalizedName == "wild";
            bool isDonkey = normalizedName == "donkey";
            bool isBoots = normalizedName == "boots" ||
                           normalizedName == "boot";
            bool isLowSymbol = normalizedName == "a" ||
                               normalizedName == "ace" ||
                               normalizedName == "k" ||
                               normalizedName == "king" ||
                               normalizedName == "q" ||
                               normalizedName == "queen" ||
                               normalizedName == "j" ||
                               normalizedName == "jack";

            hasWild |= isWild;
            hasDonkey |= isDonkey;
            hasBoots |= isBoots;
            containsOnlyWildAndLowSymbols &= isWild || isLowSymbol;
            containsOnlyDonkeyAndLowSymbols &= isDonkey || isLowSymbol;
            containsOnlyBootsAndLowSymbols &= isBoots || isLowSymbol;
        }

        bool useWildSound = hasWild && !hasDonkey && !hasBoots &&
                            containsOnlyWildAndLowSymbols;
        bool useDonkeySound = hasDonkey && !hasWild && !hasBoots &&
                              containsOnlyDonkeyAndLowSymbols;
        bool useBootsSound = hasBoots && !hasWild && !hasDonkey &&
                             containsOnlyBootsAndLowSymbols;
        AudioManager.Instance?.PlayGoldMineResultWin(
            useWildSound,
            useDonkeySound,
            useBootsSound);
    }

    private SymbolInfo FindSymbolInfo(int symbolId)
    {
        if (gameConfig?.symbols == null) return null;

        foreach (SymbolInfo symbol in gameConfig.symbols)
        {
            if (symbol != null && symbol.id == symbolId) return symbol;
        }

        return null;
    }

    private static string NormalizeWinSymbolName(string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName)) return string.Empty;

        var normalized = new List<char>(symbolName.Length);
        foreach (char character in symbolName)
        {
            if (char.IsLetterOrDigit(character))
            {
                normalized.Add(char.ToLowerInvariant(character));
            }
        }

        return new string(normalized.ToArray());
    }

    private void ResumeAfterSpecialFeature()
    {
        if (isAutoPlaying || isInFreeSpins)
        {
            StartCoroutine(DelayBeforeNextRound());
        }
        else
        {
            ProcessSpinResult();
        }
    }

    private IEnumerator DelayBeforeNextRound()
    {
        float delayTime = currentSpinSpeed == SpinSpeed.QuickSpin ? 0.3f : 0.5f;
        yield return new WaitForSeconds(delayTime);

        // Wait for special win popup using the flag and active state
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        ProcessSpinResult();
    }

    private float GetSpinDuration()
    {
        return currentSpinSpeed switch
        {
            SpinSpeed.Normal => normalSpinDuration,
            SpinSpeed.Turbo => turboSpinDuration,
            SpinSpeed.QuickSpin => quickSpinCycleDuration,
            _ => normalSpinDuration
        };
    }

    internal void OnSpinResultReceived(SpinResult result)
    {
        bool isGoldBurstTrigger = !isInGoldBurstRespins &&
                                  result?.goldBurstData != null &&
                                  result.goldBurstData.triggered;
        if (isInGoldBurstRespins && result?.goldBurstData != null)
        {
            List<List<int>> activeFeatureMatrix = GetActiveGoldBurstMatrix(
                result.goldBurstData);
            if (activeFeatureMatrix != null)
            {
                result.resultMatrix = activeFeatureMatrix;
            }
        }

        lastResult = result;
        bool isFreeGameTrigger = IsFreeSpinsTriggered(result);
        slotView?.ConfigureTrainLandingAnimations(!isGoldBurstTrigger);
        slotView?.ConfigureTrainLandingWinSequence(
            !isGoldBurstTrigger,
            1,
            !isGoldBurstTrigger && !isFreeGameTrigger);
        slotView?.ConfigureGoldBurstTriggerBarrelMerge(isGoldBurstTrigger);
        slotView?.PrepareTwoSlotBarrels(result.twoSlotBarrels);
        slotView?.PrepareThreeSlotBarrels(result.threeSlotBarrels);
        slotView?.PrepareTrains(result.trains);
        if (result.winLines != null)
        {
            for (int i = 0; i < result.winLines.Count; i++)
            {
                var line = result.winLines[i];

            }
        }
    }

    private void ProcessSpinResult()
    {
        if (lastResult == null) return;

        playerData = lastResult.playerData;

        uiManager.OnSpinCompleted(lastResult);

        // Extract server-authoritative values before nullifying lastResult
        int serverSpinsRemaining = lastResult.serverSpinsRemaining;
        int serverSpinsUsed = lastResult.serverSpinsUsed;
        double serverTotalRoundWin = lastResult.serverTotalRoundWin;
        bool isRoundOver = lastResult.isRoundOver;

        GoldBurstData goldBurst = lastResult.goldBurstData;
        if (!isInGoldBurstRespins && goldBurst != null && goldBurst.triggered &&
            goldBurst.inRespin && goldBurst.remainingRespins > 0)
        {
            if (!isInFreeSpins && lastResult.freeSpinData != null && lastResult.freeSpinData.isTriggered)
            {
                pendingFreeSpins = lastResult.freeSpinData.spinsAwarded;
            }

            GoldBurstTier tier = ResolveGoldBurstTier(
                goldBurst,
                lastResult.resultMatrix);
            StartGoldBurstRespins(tier, lastResult);
            lastResult = null;
            return;
        }

        if (isInGoldBurstRespins)
        {
            if (!isInFreeSpins && lastResult.freeSpinData != null &&
                lastResult.freeSpinData.isTriggered)
            {
                pendingFreeSpins = Mathf.Max(
                    pendingFreeSpins,
                    lastResult.freeSpinData.spinsAwarded);
            }

            bool hasAnotherRespin = goldBurst != null &&
                                    goldBurst.inRespin &&
                                    goldBurst.remainingRespins > 0;

            lastResult = null;

            if (hasAnotherRespin)
            {
                currentState = GameState.Idle;
                StartCoroutine(DelayBeforeNextGoldBurstRespin());
            }
            else if (!isCompletingGoldBurstRespins)
            {
                isCompletingGoldBurstRespins = true;
                currentState = GameState.Stopping;
                uiManager.DisableControlsDuringWinAnimation();
                StartCoroutine(CompleteGoldBurstRespinsSequence(
                    goldBurst?.prizes,
                    serverTotalRoundWin,
                    serverSpinsUsed,
                    isRoundOver));
            }

            return;
        }

        // The count is normally applied in OnReelsStoppedComplete. Keep this state-only
        // fallback in case a result is processed through another path.
        if (isInFreeSpins && freeSpinsRemaining != serverSpinsRemaining)
        {
            freeSpinsRemaining = serverSpinsRemaining;
        }


        bool freeSpinsTriggered = lastResult.freeSpinData != null &&
                                  lastResult.freeSpinData.isTriggered;

        // A retrigger replays the complete Free Games entry sequence without
        // resetting the server count or the accumulated Free Games win.
        if (freeSpinsTriggered && isInFreeSpins)
        {
            lastResult = null;
            waitingForFreeSpinStart = true;
            currentState = GameState.Stopping;
            StartCoroutine(ResumeFreeSpinsAfterRetrigger(
                isRoundOver,
                serverTotalRoundWin,
                serverSpinsUsed));
            return;
        }

        // Check if free spins were just triggered (initial trigger from base game)
        if (freeSpinsTriggered)
        {
            StartFreeSpins(lastResult.freeSpinData.spinsAwarded);
            lastResult = null;
            return;
        }

        lastResult = null;

        if (isAutoPlaying && !isInFreeSpins)
        {
            if (autoPlayTotalRounds != -1)
            {
                autoPlayRemainingRounds--;
            }

            uiManager.UpdateAutoPlayCount();

            if (autoPlayTotalRounds != -1 && autoPlayRemainingRounds <= 0)
            {
                currentState = GameState.Idle;
                StopAutoPlay();
            }
            else
            {
                // Before requesting the next spin, verify the player can still afford it.
                // If not, stop autoplay (restores all UI) then show the popup.
                double totalPay = GetTotalPay();
                if (playerData.balance < totalPay)
                {
                    currentState = GameState.Idle;
                    StopAutoPlay();
                    if (popupManager != null) popupManager.ShowInsufficientFundsError();
                }
                else
                {
                    currentState = GameState.Idle;
                    RequestSpin();
                }
            }
        }
        else if (isInFreeSpins)
        {
            // Free spin counter already updated in OnSpinResultReceived
            // No need to update again here

            if (isRoundOver || freeSpinsRemaining <= 0)
            {
                // Always use server-authoritative spinsUsed
                EndFreeSpins(serverTotalRoundWin, serverSpinsUsed);
            }
            else
            {
                currentState = GameState.Idle;
                StartCoroutine(DelayBeforeNextFreeSpin());
            }
        }
        else
        {
            currentState = GameState.Idle;
        }
    }

    #endregion

    #region Spin Speed Control

    internal void SetSpinSpeed(SpinSpeed speed)
    {
        currentSpinSpeed = speed;
    }

    #endregion



    #region Auto Play

    internal void StartAutoPlay(int rounds)
    {
        if (currentState != GameState.Idle) return;

        // Check balance BEFORE locking any UI — if insufficient, show popup and bail.
        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = rounds;
        autoPlayRemainingRounds = rounds;
        wasAutoPlayingBeforeFreeSpins = false;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    internal void StopAutoPlay()
    {
        isAutoPlaying = false;
        autoPlayRemainingRounds = 0;
        wasAutoPlayingBeforeFreeSpins = false;

        uiManager.OnAutoPlayStopped();
    }

    internal bool ShouldResumeAutoPlay()
    {
        return wasAutoPlayingBeforeFreeSpins && (savedAutoPlayTotalRounds == -1 || savedAutoPlayRemainingRounds > 0);
    }

    internal void ResumeAutoPlay()
    {
        if (!ShouldResumeAutoPlay()) return;

        int remaining = savedAutoPlayRemainingRounds;
        int total = savedAutoPlayTotalRounds;
        wasAutoPlayingBeforeFreeSpins = false;

        if (currentState != GameState.Idle) return;

        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = total;
        autoPlayRemainingRounds = remaining;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    #endregion

    #region Free Spins

    private void StartGoldBurstRespins(
        GoldBurstTier tier,
        SpinResult triggerResult)
    {
        List<List<int>> entryMatrix = GetGoldBurstEntryMatrix(
            triggerResult,
            tier);
        if (entryMatrix == null)
        {
            Debug.LogError(
                $"[GameManager] The server did not provide a valid {tier} Gold Burst entry matrix.",
                this);
            popupManager?.ShowServerError(
                "The Gold Burst layout could not be displayed. Please reconnect and try again.");
            currentState = GameState.Idle;
            uiManager.EnableControlsAfterWinAnimation();
            return;
        }

        AudioManager.Instance?.PlayGoldMineRespinTrigger();
        activeGoldBurstTier = tier;
        isInGoldBurstRespins = true;
        isCompletingGoldBurstRespins = false;
        uiManager.UseGoldBurstFeatureSpinCountDisplay(tier);
        uiManager.ShowGoodLuckDisplay();
        uiManager.HideFeatureSpinCount();
        uiManager.DisableControlsDuringWinAnimation();

        int previousTotal = autoPlayTotalRounds;
        int previousRemaining = autoPlayRemainingRounds;
        if (isAutoPlaying)
        {
            StopAutoPlay();
            wasAutoPlayingBeforeFreeSpins = true;
            savedAutoPlayTotalRounds = previousTotal;
            savedAutoPlayRemainingRounds = previousTotal != -1 ? previousRemaining - 1 : -1;
        }

        currentState = GameState.Stopping;
        int initialRespins = Mathf.Max(
            0,
            triggerResult?.goldBurstData?.remainingRespins ?? 0);
        StartCoroutine(StartGoldBurstRespinsSequence(
            tier,
            entryMatrix,
            initialRespins));
    }

    private IEnumerator StartGoldBurstRespinsSequence(
        GoldBurstTier tier,
        List<List<int>> entryMatrix,
        int initialRespins)
    {
        bool entryMatrixApplied = false;
        if (slotView != null)
        {
            yield return slotView.PlayGoldBurstTriggerPresentation(
                tier,
                () =>
                {
                    entryMatrixApplied =
                        slotView.ApplyGoldBurstEntryMatrix(entryMatrix);
                    return entryMatrixApplied;
                });
        }

        if (!entryMatrixApplied)
        {
            Debug.LogError(
                "[GameManager] The server-provided Gold Burst entry matrix could not be applied.",
                this);
            popupManager?.ShowServerError(
                "The Gold Burst layout could not be displayed. Please reconnect and try again.");
            RestoreBackgroundMusicAfterGoldBurst();
            isInGoldBurstRespins = false;
            activeGoldBurstTier = GoldBurstTier.Cold;
            currentState = GameState.Idle;
            uiManager.EnableControlsAfterWinAnimation();
            yield break;
        }

        uiManager.UpdateFreeSpinCount(initialRespins);
        currentState = GameState.Idle;
        RequestSpin();
    }

    private IEnumerator DelayBeforeNextGoldBurstRespin()
    {
        currentState = GameState.Stopping;
        yield return new WaitForSeconds(0.3f);

        while (waitingForSpecialWin ||
               uiManager.IsSpecialWinActive ||
               (slotView != null && slotView.IsGoldBurstMergeInProgress()))
        {
            yield return null;
        }

        currentState = GameState.Idle;
        RequestSpin();
    }

    private IEnumerator CompleteGoldBurstRespinsSequence(
        IReadOnlyList<GoldBurstPrizePlacement> prizes,
        double totalRoundWin,
        int totalSpinsUsed,
        bool isRoundOver)
    {
        if (slotView != null)
        {
            yield return slotView.PlayGoldBurstFinalPresentation(
                prizes,
                totalRoundWin);
        }

        EndGoldBurstRespins(totalRoundWin, totalSpinsUsed, isRoundOver);
    }

    private static GoldBurstTier ResolveGoldBurstTier(
        GoldBurstData goldBurst,
        List<List<int>> matrix)
    {
        string serverMode = goldBurst?.mode;
        if (!string.IsNullOrWhiteSpace(serverMode))
        {
            if (serverMode.IndexOf(
                    "ULTIMATE",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GoldBurstTier.Ultimate;
            }
            if (serverMode.IndexOf(
                    "MEGA",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GoldBurstTier.Mega;
            }
            if (serverMode.IndexOf(
                    "GOLD_BURST",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GoldBurstTier.Cold;
            }
        }

        int highestSymbolId = FirstGoldBurstSymbolId;
        if (matrix != null)
        {
            foreach (List<int> column in matrix)
            {
                if (column == null) continue;
                foreach (int symbolId in column)
                {
                    if (symbolId >= FirstGoldBurstSymbolId &&
                        symbolId <= LastGoldBurstSymbolId)
                    {
                        highestSymbolId = Mathf.Max(highestSymbolId, symbolId);
                    }
                }
            }
        }

        return highestSymbolId switch
        {
            13 => GoldBurstTier.Ultimate,
            12 => GoldBurstTier.Mega,
            _ => GoldBurstTier.Cold
        };
    }

    private List<List<int>> GetActiveGoldBurstMatrix(GoldBurstData goldBurst)
    {
        if (goldBurst == null) return null;

        if (activeGoldBurstTier == GoldBurstTier.Ultimate)
        {
            return CombineGoldBurstMatrices(
                goldBurst.expandedMatrix,
                goldBurst.secondaryExpandedMatrix);
        }

        return activeGoldBurstTier == GoldBurstTier.Mega &&
               HasMatrixDimensions(
                   goldBurst.expandedMatrix,
                   ExpandedGoldBurstReelCount,
                   GoldBurstRowCount)
            ? CloneMatrix(goldBurst.expandedMatrix)
            : null;
    }

    private static List<List<int>> GetGoldBurstEntryMatrix(
        SpinResult triggerResult,
        GoldBurstTier tier)
    {
        if (triggerResult == null) return null;

        GoldBurstData goldBurst = triggerResult.goldBurstData;
        if (tier == GoldBurstTier.Ultimate)
        {
            List<List<int>> combined = CombineGoldBurstMatrices(
                goldBurst?.expandedMatrix,
                goldBurst?.secondaryExpandedMatrix);
            if (combined != null) return combined;

            return HasMatrixDimensions(
                    triggerResult.resultMatrix,
                    ExpandedGoldBurstReelCount * 2,
                    GoldBurstRowCount)
                ? CloneMatrix(triggerResult.resultMatrix)
                : null;
        }

        if (tier == GoldBurstTier.Mega)
        {
            if (HasMatrixDimensions(
                    goldBurst?.expandedMatrix,
                    ExpandedGoldBurstReelCount,
                    GoldBurstRowCount))
            {
                return CloneMatrix(goldBurst.expandedMatrix);
            }

            return HasMatrixDimensions(
                    triggerResult.resultMatrix,
                    ExpandedGoldBurstReelCount,
                    GoldBurstRowCount)
                ? CloneMatrix(triggerResult.resultMatrix)
                : null;
        }

        return HasMatrixDimensions(
                triggerResult.resultMatrix,
                5,
                GoldBurstRowCount)
            ? CloneMatrix(triggerResult.resultMatrix)
            : null;
    }

    private static List<List<int>> CombineGoldBurstMatrices(
        List<List<int>> firstMatrix,
        List<List<int>> secondMatrix)
    {
        if (!HasMatrixDimensions(
                firstMatrix,
                ExpandedGoldBurstReelCount,
                GoldBurstRowCount) ||
            !HasMatrixDimensions(
                secondMatrix,
                ExpandedGoldBurstReelCount,
                GoldBurstRowCount))
        {
            return null;
        }

        var combined = new List<List<int>>(ExpandedGoldBurstReelCount * 2);
        for (int reelIndex = 0;
             reelIndex < ExpandedGoldBurstReelCount;
             reelIndex++)
        {
            combined.Add(new List<int>(firstMatrix[reelIndex]));
        }
        for (int reelIndex = 0;
             reelIndex < ExpandedGoldBurstReelCount;
             reelIndex++)
        {
            combined.Add(new List<int>(secondMatrix[reelIndex]));
        }

        return combined;
    }

    private static bool HasMatrixDimensions(
        List<List<int>> matrix,
        int reelCount,
        int rowCount)
    {
        if (matrix == null || matrix.Count < reelCount) return false;
        for (int reelIndex = 0; reelIndex < reelCount; reelIndex++)
        {
            if (matrix[reelIndex] == null ||
                matrix[reelIndex].Count < rowCount)
            {
                return false;
            }
        }

        return true;
    }

    private static List<List<int>> CloneMatrix(List<List<int>> matrix)
    {
        if (matrix == null) return null;

        var clone = new List<List<int>>(matrix.Count);
        foreach (List<int> column in matrix)
        {
            clone.Add(column != null ? new List<int>(column) : new List<int>());
        }

        return clone;
    }

    private void EndGoldBurstRespins(double totalRoundWin, int totalSpinsUsed, bool isRoundOver)
    {
        slotView?.EndGoldBurstPresentation();
        RestoreBackgroundMusicAfterGoldBurst();
        isCompletingGoldBurstRespins = false;
        isInGoldBurstRespins = false;
        activeGoldBurstTier = GoldBurstTier.Cold;
        currentState = GameState.Idle;

        if (isInFreeSpins)
        {
            uiManager.UseBaseFeatureSpinCountDisplay();
            uiManager.UpdateFreeSpinCumulativeWin(totalRoundWin);
            uiManager.UpdateFreeSpinCount(freeSpinsRemaining);

            if (isRoundOver || freeSpinsRemaining <= 0)
            {
                EndFreeSpins(totalRoundWin, totalSpinsUsed);
            }
            else
            {
                StartCoroutine(DelayBeforeNextFreeSpin());
            }
            return;
        }

        uiManager.HideFeatureSpinCount();
        uiManager.UseBaseFeatureSpinCountDisplay();

        if (pendingFreeSpins > 0)
        {
            int spins = pendingFreeSpins;
            pendingFreeSpins = 0;
            StartFreeSpins(spins);
        }
        else if (ShouldResumeAutoPlay())
        {
            ResumeAutoPlay();
        }
        else
        {
            uiManager.EnableControlsAfterWinAnimation();
        }
    }

    private void RestoreBackgroundMusicAfterGoldBurst()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null) return;

        audioManager.StopGoldMineRespinBg();
        if (isInFreeSpins)
        {
            audioManager.PlayGoldMineBonusBg();
        }
        else
        {
            audioManager.PlayMainBg();
        }
    }

    private void StartFreeSpins(int spins)
    {
        isInFreeSpins = true;
        isCompletingFreeSpins = false;
        freeSpinsRemaining = spins;
        freeSpinsUsed = 0;
        waitingForFreeSpinStart = true;

        int prevTotal = autoPlayTotalRounds;
        int prevRemaining = autoPlayRemainingRounds;

        if (isAutoPlaying)
        {
            StopAutoPlay();
            wasAutoPlayingBeforeFreeSpins = true;
            savedAutoPlayTotalRounds = prevTotal;
            savedAutoPlayRemainingRounds = (prevTotal != -1) ? (prevRemaining - 1) : -1;
        }

        uiManager.OnFreeSpinsStarted(spins);
        currentState = GameState.Stopping;
        StartCoroutine(BeginFreeSpinsSequence());
    }

    private IEnumerator BeginFreeSpinsSequence()
    {
        if (slotView != null)
        {
            yield return slotView.PlayFreeGamesStartPresentation(uiManager);
        }

        AudioManager.Instance?.PlayGoldMineBonusBg();
        currentState = GameState.Idle;
        StartFirstFreeSpin();
    }

    private IEnumerator ResumeFreeSpinsAfterRetrigger(
        bool isRoundOver,
        double totalRoundWin,
        int totalSpinsUsed)
    {
        if (slotView != null)
        {
            yield return slotView.PlayFreeGamesStartPresentation(uiManager);
        }

        waitingForFreeSpinStart = false;

        if (isRoundOver || freeSpinsRemaining <= 0)
        {
            EndFreeSpins(totalRoundWin, totalSpinsUsed);
            yield break;
        }

        currentState = GameState.Idle;
        StartCoroutine(DelayBeforeNextFreeSpin());
    }

    internal void StartFirstFreeSpin()
    {
        waitingForFreeSpinStart = false;

        StartCoroutine(DelayBeforeFirstFreeSpin());
    }


    private IEnumerator DelayBeforeFirstFreeSpin()
    {
        yield return new WaitForSeconds(0.5f);
        RequestSpin();
    }

    private IEnumerator DelayBeforeNextFreeSpin()
    {
        yield return new WaitForSeconds(0.3f);

        // Wait for special win popup if it's still active or pending
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        RequestSpin();
    }

    private void EndFreeSpins(double totalRoundWin, int totalSpinsUsed)
    {
        if (isCompletingFreeSpins) return;

        isCompletingFreeSpins = true;
        waitingForFreeSpinStart = true;
        currentState = GameState.Stopping;
        StartCoroutine(CompleteFreeSpinsSequence(totalRoundWin, totalSpinsUsed));
    }

    private IEnumerator CompleteFreeSpinsSequence(
        double totalRoundWin,
        int totalSpinsUsed)
    {
        double finalWin = System.Math.Max(
            totalRoundWin,
            uiManager != null ? uiManager.GetFreeSpinCumulativeWin() : 0d);
        if (slotView != null)
        {
            yield return slotView.PlayFreeGamesEndPresentation(
                finalWin,
                uiManager);
        }

        isInFreeSpins = false;
        freeSpinsRemaining = 0;
        waitingForFreeSpinStart = false;
        isCompletingFreeSpins = false;
        AudioManager.Instance?.PlayMainBg();
        uiManager.OnFreeSpinsEnded(finalWin, totalSpinsUsed);
        currentState = GameState.Idle;
    }

    #endregion

    #region Connection Events

    internal void OnDisconnected()
    {
        if (isInGoldBurstRespins)
        {
            RestoreBackgroundMusicAfterGoldBurst();
        }

        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }

        wasAutoPlayingBeforeFreeSpins = false;
        isCompletingFreeSpins = false;
        waitingForFreeSpinStart = false;
        if (isAutoPlaying)
        {
            StopAutoPlay();
        }

        currentState = GameState.Idle;
        // Note: The disconnection popup is shown by SocketIOManager.OnSocketDisconnected()
        // to avoid duplicates. GameManager only cleans up state here.
    }

    internal void ExitGame()
    {
        socketManager.CloseSocket();

    }

    #endregion

    #region Helper Methods

    internal double GetTotalPay()
    {
        double divisor = (gameConfig != null && gameConfig.creditDivisor > 0) ? gameConfig.creditDivisor : 25;
        return currentBetAmount * divisor;
    }

    internal bool CanAffordBet()
    {
        double totalPay = GetTotalPay();
        return playerData.balance >= totalPay;
    }

    internal bool IsSpinning()
    {
        return currentState == GameState.Spinning || currentState == GameState.Stopping;
    }

    /// <summary>
    /// Returns true if at least one scatter symbol appears anywhere in the result matrix.
    /// Uses the server-configured scatterSymbolId (default 12) as the reference ID.
    /// </summary>
    private bool ResultMatrixHasScatter(List<List<int>> matrix)
    {
        if (matrix == null) return false;

        int scatterId = gameConfig != null ? gameConfig.scatterSymbolId : 12;

        foreach (var col in matrix)
        {
            if (col == null) continue;
            foreach (int sym in col)
            {
                if (sym == scatterId) return true;
            }
        }

        return false;
    }

    #endregion
}
