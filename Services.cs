using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeTestProject;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Read = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class EquipmentCatalog
{
    public static EquipmentCatalogLoadResult Load(string directory)
    {
        var items = new List<EquipmentDefinition>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
            return new([], [$"장비 폴더를 찾을 수 없습니다: {directory}"]);

        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<EquipmentCatalogDocument>(File.ReadAllText(path, Encoding.UTF8), JsonDefaults.Read)
                          ?? throw new InvalidDataException("JSON 내용이 비어 있습니다.");
                var validation = Validate(raw);
                if (validation.Count > 0) throw new InvalidDataException(string.Join(", ", validation));
                if (!ids.Add(raw.Id)) throw new InvalidDataException($"중복 장비 ID입니다: {raw.Id}");
                items.Add(new EquipmentDefinition
                {
                    Id = raw.Id.Trim(),
                    Name = raw.Name.Trim(),
                    Manufacturer = raw.Manufacturer.Trim(),
                    Model = raw.Model.Trim(),
                    IpAddress = raw.IpAddress.Trim(),
                    Port = raw.Port,
                    AccentColor = ParseColor(raw.AccentColor),
                    ImagePath = ResolveImagePath(path, raw.ImagePath, errors)
                });
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        if (items.Count == 0) errors.Add("정상적으로 로드된 장비가 없습니다.");
        return new(items, errors);
    }

    private static List<string> Validate(EquipmentCatalogDocument value)
    {
        var errors = new List<string>();
        Required(value.Id, "id", errors); Required(value.Name, "name", errors);
        Required(value.Manufacturer, "manufacturer", errors); Required(value.Model, "model", errors);
        Required(value.IpAddress, "ipAddress", errors); Required(value.AccentColor, "accentColor", errors);
        if (!string.IsNullOrWhiteSpace(value.IpAddress) && !IPAddress.TryParse(value.IpAddress, out _)) errors.Add("ipAddress 형식 오류");
        if (value.Port is < 1 or > 65535) errors.Add("port는 1~65535 범위여야 합니다");
        if (!TryParseColor(value.AccentColor, out _)) errors.Add("accentColor는 #RRGGBB 형식이어야 합니다");
        return errors;
    }

    private static string? ResolveImagePath(string jsonPath, string? configuredPath, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return null;
        if (Path.IsPathRooted(configuredPath)) { errors.Add($"{Path.GetFileName(jsonPath)}: imagePath는 상대 경로여야 합니다."); return null; }
        var directory = Path.GetFullPath(Path.GetDirectoryName(jsonPath)!);
        var candidate = Path.GetFullPath(Path.Combine(directory, configuredPath));
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            errors.Add($"{Path.GetFileName(jsonPath)}: 이미지 파일을 찾을 수 없습니다 - {configuredPath}");
            return null;
        }
        return candidate;
    }

    private static Color ParseColor(string value) { TryParseColor(value, out var color); return color; }
    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#') return false;
        if (!int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;
        color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return true;
    }

    private static void Required(string? value, string field, ICollection<string> errors)
    { if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} 값이 필요합니다"); }

    private sealed class EquipmentCatalogDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Model { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int Port { get; set; }
        public string AccentColor { get; set; } = "";
        public string? ImagePath { get; set; }
    }
}

public static class ProductCatalog
{
    public static ProductCatalogLoadResult Load(string directory)
    {
        var items = new List<ProductDocument>(); var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory)) return new([], [$"검사 대상 폴더를 찾을 수 없습니다: {directory}"]);
        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(x => x))
        {
            try
            {
                var item = JsonSerializer.Deserialize<ProductDocument>(File.ReadAllText(path, Encoding.UTF8), JsonDefaults.Read)
                           ?? throw new InvalidDataException("JSON 내용이 비어 있습니다.");
                Validate(item);
                if (!ids.Add(item.ProductId)) throw new InvalidDataException($"중복 productId입니다: {item.ProductId}");
                items.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }
        return new(items, errors);
    }

    public static void Validate(ProductDocument value)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != "2.0") errors.Add("schemaVersion은 2.0이어야 합니다");
        if (string.IsNullOrWhiteSpace(value.ProductId)) errors.Add("productId가 필요합니다");
        if (string.IsNullOrWhiteSpace(value.Name)) errors.Add("name이 필요합니다");
        if (value.WaferDiameterMm <= 0) errors.Add("waferDiameterMm은 양수여야 합니다");
        if (string.IsNullOrWhiteSpace(value.Material)) errors.Add("material이 필요합니다");
        if (value.AcceptanceYieldPercent is <= 0 or > 100) errors.Add("acceptanceYieldPercent는 0 초과 100 이하여야 합니다");
        if (value.AllowedRecipeIds.Count == 0 || value.AllowedRecipeIds.Any(string.IsNullOrWhiteSpace)) errors.Add("allowedRecipeIds가 필요합니다");
        if (errors.Count > 0) throw new InvalidDataException(string.Join(", ", errors));
    }
}

public static class RecipeCatalog
{
    public static RecipeCatalogLoadResult Load(string directory)
    {
        var items = new List<RecipeDocument>(); var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory)) return new([], [$"레시피 폴더를 찾을 수 없습니다: {directory}"]);
        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(x => x))
        {
            try
            {
                var item = JsonSerializer.Deserialize<RecipeDocument>(File.ReadAllText(path, Encoding.UTF8), JsonDefaults.Read)
                           ?? throw new InvalidDataException("JSON 내용이 비어 있습니다.");
                Validate(item);
                if (!ids.Add(item.RecipeId)) throw new InvalidDataException($"중복 recipeId입니다: {item.RecipeId}");
                items.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }
        return new(items, errors);
    }

    public static void Validate(RecipeDocument value)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != "2.0") errors.Add("schemaVersion은 2.0이어야 합니다");
        Required(value.RecipeId, "recipeId", errors); Required(value.Name, "name", errors);
        Required(value.Version, "version", errors); Required(value.Author, "author", errors);
        if (value.CreatedAt == default) errors.Add("createdAt이 필요합니다");
        if (value.CompatibleEquipmentModels.Count == 0) errors.Add("compatibleEquipmentModels가 필요합니다");
        if (value.Steps.Count == 0) errors.Add("steps가 필요합니다");
        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < value.Steps.Count; index++)
        {
            var step = value.Steps[index];
            if (step.Sequence != index + 1) errors.Add($"steps[{index}].sequence는 {index + 1}이어야 합니다");
            Required(step.Id, $"steps[{index}].id", errors); Required(step.Name, $"steps[{index}].name", errors);
            Required(step.Command, $"steps[{index}].command", errors);
            if (step.DurationSeconds <= 0) errors.Add($"steps[{index}].durationSeconds는 양수여야 합니다");
            if (!string.IsNullOrWhiteSpace(step.Id) && !stepIds.Add(step.Id)) errors.Add($"중복 step id입니다: {step.Id}");
        }
        if (errors.Count > 0) throw new InvalidDataException(string.Join(", ", errors));
    }

    private static void Required(string? value, string field, ICollection<string> errors)
    { if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} 값이 필요합니다"); }
}

public sealed class MockConnectionService
{
    public async Task ConnectAsync(EquipmentState equipment, CancellationToken cancellationToken = default)
    {
        equipment.ConnectionStatus = ConnectionStatus.Connecting;
        await Task.Delay(700, cancellationToken);
        equipment.ConnectionStatus = ConnectionStatus.Connected;
        equipment.LastConnectedAt = DateTimeOffset.Now;
    }
}

public sealed class EquipmentStateStore
{
    private readonly string _path;
    public EquipmentStateStore(string? root = null) =>
        _path = Path.Combine(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeTestProject"), "equipment-state.json");

    public IReadOnlyList<PersistedEquipmentState> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<PersistedEquipmentState>>(File.ReadAllText(_path), JsonDefaults.Read) ?? [] : []; }
        catch { return []; }
    }

    public Task SaveAsync(IEnumerable<EquipmentState> equipment) =>
        AtomicJson.WriteAsync(_path, equipment.Select(x => new PersistedEquipmentState(x.Definition.Id, x.ConnectionStatus, x.LastConnectedAt)).ToList());
}

public sealed class JobStore
{
    public string RootDirectory { get; }
    public string JobsFilePath => Path.Combine(RootDirectory, "jobs.json");
    public JobStore(string? root = null) =>
        RootDirectory = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeTestProject");

    public List<InspectionJob> Load()
    {
        try { return File.Exists(JobsFilePath) ? JsonSerializer.Deserialize<List<InspectionJob>>(File.ReadAllText(JobsFilePath), JsonDefaults.Read) ?? [] : []; }
        catch { return []; }
    }

    public Task SaveAsync(IEnumerable<InspectionJob> jobs) => AtomicJson.WriteAsync(JobsFilePath, jobs.ToList());
}

public sealed class RunArtifactStore
{
    public string ResultsDirectory { get; }
    public RunArtifactStore(string root) => ResultsDirectory = Path.Combine(root, "Results");
    public string GetRunDirectory(string jobId, string runId) => Path.Combine(ResultsDirectory, Clean(jobId), Clean(runId));
    public string GetResultPath(string jobId, string runId) => Path.Combine(GetRunDirectory(jobId, runId), "run-result.json");
    public string GetLogPath(string jobId, string runId) => Path.Combine(GetRunDirectory(jobId, runId), "run.log");
    public string GetReportPath(string jobId, string runId) => Path.Combine(GetRunDirectory(jobId, runId), "report.pdf");

    public async Task SaveCheckpointAsync(JobRunResult result)
    {
        await AtomicJson.WriteAsync(GetResultPath(result.JobId, result.RunId), result);
        var logPath = GetLogPath(result.JobId, result.RunId);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(logPath, string.Join(Environment.NewLine, result.Logs), new UTF8Encoding(true));
    }

    public JobRunResult? Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<JobRunResult>(File.ReadAllText(path), JsonDefaults.Read) : null; }
        catch { return null; }
    }

    private static string Clean(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

internal static class AtomicJson
{
    public static async Task WriteAsync<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, JsonDefaults.Write), new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }
}

public sealed class LotTestRunner
{
    private static readonly string[] DefectTypes = ["Particle", "Scratch", "Pattern", "Edge", "Contamination"];
    public const int GridSize = 21;

    public async Task RunAsync(
        InspectionJob job,
        EquipmentState equipment,
        JobRunResult result,
        IProgress<LotRunProgress> progress,
        Func<JobRunResult, Task> checkpoint,
        CancellationToken cancellationToken)
    {
        var steps = job.RecipeSnapshot.Steps.OrderBy(x => x.Sequence).ToList();
        var completedUnits = 0d;
        var totalUnits = 25d * steps.Sum(x => x.DurationSeconds);
        AddLog(result, "INFO", null, $"Lot 검사 시작 - Job {job.JobId}, Lot {job.LotId}, 장비 {equipment.Definition.Name}");
        try
        {
            for (var waferIndex = 0; waferIndex < 25; waferIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var wafer = result.Wafers[waferIndex];
                var setting = job.Simulation!.Wafers[waferIndex];
                wafer.Status = WaferExecutionStatus.Running;
                wafer.StartedAt = DateTimeOffset.Now;
                result.CurrentWaferId = wafer.WaferId;
                AddLog(result, "INFO", wafer.WaferId, "웨이퍼 검사 시작");
                var completedStepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    var step = steps[stepIndex];
                    AddLog(result, "INFO", wafer.WaferId, $"단계 시작: {step.Name} ({step.Command})");
                    var realDurationMs = Math.Max(80, step.DurationSeconds * 1000 / job.Simulation.SpeedFactor);
                    var startedTick = Environment.TickCount64;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var elapsed = Math.Min(Environment.TickCount64 - startedTick, realDurationMs);
                        var stepUnits = step.DurationSeconds * elapsed / realDurationMs;
                        result.ProgressPercent = (int)Math.Clamp((completedUnits + stepUnits) * 100 / totalUnits, 0, 99);
                        progress.Report(new(result.RunId, wafer.WaferId, result.ProgressPercent, step.Id, step.Name,
                            stepIndex + 1, new HashSet<string>(completedStepIds), WaferExecutionStatus.Running, null));
                        if (elapsed >= realDurationMs) break;
                        await Task.Delay(80, cancellationToken);
                    }

                    if (setting.Outcome == SimulationOutcome.EquipmentError &&
                        string.Equals(setting.FailedStepId, step.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        wafer.Status = WaferExecutionStatus.EquipmentError;
                        wafer.FailedStepId = step.Id;
                        wafer.FailureReason = $"{step.Name} 단계에서 모의 장비 오류가 발생했습니다.";
                        wafer.FinishedAt = DateTimeOffset.Now;
                        result.Status = JobStatus.Failed;
                        result.FailureReason = $"{wafer.WaferId}: {wafer.FailureReason}";
                        result.FinishedAt = DateTimeOffset.Now;
                        AddLog(result, "ERROR", wafer.WaferId, wafer.FailureReason);
                        for (var remaining = waferIndex + 1; remaining < result.Wafers.Count; remaining++)
                            result.Wafers[remaining].Status = WaferExecutionStatus.NotRun;
                        await checkpoint(result);
                        return;
                    }

                    completedUnits += step.DurationSeconds;
                    completedStepIds.Add(step.Id);
                    AddLog(result, "INFO", wafer.WaferId, $"단계 완료: {step.Name}");
                }

                PopulateWaferResult(result.RunId, wafer, setting, job.ProductSnapshot.AcceptanceYieldPercent);
                wafer.FinishedAt = DateTimeOffset.Now;
                AddLog(result, "INFO", wafer.WaferId,
                    $"웨이퍼 검사 완료 - {StatusText(wafer.Status)}, 수율 {wafer.YieldPercent:0.00}%, 결함 {wafer.Defects.Count}개");
                result.ProgressPercent = (waferIndex + 1) * 100 / 25;
                await checkpoint(result);
                progress.Report(new(result.RunId, wafer.WaferId, result.ProgressPercent, string.Empty, "완료",
                    steps.Count, new HashSet<string>(completedStepIds), wafer.Status, result.Logs.Last()));
            }
            result.Status = JobStatus.Completed;
            result.ProgressPercent = 100;
            result.CurrentWaferId = string.Empty;
            result.FinishedAt = DateTimeOffset.Now;
            AddLog(result, "INFO", null, $"Lot 검사 완료 - Lot 수율 {result.LotYieldPercent:0.00}%");
            await checkpoint(result);
        }
        catch (OperationCanceledException)
        {
            var current = result.Wafers.FirstOrDefault(x => x.Status == WaferExecutionStatus.Running);
            if (current is not null) current.Status = WaferExecutionStatus.NotRun;
            foreach (var wafer in result.Wafers.Where(x => x.Status == WaferExecutionStatus.Pending)) wafer.Status = WaferExecutionStatus.NotRun;
            result.Status = JobStatus.Canceled;
            result.FailureReason = "사용자가 검사를 취소했습니다.";
            result.FinishedAt = DateTimeOffset.Now;
            AddLog(result, "WARN", result.CurrentWaferId, result.FailureReason);
            await checkpoint(result);
        }
        catch (Exception ex)
        {
            var current = result.Wafers.FirstOrDefault(x => x.Status == WaferExecutionStatus.Running);
            if (current is not null) { current.Status = WaferExecutionStatus.EquipmentError; current.FailureReason = ex.Message; }
            foreach (var wafer in result.Wafers.Where(x => x.Status == WaferExecutionStatus.Pending)) wafer.Status = WaferExecutionStatus.NotRun;
            result.Status = JobStatus.Failed;
            result.FailureReason = ex.Message;
            result.FinishedAt = DateTimeOffset.Now;
            AddLog(result, "ERROR", result.CurrentWaferId, ex.Message);
            await checkpoint(result);
        }
    }

    public static JobRunResult CreateRun(InspectionJob job, EquipmentDefinition equipment, string runId)
    {
        return new()
        {
            JobId = job.JobId, RunId = runId, CustomerName = job.CustomerName, RequestNumber = job.RequestNumber,
            LotId = job.LotId, ProductId = job.ProductSnapshot.ProductId, ProductName = job.ProductSnapshot.Name,
            RecipeId = job.RecipeSnapshot.RecipeId, RecipeName = job.RecipeSnapshot.Name,
            RecipeVersion = job.RecipeSnapshot.Version, EquipmentId = equipment.Id, EquipmentName = equipment.Name,
            Status = JobStatus.Running, StartedAt = DateTimeOffset.Now, SpeedFactor = job.Simulation?.SpeedFactor ?? 10,
            Wafers = Enumerable.Range(1, 25).Select(number => new WaferResult { WaferId = $"Wafer{number:00}" }).ToList()
        };
    }

    public static void PopulateWaferResult(string runId, WaferResult wafer, WaferSimulationSetting setting, double threshold)
    {
        wafer.DefectLevel = setting.Outcome == SimulationOutcome.Ng ? setting.DefectLevel : null;
        wafer.Dies = BuildGrid();
        var valid = wafer.Dies.Where(x => x.IsValid).ToList();
        wafer.ValidDieCount = valid.Count;
        if (setting.Outcome == SimulationOutcome.Normal)
        {
            wafer.Status = WaferExecutionStatus.Normal;
            wafer.PassDieCount = valid.Count;
            return;
        }

        var level = setting.DefectLevel ?? DefectLevel.Low;
        var (lower, upper) = level switch
        {
            DefectLevel.Low => (Math.Max(0, threshold - 5), threshold),
            DefectLevel.Medium => (Math.Max(0, threshold - 15), Math.Max(0, threshold - 5)),
            _ => (Math.Max(0, threshold - 30), Math.Max(0, threshold - 15))
        };
        var random = new Random(StableSeed($"{runId}|{wafer.WaferId}|{level}"));
        var targetYield = lower + random.NextDouble() * Math.Max(.01, upper - lower);
        var failCount = Math.Clamp((int)Math.Ceiling(valid.Count * (100 - targetYield) / 100d), 1, valid.Count);
        foreach (var die in valid.OrderBy(_ => random.Next()).Take(failCount))
        {
            die.IsPass = false;
            wafer.Defects.Add(new DefectRecord
            {
                Row = die.Row, Column = die.Column, Type = DefectTypes[random.Next(DefectTypes.Length)]
            });
        }
        wafer.Status = WaferExecutionStatus.Ng;
        wafer.PassDieCount = valid.Count - failCount;
    }

    public static List<DieResult> BuildGrid()
    {
        var result = new List<DieResult>(GridSize * GridSize);
        const double center = (GridSize - 1) / 2d;
        const double radius = 10.15;
        for (var row = 0; row < GridSize; row++)
        for (var column = 0; column < GridSize; column++)
        {
            var valid = Math.Pow(row - center, 2) + Math.Pow(column - center, 2) <= radius * radius;
            result.Add(new DieResult { Row = row, Column = column, IsValid = valid, IsPass = valid });
        }
        return result;
    }

    private static int StableSeed(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
    }

    private static void AddLog(JobRunResult result, string level, string? waferId, string message) =>
        result.Logs.Add(new RunLogEntry(DateTimeOffset.Now, level, waferId, message));
    private static string StatusText(WaferExecutionStatus status) => status == WaferExecutionStatus.Normal ? "정상" : "NG";
}
