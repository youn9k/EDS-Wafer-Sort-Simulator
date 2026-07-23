using System.Collections.Concurrent;

namespace RecipeTestProject;

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected
}

public enum TestStatus
{
    Idle,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public sealed class EquipmentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; }
    public Color AccentColor { get; init; }
    public string? ImagePath { get; init; }
}

public sealed class EquipmentState
{
    public EquipmentState(EquipmentDefinition definition)
    {
        Definition = definition;
    }

    public EquipmentDefinition Definition { get; }
    public ConnectionStatus ConnectionStatus { get; set; }
    public DateTimeOffset? LastConnectedAt { get; set; }
    public TestStatus TestStatus { get; set; } = TestStatus.Idle;
    public int ProgressPercent { get; set; }
    public string CurrentStepName { get; set; } = string.Empty;
    public TestRun? ActiveRun { get; set; }
    public TestResult? LastResult { get; set; }

    public bool IsRunning => TestStatus == TestStatus.Running && ActiveRun is not null;
}

public sealed class RecipeDocument
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string TargetEquipmentModel { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public WaferSettings Wafer { get; set; } = new();
    public InspectionSettings Inspection { get; set; } = new();
    public List<RecipeStep> Steps { get; set; } = [];
}

public sealed class WaferSettings
{
    public int DiameterMm { get; set; }
    public string Material { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
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

public sealed record TestSimulationSettings(bool ShouldFail, string? FailedStepId);

public sealed class TestRun
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();

    public required string RunId { get; init; }
    public required EquipmentState Equipment { get; init; }
    public required RecipeDocument Recipe { get; init; }
    public required string RecipeSourcePath { get; init; }
    public required TestSimulationSettings Simulation { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public CancellationTokenSource Cancellation { get; } = new();

    public void AddLog(string level, string message) =>
        _logs.Enqueue(new LogEntry(DateTimeOffset.Now, level, message));

    public IReadOnlyList<LogEntry> GetLogs() => _logs.ToArray();
}

public sealed class TestResult
{
    public required TestStatus Status { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset FinishedAt { get; init; }
    public required int FinalProgressPercent { get; init; }
    public string? FailedStepId { get; init; }
    public string? FailedStepName { get; init; }
    public string? FailureReason { get; init; }
    public required string LogText { get; init; }
    public string? LogFilePath { get; set; }
    public TimeSpan Duration => FinishedAt - StartedAt;
}

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message)
{
    public override string ToString() => $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level,-5}] {Message}";
}

public sealed record TestProgress(
    string EquipmentId,
    string CurrentStepId,
    string CurrentStepName,
    int Percent,
    IReadOnlySet<string> CompletedStepIds,
    string? FailedStepId,
    string LogMessage);

public sealed record PersistedEquipmentState(string EquipmentId, ConnectionStatus ConnectionStatus, DateTimeOffset? LastConnectedAt);

public sealed record RecipeLoadResult(RecipeDocument Recipe, string RawJson, string SourcePath);

public sealed record EquipmentCatalogLoadResult(
    IReadOnlyList<EquipmentDefinition> Equipment,
    IReadOnlyList<string> Errors);
