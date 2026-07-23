using System.Text;
using System.Text.Json;
using System.Net;

namespace RecipeTestProject;

public static class EquipmentCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static EquipmentCatalogLoadResult Load(string directory)
    {
        var equipment = new List<EquipmentDefinition>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directory))
        {
            errors.Add($"장비 카탈로그 폴더를 찾을 수 없습니다: {directory}");
            return new EquipmentCatalogLoadResult(equipment, errors);
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add($"장비 카탈로그 폴더를 읽을 수 없습니다: {ex.Message}");
            return new EquipmentCatalogLoadResult(equipment, errors);
        }

        foreach (var path in files)
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var document = JsonSerializer.Deserialize<EquipmentCatalogDocument>(
                    File.ReadAllText(path, Encoding.UTF8), JsonOptions);
                if (document is null)
                    throw new EquipmentCatalogException("JSON 내용이 비어 있습니다.");

                var validationErrors = Validate(document);
                if (validationErrors.Count > 0)
                    throw new EquipmentCatalogException(string.Join(", ", validationErrors));

                if (!ids.Add(document.Id))
                    throw new EquipmentCatalogException($"중복된 장비 ID입니다: {document.Id}");

                var imagePath = ResolveImagePath(path, document.ImagePath, errors);
                equipment.Add(new EquipmentDefinition
                {
                    Id = document.Id.Trim(),
                    Name = document.Name.Trim(),
                    Manufacturer = document.Manufacturer.Trim(),
                    Model = document.Model.Trim(),
                    IpAddress = document.IpAddress.Trim(),
                    Port = document.Port,
                    AccentColor = ParseColor(document.AccentColor),
                    ImagePath = imagePath
                });
            }
            catch (JsonException ex)
            {
                errors.Add($"{fileName}: JSON 형식 오류 - {ex.Message}");
            }
            catch (EquipmentCatalogException ex)
            {
                errors.Add($"{fileName}: {ex.Message}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{fileName}: 파일을 읽을 수 없습니다 - {ex.Message}");
            }
        }

        if (files.Length == 0)
            errors.Add($"장비 JSON 파일이 없습니다: {directory}");
        else if (equipment.Count == 0)
            errors.Add("정상적으로 로드된 장비가 없습니다.");

        return new EquipmentCatalogLoadResult(equipment, errors);
    }

    private static List<string> Validate(EquipmentCatalogDocument document)
    {
        var errors = new List<string>();
        Required(document.Id, "id", errors);
        Required(document.Name, "name", errors);
        Required(document.Manufacturer, "manufacturer", errors);
        Required(document.Model, "model", errors);
        Required(document.IpAddress, "ipAddress", errors);
        Required(document.AccentColor, "accentColor", errors);

        if (!string.IsNullOrWhiteSpace(document.IpAddress) && !IPAddress.TryParse(document.IpAddress, out _))
            errors.Add("ipAddress 형식이 올바르지 않습니다");
        if (document.Port is < 1 or > 65535)
            errors.Add("port는 1~65535 범위여야 합니다");
        if (!string.IsNullOrWhiteSpace(document.AccentColor) && !TryParseColor(document.AccentColor, out _))
            errors.Add("accentColor는 #RRGGBB 형식이어야 합니다");
        return errors;
    }

    private static string? ResolveImagePath(string jsonPath, string? configuredPath, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return null;

        var fileName = Path.GetFileName(jsonPath);
        if (Path.IsPathRooted(configuredPath))
        {
            errors.Add($"{fileName}: imagePath는 JSON 파일 기준 상대 경로여야 합니다.");
            return null;
        }

        var directory = Path.GetFullPath(Path.GetDirectoryName(jsonPath)!);
        var candidate = Path.GetFullPath(Path.Combine(directory, configuredPath));
        var allowedPrefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{fileName}: imagePath가 equipment 폴더 밖을 가리킵니다.");
            return null;
        }
        if (!File.Exists(candidate))
        {
            errors.Add($"{fileName}: 이미지 파일을 찾을 수 없습니다 - {configuredPath}");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(candidate);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return candidate;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            errors.Add($"{fileName}: 이미지 파일을 읽을 수 없습니다 - {ex.Message}");
            return null;
        }
    }

    private static Color ParseColor(string value)
    {
        TryParseColor(value, out var color);
        return color;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (value.Length != 7 || value[0] != '#') return false;
        if (!int.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
            return false;
        color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private static void Required(string? value, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} 값이 필요합니다");
    }

    private sealed class EquipmentCatalogDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string AccentColor { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }

    private sealed class EquipmentCatalogException(string message) : Exception(message);
}

public sealed class RecipeValidationException(string message) : Exception(message);

public sealed class RecipeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public RecipeLoadResult LoadAndValidate(string path)
    {
        string raw;
        RecipeDocument? recipe;
        try
        {
            raw = File.ReadAllText(path, Encoding.UTF8);
            recipe = JsonSerializer.Deserialize<RecipeDocument>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new RecipeValidationException($"JSON 형식이 올바르지 않습니다.\r\n경로: {ex.Path ?? "알 수 없음"}\r\n{ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RecipeValidationException($"레시피 파일을 읽을 수 없습니다.\r\n{ex.Message}");
        }

        if (recipe is null)
            throw new RecipeValidationException("레시피 내용이 비어 있습니다.");

        Validate(recipe);
        return new RecipeLoadResult(recipe, raw, path);
    }

    public void Validate(RecipeDocument recipe)
    {
        var errors = new List<string>();
        Required(recipe.SchemaVersion, "schemaVersion", errors);
        Required(recipe.RecipeId, "recipeId", errors);
        Required(recipe.Name, "name", errors);
        Required(recipe.Version, "version", errors);
        Required(recipe.TargetEquipmentModel, "targetEquipmentModel", errors);
        Required(recipe.Author, "author", errors);
        if (recipe.CreatedAt == default) errors.Add("createdAt은 유효한 날짜여야 합니다.");
        if (recipe.Wafer is null) errors.Add("wafer 설정이 필요합니다.");
        if (recipe.Inspection is null) errors.Add("inspection 설정이 필요합니다.");
        if (recipe.Steps is null || recipe.Steps.Count == 0) errors.Add("steps에는 한 개 이상의 단계가 필요합니다.");

        if (recipe.Wafer is not null)
        {
            if (recipe.Wafer.DiameterMm <= 0) errors.Add("wafer.diameterMm은 양수여야 합니다.");
            Required(recipe.Wafer.Material, "wafer.material", errors);
        }

        if (recipe.Inspection is not null)
        {
            Required(recipe.Inspection.ScanMode, "inspection.scanMode", errors);
            if (recipe.Inspection.ResolutionMicrometer <= 0) errors.Add("inspection.resolutionMicrometer는 양수여야 합니다.");
            if (recipe.Inspection.DefectThresholdMicrometer <= 0) errors.Add("inspection.defectThresholdMicrometer는 양수여야 합니다.");
            if (recipe.Inspection.EdgeExclusionMm < 0) errors.Add("inspection.edgeExclusionMm은 0 이상이어야 합니다.");
        }

        if (recipe.Steps is not null)
        {
            var duplicateSequences = recipe.Steps.GroupBy(x => x.Sequence).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var sequence in duplicateSequences) errors.Add($"steps.sequence {sequence}이(가) 중복되었습니다.");
            var duplicateIds = recipe.Steps.Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in duplicateIds) errors.Add($"steps.id '{id}'이(가) 중복되었습니다.");

            for (var i = 0; i < recipe.Steps.Count; i++)
            {
                var step = recipe.Steps[i];
                var prefix = $"steps[{i}]";
                if (step.Sequence != i + 1) errors.Add($"{prefix}.sequence는 {i + 1}이어야 합니다.");
                Required(step.Id, $"{prefix}.id", errors);
                Required(step.Name, $"{prefix}.name", errors);
                Required(step.Command, $"{prefix}.command", errors);
                if (step.DurationSeconds <= 0) errors.Add($"{prefix}.durationSeconds는 양수여야 합니다.");
                foreach (var parameter in step.Parameters ?? [])
                    Required(parameter.Name, $"{prefix}.parameters.name", errors);
            }
        }

        if (errors.Count > 0)
            throw new RecipeValidationException("레시피 검증에 실패했습니다.\r\n\r\n• " + string.Join("\r\n• ", errors));
    }

    private static void Required(string? value, string path, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{path} 값이 필요합니다.");
    }
}

public sealed class MockConnectionService
{
    public async Task ConnectAsync(EquipmentState equipment, CancellationToken cancellationToken = default)
    {
        equipment.ConnectionStatus = ConnectionStatus.Connecting;
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        equipment.ConnectionStatus = ConnectionStatus.Connected;
        equipment.LastConnectedAt = DateTimeOffset.Now;
    }
}

public sealed class MockTestRunner
{
    public async Task<TestResult> RunAsync(TestRun run, IProgress<TestProgress> progress)
    {
        var orderedSteps = run.Recipe.Steps.OrderBy(x => x.Sequence).ToList();
        var totalMilliseconds = orderedSteps.Sum(x => x.DurationSeconds * 1000L);
        var completedMilliseconds = 0L;
        var completedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastPercent = 0;
        RecipeStep? currentStep = null;

        run.AddLog("INFO", $"테스트 시작 - 장비: {run.Equipment.Definition.Name}, 레시피: {run.Recipe.Name} ({run.Recipe.Version})");
        run.AddLog("INFO", $"레시피 파일: {run.RecipeSourcePath}");
        run.AddLog("INFO", run.Simulation.ShouldFail ? $"모의 결과: 실패 / 단계: {run.Simulation.FailedStepId}" : "모의 결과: 성공");

        try
        {
            foreach (var step in orderedSteps)
            {
                currentStep = step;
                run.Cancellation.Token.ThrowIfCancellationRequested();
                run.AddLog("INFO", $"단계 시작 [{step.Sequence}/{orderedSteps.Count}] {step.Name} ({step.Command})");
                var started = Environment.TickCount64;
                var durationMs = step.DurationSeconds * 1000L;

                while (true)
                {
                    run.Cancellation.Token.ThrowIfCancellationRequested();
                    var elapsed = Math.Min(Environment.TickCount64 - started, durationMs);
                    lastPercent = (int)Math.Clamp((completedMilliseconds + elapsed) * 100 / totalMilliseconds, 0, 100);
                    progress.Report(new TestProgress(run.Equipment.Definition.Id, step.Id, step.Name, lastPercent,
                        new HashSet<string>(completedIds, StringComparer.OrdinalIgnoreCase), null, $"{step.Name} 진행 중"));
                    if (elapsed >= durationMs) break;
                    await Task.Delay(100, run.Cancellation.Token);
                }

                completedMilliseconds += durationMs;
                lastPercent = (int)Math.Clamp(completedMilliseconds * 100 / totalMilliseconds, 0, 100);

                if (run.Simulation.ShouldFail && string.Equals(run.Simulation.FailedStepId, step.Id, StringComparison.OrdinalIgnoreCase))
                {
                    run.AddLog("ERROR", $"단계 실패: {step.Name} - 모의 장비 응답 오류");
                    progress.Report(new TestProgress(run.Equipment.Definition.Id, step.Id, step.Name, lastPercent,
                        new HashSet<string>(completedIds, StringComparer.OrdinalIgnoreCase), step.Id, $"{step.Name} 실패"));
                    return CreateResult(run, TestStatus.Failed, lastPercent, step, "모의 장비 응답에서 실패 결과를 수신했습니다.");
                }

                completedIds.Add(step.Id);
                run.AddLog("INFO", $"단계 완료: {step.Name}");
                progress.Report(new TestProgress(run.Equipment.Definition.Id, step.Id, step.Name, lastPercent,
                    new HashSet<string>(completedIds, StringComparer.OrdinalIgnoreCase), null, $"{step.Name} 완료"));
            }

            run.AddLog("INFO", "테스트가 성공적으로 완료되었습니다.");
            return CreateResult(run, TestStatus.Succeeded, 100, null, null);
        }
        catch (OperationCanceledException)
        {
            run.AddLog("WARN", $"사용자가 테스트를 취소했습니다. 진행률: {lastPercent}%");
            return CreateResult(run, TestStatus.Canceled, lastPercent, currentStep, "사용자가 테스트를 취소했습니다.");
        }
        catch (Exception ex)
        {
            run.AddLog("ERROR", $"예상하지 못한 오류: {ex.Message}");
            return CreateResult(run, TestStatus.Failed, lastPercent, currentStep, ex.Message);
        }
    }

    private static TestResult CreateResult(TestRun run, TestStatus status, int percent, RecipeStep? step, string? reason)
    {
        var finished = DateTimeOffset.Now;
        return new TestResult
        {
            Status = status,
            StartedAt = run.StartedAt,
            FinishedAt = finished,
            FinalProgressPercent = percent,
            FailedStepId = status == TestStatus.Failed ? step?.Id : null,
            FailedStepName = status == TestStatus.Failed ? step?.Name : null,
            FailureReason = reason,
            LogText = string.Join(Environment.NewLine, run.GetLogs())
        };
    }
}

public sealed class LogStore
{
    public string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeTestProject", "Logs");

    public async Task<string> SaveAsync(TestRun run, TestResult result)
    {
        Directory.CreateDirectory(LogDirectory);
        var fileName = $"{run.StartedAt:yyyyMMdd_HHmmss}_{Clean(run.Equipment.Definition.Id)}_{Clean(run.Recipe.RecipeId)}_{result.Status}.log";
        var path = Path.Combine(LogDirectory, fileName);
        await File.WriteAllTextAsync(path, result.LogText, new UTF8Encoding(true));
        result.LogFilePath = path;
        return path;
    }

    private static string Clean(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

public sealed class EquipmentStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeTestProject", "equipment-state.json");

    public IReadOnlyList<PersistedEquipmentState> Load()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            return JsonSerializer.Deserialize<List<PersistedEquipmentState>>(File.ReadAllText(_path), Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<EquipmentState> equipment)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var data = equipment.Select(x => new PersistedEquipmentState(x.Definition.Id, x.ConnectionStatus, x.LastConnectedAt)).ToList();
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(data, Options), Encoding.UTF8);
    }
}
