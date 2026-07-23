using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace RecipeTestProject;

public enum ConnectionStatus { Disconnected, Connecting, Connected }
public enum JobStatus { Pending, Running, Completed, Failed, Canceled, Interrupted }
public enum WaferExecutionStatus { Pending, Running, Normal, Ng, EquipmentError, NotRun }
public enum SimulationOutcome { Normal, Ng, EquipmentError }
public enum DefectLevel { Low, Medium, High }

public sealed class EquipmentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; }
    [JsonIgnore] public Color AccentColor { get; init; }
    [JsonIgnore] public string? ImagePath { get; init; }
}

public sealed class EquipmentState
{
    public EquipmentState(EquipmentDefinition definition) => Definition = definition;
    public EquipmentDefinition Definition { get; }
    public ConnectionStatus ConnectionStatus { get; set; }
    public DateTimeOffset? LastConnectedAt { get; set; }
    public string? ActiveJobId { get; set; }
    public int ProgressPercent { get; set; }
    public string CurrentWaferId { get; set; } = string.Empty;
    public bool IsBusy => !string.IsNullOrWhiteSpace(ActiveJobId);
}

public sealed class ProductDocument
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WaferDiameterMm { get; set; }
    public string Material { get; set; } = string.Empty;
    public double AcceptanceYieldPercent { get; set; }
    public List<string> AllowedRecipeIds { get; set; } = [];
}

public sealed class RecipeDocument
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> CompatibleEquipmentModels { get; set; } = [];
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public InspectionSettings Inspection { get; set; } = new();
    public List<RecipeStep> Steps { get; set; } = [];
}

public sealed class InspectionSettings
{
    public string ScanMode { get; set; } = string.Empty;
    public double ResolutionMicrometer { get; set; }
    public double DefectThresholdMicrometer { get; set; }
    public double EdgeExclusionMm { get; set; }
}

public sealed class RecipeStep
{
    public int Sequence { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public List<RecipeParameter> Parameters { get; set; } = [];
}

public sealed class RecipeParameter
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

public sealed class WaferSimulationSetting
{
    public string WaferId { get; set; } = string.Empty;
    public SimulationOutcome Outcome { get; set; } = SimulationOutcome.Normal;
    public DefectLevel? DefectLevel { get; set; }
    public string? FailedStepId { get; set; }
}

public sealed class JobSimulationSettings
{
    public int SpeedFactor { get; set; } = 10;
    public List<WaferSimulationSetting> Wafers { get; set; } = [];

    public static JobSimulationSettings CreateDefault() => new()
    {
        SpeedFactor = 10,
        Wafers = Enumerable.Range(1, 25)
            .Select(number => new WaferSimulationSetting { WaferId = $"Wafer{number:00}" })
            .ToList()
    };
}

public sealed class InspectionJob
{
    public string JobId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public ProductDocument ProductSnapshot { get; set; } = new();
    public RecipeDocument RecipeSnapshot { get; set; } = new();
    public string EquipmentId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public bool HasNgWafers { get; set; }
    public int ProgressPercent { get; set; }
    public string CurrentWaferId { get; set; } = string.Empty;
    public JobSimulationSettings? Simulation { get; set; }
    public List<JobRunSummary> Runs { get; set; } = [];
}

public sealed class JobRunSummary
{
    public string RunId { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int ProgressPercent { get; set; }
    public bool HasNgWafers { get; set; }
    public double? LotYieldPercent { get; set; }
    public string ResultFilePath { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public string? ReportFilePath { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class JobRunResult
{
    public string JobId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string RecipeVersion { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Running;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int SpeedFactor { get; set; }
    public int ProgressPercent { get; set; }
    public string CurrentWaferId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public List<WaferResult> Wafers { get; set; } = [];
    public List<RunLogEntry> Logs { get; set; } = [];

    [JsonIgnore] public int NormalCount => Wafers.Count(x => x.Status == WaferExecutionStatus.Normal);
    [JsonIgnore] public int NgCount => Wafers.Count(x => x.Status == WaferExecutionStatus.Ng);
    [JsonIgnore] public int CompletedCount => NormalCount + NgCount;
    [JsonIgnore] public int TotalDefectCount => Wafers.Sum(x => x.Defects.Count);
    [JsonIgnore] public bool HasNgWafers => NgCount > 0;
    [JsonIgnore] public double? LotYieldPercent
    {
        get
        {
            var completed = Wafers.Where(x => x.Status is WaferExecutionStatus.Normal or WaferExecutionStatus.Ng).ToList();
            var tested = completed.Sum(x => x.ValidDieCount);
            return tested == 0 ? null : completed.Sum(x => x.PassDieCount) * 100d / tested;
        }
    }
}

public sealed class WaferResult
{
    public string WaferId { get; set; } = string.Empty;
    public WaferExecutionStatus Status { get; set; } = WaferExecutionStatus.Pending;
    public DefectLevel? DefectLevel { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int ValidDieCount { get; set; }
    public int PassDieCount { get; set; }
    public string? FailedStepId { get; set; }
    public string? FailureReason { get; set; }
    public List<DieResult> Dies { get; set; } = [];
    public List<DefectRecord> Defects { get; set; } = [];
    [JsonIgnore] public double? YieldPercent => ValidDieCount == 0 ? null : PassDieCount * 100d / ValidDieCount;
}

public sealed class DieResult
{
    public int Row { get; set; }
    public int Column { get; set; }
    public bool IsValid { get; set; }
    public bool IsPass { get; set; }
}

public sealed class DefectRecord
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string Type { get; set; } = string.Empty;
}

public sealed record RunLogEntry(DateTimeOffset Timestamp, string Level, string? WaferId, string Message)
{
    public override string ToString() =>
        $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level,-5}] [{WaferId ?? "LOT",-8}] {Message}";
}

public sealed record LotRunProgress(
    string RunId,
    string WaferId,
    int OverallPercent,
    string CurrentStepId,
    string CurrentStepName,
    int CurrentStepIndex,
    IReadOnlySet<string> CompletedStepIds,
    WaferExecutionStatus WaferStatus,
    RunLogEntry? NewLog);

public sealed record PersistedEquipmentState(string EquipmentId, ConnectionStatus ConnectionStatus, DateTimeOffset? LastConnectedAt);
public sealed record EquipmentCatalogLoadResult(IReadOnlyList<EquipmentDefinition> Equipment, IReadOnlyList<string> Errors);
public sealed record ProductCatalogLoadResult(IReadOnlyList<ProductDocument> Products, IReadOnlyList<string> Errors);
public sealed record RecipeCatalogLoadResult(IReadOnlyList<RecipeDocument> Recipes, IReadOnlyList<string> Errors);
