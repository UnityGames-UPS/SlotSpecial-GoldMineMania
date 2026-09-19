using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#region Server Communication Models

[Serializable]
public class InitData
{
    public string id = "initData";
    public ServerGameData gameData;
    public ServerFeatures features;
    public ServerUIData uiData;
    public ServerPlayer player;
    public JackpotData jackpotData;
}

[Serializable]
public class JackpotData
{
    public JackpotValues values;//jackpotfeature

}

[Serializable]
public class JackpotValues
{
    public string miniJackpot;
    public string minorJackpot;
    public string majorJackpot;
    public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
    public string gameId;
    public JackpotValues values;
}

[Serializable]
public class ServerGameData
{
    public List<List<int>> lines;
    public List<double> bets;
    public double creditDivisor = 25;
    public int totalLines;
}

[Serializable]
public class ServerFeatures
{
    public FreeGamesFeature freeGames;
    public GoldBurstRespinFeature goldBurstRespin;
    public GoldMineJourneyFeature goldMineJourney;
    public int betMultiplier;
    public int maxWinMultiplier;
    public int minWinMultiplier;
}

[Serializable]
public class USpinSegment
{
    public int sliceIndex;
    public string type;
    public double multiplier;
    public int freeGames;
}

[Serializable]
public class FreeGamesFeature
{
    public bool enabled;
    public int minTrigger;
    public int symbolId;
    public int initialFreeGames;
    public double payMultiplier;
    public int maxTotalFreeGames;
    public List<int> reel3AllowedSymbols;
}

[Serializable]
public class GoldBurstRespinFeature
{
    public bool enabled;
    public int minTrigger;
    public List<int> triggerSymbols;
}

[Serializable]
public class GoldMineJourneyFeature
{
    public bool enabled;
    public int symbolId;
    public int requiredCollect;
    public int currentCollect;
}

[Serializable]
public class ExtraSpinsData
{
    [JsonProperty("2")] public int _2; // Keep for safety/compatibility with UI
    [JsonProperty("3")] public int _3;
    [JsonProperty("4")] public int _4;
    [JsonProperty("5")] public int _5;
}

[Serializable]
public class ServerUIData
{
    public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
    public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
    public int id;
    public string name;
    public string group;
    public List<double> multiplier; // Keep for fallback compatibility
    public List<double> payout;
    public string description;
    public int minMatch;
}


[Serializable]
public class ServerPlayer
{
    public double balance;
}

// ============================================================================
// FIXED: Server Response Models - Must match actual server JSON structure
// ============================================================================

[Serializable]
public class ServerSpinResponse
{
    public string id = "ResultData";
    public bool success;
    public List<List<string>> matrix; // Root level matrix sent by server
    public ServerPlayerBalance player;
    public ServerPayload payload;

    [JsonExtensionData]
    public IDictionary<string, JToken> additionalData;
}

[Serializable]
public class ServerPlayerBalance
{
    public double? balance; // Nullable because server sends null
}

[Serializable]
public class ServerPayload
{
    public List<List<string>> reels;        // Keep for fallback compatibility
    public double totalWin;                  // Keep for fallback compatibility
    public int scatterCount;
    public bool scatterTriggered;
    public bool isRoundOver;                 // True when free spin round is over
    public double totalRoundWin;             // Total round win (at payload level when isRoundOver)

    // CNY fields
    public double winAmount;
    public double grandTotalWin;
    public double netReturnRatio;
    public List<ServerWaysWin> waysWins;
    public ServerUSpinResult uSpin;
    public ServerMoneyBagResult moneyBag;
    public ServerFreeGamesResult freeGames;
    public ServerGoldBurstResult goldBurst;

    [JsonExtensionData]
    public IDictionary<string, JToken> additionalData;
}

[Serializable]
public class ServerWaysWin
{
    public int symbolId;
    public int matchCount;
    public int waysCount;
    public List<ServerPosition> matchedPositions;
    public double basePayout;
    public double appliedMultiplier;
    public double winInCredits;
    public double winInCash;
    public string winType;
}

[Serializable]
public class ServerPosition
{
    public int row;
    public int col;
}

[Serializable]
public class ServerUSpinResult
{
    public bool triggered;
    public ServerUSpinResultDetail result;
}

[Serializable]
public class ServerUSpinResultDetail
{
    public int sliceIndex;
    public string type;
    public double multiplierAwarded;
    public int freeGamesAwarded;
    public double winInCash;
}

[Serializable]
public class ServerMoneyBagResult
{
    public bool triggered;
    public ServerMoneyBagResultDetail result;
}

[Serializable]
public class ServerMoneyBagResultDetail
{
    public int pickedIndex;
    public List<int> revealed;
    public int creditsAwarded;
    public double winInCash;
}

[Serializable]
public class ServerFreeGamesResult
{
    public bool triggered;
    public int totalAwarded;
    public int? played;
    public int? remaining;
    public double totalFreeGamesWin;
}

[Serializable]
public class ServerGoldBurstResult
{
    public bool triggered;
    public bool inRespin;
    public int remainingRespins;

    [JsonExtensionData]
    public IDictionary<string, JToken> additionalData;
}

// ============================================================================
// Client-Side Spin Request
// ============================================================================

[Serializable]
public class SpinRequest
{
    public string type = "SPIN";
    public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
    public int betIndex;
    public bool isFreeSpin;
}

[Serializable]
public class JackpotOpenRequest
{
    public string type = "JACKPOT_OPEN";
    public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
  public string tier;
}


#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
    public int reelCount = 5;
    public int rowCount = 3;
    public int symbolCount = 15;
    public int paylineCount = 50;
    public List<List<int>> paylines;
    public List<double> availableBets;
    public List<SymbolInfo> symbols;

    // Wild configuration
    public int wildSymbolId = 9;

    // Scatter configuration
    public bool freeGamesEnabled;
    public int scatterSymbolId = 10;
    public int freeGameMinTrigger = 3;
    public int maxTotalFreeSpins = 300;
    public List<int> freeGameReel3AllowedSymbols = new List<int>();

    // Gold Mine Mania feature configuration supplied by game:init.
    public bool goldBurstRespinEnabled;
    public int goldBurstMinTrigger = 6;
    public List<int> goldBurstTriggerSymbolIds = new List<int>();
    public bool goldMineJourneyEnabled;
    public int goldMineJourneySymbolId = 14;
    public int goldMineJourneyRequiredCollect = 10;
    public int goldMineJourneyCurrentCollect;

    public int betMultiplier = 1;      // CNY is cash-bet based, multiplier default is 1
    public double creditDivisor = 25;  // Credit divisor sent in initData
    public int maxWinMultiplier = 10000;
    public int minWinMultiplier = 10;
    public int initialFreeSpins = 10;
    public ExtraSpinsData extraSpinsData; // Keep to avoid compilation error in UI

}

[Serializable]
public class SymbolInfo
{
    public int id;
    public string name;
    public List<double> multipliers;
    public bool isWild;
    public bool isScatter;
    public int wildMultiplier = 1;
    public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
    public double balance;
    public int currentBetIndex;
}

[Serializable]
public class SpinResult
{
    public List<List<int>> resultMatrix;  // Client uses int matrix
    public double winAmount;
    public double grandTotalWin;
    public List<WinLine> winLines;
    public PlayerData playerData;
    public FreeSpinData freeSpinData;
    public ScatterData scatterData;
    public OverlayScatterData overlayScatterData; // Keep for safety/UI compilation
    public Dictionary<string, int> stickyWilds;  // Keep for safety/UI compilation

    // Server-authoritative free spin state
    public int serverSpinsRemaining;
    public int serverSpinsUsed;
    public int serverTotalSpins;
    public double serverTotalRoundWin;
    public bool isRoundOver;
    
    // Server-authoritative wheel data
    public USpinResultData uSpinData;
    public MoneyBagResultData moneyBagData;
    public GoldBurstData goldBurstData;
    public List<TwoSlotBarrelPlacement> twoSlotBarrels;
    public List<ThreeSlotBarrelPlacement> threeSlotBarrels;
    public List<TrainPlacement> trains;

    public double GetMoneyBagWin()
    {
        return (moneyBagData != null && moneyBagData.triggered) ? moneyBagData.winInCash : 0;
    }

    public double GetUSpinCashWin()
    {
        return (uSpinData != null && uSpinData.triggered && uSpinData.type == "MULTIPLIER") ? uSpinData.winInCash : 0;
    }

    public double GetTotalFeatureDeferredWins()
    {
        return GetMoneyBagWin() + GetUSpinCashWin();
    }
}

[Serializable]
public class WinLine
{
    public int lineId;
    public int symbolId;
    public List<int> positions;  // Flat list: [row * 5 + col]
    public double winAmount;
}

[Serializable]
public class FreeSpinData
{
    public bool isTriggered;
    public int spinsAwarded;
    public int remainingSpins;
    public bool isBought;
}

[Serializable]
public class ScatterData
{
    public bool isTriggered;
    public int scatterCount;
    public double winAmount;
}

[Serializable]
public class OverlayScatterData
{
    public bool isTriggered;
    public int count;
    public int extraSpins;
    public List<List<int>> positions;
}

[Serializable]
public class USpinResultData
{
    public bool triggered;
    public int sliceIndex;
    public string type;
    public double multiplierAwarded;
    public int freeGamesAwarded;
    public double winInCash;
}

[Serializable]
public class MoneyBagResultData
{
    public bool triggered;
    public int pickedIndex;
    public List<int> revealed;
    public int creditsAwarded;
    public double winInCash;
}

[Serializable]
public class GoldBurstData
{
    public bool triggered;
    public bool inRespin;
    public int remainingRespins;
}

[Serializable]
public class TwoSlotBarrelPlacement
{
    public int reelIndex;
    public int startRow;
}

[Serializable]
public class ThreeSlotBarrelPlacement
{
    public int reelIndex;
}

public enum TrainVisualType
{
    Green,
    Red,
    HorizontalPurple,
    VerticalPurple,
    Golden
}

[Serializable]
public class TrainPlacement
{
    public TrainVisualType type;
    public int startRow;
    public int startCol;
    public int rowCount;
    public int columnCount;
}

#endregion

#region Platform Communication

[Serializable]
public class AuthData
{
    public string token;
    public string socketURL;
    public string nameSpace;
}

#endregion

#region Enums

public enum GameState
{
    Initializing,
    Idle,
    Spinning,
    Stopping,
    ShowingWin,
    FreeSpinMode
}

public enum SpinSpeed
{
    Normal,
    Turbo,
    QuickSpin
}

public enum WinPopupType
{
    RegularWin,         // Normal credit win (multiplier < 500x)
    BigWin,             // Big win (multiplier >= 500x)
    FreeSpinTrigger,    // Free spins awarded from wheel
    MoneyBagCollect,    // Money bag feature collect
    FreeSpinComplete    // All free spins completed
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// Converts server data to client GameConfig
/// </summary>
public static class InitDataConverter
{
    internal static GameConfig ConvertToGameConfig(InitData serverData)
    {
        var config = new GameConfig
        {
            reelCount = 5,
            rowCount = (serverData.gameData.totalLines == 243) ? 3 : (serverData.gameData.totalLines == 1024 ? 4 : 3),
            symbolCount = serverData.uiData.paylines.symbols.Count,
            paylineCount = serverData.gameData.totalLines,
            paylines = serverData.gameData.lines,
            availableBets = serverData.gameData.bets,
            creditDivisor = (serverData.gameData != null && serverData.gameData.creditDivisor > 0) ? serverData.gameData.creditDivisor : 25,
            symbols = new List<SymbolInfo>()
        };

        foreach (var serverSymbol in serverData.uiData.paylines.symbols)
        {
            var symbolInfo = new SymbolInfo
            {
                id = serverSymbol.id,
                name = serverSymbol.name,
                multipliers = new List<double>(),
                isWild = ContainsToken(serverSymbol.name, "wild") ||
                         ContainsToken(serverSymbol.group, "wild"),
                isScatter = ContainsToken(serverSymbol.name, "scatter") ||
                            ContainsToken(serverSymbol.group, "scatter"),
                minMatch = serverSymbol.minMatch
            };

            // Store raw payout values for info page
            if (serverSymbol.payout != null)
            {
                for (int i = serverSymbol.payout.Count - 1; i >= 0; i--)
                {
                    symbolInfo.multipliers.Add(serverSymbol.payout[i]);
                }
            }
            config.symbols.Add(symbolInfo);

            if (symbolInfo.isWild)
            {
                config.wildSymbolId = symbolInfo.id;
            }
            if (symbolInfo.isScatter &&
                (ContainsToken(symbolInfo.name, "freegame") ||
                 ContainsToken(serverSymbol.group, "freegame")))
            {
                config.scatterSymbolId = symbolInfo.id;
            }
        }

        if (serverData.features != null)
        {
            config.betMultiplier = serverData.features.betMultiplier > 0 ? serverData.features.betMultiplier : 1;
            config.maxWinMultiplier = serverData.features.maxWinMultiplier;
            config.minWinMultiplier = serverData.features.minWinMultiplier;

            if (serverData.features.freeGames != null)
            {
                FreeGamesFeature freeGames = serverData.features.freeGames;
                config.freeGamesEnabled = freeGames.enabled;
                config.scatterSymbolId = freeGames.symbolId;
                config.freeGameMinTrigger = freeGames.minTrigger > 0 ? freeGames.minTrigger : 3;
                config.maxTotalFreeSpins = freeGames.maxTotalFreeGames > 0
                    ? freeGames.maxTotalFreeGames
                    : config.maxTotalFreeSpins;
                config.initialFreeSpins = freeGames.initialFreeGames > 0
                    ? freeGames.initialFreeGames
                    : config.initialFreeSpins;
                config.freeGameReel3AllowedSymbols = freeGames.reel3AllowedSymbols != null
                    ? new List<int>(freeGames.reel3AllowedSymbols)
                    : new List<int>();
            }

            if (serverData.features.goldBurstRespin != null)
            {
                GoldBurstRespinFeature goldBurst = serverData.features.goldBurstRespin;
                config.goldBurstRespinEnabled = goldBurst.enabled;
                config.goldBurstMinTrigger = goldBurst.minTrigger > 0 ? goldBurst.minTrigger : 6;
                config.goldBurstTriggerSymbolIds = goldBurst.triggerSymbols != null
                    ? new List<int>(goldBurst.triggerSymbols)
                    : new List<int>();
            }

            if (serverData.features.goldMineJourney != null)
            {
                GoldMineJourneyFeature journey = serverData.features.goldMineJourney;
                config.goldMineJourneyEnabled = journey.enabled;
                config.goldMineJourneySymbolId = journey.symbolId;
                config.goldMineJourneyRequiredCollect = journey.requiredCollect;
                config.goldMineJourneyCurrentCollect = journey.currentCollect;
            }
        }

        return config;
    }

    private static bool ContainsToken(string value, string token)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
    {
        return new PlayerData
        {
            balance = serverPlayer.balance,
            currentBetIndex = defaultBetIndex
        };
    }

    /// <summary>
    /// Converts server response to client SpinResult
    /// </summary>
    internal static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, double betAmount, GameConfig gameConfig)
    {
        double winAmountVal = serverResponse.payload.winAmount > 0 ? serverResponse.payload.winAmount : serverResponse.payload.totalWin;
        double totalPay = (gameConfig != null && gameConfig.creditDivisor > 0) ? betAmount * gameConfig.creditDivisor : betAmount * 25;
        double newBalance = serverResponse.player?.balance ?? CalculateNewBalance(currentBalance, totalPay, winAmountVal);

        int spinsRemaining = 0;
        int spinsUsed = 0;
        int totalSpins = 0;
        double totalRoundWin = 0;
        bool isRoundOver = false;

        if (serverResponse.payload.freeGames != null)
        {
            ServerFreeGamesResult freeGames = serverResponse.payload.freeGames;
            totalSpins = freeGames.totalAwarded > 0
                ? freeGames.totalAwarded
                : (gameConfig != null && gameConfig.initialFreeSpins > 0 ? gameConfig.initialFreeSpins : 10);

            // The server's explicit remaining value is authoritative. The calculation is
            // retained only for compatibility with older responses that omitted it.
            int reportedPlayed = freeGames.played ?? -1;
            int fallbackRemaining = Math.Max(0, totalSpins - Math.Max(0, reportedPlayed));
            spinsRemaining = Math.Max(0, freeGames.remaining ?? fallbackRemaining);
            spinsUsed = reportedPlayed >= 0
                ? reportedPlayed
                : Math.Max(0, totalSpins - spinsRemaining);
            totalSpins = Math.Max(totalSpins, spinsUsed + spinsRemaining);
            totalRoundWin = freeGames.totalFreeGamesWin;
            isRoundOver = serverResponse.payload.isRoundOver ||
                          (spinsRemaining <= 0 && totalSpins > 0);
        }
        else
        {
            isRoundOver = serverResponse.payload.isRoundOver;
            totalRoundWin = serverResponse.payload.totalRoundWin;
        }

        double grandTotalWinVal = serverResponse.payload.grandTotalWin > 0 
            ? serverResponse.payload.grandTotalWin 
            : (winAmountVal + (serverResponse.payload.moneyBag != null && serverResponse.payload.moneyBag.result != null ? serverResponse.payload.moneyBag.result.winInCash : 0) + (serverResponse.payload.uSpin != null && serverResponse.payload.uSpin.result != null ? serverResponse.payload.uSpin.result.winInCash : 0));

        ConvertBarrelPlacements(
            serverResponse,
            out List<TwoSlotBarrelPlacement> twoSlotBarrels,
            out List<ThreeSlotBarrelPlacement> threeSlotBarrels,
            out List<TrainPlacement> trains);

        var result = new SpinResult
        {
            resultMatrix = ConvertReelsToMatrix(serverResponse.payload.reels, serverResponse.matrix, serverResponse.payload.waysWins, gameConfig),
            winAmount = winAmountVal,
            grandTotalWin = grandTotalWinVal,
            winLines = ConvertWinningLines(serverResponse.payload.waysWins, gameConfig),

            playerData = new PlayerData
            {
                balance = newBalance,
                currentBetIndex = 0
            },

            freeSpinData = (serverResponse.payload.freeGames != null && serverResponse.payload.freeGames.triggered)
                ? new FreeSpinData
                {
                    isTriggered = true,
                    spinsAwarded = serverResponse.payload.freeGames.totalAwarded > 0
                        ? serverResponse.payload.freeGames.totalAwarded
                        : totalSpins,
                    remainingSpins = spinsRemaining,
                    isBought = false
                }
                : null,

            scatterData = serverResponse.payload.scatterTriggered
                ? new ScatterData
                {
                    isTriggered = true,
                    scatterCount = serverResponse.payload.scatterCount,
                    winAmount = 0
                }
                : null,

            overlayScatterData = null,
            stickyWilds = null,

            serverSpinsRemaining = spinsRemaining,
            serverSpinsUsed = spinsUsed,
            serverTotalSpins = totalSpins,
            serverTotalRoundWin = totalRoundWin,
            isRoundOver = isRoundOver,
            
            uSpinData = (serverResponse.payload.uSpin != null && serverResponse.payload.uSpin.triggered && serverResponse.payload.uSpin.result != null)
                ? new USpinResultData
                {
                    triggered = true,
                    sliceIndex = serverResponse.payload.uSpin.result.sliceIndex,
                    type = serverResponse.payload.uSpin.result.type,
                    multiplierAwarded = serverResponse.payload.uSpin.result.multiplierAwarded,
                    freeGamesAwarded = serverResponse.payload.uSpin.result.freeGamesAwarded,
                    winInCash = serverResponse.payload.uSpin.result.winInCash
                }
                : null,
                
            moneyBagData = (serverResponse.payload.moneyBag != null && serverResponse.payload.moneyBag.triggered && serverResponse.payload.moneyBag.result != null)
                ? new MoneyBagResultData
                {
                    triggered = true,
                    pickedIndex = serverResponse.payload.moneyBag.result.pickedIndex,
                    revealed = serverResponse.payload.moneyBag.result.revealed,
                    creditsAwarded = serverResponse.payload.moneyBag.result.creditsAwarded,
                    winInCash = serverResponse.payload.moneyBag.result.winInCash
                }
                : null,

            goldBurstData = serverResponse.payload.goldBurst != null
                ? new GoldBurstData
                {
                    triggered = serverResponse.payload.goldBurst.triggered,
                    inRespin = serverResponse.payload.goldBurst.inRespin,
                    remainingRespins = serverResponse.payload.goldBurst.remainingRespins
                }
                : null,

            twoSlotBarrels = twoSlotBarrels,
            threeSlotBarrels = threeSlotBarrels,
            trains = trains
        };

        return result;
    }

    private static void ConvertBarrelPlacements(
        ServerSpinResponse serverResponse,
        out List<TwoSlotBarrelPlacement> twoSlotBarrels,
        out List<ThreeSlotBarrelPlacement> threeSlotBarrels,
        out List<TrainPlacement> trains)
    {
        var twoSlotPlacementsByReel = new Dictionary<int, TwoSlotBarrelPlacement>();
        var threeSlotPlacementsByReel = new Dictionary<int, ThreeSlotBarrelPlacement>();
        var trainPlacements = new List<TrainPlacement>();

        CollectBarrelPlacements(
            serverResponse?.additionalData,
            twoSlotPlacementsByReel,
            threeSlotPlacementsByReel,
            trainPlacements);
        CollectBarrelPlacements(
            serverResponse?.payload?.additionalData,
            twoSlotPlacementsByReel,
            threeSlotPlacementsByReel,
            trainPlacements);
        CollectBarrelPlacements(
            serverResponse?.payload?.goldBurst?.additionalData,
            twoSlotPlacementsByReel,
            threeSlotPlacementsByReel,
            trainPlacements);

        foreach (TrainPlacement train in trainPlacements)
        {
            for (int reelIndex = train.startCol;
                 reelIndex < train.startCol + train.columnCount;
                 reelIndex++)
            {
                twoSlotPlacementsByReel.Remove(reelIndex);
                threeSlotPlacementsByReel.Remove(reelIndex);
            }
        }

        twoSlotBarrels = twoSlotPlacementsByReel.Values
            .OrderBy(placement => placement.reelIndex)
            .ToList();
        threeSlotBarrels = threeSlotPlacementsByReel.Values
            .OrderBy(placement => placement.reelIndex)
            .ToList();
        trains = trainPlacements
            .OrderBy(placement => placement.startCol)
            .ThenBy(placement => placement.startRow)
            .ToList();
    }

    private static void CollectBarrelPlacements(
        IDictionary<string, JToken> additionalData,
        Dictionary<int, TwoSlotBarrelPlacement> twoSlotPlacementsByReel,
        Dictionary<int, ThreeSlotBarrelPlacement> threeSlotPlacementsByReel,
        List<TrainPlacement> trainPlacements)
    {
        if (additionalData == null) return;

        foreach (KeyValuePair<string, JToken> entry in additionalData)
        {
            CollectBarrelPlacements(
                entry.Key,
                entry.Value,
                twoSlotPlacementsByReel,
                threeSlotPlacementsByReel,
                trainPlacements);
        }
    }

    private static void CollectBarrelPlacements(
        string propertyName,
        JToken token,
        Dictionary<int, TwoSlotBarrelPlacement> twoSlotPlacementsByReel,
        Dictionary<int, ThreeSlotBarrelPlacement> threeSlotPlacementsByReel,
        List<TrainPlacement> trainPlacements)
    {
        if (token == null) return;

        if ((string.Equals(propertyName, "coveredPositions", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(propertyName, "coveredSlots", StringComparison.OrdinalIgnoreCase)) &&
            token is JArray coveredPositions)
        {
            AddFeaturePlacement(
                coveredPositions,
                twoSlotPlacementsByReel,
                threeSlotPlacementsByReel,
                trainPlacements);
        }

        if (token is JObject objectToken)
        {
            foreach (JProperty property in objectToken.Properties())
            {
                CollectBarrelPlacements(
                    property.Name,
                    property.Value,
                    twoSlotPlacementsByReel,
                    threeSlotPlacementsByReel,
                    trainPlacements);
            }
        }
        else if (token is JArray arrayToken)
        {
            foreach (JToken child in arrayToken)
            {
                CollectBarrelPlacements(
                    string.Empty,
                    child,
                    twoSlotPlacementsByReel,
                    threeSlotPlacementsByReel,
                    trainPlacements);
            }
        }
    }

    private static void AddFeaturePlacement(
        JArray coveredPositions,
        Dictionary<int, TwoSlotBarrelPlacement> twoSlotPlacementsByReel,
        Dictionary<int, ThreeSlotBarrelPlacement> threeSlotPlacementsByReel,
        List<TrainPlacement> trainPlacements)
    {
        var positions = coveredPositions
            .OfType<JObject>()
            .Select(position => new
            {
                Row = position.Value<int?>("row"),
                Col = position.Value<int?>("col")
            })
            .Where(position => position.Row.HasValue && position.Col.HasValue)
            .ToList();

        List<int> rows = positions
            .Select(position => position.Row.Value)
            .Distinct()
            .OrderBy(row => row)
            .ToList();
        List<int> columns = positions
            .Select(position => position.Col.Value)
            .Distinct()
            .OrderBy(column => column)
            .ToList();

        if (rows.Count == 0 || columns.Count == 0 ||
            rows[0] < 0 || rows[rows.Count - 1] >= 3 ||
            columns[0] < 0 || columns[columns.Count - 1] >= 5 ||
            positions.Count != rows.Count * columns.Count ||
            !AreConsecutive(rows) || !AreConsecutive(columns))
        {
            return;
        }

        int startRow = rows[0];
        int startCol = columns[0];

        if (rows.Count == 2 && columns.Count == 1)
        {
            twoSlotPlacementsByReel[startCol] = new TwoSlotBarrelPlacement
            {
                reelIndex = startCol,
                startRow = startRow
            };
            return;
        }

        if (rows.Count == 3 && columns.Count == 1)
        {
            threeSlotPlacementsByReel[startCol] = new ThreeSlotBarrelPlacement
            {
                reelIndex = startCol
            };
            return;
        }

        TrainVisualType? trainType = GetTrainVisualType(rows.Count, columns.Count);
        if (!trainType.HasValue) return;

        trainPlacements.Add(new TrainPlacement
        {
            type = trainType.Value,
            startRow = startRow,
            startCol = startCol,
            rowCount = rows.Count,
            columnCount = columns.Count
        });
    }

    private static TrainVisualType? GetTrainVisualType(int rowCount, int columnCount)
    {
        if (rowCount == 2 && columnCount == 2) return TrainVisualType.Green;
        if (rowCount == 2 && columnCount == 3) return TrainVisualType.HorizontalPurple;
        if (rowCount == 2 && columnCount == 4) return TrainVisualType.Red;
        if (rowCount == 3 && columnCount == 2) return TrainVisualType.VerticalPurple;
        if (rowCount == 3 && columnCount == 4) return TrainVisualType.Golden;

        return null;
    }

    private static bool AreConsecutive(List<int> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index] != values[index - 1] + 1) return false;
        }

        return true;
    }

    private static List<List<int>> ConvertReelsToMatrix(List<List<string>> serverReels, List<List<string>> serverMatrix, List<ServerWaysWin> waysWins, GameConfig gameConfig)
    {
        var sourceReels = serverMatrix ?? serverReels;
        int rowCount = gameConfig != null ? gameConfig.rowCount : 3;

        if (sourceReels == null || sourceReels.Count == 0)
        {
            UnityEngine.Debug.LogError("Invalid server reels/matrix: sourceReels is null or empty");
            return GenerateDefaultMatrix(rowCount);
        }

        int totalRows = sourceReels.Count;
        int totalCols = sourceReels[0].Count;

        var matrix = new List<List<int>>();

        for (int col = 0; col < totalCols; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < totalRows; row++)
            {
                if (col >= sourceReels[row].Count)
                {
                    UnityEngine.Debug.LogError($"Invalid server data at row {row}, col {col}");
                    column.Add(0);
                    continue;
                }

                string symbolStr = sourceReels[row][col];
                if (!int.TryParse(symbolStr, out int symbolId))
                {
                    UnityEngine.Debug.LogError($"Failed to parse symbol: {symbolStr}");
                    column.Add(0);
                    continue;
                }

                column.Add(symbolId);
            }
            matrix.Add(column);
        }

        return matrix;
    }

    private static List<List<int>> GenerateDefaultMatrix(int rowCount)
    {
        var matrix = new List<List<int>>();
        for (int col = 0; col < 5; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(0);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    private static List<WinLine> ConvertWinningLines(List<ServerWaysWin> serverWaysWins, GameConfig gameConfig)
    {
        var winLines = new List<WinLine>();
        if (serverWaysWins == null) return winLines;

        int index = 0;
        foreach (var waysWin in serverWaysWins)
        {
            var flatPositions = new List<int>();
            if (waysWin.matchedPositions != null)
            {
                foreach (var pos in waysWin.matchedPositions)
                {
                    int flatIndex = pos.row * 5 + pos.col;
                    flatPositions.Add(flatIndex);
                }
            }

            winLines.Add(new WinLine
            {
                lineId = index++,
                symbolId = waysWin.symbolId,
                positions = flatPositions,
                winAmount = waysWin.winInCash
            });
        }

        return winLines;
    }

    private static double CalculateNewBalance(double currentBalance, double totalPay, double winAmount)
    {
        return currentBalance + winAmount;
    }
}

#endregion
