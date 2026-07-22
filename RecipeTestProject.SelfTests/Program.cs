using RecipeTestProject;

var failures = new List<string>();

await CheckAsync("정상 레시피 파싱", () =>
{
    var service = new RecipeService();
    var wis3000 = service.LoadAndValidate(Path.Combine("SampleRecipes", "WIS-3000_Standard_Inspection.json"));
    var wis5000 = service.LoadAndValidate(Path.Combine("SampleRecipes", "WIS-5000_High_Resolution_Inspection.json"));
    Assert(wis3000.Recipe.TargetEquipmentModel == "WIS-3000", "WIS-3000 대상 모델이 다릅니다.");
    Assert(wis5000.Recipe.TargetEquipmentModel == "WIS-5000", "WIS-5000 대상 모델이 다릅니다.");
    Assert(wis3000.Recipe.Steps.Sum(x => x.DurationSeconds) is >= 15 and <= 25, "샘플 실행 시간 범위를 벗어났습니다.");
    return Task.CompletedTask;
});

await CheckAsync("잘못된 JSON 차단", () =>
{
    var service = new RecipeService();
    try
    {
        service.LoadAndValidate(Path.Combine("SampleRecipes", "ERROR_Invalid_JSON.json"));
        throw new InvalidOperationException("잘못된 JSON이 허용되었습니다.");
    }
    catch (RecipeValidationException)
    {
        return Task.CompletedTask;
    }
});

await CheckAsync("단계 검증", () =>
{
    var recipe = CreateRecipe(1);
    recipe.Steps.Add(new RecipeStep { Sequence = 1, Id = "DUPLICATE", Name = "중복", Command = "Scan", DurationSeconds = 0 });
    try
    {
        new RecipeService().Validate(recipe);
        throw new InvalidOperationException("중복 순서와 0초 단계가 허용되었습니다.");
    }
    catch (RecipeValidationException)
    {
        return Task.CompletedTask;
    }
});

await CheckAsync("성공 실행", async () =>
{
    var run = CreateRun(CreateRecipe(1), new TestSimulationSettings(false, null));
    var result = await new MockTestRunner().RunAsync(run, new Progress<TestProgress>());
    Assert(result.Status == TestStatus.Succeeded, "성공 상태가 아닙니다.");
    Assert(result.FinalProgressPercent == 100, "성공 진행률이 100%가 아닙니다.");
});

await CheckAsync("지정 단계 실패", async () =>
{
    var recipe = CreateRecipe(1);
    var run = CreateRun(recipe, new TestSimulationSettings(true, recipe.Steps[0].Id));
    var result = await new MockTestRunner().RunAsync(run, new Progress<TestProgress>());
    Assert(result.Status == TestStatus.Failed, "실패 상태가 아닙니다.");
    Assert(result.FailedStepId == recipe.Steps[0].Id, "실패 단계가 다릅니다.");
});

await CheckAsync("실행 취소", async () =>
{
    var run = CreateRun(CreateRecipe(2), new TestSimulationSettings(false, null));
    var execution = new MockTestRunner().RunAsync(run, new Progress<TestProgress>());
    await Task.Delay(150);
    run.Cancellation.Cancel();
    var result = await execution;
    Assert(result.Status == TestStatus.Canceled, "취소 상태가 아닙니다.");
    Assert(result.FinalProgressPercent < 100, "취소 진행률이 100%입니다.");
});

if (failures.Count == 0)
{
    Console.WriteLine("모든 서비스 검증을 통과했습니다.");
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

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static RecipeDocument CreateRecipe(int durationSeconds) => new()
{
    SchemaVersion = "1.0",
    RecipeId = "SELF-TEST",
    Name = "서비스 검증 레시피",
    Version = "1.0",
    TargetEquipmentModel = "WIS-3000",
    Author = "SelfTest",
    CreatedAt = DateTimeOffset.Now,
    Wafer = new WaferSettings { DiameterMm = 300, Material = "Silicon", LotId = "TEST" },
    Inspection = new InspectionSettings { ScanMode = "FullWafer", ResolutionMicrometer = 1, DefectThresholdMicrometer = 1, EdgeExclusionMm = 0 },
    Steps =
    [
        new RecipeStep { Sequence = 1, Id = "SCAN", Name = "검사", Command = "ScanSurface", DurationSeconds = durationSeconds }
    ]
};

static TestRun CreateRun(RecipeDocument recipe, TestSimulationSettings simulation)
{
    var definition = EquipmentCatalog.Create()[0];
    return new TestRun
    {
        RunId = Guid.NewGuid().ToString("N"),
        Equipment = new EquipmentState(definition) { ConnectionStatus = ConnectionStatus.Connected },
        Recipe = recipe,
        RecipeSourcePath = "self-test.json",
        Simulation = simulation,
        StartedAt = DateTimeOffset.Now
    };
}
