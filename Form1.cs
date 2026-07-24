using System.Diagnostics;
using System.Text.Json;

namespace RecipeTestProject;

public partial class Form1 : Form
{
    private readonly TestCellCatalogLoadResult _cellCatalog;
    private readonly ProductCatalogLoadResult _productCatalog;
    private readonly RecipeCatalogLoadResult _recipeCatalog;
    private readonly List<TestCellState> _cells = [];
    private readonly List<InspectionJob> _jobs;
    private readonly MockConnectionService _connectionService = new();
    private readonly TestCellStateStore _cellStore;
    private readonly JobStore _jobStore;
    private readonly RunArtifactStore _artifacts;
    private readonly LotTestRunner _runner = new();
    private readonly ReportService _reports = new();
    private readonly Dictionary<string, CancellationTokenSource> _runCancellations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _runTasks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JobRunResult> _activeRunResults =
        new(StringComparer.OrdinalIgnoreCase);

    private Panel _contentHost = null!;
    private Label _banner = null!;
    private System.Windows.Forms.Timer _bannerTimer = null!;
    private Button _jobsNavigationButton = null!;
    private Button _cellNavigationButton = null!;
    private Control? _currentView;
    private bool _allowClose;
    private bool _closing;

    public Form1()
    {
        _jobStore = new JobStore();
        _cellStore = new TestCellStateStore(_jobStore.RootDirectory);
        _cellCatalog = TestCellCatalog.Load(Path.Combine(AppContext.BaseDirectory, "equipment"));
        _productCatalog = ProductCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Products"));
        _recipeCatalog = RecipeCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Recipes"));
        _jobs = _jobStore.Load();
        _artifacts = new RunArtifactStore(_jobStore.RootDirectory);

        InitializeComponent();
        Font = new Font("맑은 고딕", 9F);
        BackColor = AppTheme.Background;
        BuildShell();
        RestoreCells();
        ShowJobList();
        Shown += async (_, _) =>
        {
            await RecoverInterruptedRunsAsync();
            ShowCatalogErrors();
        };
    }

    private void BuildShell()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            BackColor = Color.White,
            Padding = new Padding(12, 5, 0, 5)
        };
        var viewMenu = new ToolStripMenuItem("보기");
        var results = new ToolStripMenuItem("결과 폴더 열기");
        results.Click += (_, _) => OpenResultsFolder();
        viewMenu.DropDownItems.Add(results);
        menu.Items.Add(viewMenu);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 215,
            BackColor = Color.FromArgb(35, 48, 61),
            Padding = new Padding(12, 22, 12, 12)
        };
        var product = new Label
        {
            Text = "EDS WAFER SORT\r\nSIMULATOR",
            Dock = DockStyle.Top,
            Height = 76,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0)
        };
        _cellNavigationButton = SidebarButton("장비 목록");
        _cellNavigationButton.Click += (_, _) => ShowCellList();
        _jobsNavigationButton = SidebarButton("전체 작업");
        _jobsNavigationButton.Click += (_, _) => ShowJobList();
        sidebar.Controls.Add(_cellNavigationButton);
        sidebar.Controls.Add(_jobsNavigationButton);
        sidebar.Controls.Add(product);

        _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
        _banner = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Visible = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            ForeColor = Color.White,
            Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold)
        };
        _contentHost.Controls.Add(_banner);
        body.Controls.Add(_contentHost);
        body.Controls.Add(sidebar);
        Controls.Add(body);
        Controls.Add(menu);
        MainMenuStrip = menu;

        _bannerTimer = new System.Windows.Forms.Timer { Interval = 4500 };
        _bannerTimer.Tick += (_, _) =>
        {
            _bannerTimer.Stop();
            _banner.Visible = false;
        };
    }

    private static Button SidebarButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(35, 48, 61),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void RestoreCells()
    {
        var saved = _cellStore.Load().ToDictionary(x => x.TestCellId, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in _cellCatalog.TestCells)
        {
            if (saved.TryGetValue(definition.Id, out var state))
            {
                _cells.Add(new TestCellState(definition)
                {
                    ConnectionStatus = state.ConnectionStatus == ConnectionStatus.Connecting
                        ? ConnectionStatus.Disconnected
                        : state.ConnectionStatus,
                    LastConnectedAt = state.LastConnectedAt,
                    ErrorComponent = state.ErrorComponent,
                    ErrorMessage = state.ErrorMessage
                });
            }
            else
            {
                _cells.Add(new TestCellState(definition)
                {
                    ConnectionStatus = ConnectionStatus.Connected,
                    LastConnectedAt = DateTimeOffset.Now
                });
            }
        }
    }

    private void ShowJobList()
    {
        var view = new JobListView();
        view.SetJobs(_jobs, FindCell);
        view.CreateRequested += ShowJobCreate;
        view.JobActivated += ActivateJob;
        SetContent(view);
    }

    private void ActivateJob(InspectionJob job)
    {
        var latest = job.Runs.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        if (job.Status == JobStatus.Running && latest is not null)
        {
            ShowRunResult(job, latest);
            return;
        }
        if (job.Status != JobStatus.Pending && latest is not null && File.Exists(latest.ResultFilePath))
        {
            ShowRunResult(job, latest);
            return;
        }
        ShowJobDetail(job);
    }

    private void ShowJobCreate()
    {
        var view = new JobCreateView(_productCatalog.Products, _recipeCatalog.Recipes, _cells);
        view.CancelRequested += ShowJobList;
        view.CellListRequested += ShowCellList;
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
            TestCellId = request.TestCell.Definition.Id,
            TestCellSnapshot = Clone(request.TestCell.Definition),
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
        var view = new JobDetailView(job, FindCell(job.TestCellId));
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
            ShowBanner("모의 EDS 결과 설정을 저장했습니다.", JobStatus.Completed);
        };
        SetContent(view);
    }

    private async Task StartJobAsync(InspectionJob job)
    {
        var cell = FindCell(job.TestCellId);
        var blocked = JobStartValidator.GetBlockReason(job, cell);
        if (blocked is not null)
        {
            MessageBox.Show(
                this,
                blocked,
                "EDS 시작",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        var readyCell = cell!;

        var runId = $"RUN-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..25];
        var result = LotTestRunner.CreateRun(job, runId);
        var summary = new JobRunSummary
        {
            RunId = runId,
            Status = JobStatus.Running,
            StartedAt = result.StartedAt,
            ResultFilePath = _artifacts.GetResultPath(job.JobId, runId),
            LogFilePath = _artifacts.GetLogPath(job.JobId, runId)
        };
        job.Runs.Add(summary);
        job.Status = JobStatus.Running;
        job.ProgressPercent = 0;
        job.CurrentWaferId = "Wafer01";
        job.HasLowYieldWafers = false;
        readyCell.ActiveJobId = job.JobId;
        readyCell.ProgressPercent = 0;
        readyCell.CurrentWaferId = "Wafer01";
        _activeRunResults[runId] = result;
        await SaveCheckpointAsync(job, summary, result);
        await _cellStore.SaveAsync(_cells);

        ShowJobProgress(job, result, readyCell);
        var cancellation = new CancellationTokenSource();
        _runCancellations[job.JobId] = cancellation;
        var progress = new Progress<LotRunProgress>(update =>
        {
            job.ProgressPercent = update.OverallPercent;
            job.CurrentWaferId = update.WaferId;
            readyCell.ProgressPercent = update.OverallPercent;
            readyCell.CurrentWaferId = update.WaferId;
            RefreshCurrentLiveView(update);
        });
        var execution = ExecuteRunAsync(job, readyCell, result, summary, progress, cancellation);
        _runTasks[job.JobId] = execution;
        try
        {
            await execution;
        }
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
        TestCellState cell,
        JobRunResult result,
        JobRunSummary summary,
        IProgress<LotRunProgress> progress,
        CancellationTokenSource cancellation)
    {
        await _runner.RunAsync(
            job,
            cell,
            result,
            progress,
            snapshot => SaveCheckpointAsync(job, summary, snapshot),
            cancellation.Token);

        if (result.Status == JobStatus.Completed)
        {
            try
            {
                summary.ReportFilePath = await _reports.GenerateAsync(
                    job,
                    result,
                    _artifacts.GetReportPath(job.JobId, result.RunId));
            }
            catch (Exception ex)
            {
                summary.ReportFilePath = null;
                ShowBanner($"PDF 자동 저장 실패: {ex.Message}", JobStatus.Failed);
            }
        }

        UpdateFromResult(job, summary, result);
        cell.ActiveJobId = null;
        cell.ProgressPercent = 0;
        cell.CurrentWaferId = string.Empty;
        await _artifacts.SaveCheckpointAsync(result);
        await _jobStore.SaveAsync(_jobs);
        await _cellStore.SaveAsync(_cells);

        if (_currentView is JobProgressView current && current.RunId == result.RunId)
            ShowRunResult(job, summary);
        else
        {
            RefreshCurrentLiveView(statusMayHaveChanged: true);
            ShowBanner($"{job.LotId}: {AppTheme.JobText(result.Status)}", result.Status);
        }
    }

    private async Task SaveCheckpointAsync(
        InspectionJob job,
        JobRunSummary summary,
        JobRunResult result)
    {
        UpdateFromResult(job, summary, result);
        await _artifacts.SaveCheckpointAsync(result);
        await _jobStore.SaveAsync(_jobs);
    }

    private static void UpdateFromResult(
        InspectionJob job,
        JobRunSummary summary,
        JobRunResult result)
    {
        summary.Status = result.Status;
        summary.FinishedAt = result.FinishedAt;
        summary.ProgressPercent = result.ProgressPercent;
        summary.HasLowYieldWafers = result.HasLowYieldWafers;
        summary.LotYieldPercent = result.LotYieldPercent;
        summary.FailureReason = result.FailureReason;
        summary.ErrorComponent = result.ErrorComponent;
        job.Status = result.Status;
        job.ProgressPercent = result.ProgressPercent;
        job.CurrentWaferId = result.CurrentWaferId;
        job.HasLowYieldWafers = result.HasLowYieldWafers;
    }

    private void CancelRun(string jobId)
    {
        if (!_runCancellations.TryGetValue(jobId, out var cancellation)) return;
        if (MessageBox.Show(
                this,
                "진행 중인 EDS Run을 취소하시겠습니까?\r\n완료된 Wafer 결과는 보존됩니다.",
                "Run 취소",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
            cancellation.Cancel();
    }

    private void ShowRunResult(InspectionJob job, JobRunSummary run)
    {
        if (run.Status == JobStatus.Running)
        {
            if (_activeRunResults.TryGetValue(run.RunId, out var activeResult) &&
                FindCell(job.TestCellId) is { } cell)
            {
                ShowJobProgress(job, activeResult, cell);
                return;
            }
            MessageBox.Show(
                this,
                "진행 중인 Run 정보를 찾을 수 없습니다. Job 상태를 다시 확인하세요.",
                "진행 화면 열기",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            ShowJobDetail(job);
            return;
        }

        var result = _artifacts.Load(run.ResultFilePath);
        if (result is null)
        {
            MessageBox.Show(
                this,
                $"결과 파일을 찾거나 읽을 수 없습니다.\r\n\r\n{run.ResultFilePath}",
                "결과 파일 없음",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            ShowJobDetail(job);
            return;
        }
        var view = new JobResultView(job, run, result);
        view.BackRequested += () => ShowJobDetail(job);
        view.SaveLogRequested += SaveLogCopy;
        view.SaveReportRequested += SaveReportCopy;
        SetContent(view);
    }

    private void ShowJobProgress(InspectionJob job, JobRunResult result, TestCellState cell)
    {
        var view = new JobProgressView(job, result, cell);
        view.CancelRequested += () => CancelRun(job.JobId);
        SetContent(view);
    }

    private void RefreshCurrentLiveView(
        LotRunProgress? progress = null,
        bool statusMayHaveChanged = false)
    {
        switch (_currentView)
        {
            case JobProgressView progressView
                when progress is not null && progressView.RunId == progress.RunId:
                progressView.ApplyProgress(progress);
                break;
            case JobListView jobs:
                jobs.RefreshStates(statusMayHaveChanged);
                break;
            case JobDetailView detail:
                detail.RefreshView();
                break;
            case TestCellListView cells:
                cells.RefreshStates();
                break;
            case TestCellDetailView detail:
                detail.RefreshView();
                break;
        }
    }

    private async Task DeleteJobAsync(InspectionJob job)
    {
        if (job.Status == JobStatus.Running) return;
        if (MessageBox.Show(
                this,
                "정말로 삭제하시겠습니까?\r\n자동 저장된 결과 파일은 보존됩니다.",
                "Job 삭제",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        _jobs.Remove(job);
        await _jobStore.SaveAsync(_jobs);
        ShowJobList();
        ShowBanner("Job을 삭제했습니다. 결과 파일은 보존됩니다.", JobStatus.Completed);
    }

    private void ShowCellList()
    {
        var view = new TestCellListView(_cells);
        view.CellActivated += ShowCellDetail;
        SetContent(view);
    }

    private void ShowCellDetail(TestCellState cell)
    {
        var view = new TestCellDetailView(cell, _jobs);
        view.BackRequested += ShowCellList;
        view.ConnectionToggleRequested += async () =>
        {
            if (cell.IsBusy) return;
            if (cell.ConnectionStatus == ConnectionStatus.Connected)
            {
                cell.ConnectionStatus = ConnectionStatus.Disconnected;
                await _cellStore.SaveAsync(_cells);
                view.RefreshView();
                return;
            }
            try
            {
                var connection = _connectionService.ConnectAsync(cell);
                view.RefreshView();
                await connection;
                await _cellStore.SaveAsync(_cells);
                view.RefreshView();
                ShowBanner($"{cell.Definition.Name}에 연결했습니다.", JobStatus.Completed);
            }
            catch (Exception ex)
            {
                cell.ConnectionStatus = ConnectionStatus.Disconnected;
                MessageBox.Show(this, ex.Message, "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        view.ErrorResetRequested += async () =>
        {
            if (cell.IsBusy || !cell.HasError) return;
            cell.ResetError();
            await _cellStore.SaveAsync(_cells);
            view.RefreshView();
            ShowBanner($"{cell.Definition.Name} 오류를 리셋했습니다.", JobStatus.Completed);
        };
        SetContent(view);
    }

    private async Task RecoverInterruptedRunsAsync()
    {
        var changed = false;
        foreach (var job in _jobs.Where(x => x.Status == JobStatus.Running))
        {
            var run = job.Runs
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault(x => x.Status == JobStatus.Running);
            if (run is null)
            {
                job.Status = JobStatus.Interrupted;
                changed = true;
                continue;
            }
            var result = _artifacts.Load(run.ResultFilePath);
            if (result is not null)
            {
                foreach (var wafer in result.Wafers.Where(x =>
                             x.Status is WaferExecutionStatus.Running or WaferExecutionStatus.Pending))
                    wafer.Status = WaferExecutionStatus.NotRun;
                result.Status = JobStatus.Interrupted;
                result.FinishedAt = DateTimeOffset.Now;
                result.FailureReason = "프로그램이 비정상 종료되어 Run이 중단되었습니다.";
                result.Logs.Add(new RunLogEntry(
                    DateTimeOffset.Now,
                    "WARN",
                    result.CurrentWaferId,
                    result.FailureReason));
                await _artifacts.SaveCheckpointAsync(result);
                UpdateFromResult(job, run, result);
            }
            else
            {
                run.Status = JobStatus.Interrupted;
                run.FinishedAt = DateTimeOffset.Now;
                run.FailureReason = "비정상 종료된 Run의 체크포인트 파일을 찾을 수 없습니다.";
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

    private void SaveLogCopy(JobRunSummary run) =>
        SaveCopy(
            run.LogFilePath,
            "로그 파일 저장",
            "로그 파일 (*.log)|*.log|텍스트 파일 (*.txt)|*.txt");

    private void SaveReportCopy(JobRunSummary run)
    {
        if (!string.IsNullOrWhiteSpace(run.ReportFilePath))
            SaveCopy(run.ReportFilePath, "결과 보고서 저장", "PDF 보고서 (*.pdf)|*.pdf");
    }

    private void SaveCopy(string source, string title, string filter)
    {
        if (!File.Exists(source))
        {
            MessageBox.Show(
                this,
                $"자동 저장 파일을 찾을 수 없습니다.\r\n{source}",
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = Path.GetFileName(source),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.Copy(source, dialog.FileName, true);
            ShowBanner("파일을 저장했습니다.", JobStatus.Completed);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenResultsFolder()
    {
        try
        {
            Directory.CreateDirectory(_artifacts.ResultsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", _artifacts.ResultsDirectory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "결과 폴더 열기 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private TestCellState? FindCell(string id) =>
        _cells.FirstOrDefault(x =>
            string.Equals(x.Definition.Id, id, StringComparison.OrdinalIgnoreCase));

    private static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(
            JsonSerializer.Serialize(value, JsonDefaults.Write),
            JsonDefaults.Read)!;

    private void SetContent(Control view)
    {
        if (_currentView is not null)
        {
            _contentHost.Controls.Remove(_currentView);
            _currentView.Dispose();
        }
        _currentView = view;
        view.Dock = DockStyle.Fill;
        _contentHost.Controls.Add(view);
        view.BringToFront();
        _banner.BringToFront();
        var cellSelected = view is TestCellListView or TestCellDetailView;
        _cellNavigationButton.BackColor = cellSelected
            ? Color.FromArgb(47, 106, 165)
            : Color.FromArgb(35, 48, 61);
        _jobsNavigationButton.BackColor = cellSelected
            ? Color.FromArgb(35, 48, 61)
            : Color.FromArgb(47, 106, 165);
    }

    private void ShowBanner(string message, JobStatus status)
    {
        if (IsDisposed || Disposing) return;
        _banner.Text = message;
        _banner.BackColor = status == JobStatus.Completed ? AppTheme.Success :
            status is JobStatus.Failed or JobStatus.Interrupted ? AppTheme.Danger :
            AppTheme.Warning;
        _banner.Visible = true;
        _banner.BringToFront();
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    private void ShowCatalogErrors()
    {
        var errors = _cellCatalog.Errors
            .Concat(_productCatalog.Errors)
            .Concat(_recipeCatalog.Errors)
            .Concat(CatalogRelationshipValidator.Validate(
                _productCatalog.Products,
                _recipeCatalog.Recipes,
                _cellCatalog.TestCells))
            .ToList();
        if (errors.Count == 0) return;
        MessageBox.Show(
            this,
            "일부 카탈로그 파일 또는 자산에 문제가 있습니다.\r\n\r\n" +
            string.Join("\r\n", errors.Select(x => $"• {x}")),
            "카탈로그 경고",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            base.OnFormClosing(e);
            return;
        }
        if (_runTasks.Count == 0)
        {
            _allowClose = true;
            base.OnFormClosing(e);
            return;
        }
        e.Cancel = true;
        if (_closing) return;
        if (MessageBox.Show(
                this,
                $"{_runTasks.Count}개의 EDS Run이 진행 중입니다.\r\n모두 취소하고 종료하시겠습니까?",
                "프로그램 종료",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        _closing = true;
        _ = CancelAllAndCloseAsync();
    }

    private async Task CancelAllAndCloseAsync()
    {
        foreach (var source in _runCancellations.Values) source.Cancel();
        try
        {
            await Task.WhenAll(_runTasks.Values.ToArray());
        }
        catch
        {
            // 각 Run이 체크포인트를 저장한 뒤 종료하므로 종료 자체는 계속한다.
        }
        await _jobStore.SaveAsync(_jobs);
        await _cellStore.SaveAsync(_cells);
        _allowClose = true;
        Close();
    }
}
