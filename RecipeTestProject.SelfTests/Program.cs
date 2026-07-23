using RecipeTestProject;
using System.Drawing;

var failures = new List<string>();

await CheckAsync("장비 카탈로그 샘플 로드", () =>
{
    var result = EquipmentCatalog.Load(Path.Combine("equipment"));
    Assert(result.Errors.Count == 0, $"샘플 장비 로드 오류: {string.Join(" | ", result.Errors)}");
    Assert(result.Equipment.Count == 6, "샘플 장비가 6대가 아닙니다.");
    var first = result.Equipment[0];
    Assert(first.Id == "WIS-A01", "첫 장비 ID가 다릅니다.");
    Assert(first.Manufacturer == "WIS Systems", "제조사 값이 다릅니다.");
    Assert(first.Model == "WIS-3000", "장비 모델이 다릅니다.");
    Assert(first.IpAddress == "192.168.10.101" && first.Port == 5001, "통신 주소가 다릅니다.");
    Assert(first.AccentColor == Color.FromArgb(33, 115, 186), "강조색이 다릅니다.");
    return Task.CompletedTask;
});

await CheckAsync("장비 카탈로그 부분 오류 허용", () =>
{
    var directory = CreateTemporaryDirectory();
    try
    {
        File.WriteAllText(Path.Combine(directory, "01-valid.json"), ValidEquipmentJson("TEST-01"));
        File.WriteAllText(Path.Combine(directory, "02-duplicate.json"), ValidEquipmentJson("test-01"));
        File.WriteAllText(Path.Combine(directory, "03-invalid-json.json"), "{ invalid");
        File.WriteAllText(Path.Combine(directory, "04-invalid-values.json"),
            ValidEquipmentJson("TEST-04")
                .Replace("\"192.168.10.1\"", "\"not-an-ip\"")
                .Replace("\"#2173BA\"", "\"blue\"")
                .Replace("\"port\": 5001", "\"port\": 70000"));

        var result = EquipmentCatalog.Load(directory);
        Assert(result.Equipment.Count == 1, "정상 장비만 로드되지 않았습니다.");
        Assert(result.Equipment[0].Id == "TEST-01", "파일명 순서에 따른 첫 중복 ID가 유지되지 않았습니다.");
        Assert(result.Errors.Count == 3, "잘못된 파일별 오류가 모두 보고되지 않았습니다.");
        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await CheckAsync("장비 카탈로그 폴더 없음", () =>
{
    var missing = Path.Combine(Path.GetTempPath(), $"RecipeTestProject-missing-{Guid.NewGuid():N}");
    var result = EquipmentCatalog.Load(missing);
    Assert(result.Equipment.Count == 0, "없는 폴더에서 장비가 로드되었습니다.");
    Assert(result.Errors.Count == 1, "없는 폴더 오류가 보고되지 않았습니다.");
    return Task.CompletedTask;
});

await CheckAsync("장비 이미지 경로 검증", () =>
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var imageDirectory = Path.Combine(directory, "images");
        Directory.CreateDirectory(imageDirectory);
        var imagePath = Path.Combine(imageDirectory, "equipment.png");
        using (var bitmap = new Bitmap(2, 2))
            bitmap.Save(imagePath);

        File.WriteAllText(Path.Combine(directory, "01-image.json"),
            WithImagePath(ValidEquipmentJson("IMAGE-01"), "images/equipment.png"));
        File.WriteAllText(Path.Combine(directory, "02-outside.json"),
            WithImagePath(ValidEquipmentJson("IMAGE-02"), "../outside.png"));

        var result = EquipmentCatalog.Load(directory);
        Assert(result.Equipment.Count == 2, "이미지 오류 때문에 장비 정의가 제외되었습니다.");
        Assert(result.Equipment[0].ImagePath == imagePath, "정상 이미지 경로가 해석되지 않았습니다.");
        Assert(result.Equipment[1].ImagePath is null, "폴더 밖 이미지 경로가 허용되었습니다.");
        Assert(result.Errors.Count == 1, "잘못된 이미지 경로 경고가 보고되지 않았습니다.");
        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

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
    var definition = EquipmentCatalog.Load(Path.Combine("equipment")).Equipment[0];
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

static string CreateTemporaryDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), $"RecipeTestProject-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
}

static string ValidEquipmentJson(string id) =>
    $$"""
    {
      "id": "{{id}}",
      "name": "테스트 장비",
      "manufacturer": "WIS Systems",
      "model": "WIS-3000",
      "ipAddress": "192.168.10.1",
      "port": 5001,
      "accentColor": "#2173BA"
    }
    """;

static string WithImagePath(string json, string imagePath)
{
    var closingBrace = json.LastIndexOf('}');
    return json.Insert(closingBrace, $",\n  \"imagePath\": \"{imagePath}\"\n");
}
