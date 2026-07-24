using System.Text.Json.Serialization;

namespace RecipeTestProject;

public enum ConnectionStatus { Disconnected, Connecting, Connected }
public enum JobStatus { Pending, Running, Completed, Failed, Canceled, Interrupted }
public enum WaferExecutionStatus { Pending, Running, Completed, EquipmentError, NotRun }
public enum WaferDisposition { Passed, LowYield }
public enum TestCellComponent { Tester, Prober, ProbeCard }

public sealed class TestCellComponentDefinition
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ImageFile { get; set; } = string.Empty;
    [JsonIgnore] public string? ImagePath { get; set; }
}

public sealed class ProbeCardDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool FixedMounted { get; set; } = true;
}

public sealed class TestCellDefinition
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public TestCellComponentDefinition Tester { get; set; } = new();
    public TestCellComponentDefinition Prober { get; set; } = new();
    public ProbeCardDefinition ProbeCard { get; set; } = new();
    public List<int> SupportedWaferDiametersMm { get; set; } = [];
    public List<string> Capabilities { get; set; } = [];
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string AccentColorHex { get; set; } = "#2068B3";
    public string? ImageSourceUrl { get; set; }
    [JsonIgnore] public Color AccentColor { get; set; }
}

public sealed class TestCellState
{
    public TestCellState(TestCellDefinition definition) => Definition = definition;
    public TestCellDefinition Definition { get; }
    public ConnectionStatus ConnectionStatus { get; set; }
    public DateTimeOffset? LastConnectedAt { get; set; }
    public string? ActiveJobId { get; set; }
    public int ProgressPercent { get; set; }
    public string CurrentWaferId { get; set; } = string.Empty;
    public TestCellComponent? ErrorComponent { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsBusy => !string.IsNullOrWhiteSpace(ActiveJobId);
    public bool HasError => ErrorComponent is not null;
    public bool IsReady => ConnectionStatus == ConnectionStatus.Connected && !IsBusy && !HasError;

    public void SetError(TestCellComponent component, string message)
    {
        ErrorComponent = component;
        ErrorMessage = message;
    }

    public void ResetError()
    {
        ErrorComponent = null;
        ErrorMessage = null;
    }
}

public sealed class ProductDocument
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WaferDiameterMm { get; set; }
    public string Material { get; set; } = string.Empty;
    public double DieWidthMm { get; set; }
    public double DieHeightMm { get; set; }
    public double EdgeExclusionMm { get; set; }
    public double AcceptanceYieldPercent { get; set; }
    public List<string> AllowedRecipeIds { get; set; } = [];
}

public sealed class RecipeDocument
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ProductFamily { get; set; } = string.Empty;
    public List<string> CompatibleTestCellIds { get; set; } = [];
    public List<string> RequiredCapabilities { get; set; } = [];
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<FinalBinDefinition> FinalBins { get; set; } = [];
    public List<RecipeStep> Steps { get; set; } = [];

    [JsonIgnore] public FinalBinDefinition PassBin =>
        FinalBins.Single(x => x.IsPass);
    [JsonIgnore] public IReadOnlyList<FinalBinDefinition> FailBins =>
        FinalBins.Where(x => !x.IsPass).ToList();
}

public sealed class FinalBinDefinition
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#D9534F";
    public bool IsPass { get; set; }
    public string? RelatedStepId { get; set; }
}

public sealed class RecipeStep
{
    public int Sequence { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public List<TestCellComponent> AllowedErrorComponents { get; set; } = [];
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
    public bool UseLotDefault { get; set; } = true;
    public double? TargetYieldPercent { get; set; }
    public string? DominantFailBinCode { get; set; }
}

public sealed class CellErrorSimulation
{
    public bool Enabled { get; set; }
    public TestCellComponent Component { get; set; } = TestCellComponent.Tester;
    public string WaferId { get; set; } = "Wafer01";
    public string FailedStepId { get; set; } = string.Empty;
}

public sealed class JobSimulationSettings
{
    public int SpeedFactor { get; set; } = 20;
    public double DefaultTargetYieldPercent { get; set; } = 98;
    public Dictionary<string, double> DefaultFailBinDistribution { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<WaferSimulationSetting> Wafers { get; set; } = [];
    public CellErrorSimulation CellError { get; set; } = new();

    public static JobSimulationSettings CreateDefault(RecipeDocument recipe) => new()
    {
        SpeedFactor = 20,
        DefaultTargetYieldPercent = 98,
        DefaultFailBinDistribution = recipe.FailBins.ToDictionary(
            x => x.Code,
            _ => 100d / Math.Max(1, recipe.FailBins.Count),
            StringComparer.OrdinalIgnoreCase),
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
    public string TestCellId { get; set; } = string.Empty;
    public TestCellDefinition TestCellSnapshot { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public bool HasLowYieldWafers { get; set; }
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
    public bool HasLowYieldWafers { get; set; }
    public double? LotYieldPercent { get; set; }
    public string ResultFilePath { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public string? ReportFilePath { get; set; }
    public string? FailureReason { get; set; }
    public TestCellComponent? ErrorComponent { get; set; }
}

public sealed class JobRunResult
{
    public string JobId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public ProductDocument ProductSnapshot { get; set; } = new();
    public RecipeDocument RecipeSnapshot { get; set; } = new();
    public TestCellDefinition TestCellSnapshot { get; set; } = new();
    public JobStatus Status { get; set; } = JobStatus.Running;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int SpeedFactor { get; set; }
    public int ProgressPercent { get; set; }
    public string CurrentWaferId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public TestCellComponent? ErrorComponent { get; set; }
    public List<WaferResult> Wafers { get; set; } = [];
    public List<RunLogEntry> Logs { get; set; } = [];

    [JsonIgnore] public int PassedWaferCount => Wafers.Count(x =>
        x.Status == WaferExecutionStatus.Completed && x.Disposition == WaferDisposition.Passed);
    [JsonIgnore] public int LowYieldWaferCount => Wafers.Count(x =>
        x.Status == WaferExecutionStatus.Completed && x.Disposition == WaferDisposition.LowYield);
    [JsonIgnore] public int CompletedCount => PassedWaferCount + LowYieldWaferCount;
    [JsonIgnore] public int PassDieCount => Wafers.Sum(x => x.PassDieCount);
    [JsonIgnore] public int FailDieCount => Wafers.Sum(x => Math.Max(0, x.ValidDieCount - x.PassDieCount));
    [JsonIgnore] public bool HasLowYieldWafers => LowYieldWaferCount > 0;
    [JsonIgnore] public double? LotYieldPercent
    {
        get
        {
            var completed = Wafers.Where(x => x.Status == WaferExecutionStatus.Completed).ToList();
            var tested = completed.Sum(x => x.ValidDieCount);
            return tested == 0 ? null : completed.Sum(x => x.PassDieCount) * 100d / tested;
        }
    }

    public Dictionary<string, int> GetBinCounts() => Wafers
        .Where(x => x.Status == WaferExecutionStatus.Completed)
        .SelectMany(x => x.Dies.Where(d => d.IsValid))
        .GroupBy(x => x.FinalBinCode, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
}

public sealed class WaferResult
{
    public string WaferId { get; set; } = string.Empty;
    public WaferExecutionStatus Status { get; set; } = WaferExecutionStatus.Pending;
    public WaferDisposition? Disposition { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int GridRows { get; set; }
    public int GridColumns { get; set; }
    public int ValidDieCount { get; set; }
    public int PassDieCount { get; set; }
    public string? FailedStepId { get; set; }
    public string? FailureReason { get; set; }
    public List<DieResult> Dies { get; set; } = [];
    [JsonIgnore] public double? YieldPercent =>
        ValidDieCount == 0 ? null : PassDieCount * 100d / ValidDieCount;
    [JsonIgnore] public Dictionary<string, int> BinCounts => Dies
        .Where(x => x.IsValid)
        .GroupBy(x => x.FinalBinCode, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
}

public sealed class DieResult
{
    public int Row { get; set; }
    public int Column { get; set; }
    public double CenterXmm { get; set; }
    public double CenterYmm { get; set; }
    public bool IsValid { get; set; }
    public string FinalBinCode { get; set; } = string.Empty;
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

public sealed record PersistedTestCellState(
    string TestCellId,
    ConnectionStatus ConnectionStatus,
    DateTimeOffset? LastConnectedAt,
    TestCellComponent? ErrorComponent,
    string? ErrorMessage);

public sealed record TestCellCatalogLoadResult(
    IReadOnlyList<TestCellDefinition> TestCells,
    IReadOnlyList<string> Errors);
public sealed record ProductCatalogLoadResult(
    IReadOnlyList<ProductDocument> Products,
    IReadOnlyList<string> Errors);
public sealed record RecipeCatalogLoadResult(
    IReadOnlyList<RecipeDocument> Recipes,
    IReadOnlyList<string> Errors);
