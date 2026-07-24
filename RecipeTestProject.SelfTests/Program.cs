using RecipeTestProject;

var failures = new List<string>();
var root = Directory.GetCurrentDirectory();
var products = ProductCatalog.Load(Path.Combine(root, "Products"));
var recipes = RecipeCatalog.Load(Path.Combine(root, "Recipes"));
var cells = TestCellCatalog.Load(Path.Combine(root, "equipment"));

await CheckAsync("EDS v3 카탈로그와 관계 검증", () =>
{
    Assert(products.Errors.Count == 0, string.Join(" | ", products.Errors));
    Assert(recipes.Errors.Count == 0, string.Join(" | ", recipes.Errors));
    Assert(cells.TestCells.Count == 2, "초기 Test Cell은 정확히 2대여야 합니다.");
    Assert(products.Products.Count == 2, "Product는 정확히 2개여야 합니다.");
    Assert(recipes.Recipes.Count == 2, "Recipe는 정확히 2개여야 합니다.");
    var relationships = CatalogRelationshipValidator.Validate(
        products.Products,
        recipes.Recipes,
        cells.TestCells);
    Assert(relationships.Count == 0, string.Join(" | ", relationships));
    Assert(products.Products.All(x => x.AcceptanceYieldPercent == 95),
        "두 Product의 합격 수율은 95%여야 합니다.");
    Assert(recipes.Recipes.All(x => x.Steps.Sum(s => s.DurationSeconds) == 24),
        "각 Recipe의 Wafer 실행 시간은 24초여야 합니다.");
    Assert(recipes.Recipes.All(x => x.FailBins.Count == 4),
        "각 Recipe에는 4개의 실패 Final Bin이 필요합니다.");
    return Task.CompletedTask;
});

await CheckAsync("v3 스키마 필수값과 중복 ID 거부", () =>
{
    AssertThrows(() => ProductCatalog.Validate(new ProductDocument { SchemaVersion = "2.0" }));
    AssertThrows(() => RecipeCatalog.Validate(new RecipeDocument { SchemaVersion = "2.0" }));
    AssertThrows(() => TestCellCatalog.Validate(new TestCellDefinition { SchemaVersion = "2.0" }));
    var duplicateProducts = new[]
    {
        products.Products[0],
        products.Products[0]
    };
    Assert(
        duplicateProducts.Select(x => x.ProductId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != duplicateProducts.Length,
        "중복 ID fixture가 잘못되었습니다.");
    return Task.CompletedTask;
});

await CheckAsync("Product → Recipe → Test Cell 호환 필터", () =>
{
    foreach (var product in products.Products)
    {
        var compatibleRecipes = recipes.Recipes
            .Where(x => product.AllowedRecipeIds.Contains(x.RecipeId, StringComparer.OrdinalIgnoreCase))
            .ToList();
        Assert(compatibleRecipes.Count == 1, $"{product.ProductId}에 호환 Recipe가 정확히 하나여야 합니다.");
        var compatibleCells = cells.TestCells.Where(cell =>
            compatibleRecipes[0].CompatibleTestCellIds.Contains(cell.Id, StringComparer.OrdinalIgnoreCase) &&
            cell.SupportedWaferDiametersMm.Contains(product.WaferDiameterMm) &&
            compatibleRecipes[0].RequiredCapabilities.All(capability =>
                cell.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))).ToList();
        Assert(compatibleCells.Count == 1, $"{product.ProductId}에 호환 Test Cell이 정확히 하나여야 합니다.");
    }
    return Task.CompletedTask;
});

await CheckAsync("동적 300/200 mm die geometry", () =>
{
    var memory = products.Products.Single(x => x.ProductId == "LPDDR5X-300");
    var mcu = products.Products.Single(x => x.ProductId == "AUTO-MCU-200");
    var memoryGrid = LotTestRunner.BuildGrid(memory);
    var mcuGrid = LotTestRunner.BuildGrid(mcu);
    Assert(memoryGrid.Count(x => x.IsValid) > mcuGrid.Count(x => x.IsValid),
        "300 mm Memory Wafer의 유효 die가 200 mm MCU보다 많아야 합니다.");
    Assert(memoryGrid.Any(x => !x.IsValid) && mcuGrid.Any(x => !x.IsValid),
        "원형 edge masking이 적용되지 않았습니다.");
    Assert(memoryGrid.All(x =>
            !x.IsValid ||
            x.CenterXmm * x.CenterXmm + x.CenterYmm * x.CenterYmm <= 147d * 147d),
        "300 mm edge exclusion 범위를 벗어난 die가 유효 처리됐습니다.");
    Assert(mcuGrid.All(x =>
            !x.IsValid ||
            x.CenterXmm * x.CenterXmm + x.CenterYmm * x.CenterYmm <= 97d * 97d),
        "200 mm edge exclusion 범위를 벗어난 die가 유효 처리됐습니다.");
    return Task.CompletedTask;
});

await CheckAsync("98% 수율, 균등 Bin, 결정적 결과", () =>
{
    var (job, _) = CreateJob("LPDDR5X-300");
    var first = new WaferResult { WaferId = "Wafer01" };
    var second = new WaferResult { WaferId = "Wafer01" };
    var distribution = job.Simulation!.DefaultFailBinDistribution;
    LotTestRunner.PopulateWaferResult(
        "RUN-DETERMINISTIC",
        first,
        job.ProductSnapshot,
        job.RecipeSnapshot,
        98,
        distribution,
        null);
    LotTestRunner.PopulateWaferResult(
        "RUN-DETERMINISTIC",
        second,
        job.ProductSnapshot,
        job.RecipeSnapshot,
        98,
        distribution,
        null);
    Assert(first.Disposition == WaferDisposition.Passed, "98% Wafer는 Passed여야 합니다.");
    Assert(Math.Abs(first.YieldPercent!.Value - 98) < .1, "유효 die에 가장 가까운 98%가 생성되지 않았습니다.");
    Assert(first.Dies.Select(x => x.FinalBinCode)
        .SequenceEqual(second.Dies.Select(x => x.FinalBinCode)),
        "동일 Run/Wafer 결과가 결정적으로 재생성되지 않았습니다.");
    var failCounts = first.BinCounts
        .Where(x => !string.Equals(x.Key, "PASS", StringComparison.OrdinalIgnoreCase))
        .Select(x => x.Value)
        .ToList();
    Assert(failCounts.Max() - failCounts.Min() <= 1, "기본 실패 Bin이 균등 배분되지 않았습니다.");
    return Task.CompletedTask;
});

await CheckAsync("대표 Final Bin 60% 배정", () =>
{
    var (job, _) = CreateJob("AUTO-MCU-200");
    var wafer = new WaferResult { WaferId = "Wafer07" };
    var dominant = job.RecipeSnapshot.FailBins[0].Code;
    LotTestRunner.PopulateWaferResult(
        "RUN-DOMINANT",
        wafer,
        job.ProductSnapshot,
        job.RecipeSnapshot,
        80,
        job.Simulation!.DefaultFailBinDistribution,
        dominant);
    var fail = wafer.ValidDieCount - wafer.PassDieCount;
    var expected = (int)Math.Round(fail * .6, MidpointRounding.AwayFromZero);
    Assert(wafer.BinCounts.GetValueOrDefault(dominant) == expected,
        "대표 Final Bin이 실패 die의 60%로 배정되지 않았습니다.");
    Assert(wafer.Disposition == WaferDisposition.LowYield, "80% Wafer는 Low Yield여야 합니다.");
    return Task.CompletedTask;
});

await CheckAsync("25장 완료와 Low Yield 이후 계속 실행", async () =>
{
    var (job, cell) = CreateJob("LPDDR5X-300");
    job.Simulation!.SpeedFactor = 1000;
    job.Simulation.Wafers[2] = new WaferSimulationSetting
    {
        WaferId = "Wafer03",
        UseLotDefault = false,
        TargetYieldPercent = 90,
        DominantFailBinCode = job.RecipeSnapshot.FailBins[1].Code
    };
    var result = LotTestRunner.CreateRun(job, "RUN-COMPLETE");
    cell.ActiveJobId = job.JobId;
    await new LotTestRunner().RunAsync(
        job,
        cell,
        result,
        new Progress<LotRunProgress>(),
        _ => Task.CompletedTask,
        CancellationToken.None);
    Assert(result.Status == JobStatus.Completed && result.CompletedCount == 25,
        "25장 Run이 완료되지 않았습니다.");
    Assert(result.LowYieldWaferCount == 1, "Low Yield Wafer 수가 잘못되었습니다.");
    Assert(result.Wafers[24].Status == WaferExecutionStatus.Completed,
        "Low Yield 이후 Wafer 검사가 계속되지 않았습니다.");
    Assert(result.LotYieldPercent is > 95 and < 98, "Lot 집계 수율이 올바르지 않습니다.");
});

await CheckAsync("Tester/Prober/Probe Card 오류와 수동 리셋", async () =>
{
    foreach (var component in Enum.GetValues<TestCellComponent>())
    {
        var (job, cell) = CreateJob("AUTO-MCU-200");
        job.Simulation!.SpeedFactor = 1000;
        var step = job.RecipeSnapshot.Steps.First(x => x.AllowedErrorComponents.Contains(component));
        job.Simulation.CellError = new CellErrorSimulation
        {
            Enabled = true,
            Component = component,
            WaferId = "Wafer03",
            FailedStepId = step.Id
        };
        var result = LotTestRunner.CreateRun(job, $"RUN-ERROR-{component}");
        cell.ActiveJobId = job.JobId;
        await new LotTestRunner().RunAsync(
            job,
            cell,
            result,
            new Progress<LotRunProgress>(),
            _ => Task.CompletedTask,
            CancellationToken.None);
        Assert(result.Status == JobStatus.Failed, $"{component} 오류 Run이 Failed가 아닙니다.");
        Assert(result.Wafers[2].Status == WaferExecutionStatus.EquipmentError,
            $"{component} 오류 Wafer 상태가 잘못되었습니다.");
        Assert(result.Wafers[3].Status == WaferExecutionStatus.NotRun,
            $"{component} 오류 이후 Wafer가 NotRun이 아닙니다.");
        Assert(cell.ErrorComponent == component, $"{component} 오류가 Cell에 유지되지 않았습니다.");
        cell.ActiveJobId = null;
        Assert(JobStartValidator.GetBlockReason(job, cell)?.Contains("리셋") == true,
            "오류 Cell의 시작 차단 사유가 없습니다.");
        cell.ResetError();
        Assert(!cell.HasError, "수동 오류 리셋에 실패했습니다.");
    }
});

await CheckAsync("연결·점유 상태 시작 차단", () =>
{
    var (job, cell) = CreateJob("LPDDR5X-300");
    Assert(JobStartValidator.GetBlockReason(job, cell) is null, "유휴 Cell에서 시작이 차단됐습니다.");
    cell.ConnectionStatus = ConnectionStatus.Disconnected;
    Assert(JobStartValidator.GetBlockReason(job, cell)?.Contains("연결") == true,
        "연결 해제 Cell이 차단되지 않았습니다.");
    cell.ConnectionStatus = ConnectionStatus.Connected;
    cell.ActiveJobId = "JOB-OTHER";
    Assert(JobStartValidator.GetBlockReason(job, cell)?.Contains("실행 중") == true,
        "사용 중 Cell이 차단되지 않았습니다.");
    return Task.CompletedTask;
});

await CheckAsync("사용자 취소 시 완료 결과 보존", async () =>
{
    var (job, cell) = CreateJob("AUTO-MCU-200");
    job.Simulation!.SpeedFactor = 100;
    using var cancellation = new CancellationTokenSource(350);
    var result = LotTestRunner.CreateRun(job, "RUN-CANCEL");
    await new LotTestRunner().RunAsync(
        job,
        cell,
        result,
        new Progress<LotRunProgress>(),
        _ => Task.CompletedTask,
        cancellation.Token);
    Assert(result.Status == JobStatus.Canceled, "Run이 Canceled가 아닙니다.");
    Assert(result.Wafers.All(x => x.Status != WaferExecutionStatus.Pending),
        "취소 후 Pending Wafer가 남았습니다.");
});

await CheckAsync("Job/체크포인트 저장과 누락 결과 처리", async () =>
{
    var temp = CreateTemp();
    try
    {
        var store = new JobStore(temp);
        var (job, _) = CreateJob("LPDDR5X-300");
        await store.SaveAsync([job]);
        var loaded = store.Load();
        Assert(loaded.Count == 1 && loaded[0].TestCellSnapshot.Tester.Model == "T5503HS2",
            "Job과 Test Cell 스냅샷을 복원하지 못했습니다.");

        var artifacts = new RunArtifactStore(temp);
        var result = LotTestRunner.CreateRun(job, "RUN-STORE");
        result.Status = JobStatus.Canceled;
        await artifacts.SaveCheckpointAsync(result);
        var path = artifacts.GetResultPath(job.JobId, result.RunId);
        Assert(artifacts.Load(path)?.Status == JobStatus.Canceled,
            "Run 체크포인트를 복원하지 못했습니다.");
        File.Delete(path);
        Assert(artifacts.Load(path) is null, "누락 결과 파일이 정상 결과로 처리됐습니다.");
    }
    finally
    {
        Directory.Delete(temp, true);
    }
});

await CheckAsync("앱 시작 시 기존 데이터 보존", () =>
{
    var temp = CreateTemp();
    try
    {
        var jobsPath = Path.Combine(temp, "jobs.json");
        File.WriteAllText(jobsPath, "legacy");
        var results = Path.Combine(temp, "Results");
        Directory.CreateDirectory(results);
        var resultPath = Path.Combine(results, "existing.json");
        File.WriteAllText(resultPath, "{}");

        var store = new JobStore(temp);
        _ = store.Load();

        Assert(File.Exists(jobsPath), "앱 시작 시 기존 jobs.json이 삭제됐습니다.");
        Assert(File.Exists(resultPath), "앱 시작 시 기존 결과 파일이 삭제됐습니다.");
    }
    finally
    {
        Directory.Delete(temp, true);
    }
    return Task.CompletedTask;
});

await CheckAsync("공식 Test Cell 이미지 자산", () =>
{
    foreach (var cell in cells.TestCells)
    {
        foreach (var component in new[] { cell.Tester, cell.Prober })
        {
            Assert(
                !string.IsNullOrWhiteSpace(component.ImagePath) && File.Exists(component.ImagePath),
                $"{cell.Id} {component.Model} 공식 이미지 자산이 없습니다.");
        }
    }
    return Task.CompletedTask;
});

await CheckAsync("완료 Run PDF 생성", async () =>
{
    var output = Path.Combine(root, "output", "pdf");
    Directory.CreateDirectory(output);
    var (job, _) = CreateJob("LPDDR5X-300");
    job.JobId = "JOB-EDS-SAMPLE";
    job.LotId = "LOT-EDS-SAMPLE-001";
    var result = LotTestRunner.CreateRun(job, "RUN-EDS-SAMPLE");
    for (var index = 0; index < result.Wafers.Count; index++)
    {
        var target = index is 4 or 14 ? 92 : 98;
        LotTestRunner.PopulateWaferResult(
            result.RunId,
            result.Wafers[index],
            job.ProductSnapshot,
            job.RecipeSnapshot,
            target,
            job.Simulation!.DefaultFailBinDistribution,
            target < 95 ? job.RecipeSnapshot.FailBins[index == 4 ? 0 : 2].Code : null);
    }
    result.Status = JobStatus.Completed;
    result.ProgressPercent = 100;
    result.FinishedAt = DateTimeOffset.Now;
    var path = Path.Combine(output, "sample-eds-lot-report.pdf");
    await new ReportService().GenerateAsync(job, result, path);
    Assert(File.Exists(path) && new FileInfo(path).Length > 20_000,
        "EDS PDF 보고서가 정상 생성되지 않았습니다.");
});

if (failures.Count == 0)
{
    Console.WriteLine("모든 EDS 자체 검증을 통과했습니다.");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
return 1;

async Task CheckAsync(string name, Func<Task> action)
{
    try
    {
        await action();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL  {name}: {ex.Message}");
    }
}

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

void AssertThrows(Action action)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new InvalidOperationException("유효하지 않은 v3 문서가 허용되었습니다.");
}

(InspectionJob Job, TestCellState Cell) CreateJob(string productId)
{
    var product = products.Products.Single(x => x.ProductId == productId);
    var recipe = recipes.Recipes.Single(x =>
        product.AllowedRecipeIds.Contains(x.RecipeId, StringComparer.OrdinalIgnoreCase));
    var definition = cells.TestCells.Single(x =>
        recipe.CompatibleTestCellIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase));
    var cell = new TestCellState(definition)
    {
        ConnectionStatus = ConnectionStatus.Connected,
        LastConnectedAt = DateTimeOffset.Now
    };
    var job = new InspectionJob
    {
        JobId = $"JOB-SELFTEST-{productId}",
        CustomerName = "SelfTest Customer",
        RequestNumber = "REQ-001",
        LotId = $"LOT-{productId}",
        ProductSnapshot = product,
        RecipeSnapshot = recipe,
        TestCellId = definition.Id,
        TestCellSnapshot = definition,
        CreatedAt = DateTimeOffset.Now,
        Simulation = JobSimulationSettings.CreateDefault(recipe)
    };
    return (job, cell);
}

string CreateTemp()
{
    var path = Path.Combine(Path.GetTempPath(), $"EdsWaferSort-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
}
