using RecipeTestProject;

var failures = new List<string>();
var root = Directory.GetCurrentDirectory();

await CheckAsync("v2 Product/Recipe 카탈로그", () =>
{
    var products = ProductCatalog.Load(Path.Combine(root, "Products"));
    var recipes = RecipeCatalog.Load(Path.Combine(root, "Recipes"));
    Assert(products.Errors.Count == 0, string.Join(" | ", products.Errors));
    Assert(recipes.Errors.Count == 0, string.Join(" | ", recipes.Errors));
    Assert(products.Products.Count >= 2, "Product 샘플이 부족합니다.");
    Assert(recipes.Recipes.Count >= 2, "Recipe 샘플이 부족합니다.");
    Assert(products.Products.All(p => p.AllowedRecipeIds.All(id => recipes.Recipes.Any(r => r.RecipeId == id))),
        "Product가 존재하지 않는 Recipe를 참조합니다.");
    return Task.CompletedTask;
});

await CheckAsync("장비 카탈로그는 생산 파일 개수에 의존하지 않음", () =>
{
    var equipment = EquipmentCatalog.Load(Path.Combine(root, "equipment"));
    Assert(equipment.Equipment.Count > 0, "장비를 로드하지 못했습니다.");
    Assert(equipment.Equipment.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == equipment.Equipment.Count,
        "장비 ID가 중복됩니다.");
    return Task.CompletedTask;
});

await CheckAsync("v2 스키마 검증", () =>
{
    AssertThrows(() => ProductCatalog.Validate(new ProductDocument { SchemaVersion = "1.0" }));
    AssertThrows(() => RecipeCatalog.Validate(new RecipeDocument { SchemaVersion = "1.0" }));
    return Task.CompletedTask;
});

await CheckAsync("21x21 원형 웨이퍼 맵", () =>
{
    var grid = LotTestRunner.BuildGrid();
    Assert(grid.Count == 441, "21x21 셀 수가 아닙니다.");
    Assert(grid.Count(x => x.IsValid) is > 300 and < 400, "원형 유효 다이 수가 예상 범위를 벗어났습니다.");
    Assert(!grid.Single(x => x.Row == 0 && x.Column == 0).IsValid, "모서리 셀이 유효합니다.");
    Assert(grid.Single(x => x.Row == 10 && x.Column == 10).IsValid, "중앙 셀이 유효하지 않습니다.");
    return Task.CompletedTask;
});

await CheckAsync("정상·NG 수준과 결정적 결과", () =>
{
    var normal = new WaferResult { WaferId = "Wafer01" };
    LotTestRunner.PopulateWaferResult("RUN-A", normal, new WaferSimulationSetting { WaferId = normal.WaferId }, 98);
    Assert(normal.Status == WaferExecutionStatus.Normal && normal.YieldPercent == 100 && normal.Defects.Count == 0,
        "정상 결과는 100%/결함 0이어야 합니다.");

    var counts = new List<int>();
    foreach (var level in new[] { DefectLevel.Low, DefectLevel.Medium, DefectLevel.High })
    {
        var first = new WaferResult { WaferId = "Wafer02" };
        var second = new WaferResult { WaferId = "Wafer02" };
        var setting = new WaferSimulationSetting { WaferId = first.WaferId, Outcome = SimulationOutcome.Ng, DefectLevel = level };
        LotTestRunner.PopulateWaferResult("RUN-DETERMINISTIC", first, setting, 98);
        LotTestRunner.PopulateWaferResult("RUN-DETERMINISTIC", second, setting, 98);
        Assert(first.Defects.Select(x => (x.Row, x.Column, x.Type)).SequenceEqual(second.Defects.Select(x => (x.Row, x.Column, x.Type))),
            "동일 Run/Wafer 결과가 재현되지 않습니다.");
        counts.Add(first.Defects.Count);
    }
    Assert(counts[0] < counts[1] && counts[1] < counts[2], "결함 수준에 따라 결함 수가 증가하지 않습니다.");
    return Task.CompletedTask;
});

await CheckAsync("25장 순차 완료와 NG 계속 검사", async () =>
{
    var (job, equipment) = CreateJob();
    job.Simulation!.Wafers[2].Outcome = SimulationOutcome.Ng;
    job.Simulation.Wafers[2].DefectLevel = DefectLevel.Medium;
    var result = LotTestRunner.CreateRun(job, equipment.Definition, "RUN-COMPLETE");
    await new LotTestRunner().RunAsync(job, equipment, result, new Progress<LotRunProgress>(), _ => Task.CompletedTask, CancellationToken.None);
    Assert(result.Status == JobStatus.Completed && result.CompletedCount == 25, "25장이 완료되지 않았습니다.");
    Assert(result.NgCount == 1 && result.Wafers[24].Status == WaferExecutionStatus.Normal, "NG 이후 검사가 계속되지 않았습니다.");
});

await CheckAsync("장비 오류 즉시 중단", async () =>
{
    var (job, equipment) = CreateJob();
    job.Simulation!.Wafers[2] = new WaferSimulationSetting
    {
        WaferId = "Wafer03", Outcome = SimulationOutcome.EquipmentError, FailedStepId = "SCAN"
    };
    var result = LotTestRunner.CreateRun(job, equipment.Definition, "RUN-ERROR");
    await new LotTestRunner().RunAsync(job, equipment, result, new Progress<LotRunProgress>(), _ => Task.CompletedTask, CancellationToken.None);
    Assert(result.Status == JobStatus.Failed, "Run이 실패하지 않았습니다.");
    Assert(result.CompletedCount == 2, "오류 이전 완료 Wafer 수가 잘못되었습니다.");
    Assert(result.Wafers[2].Status == WaferExecutionStatus.EquipmentError, "오류 Wafer 상태가 잘못되었습니다.");
    Assert(result.Wafers[3].Status == WaferExecutionStatus.NotRun, "오류 이후 Wafer가 미실행이 아닙니다.");
});

await CheckAsync("취소 시 완료 결과 보존", async () =>
{
    var (job, equipment) = CreateJob();
    using var cancellation = new CancellationTokenSource(250);
    var result = LotTestRunner.CreateRun(job, equipment.Definition, "RUN-CANCEL");
    await new LotTestRunner().RunAsync(job, equipment, result, new Progress<LotRunProgress>(), _ => Task.CompletedTask, cancellation.Token);
    Assert(result.Status == JobStatus.Canceled, "Run이 취소 상태가 아닙니다.");
    Assert(result.Wafers.All(x => x.Status != WaferExecutionStatus.Pending), "취소 후 Pending Wafer가 남았습니다.");
});

await CheckAsync("Job 원자 저장과 결과 복원", async () =>
{
    var temp = CreateTemp();
    try
    {
        var store = new JobStore(temp);
        var (job, _) = CreateJob();
        await store.SaveAsync([job]);
        var loaded = store.Load();
        Assert(loaded.Count == 1 && loaded[0].LotId == job.LotId, "Job을 복원하지 못했습니다.");

        var artifacts = new RunArtifactStore(temp);
        var result = new JobRunResult { JobId = job.JobId, RunId = "RUN-STORE", Status = JobStatus.Canceled };
        await artifacts.SaveCheckpointAsync(result);
        Assert(artifacts.Load(artifacts.GetResultPath(job.JobId, result.RunId))?.Status == JobStatus.Canceled,
            "Run 결과를 복원하지 못했습니다.");
    }
    finally { Directory.Delete(temp, true); }
});

await CheckAsync("완료 Run PDF 생성", async () =>
{
    var output = Path.Combine(root, "output", "pdf");
    Directory.CreateDirectory(output);
    var (job, equipment) = CreateJob();
    job.JobId = "JOB-SAMPLE-REPORT";
    job.LotId = "LOT-SAMPLE-001";
    var result = LotTestRunner.CreateRun(job, equipment.Definition, "RUN-SAMPLE-REPORT");
    for (var index = 0; index < result.Wafers.Count; index++)
    {
        var setting = index == 4
            ? new WaferSimulationSetting { WaferId = result.Wafers[index].WaferId, Outcome = SimulationOutcome.Ng, DefectLevel = DefectLevel.Low }
            : new WaferSimulationSetting { WaferId = result.Wafers[index].WaferId };
        LotTestRunner.PopulateWaferResult(result.RunId, result.Wafers[index], setting, job.ProductSnapshot.AcceptanceYieldPercent);
    }
    result.Status = JobStatus.Completed; result.ProgressPercent = 100; result.FinishedAt = DateTimeOffset.Now;
    var path = Path.Combine(output, "sample-lot-report.pdf");
    await new ReportService().GenerateAsync(job, result, path);
    Assert(File.Exists(path) && new FileInfo(path).Length > 10_000, "PDF 보고서가 정상 생성되지 않았습니다.");
});

if (failures.Count == 0)
{
    Console.WriteLine("모든 자체 검증을 통과했습니다.");
    return 0;
}
foreach (var failure in failures) Console.Error.WriteLine(failure);
return 1;

async Task CheckAsync(string name, Func<Task> action)
{
    try { await action(); Console.WriteLine($"PASS  {name}"); }
    catch (Exception ex) { failures.Add($"FAIL  {name}: {ex.Message}"); }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertThrows(Action action)
{
    try { action(); }
    catch (InvalidDataException) { return; }
    throw new InvalidOperationException("유효하지 않은 문서가 허용되었습니다.");
}

static (InspectionJob Job, EquipmentState Equipment) CreateJob()
{
    var recipe = new RecipeDocument
    {
        SchemaVersion = "2.0", RecipeId = "RCP-TEST", Name = "자체 검증 레시피", Version = "2.0.0",
        Author = "SelfTest", CreatedAt = DateTimeOffset.Now, CompatibleEquipmentModels = ["WIS-TEST"],
        Steps = [new RecipeStep { Sequence = 1, Id = "SCAN", Name = "표면 스캔", Command = "Scan", DurationSeconds = 1 }]
    };
    var product = new ProductDocument
    {
        SchemaVersion = "2.0", ProductId = "PRODUCT-TEST", Name = "자체 검증 제품", WaferDiameterMm = 300,
        Material = "Silicon", AcceptanceYieldPercent = 98, AllowedRecipeIds = [recipe.RecipeId]
    };
    var equipment = new EquipmentState(new EquipmentDefinition
    {
        Id = "EQ-TEST", Name = "자체 검증 장비", Manufacturer = "Test", Model = "WIS-TEST",
        IpAddress = "127.0.0.1", Port = 5001
    }) { ConnectionStatus = ConnectionStatus.Connected };
    var job = new InspectionJob
    {
        JobId = "JOB-SELFTEST", CustomerName = "SelfTest Customer", RequestNumber = "REQ-001", LotId = "LOT-001",
        ProductSnapshot = product, RecipeSnapshot = recipe, EquipmentId = equipment.Definition.Id,
        CreatedAt = DateTimeOffset.Now, Simulation = JobSimulationSettings.CreateDefault()
    };
    return (job, equipment);
}

static string CreateTemp()
{
    var path = Path.Combine(Path.GetTempPath(), $"RecipeTestProject-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
}
