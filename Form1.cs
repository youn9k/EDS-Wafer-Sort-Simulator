using System.Diagnostics;

namespace RecipeTestProject;

public partial class Form1 : Form
{
    private readonly EquipmentCatalogLoadResult _catalogLoadResult;
    private readonly IReadOnlyList<EquipmentDefinition> _catalog;
    private readonly List<EquipmentState> _equipment = [];
    private readonly RecipeService _recipeService = new();
    private readonly MockConnectionService _connectionService = new();
    private readonly MockTestRunner _testRunner = new();
    private readonly LogStore _logStore = new();
    private readonly EquipmentStateStore _stateStore = new();
    private readonly Dictionary<string, Task> _executionTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskStatusRow> _taskRows = new(StringComparer.OrdinalIgnoreCase);

    private MenuStrip _menu = null!;
    private ToolStripMenuItem _taskPanelMenuItem = null!;
    private SplitContainer _workspaceSplit = null!;
    private Panel _contentHost = null!;
    private Panel _taskRowsHost = null!;
    private Label _taskEmpty = null!;
    private Label _banner = null!;
    private System.Windows.Forms.Timer _bannerTimer = null!;
    private Control? _currentView;
    private bool _allowClose;
    private bool _closingInProgress;

    public Form1()
    {
        _catalogLoadResult = EquipmentCatalog.Load(Path.Combine(AppContext.BaseDirectory, "equipment"));
        _catalog = _catalogLoadResult.Equipment;
        InitializeComponent();
        Font = new Font("맑은 고딕", 9F);
        BackColor = AppTheme.Background;
        BuildShell();
        RestoreEquipment();
        ShowEquipmentList();
        Shown += (_, _) => ShowCatalogErrors();
    }

    private void BuildShell()
    {
        _menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            BackColor = Color.White,
            Font = new Font("맑은 고딕", 9.5F),
            Padding = new Padding(12, 5, 0, 5)
        };
        var equipmentMenu = new ToolStripMenuItem("장비");
        var connectMenuItem = new ToolStripMenuItem("장비 연결");
        connectMenuItem.Click += async (_, _) => await AddEquipmentAsync();
        equipmentMenu.DropDownItems.Add(connectMenuItem);
        var viewMenu = new ToolStripMenuItem("보기");
        _taskPanelMenuItem = new ToolStripMenuItem("작업 현황 숨기기");
        _taskPanelMenuItem.Click += (_, _) => SetTaskPanelVisible(_workspaceSplit.Panel2Collapsed);
        var openLogs = new ToolStripMenuItem("로그 폴더 열기");
        openLogs.Click += (_, _) => OpenLogFolder();
        viewMenu.DropDownItems.Add(_taskPanelMenuItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        viewMenu.DropDownItems.Add(openLogs);
        _menu.Items.Add(equipmentMenu);
        _menu.Items.Add(viewMenu);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 196, BackColor = Color.FromArgb(35, 48, 61), Padding = new Padding(12, 22, 12, 12) };
        var product = new Label
        {
            Text = "WAFER INSPECT\r\nTEST CENTER",
            Dock = DockStyle.Top,
            Height = 70,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0)
        };
        var listButton = new Button
        {
            Text = "▦   장비 목록",
            Dock = DockStyle.Top,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(47, 106, 165),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold)
        };
        listButton.FlatAppearance.BorderSize = 0;
        listButton.Click += (_, _) => ShowEquipmentList();
        sidebar.Controls.Add(listButton);
        sidebar.Controls.Add(product);

        _workspaceSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = AppTheme.Border,
            Panel1MinSize = 350,
            Panel2MinSize = 105
        };
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
        _workspaceSplit.Panel1.Controls.Add(_contentHost);
        BuildTaskPanel();

        body.Controls.Add(_workspaceSplit);
        body.Controls.Add(sidebar);
        Controls.Add(body);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        _bannerTimer = new System.Windows.Forms.Timer { Interval = 4000 };
        _bannerTimer.Tick += (_, _) => { _bannerTimer.Stop(); _banner.Visible = false; };
        Shown += (_, _) =>
        {
            if (_workspaceSplit.Height > 540)
                _workspaceSplit.SplitterDistance = _workspaceSplit.Height - 165;
        };
    }

    private void BuildTaskPanel()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.White,
            Padding = new Padding(14, 6, 8, 4)
        };
        var title = new Label
        {
            Text = "작업 현황",
            Dock = DockStyle.Left,
            Width = 160,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
            ForeColor = AppTheme.Text
        };
        var close = new Button
        {
            Text = "×",
            Dock = DockStyle.Right,
            Width = 34,
            FlatStyle = FlatStyle.Flat,
            ForeColor = AppTheme.Muted,
            Font = new Font("맑은 고딕", 14F),
            Cursor = Cursors.Hand
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => SetTaskPanelVisible(false);
        header.Controls.Add(close);
        header.Controls.Add(title);

        _taskRowsHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(8, 4, 8, 8)
        };
        _taskEmpty = new Label
        {
            Text = "현재 진행 중인 테스트가 없습니다.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = AppTheme.Muted
        };
        _taskRowsHost.Controls.Add(_taskEmpty);
        _workspaceSplit.Panel2.Controls.Add(_taskRowsHost);
        _workspaceSplit.Panel2.Controls.Add(header);
    }

    private void RestoreEquipment()
    {
        foreach (var saved in _stateStore.Load())
        {
            var definition = _catalog.FirstOrDefault(x => string.Equals(x.Id, saved.EquipmentId, StringComparison.OrdinalIgnoreCase));
            if (definition is null) continue;
            _equipment.Add(new EquipmentState(definition)
            {
                ConnectionStatus = saved.ConnectionStatus == ConnectionStatus.Connecting ? ConnectionStatus.Disconnected : saved.ConnectionStatus,
                LastConnectedAt = saved.LastConnectedAt
            });
        }
    }

    private void ShowEquipmentList()
    {
        var listView = new EquipmentListView();
        listView.EquipmentActivated += OpenEquipment;
        listView.SetEquipment(_equipment);
        SetContent(listView);
    }

    private void OpenEquipment(EquipmentState equipment)
    {
        if (equipment.ActiveRun is not null && (equipment.IsRunning || equipment.LastResult is not null))
        {
            ShowTestRun(equipment, equipment.ActiveRun);
            return;
        }
        ShowEquipmentDetail(equipment);
    }

    private void ShowEquipmentDetail(EquipmentState equipment)
    {
        var view = new EquipmentDetailView(equipment);
        view.CloseRequested += ShowEquipmentList;
        view.TestRequested += async () =>
        {
            if (equipment.IsRunning && equipment.ActiveRun is not null) ShowTestRun(equipment, equipment.ActiveRun);
            else await StartTestAsync(equipment);
        };
        view.ConnectionToggleRequested += async () =>
        {
            if (equipment.ConnectionStatus == ConnectionStatus.Connected)
            {
                equipment.ConnectionStatus = ConnectionStatus.Disconnected;
                await SaveEquipmentStateAsync();
                view.RefreshView();
            }
            else
            {
                view.RefreshView();
                try
                {
                    var task = _connectionService.ConnectAsync(equipment);
                    view.RefreshView();
                    await task;
                    await SaveEquipmentStateAsync();
                    ShowBanner($"{equipment.Definition.Name} 장비에 다시 연결했습니다.", TestStatus.Succeeded);
                }
                catch (Exception ex)
                {
                    equipment.ConnectionStatus = ConnectionStatus.Disconnected;
                    MessageBox.Show(this, ex.Message, "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                view.RefreshView();
            }
        };
        SetContent(view);
    }

    private async Task AddEquipmentAsync()
    {
        if (_catalog.Count == 0)
        {
            MessageBox.Show(this, "장비 카탈로그에 정상적으로 등록된 장비가 없습니다.", "장비 연결",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var existingIds = _equipment.Select(x => x.Definition.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var available = _catalog.Where(x => !existingIds.Contains(x.Id)).ToList();
        if (available.Count == 0)
        {
            MessageBox.Show(this, "연결 가능한 가상 장비를 모두 추가했습니다.", "장비 연결", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new EquipmentSelectionDialog(available);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedEquipment is null) return;
        var equipment = new EquipmentState(dialog.SelectedEquipment) { ConnectionStatus = ConnectionStatus.Connecting };
        _equipment.Add(equipment);
        ShowEquipmentList();
        try
        {
            await _connectionService.ConnectAsync(equipment);
            await SaveEquipmentStateAsync();
            if (_currentView is EquipmentListView listView) listView.RefreshStates();
            ShowBanner($"{equipment.Definition.Name} 장비에 연결했습니다.", TestStatus.Succeeded);
        }
        catch (Exception ex)
        {
            equipment.ConnectionStatus = ConnectionStatus.Disconnected;
            if (_currentView is EquipmentListView listView) listView.RefreshStates();
            MessageBox.Show(this, ex.Message, "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartTestAsync(EquipmentState equipment)
    {
        if (equipment.ConnectionStatus != ConnectionStatus.Connected)
        {
            MessageBox.Show(this, "연결된 장비에서만 테스트를 시작할 수 있습니다.", "테스트 시작", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (equipment.IsRunning)
        {
            MessageBox.Show(this, "이 장비에서 이미 테스트가 진행 중입니다.", "테스트 시작", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var fileDialog = new OpenFileDialog
        {
            Title = "웨이퍼 검사 레시피 선택",
            Filter = "JSON 레시피 (*.json)|*.json|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = GetSampleRecipeDirectory()
        };
        if (fileDialog.ShowDialog(this) != DialogResult.OK) return;

        RecipeLoadResult loaded;
        try
        {
            loaded = _recipeService.LoadAndValidate(fileDialog.FileName);
        }
        catch (RecipeValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "레시피 검증 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!string.Equals(loaded.Recipe.TargetEquipmentModel, equipment.Definition.Model, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                $"이 레시피는 선택한 장비에서 실행할 수 없습니다.\r\n\r\n레시피 대상 모델: {loaded.Recipe.TargetEquipmentModel}\r\n선택 장비 모델: {equipment.Definition.Model}",
                "장비 모델 불일치", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var simulationDialog = new SimulationDialog(loaded.Recipe);
        if (simulationDialog.ShowDialog(this) != DialogResult.OK || simulationDialog.Settings is null) return;

        var run = new TestRun
        {
            RunId = Guid.NewGuid().ToString("N"),
            Equipment = equipment,
            Recipe = loaded.Recipe,
            RecipeSourcePath = loaded.SourcePath,
            Simulation = simulationDialog.Settings,
            StartedAt = DateTimeOffset.Now
        };
        equipment.ActiveRun?.Cancellation.Dispose();
        equipment.ActiveRun = run;
        equipment.LastResult = null;
        equipment.TestStatus = TestStatus.Running;
        equipment.ProgressPercent = 0;
        equipment.CurrentStepName = "준비 중";
        AddTaskRow(equipment);
        var view = ShowTestRun(equipment, run);
        RefreshVisualState();

        var execution = ExecuteRunAsync(equipment, run, view);
        _executionTasks[equipment.Definition.Id] = execution;
        try { await execution; }
        finally { _executionTasks.Remove(equipment.Definition.Id); }
    }

    private TestRunView ShowTestRun(EquipmentState equipment, TestRun run)
    {
        var view = new TestRunView(equipment, run);
        view.CloseRequested += ShowEquipmentList;
        view.CancelRequested += () => CancelTest(equipment);
        view.NewTestRequested += async () => await StartTestAsync(equipment);
        view.SaveLogRequested += SaveLogCopy;
        if (equipment.LastResult is not null) view.ShowResult(equipment.LastResult);
        SetContent(view);
        return view;
    }

    private async Task ExecuteRunAsync(EquipmentState equipment, TestRun run, TestRunView startingView)
    {
        var progress = new Progress<TestProgress>(update =>
        {
            if (!ReferenceEquals(equipment.ActiveRun, run)) return;
            equipment.ProgressPercent = update.Percent;
            equipment.CurrentStepName = update.CurrentStepName;
            if (_currentView is TestRunView currentView && currentView.RunId == run.RunId) currentView.UpdateProgress(update);
            if (_taskRows.TryGetValue(equipment.Definition.Id, out var row)) row.UpdateState(equipment);
            if (_currentView is EquipmentListView listView) listView.RefreshStates();
        });

        var result = await _testRunner.RunAsync(run, progress);
        try
        {
            await _logStore.SaveAsync(run, result);
        }
        catch (Exception ex)
        {
            result.LogFilePath = null;
            ShowBanner($"로그 자동 저장 실패: {ex.Message}", TestStatus.Failed);
        }

        if (!ReferenceEquals(equipment.ActiveRun, run)) return;
        equipment.TestStatus = result.Status;
        equipment.ProgressPercent = result.FinalProgressPercent;
        equipment.LastResult = result;
        equipment.CurrentStepName = string.Empty;
        RemoveTaskRow(equipment);
        RefreshVisualState();

        if (_currentView is TestRunView activeView && activeView.RunId == run.RunId)
            activeView.ShowResult(result);
        else
            ShowBanner($"{equipment.Definition.Name}: {ResultMessage(result)}", result.Status);
    }

    private void CancelTest(EquipmentState equipment)
    {
        if (!equipment.IsRunning || equipment.ActiveRun is null) return;
        if (MessageBox.Show(this, "진행 중인 테스트를 취소하시겠습니까?", "테스트 취소", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            equipment.ActiveRun.Cancellation.Cancel();
    }

    private void AddTaskRow(EquipmentState equipment)
    {
        RemoveTaskRow(equipment);
        var row = new TaskStatusRow(equipment);
        _taskRows[equipment.Definition.Id] = row;
        _taskRowsHost.Controls.Add(row);
        row.BringToFront();
        UpdateTaskEmpty();
    }

    private void RemoveTaskRow(EquipmentState equipment)
    {
        if (_taskRows.Remove(equipment.Definition.Id, out var row))
        {
            _taskRowsHost.Controls.Remove(row);
            row.Dispose();
        }
        UpdateTaskEmpty();
    }

    private void UpdateTaskEmpty()
    {
        _taskEmpty.Visible = _taskRows.Count == 0;
        if (_taskEmpty.Visible) _taskEmpty.BringToFront();
    }

    private void RefreshVisualState()
    {
        if (_currentView is EquipmentListView listView) listView.RefreshStates();
        if (_currentView is EquipmentDetailView detail) detail.RefreshView();
    }

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
    }

    private void ShowBanner(string message, TestStatus status)
    {
        if (IsDisposed || Disposing) return;
        _banner.Text = message;
        _banner.BackColor = status == TestStatus.Succeeded ? AppTheme.Success : status == TestStatus.Failed ? AppTheme.Danger : AppTheme.Warning;
        _banner.Visible = true;
        _banner.BringToFront();
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    private void SetTaskPanelVisible(bool visible)
    {
        _workspaceSplit.Panel2Collapsed = !visible;
        _taskPanelMenuItem.Text = visible ? "작업 현황 숨기기" : "작업 현황 보기";
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(_logStore.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", _logStore.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "로그 폴더 열기 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveLogCopy(TestResult result)
    {
        if (string.IsNullOrWhiteSpace(result.LogFilePath) || !File.Exists(result.LogFilePath))
        {
            MessageBox.Show(this, "자동 저장된 로그 파일을 찾을 수 없습니다.", "로그 파일 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "로그 파일 저장",
            Filter = "로그 파일 (*.log)|*.log|텍스트 파일 (*.txt)|*.txt",
            FileName = Path.GetFileName(result.LogFilePath),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.Copy(result.LogFilePath, dialog.FileName, true);
            ShowBanner("로그 파일을 저장했습니다.", TestStatus.Succeeded);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "로그 파일 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveEquipmentStateAsync()
    {
        try { await _stateStore.SaveAsync(_equipment); }
        catch (Exception ex) { ShowBanner($"장비 상태 저장 실패: {ex.Message}", TestStatus.Failed); }
    }

    private void ShowCatalogErrors()
    {
        if (_catalogLoadResult.Errors.Count == 0) return;

        MessageBox.Show(this,
            "일부 장비 정보를 불러오지 못했습니다.\r\n정상적인 장비 정보만 목록에 반영되었습니다.\r\n\r\n" +
            string.Join("\r\n", _catalogLoadResult.Errors.Select(error => $"• {error}")),
            "장비 카탈로그 경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private string GetSampleRecipeDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "SampleRecipes");
        return Directory.Exists(directory) ? directory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static string ResultMessage(TestResult result) => result.Status switch
    {
        TestStatus.Succeeded => "테스트가 성공적으로 완료되었습니다.",
        TestStatus.Failed => $"{result.FailedStepName} 단계에서 테스트가 실패했습니다.",
        _ => "테스트가 취소되었습니다."
    };

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            base.OnFormClosing(e);
            return;
        }

        var running = _equipment.Where(x => x.IsRunning).ToList();
        if (running.Count == 0)
        {
            _allowClose = true;
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closingInProgress) return;
        if (MessageBox.Show(this, $"{running.Count}개의 테스트가 진행 중입니다.\r\n모두 취소하고 종료하시겠습니까?", "프로그램 종료", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _closingInProgress = true;
        _ = CancelAllAndCloseAsync(running);
    }

    private async Task CancelAllAndCloseAsync(IEnumerable<EquipmentState> running)
    {
        foreach (var equipment in running) equipment.ActiveRun?.Cancellation.Cancel();
        try
        {
            var tasks = _executionTasks.Values.ToArray();
            if (tasks.Length > 0) await Task.WhenAll(tasks);
        }
        catch { }
        await SaveEquipmentStateAsync();
        _allowClose = true;
        Close();
    }
}
