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

public static class TestCellCatalog
{
    public static TestCellCatalogLoadResult Load(string directory)
    {
        var items = new List<TestCellDefinition>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
            return new([], [$"Test Cell 폴더를 찾을 수 없습니다: {directory}"]);

        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var item = JsonSerializer.Deserialize<TestCellDefinition>(
                               File.ReadAllText(path, Encoding.UTF8), JsonDefaults.Read)
                           ?? throw new InvalidDataException("JSON 내용이 비어 있습니다.");
                Validate(item);
                if (!ids.Add(item.Id))
                    throw new InvalidDataException($"중복 Test Cell ID입니다: {item.Id}");
                item.AccentColor = ParseColor(item.AccentColorHex);
                item.ImagePath = ResolveImagePath(path, errors);
                items.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (items.Count == 0) errors.Add("정상적으로 로드된 Test Cell이 없습니다.");
        return new(items, errors);
    }

    public static void Validate(TestCellDefinition value)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != "3.0") errors.Add("schemaVersion은 3.0이어야 합니다");
        Required(value.Id, "id", errors);
        Required(value.Name, "name", errors);
        Required(value.Line, "line", errors);
        ValidateComponent(value.Tester, "tester", errors);
        ValidateComponent(value.Prober, "prober", errors);
        Required(value.ProbeCard.Name, "probeCard.name", errors);
        Required(value.ProbeCard.Model, "probeCard.model", errors);
        if (!value.ProbeCard.FixedMounted) errors.Add("probeCard.fixedMounted는 true여야 합니다");
        if (value.SupportedWaferDiametersMm.Count == 0 ||
            value.SupportedWaferDiametersMm.Any(x => x is not (200 or 300)))
            errors.Add("supportedWaferDiametersMm에는 200 또는 300이 필요합니다");
        if (value.Capabilities.Count == 0 || value.Capabilities.Any(string.IsNullOrWhiteSpace))
            errors.Add("capabilities가 필요합니다");
        if (!IPAddress.TryParse(value.IpAddress, out _)) errors.Add("ipAddress 형식 오류");
        if (value.Port is < 1 or > 65535) errors.Add("port는 1~65535 범위여야 합니다");
        if (!TryParseColor(value.AccentColorHex, out _)) errors.Add("accentColorHex는 #RRGGBB 형식이어야 합니다");
        if (errors.Count > 0) throw new InvalidDataException(string.Join(", ", errors));
    }

    private static void ValidateComponent(
        TestCellComponentDefinition value,
        string field,
        ICollection<string> errors)
    {
        Required(value.Manufacturer, $"{field}.manufacturer", errors);
        Required(value.Model, $"{field}.model", errors);
        Required(value.DisplayName, $"{field}.displayName", errors);
    }

    private static string? ResolveImagePath(string jsonPath, ICollection<string> errors)
    {
        var baseName = Path.GetFileNameWithoutExtension(jsonPath);
        var directory = Path.GetDirectoryName(jsonPath)!;
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg" })
        {
            var candidate = Path.Combine(directory, baseName + extension);
            if (File.Exists(candidate)) return candidate;
        }

        errors.Add($"{Path.GetFileName(jsonPath)}: 공식 제품 이미지 파일을 찾을 수 없어 기본 도식을 사용합니다.");
        return null;
    }

    internal static Color ParseColor(string value)
    {
        TryParseColor(value, out var color);
        return color;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#') return false;
        if (!int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return false;
        color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return true;
    }

    private static void Required(string? value, string field, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} 값이 필요합니다");
    }
}

public static class ProductCatalog
{
    public static ProductCatalogLoadResult Load(string directory)
    {
        var items = new List<ProductDocument>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
            return new([], [$"Product 폴더를 찾을 수 없습니다: {directory}"]);

        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(x => x))
        {
            try
            {
                var item = JsonSerializer.Deserialize<ProductDocument>(
                               File.ReadAllText(path, Encoding.UTF8), JsonDefaults.Read)
                           ?? throw new InvalidDataException("JSON 내용이 비어 있습니다.");
                Validate(item);
                if (!ids.Add(item.ProductId))
                    throw new InvalidDataException($"중복 productId입니다: {item.ProductId}");
                items.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return new(items, errors);
    }

    public static void Validate(ProductDocument value)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != "3.0") errors.Add("schemaVersion은 3.0이어야 합니다");
        Required(value.ProductId, "productId", errors);
        Required(value.Family, "family", errors);
        Required(value.Name, "name", errors);
        Required(value.Material, "material", errors);
        if (value.WaferDiameterMm is not (200 or 300)) errors.Add("waferDiameterMm은 200 또는 300이어야 합니다");
        if (value.DieWidthMm <= 0) errors.Add("dieWidthMm은 양수여야 합니다");
        if (value.DieHeightMm <= 0) errors.Add("dieHeightMm은 양수여야 합니다");
        if (value.EdgeExclusionMm is < 0 || value.EdgeExclusionMm >= value.WaferDiameterMm / 2d)
            errors.Add("edgeExclusionMm이 Wafer 범위를 벗어났습니다");
        if (value.AcceptanceYieldPercent is <= 0 or > 100)
            errors.Add("acceptanceYieldPercent는 0 초과 100 이하여야 합니다");
        if (value.AllowedRecipeIds.Count == 0 || value.AllowedRecipeIds.Any(string.IsNullOrWhiteSpace))
            errors.Add("allowedRecipeIds가 필요합니다");
        if (errors.Count > 0) throw new InvalidDataException(string.Join(", ", errors));
    }

    private static void Required(string? value, string field, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} 값이 필요합니다");
    }
}

public static class RecipeCatalog
{
    public static RecipeCatalogLoadResult Load(string directory)
    {
        var items = new List<RecipeDocument>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
            return new([], [$"Recipe 폴더를 찾을 수 없습니다: {directory}"]);

        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(x => x))
        {
            try
            {
                var item = JsonSerializer.Deserialize<RecipeDocument>(
                               File.ReadAllText(path, Encoding.UTF8), JsonDefaults.Read)
                           ?? throw new InvalidDataException("JSON 내용이 비어 있습니다.");
                Validate(item);
                if (!ids.Add(item.RecipeId))
                    throw new InvalidDataException($"중복 recipeId입니다: {item.RecipeId}");
                items.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return new(items, errors);
    }

    public static void Validate(RecipeDocument value)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != "3.0") errors.Add("schemaVersion은 3.0이어야 합니다");
        Required(value.RecipeId, "recipeId", errors);
        Required(value.Name, "name", errors);
        Required(value.Version, "version", errors);
        Required(value.ProductFamily, "productFamily", errors);
        Required(value.Author, "author", errors);
        if (value.CreatedAt == default) errors.Add("createdAt이 필요합니다");
        if (value.CompatibleTestCellIds.Count == 0 || value.CompatibleTestCellIds.Any(string.IsNullOrWhiteSpace))
            errors.Add("compatibleTestCellIds가 필요합니다");
        if (value.RequiredCapabilities.Count == 0 || value.RequiredCapabilities.Any(string.IsNullOrWhiteSpace))
            errors.Add("requiredCapabilities가 필요합니다");
        if (value.FinalBins.Count < 2) errors.Add("PASS와 실패 Final Bin이 필요합니다");
        if (value.FinalBins.Count(x => x.IsPass) != 1) errors.Add("PASS Final Bin은 정확히 하나여야 합니다");
        if (value.Steps.Count == 0) errors.Add("steps가 필요합니다");

        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < value.Steps.Count; index++)
        {
            var step = value.Steps[index];
            if (step.Sequence != index + 1) errors.Add($"steps[{index}].sequence는 {index + 1}이어야 합니다");
            Required(step.Id, $"steps[{index}].id", errors);
            Required(step.Name, $"steps[{index}].name", errors);
            Required(step.Command, $"steps[{index}].command", errors);
            if (step.DurationSeconds <= 0) errors.Add($"steps[{index}].durationSeconds는 양수여야 합니다");
            if (!string.IsNullOrWhiteSpace(step.Id) && !stepIds.Add(step.Id))
                errors.Add($"중복 step id입니다: {step.Id}");
        }

        var binCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bin in value.FinalBins)
        {
            Required(bin.Code, "finalBins.code", errors);
            Required(bin.DisplayName, "finalBins.displayName", errors);
            if (!string.IsNullOrWhiteSpace(bin.Code) && !binCodes.Add(bin.Code))
                errors.Add($"중복 Final Bin입니다: {bin.Code}");
            if (!bin.IsPass && (string.IsNullOrWhiteSpace(bin.RelatedStepId) || !stepIds.Contains(bin.RelatedStepId)))
                errors.Add($"{bin.Code}의 relatedStepId가 유효하지 않습니다");
            try { _ = TestCellCatalog.ParseColor(bin.ColorHex); }
            catch { errors.Add($"{bin.Code}의 colorHex가 유효하지 않습니다"); }
        }
        if (errors.Count > 0) throw new InvalidDataException(string.Join(", ", errors));
    }

    private static void Required(string? value, string field, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} 값이 필요합니다");
    }
}

public static class CatalogRelationshipValidator
{
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<ProductDocument> products,
        IReadOnlyList<RecipeDocument> recipes,
        IReadOnlyList<TestCellDefinition> cells)
    {
        var errors = new List<string>();
        foreach (var product in products)
        {
            foreach (var recipeId in product.AllowedRecipeIds)
            {
                var recipe = recipes.FirstOrDefault(x =>
                    string.Equals(x.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase));
                if (recipe is null)
                {
                    errors.Add($"{product.ProductId}: Recipe를 찾을 수 없습니다 - {recipeId}");
                    continue;
                }
                if (!string.Equals(recipe.ProductFamily, product.Family, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{product.ProductId}: Recipe 제품군이 다릅니다 - {recipeId}");
            }
        }

        foreach (var recipe in recipes)
        foreach (var cellId in recipe.CompatibleTestCellIds)
        {
            var cell = cells.FirstOrDefault(x => string.Equals(x.Id, cellId, StringComparison.OrdinalIgnoreCase));
            if (cell is null)
            {
                errors.Add($"{recipe.RecipeId}: Test Cell을 찾을 수 없습니다 - {cellId}");
                continue;
            }
            var missing = recipe.RequiredCapabilities
                .Where(x => !cell.Capabilities.Contains(x, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (missing.Count > 0)
                errors.Add($"{recipe.RecipeId}/{cell.Id}: capability 누락 - {string.Join(", ", missing)}");
        }
        return errors;
    }
}

public sealed class MockConnectionService
{
    public async Task ConnectAsync(TestCellState cell, CancellationToken cancellationToken = default)
    {
        cell.ConnectionStatus = ConnectionStatus.Connecting;
        await Task.Delay(500, cancellationToken);
        cell.ConnectionStatus = ConnectionStatus.Connected;
        cell.LastConnectedAt = DateTimeOffset.Now;
    }
}

public static class JobStartValidator
{
    public static string? GetBlockReason(InspectionJob job, TestCellState? cell)
    {
        if (job.Status == JobStatus.Running) return "현재 Run이 진행 중입니다.";
        if (job.Simulation is null) return "모의 EDS 결과 설정을 먼저 저장하세요.";
        if (cell is null) return "배정 Test Cell 정보를 찾을 수 없습니다.";
        if (cell.ConnectionStatus != ConnectionStatus.Connected)
            return "배정 Test Cell이 연결되어 있지 않습니다.";
        if (cell.HasError) return "Test Cell 오류를 먼저 리셋해야 합니다.";
        if (cell.IsBusy) return $"Test Cell이 {cell.ActiveJobId} Job을 실행 중입니다.";
        if (!cell.Definition.ProbeCard.FixedMounted)
            return "고정 Probe Card가 장착되어 있지 않습니다.";
        return null;
    }
}

public sealed class TestCellStateStore
{
    private readonly string _path;
    public TestCellStateStore(string? root = null) =>
        _path = Path.Combine(
            root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeTestProject"),
            "test-cell-state.json");

    public IReadOnlyList<PersistedTestCellState> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<List<PersistedTestCellState>>(File.ReadAllText(_path), JsonDefaults.Read) ?? []
                : [];
        }
        catch
        {
            return [];
        }
    }

    public Task SaveAsync(IEnumerable<TestCellState> cells) =>
        AtomicJson.WriteAsync(_path, cells.Select(x => new PersistedTestCellState(
            x.Definition.Id,
            x.ConnectionStatus == ConnectionStatus.Connecting ? ConnectionStatus.Disconnected : x.ConnectionStatus,
            x.LastConnectedAt,
            x.ErrorComponent,
            x.ErrorMessage)).ToList());
}

public sealed class JobStore
{
    public string RootDirectory { get; }
    public string JobsFilePath => Path.Combine(RootDirectory, "jobs.json");
    public JobStore(string? root = null) =>
        RootDirectory = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecipeTestProject");

    public List<InspectionJob> Load()
    {
        try
        {
            return File.Exists(JobsFilePath)
                ? JsonSerializer.Deserialize<List<InspectionJob>>(File.ReadAllText(JobsFilePath), JsonDefaults.Read) ?? []
                : [];
        }
        catch
        {
            return [];
        }
    }

    public Task SaveAsync(IEnumerable<InspectionJob> jobs) =>
        AtomicJson.WriteAsync(JobsFilePath, jobs.ToList());
}

public static class EdsDataMigration
{
    public const string MarkerFileName = "eds-v3-migration.completed";

    public static bool RunOnce(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        var marker = Path.Combine(rootDirectory, MarkerFileName);
        if (File.Exists(marker)) return false;

        var jobs = Path.Combine(rootDirectory, "jobs.json");
        if (File.Exists(jobs)) File.Delete(jobs);
        var results = Path.Combine(rootDirectory, "Results");
        if (Directory.Exists(results)) Directory.Delete(results, true);
        File.WriteAllText(marker,
            $"EDS Wafer Sort Simulator v3 migration completed at {DateTimeOffset.Now:O}",
            new UTF8Encoding(false));
        return true;
    }
}

public sealed class RunArtifactStore
{
    public string ResultsDirectory { get; }
    public RunArtifactStore(string root) => ResultsDirectory = Path.Combine(root, "Results");
    public string GetRunDirectory(string jobId, string runId) =>
        Path.Combine(ResultsDirectory, Clean(jobId), Clean(runId));
    public string GetResultPath(string jobId, string runId) =>
        Path.Combine(GetRunDirectory(jobId, runId), "run-result.json");
    public string GetLogPath(string jobId, string runId) =>
        Path.Combine(GetRunDirectory(jobId, runId), "run.log");
    public string GetReportPath(string jobId, string runId) =>
        Path.Combine(GetRunDirectory(jobId, runId), "report.pdf");

    public async Task SaveCheckpointAsync(JobRunResult result)
    {
        await AtomicJson.WriteAsync(GetResultPath(result.JobId, result.RunId), result);
        var logPath = GetLogPath(result.JobId, result.RunId);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(
            logPath,
            string.Join(Environment.NewLine, result.Logs),
            new UTF8Encoding(true));
    }

    public JobRunResult? Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<JobRunResult>(File.ReadAllText(path), JsonDefaults.Read)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Clean(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

internal static class AtomicJson
{
    public static async Task WriteAsync<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(
            temporary,
            JsonSerializer.Serialize(value, JsonDefaults.Write),
            new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }
}

public sealed class LotTestRunner
{
    public async Task RunAsync(
        InspectionJob job,
        TestCellState cell,
        JobRunResult result,
        IProgress<LotRunProgress> progress,
        Func<JobRunResult, Task> checkpoint,
        CancellationToken cancellationToken)
    {
        var steps = job.RecipeSnapshot.Steps.OrderBy(x => x.Sequence).ToList();
        var simulation = job.Simulation ??
                         throw new InvalidOperationException("모의 EDS 결과 설정이 저장되지 않았습니다.");
        var completedUnits = 0d;
        var totalUnits = 25d * steps.Sum(x => x.DurationSeconds);
        AddLog(result, "INFO", null,
            $"EDS Lot 시작 - Job {job.JobId}, Lot {job.LotId}, Test Cell {cell.Definition.Name}");
        try
        {
            for (var waferIndex = 0; waferIndex < 25; waferIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var wafer = result.Wafers[waferIndex];
                wafer.Status = WaferExecutionStatus.Running;
                wafer.StartedAt = DateTimeOffset.Now;
                result.CurrentWaferId = wafer.WaferId;
                AddLog(result, "INFO", wafer.WaferId, "EDS 시작");
                var completedStepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    var step = steps[stepIndex];
                    AddLog(result, "INFO", wafer.WaferId, $"단계 시작: {step.Name} ({step.Command})");
                    var realDurationMs = Math.Max(
                        30,
                        step.DurationSeconds * 1000 / Math.Max(1, simulation.SpeedFactor));
                    var startedTick = Environment.TickCount64;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var elapsed = Math.Min(Environment.TickCount64 - startedTick, realDurationMs);
                        var stepUnits = step.DurationSeconds * elapsed / realDurationMs;
                        result.ProgressPercent = (int)Math.Clamp(
                            (completedUnits + stepUnits) * 100 / totalUnits, 0, 99);
                        progress.Report(new(
                            result.RunId,
                            wafer.WaferId,
                            result.ProgressPercent,
                            step.Id,
                            step.Name,
                            stepIndex + 1,
                            new HashSet<string>(completedStepIds),
                            WaferExecutionStatus.Running,
                            null));
                        if (elapsed >= realDurationMs) break;
                        await Task.Delay(50, cancellationToken);
                    }

                    if (ShouldFail(simulation.CellError, wafer.WaferId, step.Id))
                    {
                        var component = simulation.CellError.Component;
                        wafer.Status = WaferExecutionStatus.EquipmentError;
                        wafer.FailedStepId = step.Id;
                        wafer.FailureReason =
                            $"{ComponentText(component)} 오류: {step.Name} 단계에서 실행을 중단했습니다.";
                        wafer.FinishedAt = DateTimeOffset.Now;
                        result.Status = JobStatus.Failed;
                        result.ErrorComponent = component;
                        result.FailureReason = $"{wafer.WaferId}: {wafer.FailureReason}";
                        result.FinishedAt = DateTimeOffset.Now;
                        cell.SetError(component, result.FailureReason);
                        AddLog(result, "ERROR", wafer.WaferId, wafer.FailureReason);
                        foreach (var remaining in result.Wafers.Skip(waferIndex + 1))
                            remaining.Status = WaferExecutionStatus.NotRun;
                        await checkpoint(result);
                        return;
                    }

                    completedUnits += step.DurationSeconds;
                    completedStepIds.Add(step.Id);
                    AddLog(result, "INFO", wafer.WaferId, $"단계 완료: {step.Name}");
                }

                var setting = simulation.Wafers[waferIndex];
                var targetYield = setting.UseLotDefault
                    ? simulation.DefaultTargetYieldPercent
                    : setting.TargetYieldPercent ?? simulation.DefaultTargetYieldPercent;
                PopulateWaferResult(
                    result.RunId,
                    wafer,
                    job.ProductSnapshot,
                    job.RecipeSnapshot,
                    targetYield,
                    simulation.DefaultFailBinDistribution,
                    setting.UseLotDefault ? null : setting.DominantFailBinCode);
                wafer.FinishedAt = DateTimeOffset.Now;
                AddLog(result, "INFO", wafer.WaferId,
                    $"EDS 완료 - {DispositionText(wafer.Disposition)}, 수율 {wafer.YieldPercent:0.00}%, Fail die {wafer.ValidDieCount - wafer.PassDieCount}");
                result.ProgressPercent = (waferIndex + 1) * 100 / 25;
                await checkpoint(result);
                progress.Report(new(
                    result.RunId,
                    wafer.WaferId,
                    result.ProgressPercent,
                    string.Empty,
                    "완료",
                    steps.Count,
                    new HashSet<string>(completedStepIds),
                    wafer.Status,
                    result.Logs.Last()));
            }

            result.Status = JobStatus.Completed;
            result.ProgressPercent = 100;
            result.CurrentWaferId = string.Empty;
            result.FinishedAt = DateTimeOffset.Now;
            AddLog(result, "INFO", null, $"EDS Lot 완료 - Lot 수율 {result.LotYieldPercent:0.00}%");
            await checkpoint(result);
        }
        catch (OperationCanceledException)
        {
            var current = result.Wafers.FirstOrDefault(x => x.Status == WaferExecutionStatus.Running);
            if (current is not null) current.Status = WaferExecutionStatus.NotRun;
            foreach (var wafer in result.Wafers.Where(x => x.Status == WaferExecutionStatus.Pending))
                wafer.Status = WaferExecutionStatus.NotRun;
            result.Status = JobStatus.Canceled;
            result.FailureReason = "사용자가 EDS Run을 취소했습니다.";
            result.FinishedAt = DateTimeOffset.Now;
            AddLog(result, "WARN", result.CurrentWaferId, result.FailureReason);
            await checkpoint(result);
        }
        catch (Exception ex)
        {
            var current = result.Wafers.FirstOrDefault(x => x.Status == WaferExecutionStatus.Running);
            if (current is not null)
            {
                current.Status = WaferExecutionStatus.EquipmentError;
                current.FailureReason = ex.Message;
            }
            foreach (var wafer in result.Wafers.Where(x => x.Status == WaferExecutionStatus.Pending))
                wafer.Status = WaferExecutionStatus.NotRun;
            result.Status = JobStatus.Failed;
            result.FailureReason = ex.Message;
            result.FinishedAt = DateTimeOffset.Now;
            AddLog(result, "ERROR", result.CurrentWaferId, ex.Message);
            await checkpoint(result);
        }
    }

    public static JobRunResult CreateRun(InspectionJob job, string runId) => new()
    {
        JobId = job.JobId,
        RunId = runId,
        CustomerName = job.CustomerName,
        RequestNumber = job.RequestNumber,
        LotId = job.LotId,
        ProductSnapshot = job.ProductSnapshot,
        RecipeSnapshot = job.RecipeSnapshot,
        TestCellSnapshot = job.TestCellSnapshot,
        Status = JobStatus.Running,
        StartedAt = DateTimeOffset.Now,
        SpeedFactor = job.Simulation?.SpeedFactor ?? 20,
        Wafers = Enumerable.Range(1, 25)
            .Select(number => new WaferResult { WaferId = $"Wafer{number:00}" })
            .ToList()
    };

    public static void PopulateWaferResult(
        string runId,
        WaferResult wafer,
        ProductDocument product,
        RecipeDocument recipe,
        double targetYieldPercent,
        IReadOnlyDictionary<string, double> failBinDistribution,
        string? dominantFailBinCode)
    {
        if (targetYieldPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(targetYieldPercent));
        ValidateDistribution(recipe, failBinDistribution);

        wafer.Dies = BuildGrid(product);
        wafer.GridRows = wafer.Dies.Max(x => x.Row) + 1;
        wafer.GridColumns = wafer.Dies.Max(x => x.Column) + 1;
        var valid = wafer.Dies.Where(x => x.IsValid).ToList();
        wafer.ValidDieCount = valid.Count;
        wafer.PassDieCount = Math.Clamp(
            (int)Math.Round(valid.Count * targetYieldPercent / 100d, MidpointRounding.AwayFromZero),
            0,
            valid.Count);
        var failCount = valid.Count - wafer.PassDieCount;
        var passCode = recipe.PassBin.Code;
        foreach (var die in valid) die.FinalBinCode = passCode;

        var random = new Random(StableSeed($"{runId}|{wafer.WaferId}"));
        var failedDies = valid.OrderBy(_ => random.Next()).Take(failCount).ToList();
        var counts = AllocateFailBins(
            failCount,
            recipe.FailBins.Select(x => x.Code).ToList(),
            failBinDistribution,
            dominantFailBinCode);
        var cursor = 0;
        foreach (var code in recipe.FailBins.Select(x => x.Code))
        {
            for (var index = 0; index < counts.GetValueOrDefault(code); index++)
                failedDies[cursor++].FinalBinCode = code;
        }

        wafer.Status = WaferExecutionStatus.Completed;
        wafer.Disposition = wafer.YieldPercent >= product.AcceptanceYieldPercent
            ? WaferDisposition.Passed
            : WaferDisposition.LowYield;
    }

    public static List<DieResult> BuildGrid(ProductDocument product)
    {
        var usableRadius = product.WaferDiameterMm / 2d - product.EdgeExclusionMm;
        var columns = Math.Max(1, (int)Math.Ceiling(usableRadius * 2 / product.DieWidthMm));
        var rows = Math.Max(1, (int)Math.Ceiling(usableRadius * 2 / product.DieHeightMm));
        var result = new List<DieResult>(rows * columns);
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var x = (column - (columns - 1) / 2d) * product.DieWidthMm;
            var y = (row - (rows - 1) / 2d) * product.DieHeightMm;
            var valid = x * x + y * y <= usableRadius * usableRadius;
            result.Add(new DieResult
            {
                Row = row,
                Column = column,
                CenterXmm = x,
                CenterYmm = y,
                IsValid = valid
            });
        }
        return result;
    }

    public static void ValidateDistribution(
        RecipeDocument recipe,
        IReadOnlyDictionary<string, double> distribution)
    {
        var expected = recipe.FailBins.Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expected.SetEquals(distribution.Keys))
            throw new InvalidDataException("실패 Bin 분포가 Recipe의 Final Bin과 일치하지 않습니다.");
        if (distribution.Values.Any(x => x < 0) ||
            Math.Abs(distribution.Values.Sum() - 100d) > .01)
            throw new InvalidDataException("실패 Bin 분포 합계는 100%여야 합니다.");
    }

    public static Dictionary<string, int> AllocateFailBins(
        int failCount,
        IReadOnlyList<string> failBinCodes,
        IReadOnlyDictionary<string, double> distribution,
        string? dominantFailBinCode)
    {
        var result = failBinCodes.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        if (failCount <= 0) return result;

        var remaining = failCount;
        if (!string.IsNullOrWhiteSpace(dominantFailBinCode))
        {
            if (!result.ContainsKey(dominantFailBinCode))
                throw new InvalidDataException($"유효하지 않은 대표 Final Bin입니다: {dominantFailBinCode}");
            var dominant = Math.Clamp(
                (int)Math.Round(failCount * .6, MidpointRounding.AwayFromZero),
                1,
                failCount);
            result[dominantFailBinCode] = dominant;
            remaining -= dominant;
        }

        var candidates = failBinCodes
            .Where(x => !string.Equals(x, dominantFailBinCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            if (remaining > 0) result[failBinCodes[0]] += remaining;
            return result;
        }

        var weightSum = candidates.Sum(x => distribution[x]);
        if (weightSum <= 0) weightSum = candidates.Count;
        var allocations = candidates
            .Select(code =>
            {
                var weight = weightSum == candidates.Count && distribution[code] == 0
                    ? 1
                    : distribution[code];
                var exact = remaining * weight / weightSum;
                return new
                {
                    Code = code,
                    Floor = (int)Math.Floor(exact),
                    Fraction = exact - Math.Floor(exact)
                };
            })
            .ToList();
        foreach (var item in allocations) result[item.Code] += item.Floor;
        var assigned = allocations.Sum(x => x.Floor);
        foreach (var item in allocations
                     .OrderByDescending(x => x.Fraction)
                     .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                     .Take(remaining - assigned))
            result[item.Code]++;
        return result;
    }

    private static bool ShouldFail(CellErrorSimulation setting, string waferId, string stepId) =>
        setting.Enabled &&
        string.Equals(setting.WaferId, waferId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(setting.FailedStepId, stepId, StringComparison.OrdinalIgnoreCase);

    private static int StableSeed(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
    }

    private static void AddLog(JobRunResult result, string level, string? waferId, string message) =>
        result.Logs.Add(new RunLogEntry(DateTimeOffset.Now, level, waferId, message));

    public static string ComponentText(TestCellComponent component) => component switch
    {
        TestCellComponent.Tester => "Tester",
        TestCellComponent.Prober => "Prober",
        _ => "Probe Card"
    };

    private static string DispositionText(WaferDisposition? disposition) =>
        disposition == WaferDisposition.Passed ? "Passed" : "Low Yield";
}
