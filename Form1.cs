using System.Diagnostics;
using System.Text.Json;

namespace RecipeTestProject;

public partial class Form1 : Form
{
    private readonly EquipmentCatalogLoadResult _equipmentCatalog;
    private readonly ProductCatalogLoadResult _productCatalog;
    private readonly RecipeCatalogLoadResult _recipeCatalog;
    private readonly List<EquipmentState> _equipment = [];
    private readonly List<InspectionJob> _jobs;
    private readonly MockConnectionService _connectionService = new();
    private readonly EquipmentStateStore _equipmentStore = new();
    private readonly JobStore _jobStore = new();
    private readonly RunArtifactStore _artifacts;
    private readonly LotTestRunner _runner = new();
    private readonly ReportService _reports = new();
    private readonly Dictionary<string, CancellationTokenSource> _runCancellations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _runTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JobRunResult> _activeRunResults = new(StringComparer.OrdinalIgnoreCase);

    private Panel _contentHost = null!;
    private Label _banner = null!;
    private System.Windows.Forms.Timer _bannerTimer = null!;
    private Button _jobsNavigationButton = null!;
    private Button _equipmentNavigationButton = null!;
    private Control? _currentView;
    private bool _allowClose;
    private bool _closing;

    public Form1()
    {
        _equipmentCatalog = EquipmentCatalog.Load(Path.Combine(AppContext.BaseDirectory, "equipment"));
        _productCatalog = ProductCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Products"));
        _recipeCatalog = RecipeCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Recipes"));
        _jobs = _jobStore.Load();
        _artifacts = new RunArtifactStore(_jobStore.RootDirectory);

        InitializeComponent();
        Font = new Font("맑은 고딕", 9F);
        BackColor = AppTheme.Background;
        BuildShell();
        RestoreEquipment();
        ShowJobList();
        Shown += async (_, _) =>
        {
            await RecoverInterruptedRunsAsync();
            ShowCatalogErrors();
        };
    }

    private void BuildShell()
    {
        var menu = new MenuStrip { Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(12, 5, 0, 5) };
        var equipmentMenu = new ToolStripMenuItem("장비");
        var connect = new ToolStripMenuItem("장비 연결");
        connect.Click += async (_, _) => await AddEquipmentAsync();
        equipmentMenu.DropDownItems.Add(connect);
        var viewMenu = new ToolStripMenuItem("보기");
        var results = new ToolStripMenuItem("결과 폴더 열기");
        results.Click += (_, _) => OpenResultsFolder();
        viewMenu.DropDownItems.Add(results);
        menu.Items.Add(equipmentMenu); menu.Items.Add(viewMenu);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 205, BackColor = Color.FromArgb(35, 48, 61), Padding = new Padding(12, 22, 12, 12) };
        var product = new Label
        {
            Text = "WAFER INSPECT\r\nTEST CENTER", Dock = DockStyle.Top, Height = 76, ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold), Padding = new Padding(8, 4, 0, 0)
        };
        _equipmentNavigationButton = SidebarButton("▣   장비 목록");
        _equipmentNavigationButton.Click += (_, _) => ShowEquipmentList();
        _jobsNavigationButton = SidebarButton("▦   전체 작업");
        _jobsNavigationButton.Click += (_, _) => ShowJobList();
        sidebar.Controls.Add(_equipmentNavigationButton); sidebar.Controls.Add(_jobsNavigationButton); sidebar.Controls.Add(product);

        _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
        _banner = new Label
        {
            Dock = DockStyle.Top, Height = 44, Visible = false, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0), ForeColor = Color.White, Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold)
        };
        _contentHost.Controls.Add(_banner);
        body.Controls.Add(_contentHost); body.Controls.Add(sidebar);
        Controls.Add(body); Controls.Add(menu); MainMenuStrip = menu;

        _bannerTimer = new System.Windows.Forms.Timer { Interval = 4500 };
        _bannerTimer.Tick += (_, _) => { _bannerTimer.Stop(); _banner.Visible = false; };
    }

    private static Button SidebarButton(string text)
    {
        var button = new Button
        {
            Text = text, Dock = DockStyle.Top, Height = 48, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(35, 48, 61), ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand, Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void RestoreEquipment()
    {
        foreach (var saved in _equipmentStore.Load())
        {
            var definition = _equipmentCatalog.Equipment.FirstOrDefault(x => string.Equals(x.Id, saved.EquipmentId, StringComparison.OrdinalIgnoreCase));
            if (definition is null) continue;
            _equipment.Add(new EquipmentState(definition)
            {
                ConnectionStatus = saved.ConnectionStatus == ConnectionStatus.Connecting ? ConnectionStatus.Disconnected : saved.ConnectionStatus,
                LastConnectedAt = saved.LastConnectedAt
            });
        }
    }

    private void ShowJobList()
    {
        var view = new JobListView();
        view.SetJobs(_jobs, FindEquipment);
        view.CreateRequested += ShowJobCreate;
        view.JobActivated += ShowJobDetail;
        SetContent(view);
    }

    private void ShowJobCreate()
    {
        var view = new JobCreateView(_productCatalog.Products, _recipeCatalog.Recipes, _equipment);
        view.CancelRequested += ShowJobList;
        view.EquipmentListRequested += ShowEquipmentList;
        view.CreateRequested += async request => await CreateJobAsync(request);
        SetContent(view);
    }

    private async Task CreateJobAsync(JobCreationRequest request)
    {
        var job = new InspectionJob
        {
            JobId = $"JOB-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..25],
            CustomerName = request.CustomerName,
            RequestNumber = request.RequestNumber,
            LotId = request.LotId,
            ProductSnapshot = Clone(request.Product),
            RecipeSnapshot = Clone(request.Recipe),
            EquipmentId = request.Equipment.Definition.Id,
            CreatedAt = DateTimeOffset.Now,
            Status = JobStatus.Pending
        };
        _jobs.Add(job);
        await _jobStore.SaveAsync(_jobs);
        ShowJobDetail(job);
        ShowBanner($"{job.LotId} Job을 생성했습니다.", JobStatus.Completed);
    }

    private void ShowJobDetail(InspectionJob job)
    {
        var view = new JobDetailView(job, FindEquipment(job.EquipmentId));
        view.BackRequested += ShowJobList;
        view.ConfigureRequested += () => ShowSimulationSettings(job);
        view.StartRequested += async () => await StartJobAsync(job);
        view.DeleteRequested += async () => await DeleteJobAsync(job);
        view.RunActivated += run => ShowRunResult(job, run);
        SetContent(view);
    }

    private void ShowSimulationSettings(InspectionJob job)
    {
        if (job.Status == JobStatus.Running) return;
        var view = new SimulationSettingsView(job);
        view.CancelRequested += () => ShowJobDetail(job);
        view.SaveRequested += async settings =>
        {
            job.Simulation = settings;
            await _jobStore.SaveAsync(_jobs);
            ShowJobDetail(job);
            ShowBanner("모의 결과 설정을 저장했습니다.", JobStatus.Completed);
        };
        SetContent(view);
    }

    private async Task StartJobAsync(InspectionJob job)
    {
        if (job.Simulation is null) { ShowJobDetail(job); return; }
        var equipment = FindEquipment(job.EquipmentId);
        if (equipment is null || equipment.ConnectionStatus != ConnectionStatus.Connected)
        {
            MessageBox.Show(this, "배정 장비가 연결되어 있지 않습니다.", "테스트 시작", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (equipment.IsBusy)
        {
            MessageBox.Show(this, $"배정 장비가 {equipment.ActiveJobId} Job을 실행 중입니다.", "테스트 시작", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var runId = $"RUN-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..25];
        var result = LotTestRunner.CreateRun(job, equipment.Definition, runId);
        var summary = new JobRunSummary
        {
            RunId = runId, Status = JobStatus.Running, StartedAt = result.StartedAt,
            ResultFilePath = _artifacts.GetResultPath(job.JobId, runId),
            LogFilePath = _artifacts.GetLogPath(job.JobId, runId)
        };
        job.Runs.Add(summary);
        job.Status = JobStatus.Running; job.ProgressPercent = 0; job.CurrentWaferId = "Wafer01"; job.HasNgWafers = false;
        equipment.ActiveJobId = job.JobId; equipment.ProgressPercent = 0; equipment.CurrentWaferId = "Wafer01";
        _activeRunResults[runId] = result;
        await SaveCheckpointAsync(job, summary, result);

        ShowJobProgress(job, result);

        var cancellation = new CancellationTokenSource();
        _runCancellations[job.JobId] = cancellation;
        var progress = new Progress<LotRunProgress>(update =>
        {
            job.ProgressPercent = update.OverallPercent; job.CurrentWaferId = update.WaferId;
            equipment.ProgressPercent = update.OverallPercent; equipment.CurrentWaferId = update.WaferId;
            RefreshCurrentLiveView(update);
        });
        var execution = ExecuteRunAsync(job, equipment, result, summary, progress, cancellation);
        _runTasks[job.JobId] = execution;
        try { await execution; }
        finally
        {
            _runTasks.Remove(job.JobId);
            _activeRunResults.Remove(runId);
            _runCancellations.Remove(job.JobId, out var source);
            source?.Dispose();
        }
    }

    private async Task ExecuteRunAsync(
        InspectionJob job,
        EquipmentState equipment,
        JobRunResult result,
        JobRunSummary summary,
        IProgress<LotRunProgress> progress,
        CancellationTokenSource cancellation)
    {
        await _runner.RunAsync(job, equipment, result, progress,
            snapshot => SaveCheckpointAsync(job, summary, snapshot), cancellation.Token);

        if (result.Status == JobStatus.Completed)
        {
            try
            {
                summary.ReportFilePath = await _reports.GenerateAsync(job, result, _artifacts.GetReportPath(job.JobId, result.RunId));
            }
            catch (Exception ex)
            {
                summary.ReportFilePath = null;
                ShowBanner($"PDF 자동 저장 실패: {ex.Message}", JobStatus.Failed);
            }
        }

        UpdateFromResult(job, summary, result);
        equipment.ActiveJobId = null; equipment.ProgressPercent = 0; equipment.CurrentWaferId = string.Empty;
        await _artifacts.SaveCheckpointAsync(result);
        await _jobStore.SaveAsync(_jobs);

        if (_currentView is JobProgressView current && current.RunId == result.RunId)
            ShowRunResult(job, summary);
        else
        {
            RefreshCurrentLiveView(statusMayHaveChanged: true);
            ShowBanner($"{job.LotId}: {JobCard.JobText(result.Status)}", result.Status);
        }
    }

    private async Task SaveCheckpointAsync(InspectionJob job, JobRunSummary summary, JobRunResult result)
    {
        UpdateFromResult(job, summary, result);
        await _artifacts.SaveCheckpointAsync(result);
        await _jobStore.SaveAsync(_jobs);
    }

    private static void UpdateFromResult(InspectionJob job, JobRunSummary summary, JobRunResult result)
    {
        summary.Status = result.Status; summary.FinishedAt = result.FinishedAt;
        summary.ProgressPercent = result.ProgressPercent; summary.HasNgWafers = result.HasNgWafers;
        summary.LotYieldPercent = result.LotYieldPercent; summary.FailureReason = result.FailureReason;
        job.Status = result.Status; job.ProgressPercent = result.ProgressPercent;
        job.CurrentWaferId = result.CurrentWaferId; job.HasNgWafers = result.HasNgWafers;
    }

    private void CancelRun(string jobId)
    {
        if (!_runCancellations.TryGetValue(jobId, out var cancellation)) return;
        if (MessageBox.Show(this, "진행 중인 Lot 검사를 취소하시겠습니까?", "검사 취소",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            cancellation.Cancel();
    }

    private void ShowRunResult(InspectionJob job, JobRunSummary run)
    {
        if (run.Status == JobStatus.Running)
        {
            if (_activeRunResults.TryGetValue(run.RunId, out var activeResult))
            {
                ShowJobProgress(job, activeResult);
                return;
            }

            MessageBox.Show(this, "진행 중인 Run 정보를 찾을 수 없습니다. Job 상태를 다시 확인하세요.",
                "진행 화면 열기", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowJobDetail(job);
            return;
        }

        var result = _artifacts.Load(run.ResultFilePath);
        if (result is null)
        {
            MessageBox.Show(this, $"결과 파일을 찾거나 읽을 수 없습니다.\r\n\r\n{run.ResultFilePath}",
                "결과 파일 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowJobDetail(job);
            return;
        }
        var view = new JobResultView(job, run, result);
        view.BackRequested += () => ShowJobDetail(job);
        view.SaveLogRequested += SaveLogCopy;
        view.SaveReportRequested += SaveReportCopy;
        SetContent(view);
    }

    private void ShowJobProgress(InspectionJob job, JobRunResult result)
    {
        var view = new JobProgressView(job, result);
        view.CancelRequested += () => CancelRun(job.JobId);
        SetContent(view);
    }

    private void RefreshCurrentLiveView(LotRunProgress? progress = null, bool statusMayHaveChanged = false)
    {
        switch (_currentView)
        {
            case JobProgressView progressView when progress is not null && progressView.RunId == progress.RunId:
                progressView.ApplyProgress(progress);
                break;
            case JobListView jobs:
                jobs.RefreshStates(statusMayHaveChanged);
                break;
            case JobDetailView detail:
                detail.RefreshView();
                break;
            case EquipmentListView equipment:
                equipment.RefreshStates();
                break;
            case EquipmentDetailView detail:
                detail.RefreshView();
                break;
        }
    }

    private async Task DeleteJobAsync(InspectionJob job)
    {
        if (job.Status == JobStatus.Running) return;
        if (MessageBox.Show(this, "정말로 삭제하시겠습니까?\r\n자동 저장된 결과 파일은 보존됩니다.", "Job 삭제",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _jobs.Remove(job);
        await _jobStore.SaveAsync(_jobs);
        ShowJobList();
        ShowBanner("Job을 삭제했습니다. 결과 파일은 보존됩니다.", JobStatus.Completed);
    }

    private void ShowEquipmentList()
    {
        var view = new EquipmentListView(_equipment);
        view.EquipmentActivated += ShowEquipmentDetail;
        SetContent(view);
    }

    private void ShowEquipmentDetail(EquipmentState equipment)
    {
        var view = new EquipmentDetailView(equipment);
        view.BackRequested += ShowEquipmentList;
        view.ConnectionToggleRequested += async () =>
        {
            if (equipment.ConnectionStatus == ConnectionStatus.Connected)
            {
                equipment.ConnectionStatus = ConnectionStatus.Disconnected;
                await _equipmentStore.SaveAsync(_equipment);
                view.RefreshView();
                return;
            }
            try
            {
                var connection = _connectionService.ConnectAsync(equipment);
                view.RefreshView();
                await connection;
                await _equipmentStore.SaveAsync(_equipment);
                view.RefreshView();
                ShowBanner($"{equipment.Definition.Name} 장비에 연결했습니다.", JobStatus.Completed);
            }
            catch (Exception ex)
            {
                equipment.ConnectionStatus = ConnectionStatus.Disconnected;
                MessageBox.Show(this, ex.Message, "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        SetContent(view);
    }

    private async Task AddEquipmentAsync()
    {
        var existing = _equipment.Select(x => x.Definition.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var available = _equipmentCatalog.Equipment.Where(x => !existing.Contains(x.Id)).ToList();
        if (available.Count == 0)
        {
            MessageBox.Show(this, "추가로 연결할 수 있는 가상 장비가 없습니다.", "장비 연결", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new EquipmentSelectionDialog(available);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedEquipment is null) return;
        var state = new EquipmentState(dialog.SelectedEquipment) { ConnectionStatus = ConnectionStatus.Connecting };
        _equipment.Add(state); ShowEquipmentList();
        try
        {
            await _connectionService.ConnectAsync(state);
            await _equipmentStore.SaveAsync(_equipment);
            ShowEquipmentList();
            ShowBanner($"{state.Definition.Name} 장비에 연결했습니다.", JobStatus.Completed);
        }
        catch (Exception ex)
        {
            state.ConnectionStatus = ConnectionStatus.Disconnected;
            MessageBox.Show(this, ex.Message, "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RecoverInterruptedRunsAsync()
    {
        var changed = false;
        foreach (var job in _jobs.Where(x => x.Status == JobStatus.Running))
        {
            var run = job.Runs.OrderByDescending(x => x.StartedAt).FirstOrDefault(x => x.Status == JobStatus.Running);
            if (run is null) { job.Status = JobStatus.Interrupted; changed = true; continue; }
            var result = _artifacts.Load(run.ResultFilePath);
            if (result is not null)
            {
                foreach (var wafer in result.Wafers.Where(x => x.Status is WaferExecutionStatus.Running or WaferExecutionStatus.Pending))
                    wafer.Status = WaferExecutionStatus.NotRun;
                result.Status = JobStatus.Interrupted; result.FinishedAt = DateTimeOffset.Now;
                result.FailureReason = "프로그램이 비정상 종료되어 Run이 중단되었습니다.";
                result.Logs.Add(new RunLogEntry(DateTimeOffset.Now, "WARN", result.CurrentWaferId, result.FailureReason));
                await _artifacts.SaveCheckpointAsync(result);
                UpdateFromResult(job, run, result);
            }
            else
            {
                run.Status = JobStatus.Interrupted; run.FinishedAt = DateTimeOffset.Now;
                run.FailureReason = "프로그램이 비정상 종료되었으며 체크포인트 파일을 찾을 수 없습니다.";
                job.Status = JobStatus.Interrupted;
            }
            changed = true;
        }
        if (changed)
        {
            await _jobStore.SaveAsync(_jobs);
            ShowJobList();
            ShowBanner("비정상 종료된 Run을 중단 상태로 복구했습니다.", JobStatus.Interrupted);
        }
    }

    private void SaveLogCopy(JobRunSummary run) => SaveCopy(run.LogFilePath, "로그 파일 저장", "로그 파일 (*.log)|*.log|텍스트 파일 (*.txt)|*.txt");
    private void SaveReportCopy(JobRunSummary run)
    {
        if (!string.IsNullOrWhiteSpace(run.ReportFilePath))
            SaveCopy(run.ReportFilePath, "결과 보고서 저장", "PDF 보고서 (*.pdf)|*.pdf");
    }

    private void SaveCopy(string source, string title, string filter)
    {
        if (!File.Exists(source))
        {
            MessageBox.Show(this, $"자동 저장 파일을 찾을 수 없습니다.\r\n{source}", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = Path.GetFileName(source), OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.Copy(source, dialog.FileName, true); ShowBanner("파일을 저장했습니다.", JobStatus.Completed); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenResultsFolder()
    {
        try
        {
            Directory.CreateDirectory(_artifacts.ResultsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", _artifacts.ResultsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "결과 폴더 열기 실패", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private EquipmentState? FindEquipment(string id) =>
        _equipment.FirstOrDefault(x => string.Equals(x.Definition.Id, id, StringComparison.OrdinalIgnoreCase));

    private static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonDefaults.Write), JsonDefaults.Read)!;

    private void SetContent(Control view)
    {
        if (_currentView is not null)
        {
            _contentHost.Controls.Remove(_currentView);
            _currentView.Dispose();
        }
        _currentView = view; view.Dock = DockStyle.Fill;
        _contentHost.Controls.Add(view); view.BringToFront(); _banner.BringToFront();
        var equipmentSelected = view is EquipmentListView or EquipmentDetailView;
        _equipmentNavigationButton.BackColor = equipmentSelected ? Color.FromArgb(47, 106, 165) : Color.FromArgb(35, 48, 61);
        _jobsNavigationButton.BackColor = equipmentSelected ? Color.FromArgb(35, 48, 61) : Color.FromArgb(47, 106, 165);
    }

    private void ShowBanner(string message, JobStatus status)
    {
        if (IsDisposed || Disposing) return;
        _banner.Text = message;
        _banner.BackColor = status == JobStatus.Completed ? AppTheme.Success :
            status is JobStatus.Failed or JobStatus.Interrupted ? AppTheme.Danger : AppTheme.Warning;
        _banner.Visible = true; _banner.BringToFront(); _bannerTimer.Stop(); _bannerTimer.Start();
    }

    private void ShowCatalogErrors()
    {
        var errors = _equipmentCatalog.Errors.Concat(_productCatalog.Errors).Concat(_recipeCatalog.Errors).ToList();
        foreach (var product in _productCatalog.Products)
        foreach (var recipeId in product.AllowedRecipeIds.Where(id => !_recipeCatalog.Recipes.Any(r => string.Equals(r.RecipeId, id, StringComparison.OrdinalIgnoreCase))))
            errors.Add($"{product.ProductId}: 허용 레시피를 찾을 수 없습니다 - {recipeId}");
        if (errors.Count == 0) return;
        MessageBox.Show(this, "일부 카탈로그 파일을 불러오지 못했습니다.\r\n\r\n" +
                              string.Join("\r\n", errors.Select(x => $"• {x}")),
            "카탈로그 경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose) { base.OnFormClosing(e); return; }
        if (_runTasks.Count == 0) { _allowClose = true; base.OnFormClosing(e); return; }
        e.Cancel = true;
        if (_closing) return;
        if (MessageBox.Show(this, $"{_runTasks.Count}개의 검사가 진행 중입니다.\r\n모두 취소하고 종료하시겠습니까?",
                "프로그램 종료", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _closing = true;
        _ = CancelAllAndCloseAsync();
    }

    private async Task CancelAllAndCloseAsync()
    {
        foreach (var source in _runCancellations.Values) source.Cancel();
        try { await Task.WhenAll(_runTasks.Values.ToArray()); } catch { }
        await _jobStore.SaveAsync(_jobs);
        _allowClose = true; Close();
    }
}
