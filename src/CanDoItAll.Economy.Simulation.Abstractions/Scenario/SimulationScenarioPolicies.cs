namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationScenarioDistanceCalculator
{
    private readonly SimulationScenarioDefinitionNormalizer normalizer = new();

    public decimal ActorHomeToLocation(SimulationScenarioDefinition definition, string actorId, string locationId)
    {
        SimulationScenarioDefinition canonical = normalizer.Normalize(definition).Definition;
        SimulationScenarioActorDefinition actor = canonical.Actors.First(item => string.Equals(item.ActorId, actorId, StringComparison.Ordinal));
        return LocationDistance(canonical.Locations, actor.HomeLocationId, locationId);
    }

    public decimal LocationToLocation(SimulationScenarioDefinition definition, string sourceLocationId, string targetLocationId)
    {
        SimulationScenarioDefinition canonical = normalizer.Normalize(definition).Definition;
        return LocationDistance(canonical.Locations, sourceLocationId, targetLocationId);
    }

    private static decimal LocationDistance(IReadOnlyList<SimulationScenarioLocationDefinition> locations, string sourceLocationId, string targetLocationId)
    {
        SimulationScenarioLocationDefinition source = locations.First(item => string.Equals(item.LocationId, sourceLocationId, StringComparison.Ordinal));
        SimulationScenarioLocationDefinition target = locations.First(item => string.Equals(item.LocationId, targetLocationId, StringComparison.Ordinal));
        double dx = (double)(source.X - target.X);
        double dy = (double)(source.Y - target.Y);
        double dz = (double)(source.Z - target.Z);
        return (decimal)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}

public sealed class SimulationTravelCostDurationRequest
{
    public SimulationScenarioDefinition Definition { get; set; } = new();
    public string ActorId { get; set; } = string.Empty;
    public string TargetLocationId { get; set; } = string.Empty;
    public decimal UnitsPerSecond { get; set; } = 1;
    public decimal CostPerUnit { get; set; } = 1;
}

public sealed class SimulationTravelCostDurationResult
{
    public decimal Distance { get; set; }
    public TimeSpan Duration { get; set; }
    public decimal Cost { get; set; }
}

public sealed class SimulationTravelCostDurationPolicy
{
    private readonly SimulationScenarioDistanceCalculator distanceCalculator = new();

    public SimulationTravelCostDurationResult Evaluate(SimulationTravelCostDurationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        decimal distance = distanceCalculator.ActorHomeToLocation(request.Definition, request.ActorId, request.TargetLocationId);
        decimal unitsPerSecond = request.UnitsPerSecond <= 0 ? 1 : request.UnitsPerSecond;
        return new()
        {
            Distance = distance,
            Duration = TimeSpan.FromSeconds((double)(distance / unitsPerSecond)),
            Cost = distance * request.CostPerUnit
        };
    }
}

public sealed class SimulationInventoryCapacityRequest
{
    public SimulationScenarioDefinition Definition { get; set; } = new();
    public string ActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
}

public sealed class SimulationInventoryCapacityResult
{
    public decimal Capacity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public bool IsWithinCapacity => RejectedQuantity == 0;
}

public sealed class SimulationInventoryCapacityPolicy
{
    private readonly SimulationScenarioDefinitionNormalizer normalizer = new();

    public SimulationInventoryCapacityResult Evaluate(SimulationInventoryCapacityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SimulationScenarioDefinition definition = normalizer.Normalize(request.Definition).Definition;
        decimal capacity = ResolveCarryCapacity(definition, request.ActorId, request.ResourceId);
        decimal accepted = capacity <= 0 ? request.RequestedQuantity : Math.Min(capacity, request.RequestedQuantity);
        return new()
        {
            Capacity = capacity,
            AcceptedQuantity = accepted,
            RejectedQuantity = Math.Max(0, request.RequestedQuantity - accepted)
        };
    }

    internal static decimal ResolveCarryCapacity(SimulationScenarioDefinition definition, string actorId, string resourceId)
    {
        SimulationScenarioActorDefinition? actor = definition.Actors.FirstOrDefault(item => string.Equals(item.ActorId, actorId, StringComparison.Ordinal));
        if (actor is not null &&
            (TryReadDecimal(actor.Metadata, $"carryCapacity.{resourceId}", out decimal resourceCapacity) ||
             TryReadDecimal(actor.Metadata, "carryCapacity", out resourceCapacity)))
        {
            return resourceCapacity;
        }

        return definition.InitialStores
            .Where(store => string.Equals(store.OwnerActorId, actorId, StringComparison.Ordinal) &&
                            string.Equals(store.ResourceId, resourceId, StringComparison.Ordinal) &&
                            store.Capacity is not null)
            .Select(store => store.Capacity!.Value)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool TryReadDecimal(IReadOnlyDictionary<string, string> metadata, string key, out decimal value)
    {
        value = 0;
        return metadata.TryGetValue(key, out string? text) &&
               decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}

public sealed class SimulationSurplusShortageResult
{
    public string ActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal TargetQuantity { get; set; }
    public decimal Surplus { get; set; }
    public decimal Shortage { get; set; }
}

public sealed class SimulationSurplusShortageDetector
{
    public SimulationSurplusShortageResult Evaluate(string actorId, string resourceId, decimal quantity, decimal targetQuantity)
        => new()
        {
            ActorId = actorId,
            ResourceId = resourceId,
            Quantity = quantity,
            TargetQuantity = targetQuantity,
            Surplus = Math.Max(0, quantity - targetQuantity),
            Shortage = Math.Max(0, targetQuantity - quantity)
        };
}

public sealed class SimulationTradeOffer
{
    public string OfferId { get; set; } = string.Empty;
    public string SellerActorId { get; set; } = string.Empty;
    public string BuyerActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string PriceResourceId { get; set; } = string.Empty;
    public decimal PriceQuantity { get; set; }
}

public sealed class SimulationTradeRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string BuyerActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public sealed class SimulationFeeTaxAdminBurdenResult
{
    public SimulationEvent TaxEvent { get; set; } = new();
    public decimal FeeQuantity { get; set; }
}

public sealed class SimulationFeeTaxAdminBurdenPolicy
{
    public SimulationFeeTaxAdminBurdenResult DeriveFromTrade(SimulationEvent tradeEvent, decimal feeRate, string feeResourceId)
    {
        ArgumentNullException.ThrowIfNull(tradeEvent);
        decimal fee = Math.Max(0, tradeEvent.Magnitude * feeRate);
        return new()
        {
            FeeQuantity = fee,
            TaxEvent = new()
            {
                EventId = $"{tradeEvent.EventId}.tax-fee",
                EventKind = SimulationEventKind.ActorTaxFeePay,
                ActorId = tradeEvent.TargetActorId,
                TargetActorId = tradeEvent.Metadata.GetValueOrDefault("feeCollectorActorId", tradeEvent.SourceId),
                ActorIds = [tradeEvent.TargetActorId, tradeEvent.Metadata.GetValueOrDefault("feeCollectorActorId", tradeEvent.SourceId)],
                ResourceId = feeResourceId,
                ResourceIds = [feeResourceId],
                SourceId = tradeEvent.TargetId,
                TargetId = tradeEvent.Metadata.GetValueOrDefault("feeCollectorActorId", tradeEvent.SourceId),
                PlaceId = tradeEvent.PlaceId,
                OccurredAtUtc = tradeEvent.OccurredAtUtc,
                Duration = tradeEvent.Duration,
                Magnitude = fee,
                Timing = new()
                {
                    StepIndex = tradeEvent.Timing.StepIndex,
                    Offset = tradeEvent.Timing.Offset.Add(tradeEvent.Timing.Duration),
                    Duration = tradeEvent.Timing.Duration,
                    Order = tradeEvent.Timing.Order + 1
                },
                Metadata =
                {
                    ["sourceTradeEventId"] = tradeEvent.EventId,
                    ["feeRate"] = feeRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                }
            }
        };
    }
}

public sealed class SimulationScenarioTradePolicyRequest
{
    public SimulationScenarioDefinition Definition { get; set; } = new();
    public string SellerActorId { get; set; } = string.Empty;
    public string BuyerActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceLocationId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int StepIndex { get; set; }
    public TimeSpan Offset { get; set; }
}

public sealed class SimulationScenarioTradePolicyResult
{
    public bool EmitsTrade { get; set; }
    public decimal SellerCollectionCost { get; set; }
    public decimal BuyerCollectionCost { get; set; }
    public decimal SellerCarryCapacity { get; set; }
    public decimal BuyerCarryCapacity { get; set; }
    public List<SimulationScheduledEventDefinition> ScheduledEvents { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioTradePolicy
{
    private readonly SimulationScenarioDefinitionNormalizer normalizer = new();
    private readonly SimulationScenarioDistanceCalculator distanceCalculator = new();

    public SimulationScenarioTradePolicyResult EvaluateSurplusTrade(SimulationScenarioTradePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SimulationScenarioDefinition definition = normalizer.Normalize(request.Definition).Definition;
        decimal sellerCost = distanceCalculator.ActorHomeToLocation(definition, request.SellerActorId, request.ResourceLocationId);
        decimal buyerCost = distanceCalculator.ActorHomeToLocation(definition, request.BuyerActorId, request.ResourceLocationId);
        decimal sellerCapacity = SimulationInventoryCapacityPolicy.ResolveCarryCapacity(definition, request.SellerActorId, request.ResourceId);
        decimal buyerCapacity = SimulationInventoryCapacityPolicy.ResolveCarryCapacity(definition, request.BuyerActorId, request.ResourceId);
        bool emitsTrade = sellerCost < buyerCost && sellerCapacity >= request.Quantity && request.Quantity > 0;

        var result = new SimulationScenarioTradePolicyResult
        {
            EmitsTrade = emitsTrade,
            SellerCollectionCost = sellerCost,
            BuyerCollectionCost = buyerCost,
            SellerCarryCapacity = sellerCapacity,
            BuyerCarryCapacity = buyerCapacity,
            Metadata =
            {
                ["policy"] = "distance-surplus-trade",
                ["sellerActorId"] = request.SellerActorId,
                ["buyerActorId"] = request.BuyerActorId,
                ["resourceId"] = request.ResourceId
            }
        };

        if (!emitsTrade)
        {
            return result;
        }

        result.ScheduledEvents.Add(new()
        {
            ScheduledEventId = $"event.trade.{request.SellerActorId}.{request.BuyerActorId}.{request.ResourceId}.{request.StepIndex}",
            EventKind = SimulationEventKind.ActorTradeSell,
            StepIndex = request.StepIndex,
            Offset = request.Offset,
            Order = 0,
            ActorIds = [request.SellerActorId, request.BuyerActorId],
            ResourceIds = [request.ResourceId],
            SourceId = request.SellerActorId,
            TargetId = request.BuyerActorId,
            SourceLocationId = request.ResourceLocationId,
            TargetLocationId = ResolveHomeLocation(definition, request.BuyerActorId),
            Magnitude = request.Quantity,
            Duration = TimeSpan.FromMinutes(20),
            Metadata =
            {
                ["policy"] = "distance-surplus-trade",
                ["sellerCollectionCost"] = sellerCost.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["buyerCollectionCost"] = buyerCost.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["sellerCarryCapacity"] = sellerCapacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["buyerCarryCapacity"] = buyerCapacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            }
        });

        return result;
    }

    private static string ResolveHomeLocation(SimulationScenarioDefinition definition, string actorId)
        => definition.Actors.First(item => string.Equals(item.ActorId, actorId, StringComparison.Ordinal)).HomeLocationId;

    private static bool TryReadDecimal(IReadOnlyDictionary<string, string> metadata, string key, out decimal value)
    {
        value = 0;
        return metadata.TryGetValue(key, out string? text) &&
               decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
