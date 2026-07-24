using System.Drawing.Drawing2D;

namespace RecipeTestProject;

internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(242, 245, 248);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 221, 229);
    public static readonly Color Primary = Color.FromArgb(32, 104, 179);
    public static readonly Color Text = Color.FromArgb(35, 45, 55);
    public static readonly Color Muted = Color.FromArgb(101, 113, 126);
    public static readonly Color Success = Color.FromArgb(35, 150, 83);
    public static readonly Color Danger = Color.FromArgb(205, 62, 62);
    public static readonly Color Warning = Color.FromArgb(218, 143, 24);

    public static Button PrimaryButton(string text, int width = 120) =>
        CreateButton(text, width, Primary, Color.White, 0);
    public static Button SecondaryButton(string text, int width = 110) =>
        CreateButton(text, width, Color.White, Text, 1);
    public static Button DangerButton(string text, int width = 110) =>
        CreateButton(text, width, Danger, Color.White, 0);

    private static Button CreateButton(string text, int width, Color back, Color fore, int border)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 40,
            MinimumSize = new Size(width, 40),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 0, 16, 0),
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
            Margin = new Padding(6)
        };
        button.FlatAppearance.BorderSize = border;
        button.FlatAppearance.BorderColor = Border;
        return button;
    }

    public static Label Heading(string text, float size = 17F) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Text,
        Font = new Font("맑은 고딕", size, FontStyle.Bold)
    };

    public static Panel CardPanel() => new()
    {
        BackColor = Surface,
        Padding = new Padding(18),
        Margin = new Padding(0, 0, 0, 14),
        BorderStyle = BorderStyle.FixedSingle
    };

    public static DataGridView Grid() => new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        ColumnHeadersHeight = 36,
        RowTemplate = { Height = 31 }
    };

    public static void KeepSplitRatio(
        SplitContainer split,
        double ratio,
        int preferredPanel1Minimum = 25,
        int preferredPanel2Minimum = 25)
    {
        void Apply()
        {
            var length = split.Orientation == Orientation.Vertical
                ? split.ClientSize.Width
                : split.ClientSize.Height;
            var available = length - split.SplitterWidth;
            var minimum1 = Math.Max(split.Panel1MinSize, preferredPanel1Minimum);
            var minimum2 = Math.Max(split.Panel2MinSize, preferredPanel2Minimum);
            if (available < minimum1 + minimum2) return;
            var target = Math.Clamp((int)Math.Round(available * ratio), minimum1, available - minimum2);
            if (split.SplitterDistance != target) split.SplitterDistance = target;
        }

        split.SizeChanged += (_, _) => Apply();
        split.HandleCreated += (_, _) => Apply();
    }

    public static string JobText(JobStatus status) => status switch
    {
        JobStatus.Pending => "대기",
        JobStatus.Running => "진행 중",
        JobStatus.Completed => "완료",
        JobStatus.Failed => "실패",
        JobStatus.Canceled => "취소",
        _ => "중단"
    };

    public static string WaferText(WaferResult wafer) => wafer.Status switch
    {
        WaferExecutionStatus.Pending => "대기",
        WaferExecutionStatus.Running => "진행 중",
        WaferExecutionStatus.Completed when wafer.Disposition == WaferDisposition.Passed => "Passed",
        WaferExecutionStatus.Completed => "Low Yield",
        WaferExecutionStatus.EquipmentError => "Cell 오류",
        _ => "미실행"
    };

    public static string CellStatusText(TestCellState cell) =>
        cell.ConnectionStatus == ConnectionStatus.Connecting ? "연결 중" :
        cell.ConnectionStatus != ConnectionStatus.Connected ? "연결 해제" :
        cell.HasError ? $"오류 · {LotTestRunner.ComponentText(cell.ErrorComponent!.Value)}" :
        cell.IsBusy ? $"작업 중 ({cell.ProgressPercent}%)" : "유휴";
}

internal static class TestCellImageFactory
{
    public static Bitmap Create(TestCellDefinition cell, int width = 330, int height = 155)
    {
        if (!string.IsNullOrWhiteSpace(cell.ImagePath))
        {
            try
            {
                using var stream = File.OpenRead(cell.ImagePath);
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // 이미지가 손상되어도 Cell 정보는 기본 도식으로 계속 표시한다.
            }
        }

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(237, 242, 246));
        using var accent = new SolidBrush(cell.AccentColor);
        using var body = new SolidBrush(Color.FromArgb(210, 219, 227));
        using var light = new SolidBrush(Color.White);
        using var dark = new SolidBrush(Color.FromArgb(62, 72, 82));
        graphics.FillRectangle(body, 25, 28, 105, 100);
        graphics.FillRectangle(light, 37, 40, 81, 76);
        graphics.FillRectangle(accent, 37, 40, 81, 10);
        graphics.FillRectangle(dark, 49, 62, 57, 33);
        graphics.FillRectangle(body, 158, 47, 140, 81);
        graphics.FillEllipse(light, 181, 61, 94, 48);
        graphics.DrawEllipse(new Pen(accent, 4), 181, 61, 94, 48);
        graphics.DrawLine(new Pen(dark, 3), 130, 81, 158, 81);
        graphics.DrawString("TESTER", new Font("Segoe UI", 8F, FontStyle.Bold), dark, 50, 104);
        graphics.DrawString("WAFER PROBER", new Font("Segoe UI", 8F, FontStyle.Bold), dark, 183, 111);
        return bitmap;
    }
}

internal sealed class CreateJobCard : Panel
{
    public CreateJobCard()
    {
        Width = 345;
        Height = 275;
        BackColor = Color.FromArgb(236, 244, 252);
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 16, 16);
        Cursor = Cursors.Hand;
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = "＋\r\nJob 생성",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = AppTheme.Primary,
            Font = new Font("맑은 고딕", 15F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        label.DoubleClick += (_, _) => Activated?.Invoke();
        Controls.Add(label);
        DoubleClick += (_, _) => Activated?.Invoke();
    }

    public event Action? Activated;
}

internal sealed class JobCard : Panel
{
    private readonly InspectionJob _job;
    private readonly Func<string, TestCellState?> _cell;
    private readonly Label _status;
    private readonly Label _progress;

    public JobCard(InspectionJob job, Func<string, TestCellState?> cell)
    {
        _job = job;
        _cell = cell;
        Width = 345;
        Height = 275;
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 16, 16);
        Padding = new Padding(18);
        Cursor = Cursors.Hand;

        var title = new Label
        {
            Text = job.LotId,
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            AutoEllipsis = true
        };
        _status = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
            AutoEllipsis = true
        };
        var detail = new Label
        {
            Dock = DockStyle.Top,
            Height = 145,
            ForeColor = AppTheme.Muted,
            AutoEllipsis = true,
            Text = $"Job ID  {job.JobId}\r\n고객  {job.CustomerName}\r\n의뢰번호  {job.RequestNumber}\r\nProduct  {job.ProductSnapshot.Name}\r\nRecipe  {job.RecipeSnapshot.Name}\r\nTest Cell  {CellName()}"
        };
        _progress = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            ForeColor = AppTheme.Text,
            AutoEllipsis = true
        };
        Controls.Add(_progress);
        Controls.Add(detail);
        Controls.Add(_status);
        Controls.Add(title);
        Wire(this);
        RefreshState();
    }

    public event Action? Activated;

    public void RefreshState()
    {
        var suffix = _job.Status == JobStatus.Completed && _job.HasLowYieldWafers
            ? " · Low Yield 포함"
            : string.Empty;
        _status.Text = $"{AppTheme.JobText(_job.Status)}{suffix}";
        _status.ForeColor = _job.Status switch
        {
            JobStatus.Completed when !_job.HasLowYieldWafers => AppTheme.Success,
            JobStatus.Running => AppTheme.Primary,
            JobStatus.Pending => AppTheme.Warning,
            _ => AppTheme.Danger
        };
        var last = _job.Runs.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        _progress.Text = _job.Status == JobStatus.Running
            ? $"진행률 {_job.ProgressPercent}% · {_job.CurrentWaferId}"
            : last is null
                ? "아직 실행되지 않음"
                : $"최근 실행  {last.StartedAt:yyyy-MM-dd HH:mm}";
    }

    private string CellName() => _cell(_job.TestCellId)?.Definition.Name ?? _job.TestCellSnapshot.Name;

    private void Wire(Control control)
    {
        control.DoubleClick += (_, _) => Activated?.Invoke();
        foreach (Control child in control.Controls) Wire(child);
    }
}

internal sealed class JobListView : UserControl
{
    private readonly FlowLayoutPanel _cards;
    private readonly TextBox _search;
    private readonly ComboBox _filter;
    private IReadOnlyList<InspectionJob> _jobs = [];
    private Func<string, TestCellState?> _cell = _ => null;

    public JobListView()
    {
        BackColor = AppTheme.Background;
        Padding = new Padding(28);
        var header = new Panel { Dock = DockStyle.Top, Height = 96 };
        header.Controls.Add(AppTheme.Heading("전체 작업"));
        _search = new TextBox
        {
            PlaceholderText = "고객명, 의뢰번호, Lot ID 검색",
            Width = 310,
            Location = new Point(0, 50)
        };
        _filter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 190,
            Location = new Point(325, 48)
        };
        _filter.Items.AddRange(["전체", "대기", "진행", "완료", "실패·중단·취소"]);
        _filter.SelectedIndex = 0;
        _search.TextChanged += (_, _) => Render();
        _filter.SelectedIndexChanged += (_, _) => Render();
        header.Controls.Add(_search);
        header.Controls.Add(_filter);
        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        Controls.Add(_cards);
        Controls.Add(header);
    }

    public event Action? CreateRequested;
    public event Action<InspectionJob>? JobActivated;

    public void SetJobs(IReadOnlyList<InspectionJob> jobs, Func<string, TestCellState?> cell)
    {
        _jobs = jobs;
        _cell = cell;
        Render();
    }

    public void RefreshStates(bool reapplyFilter = false)
    {
        if (reapplyFilter)
        {
            Render();
            return;
        }
        foreach (var card in _cards.Controls.OfType<JobCard>()) card.RefreshState();
    }

    private void Render()
    {
        _cards.SuspendLayout();
        foreach (Control control in _cards.Controls) control.Dispose();
        _cards.Controls.Clear();
        var create = new CreateJobCard();
        create.Activated += () => CreateRequested?.Invoke();
        _cards.Controls.Add(create);

        var query = _search.Text.Trim();
        var jobs = _jobs.Where(job =>
            string.IsNullOrWhiteSpace(query) ||
            job.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            job.RequestNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            job.LotId.Contains(query, StringComparison.OrdinalIgnoreCase));
        jobs = _filter.SelectedIndex switch
        {
            1 => jobs.Where(x => x.Status == JobStatus.Pending),
            2 => jobs.Where(x => x.Status == JobStatus.Running),
            3 => jobs.Where(x => x.Status == JobStatus.Completed),
            4 => jobs.Where(x => x.Status is JobStatus.Failed or JobStatus.Interrupted or JobStatus.Canceled),
            _ => jobs
        };
        foreach (var job in jobs.OrderByDescending(x => x.CreatedAt))
        {
            var card = new JobCard(job, _cell);
            card.Activated += () => JobActivated?.Invoke(job);
            _cards.Controls.Add(card);
        }
        _cards.ResumeLayout();
    }
}

internal sealed record JobCreationRequest(
    string CustomerName,
    string RequestNumber,
    string LotId,
    ProductDocument Product,
    RecipeDocument Recipe,
    TestCellState TestCell);

internal sealed class JobCreateView : UserControl
{
    private readonly TextBox _customer = new();
    private readonly TextBox _request = new();
    private readonly TextBox _lot = new();
    private readonly ComboBox _product = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _recipe = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Enabled = false
    };
    private readonly ComboBox _cell = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Enabled = false
    };
    private readonly Label _message = new();
    private readonly Button _create;
    private readonly IReadOnlyList<RecipeDocument> _recipes;
    private readonly IReadOnlyList<TestCellState> _cells;

    public JobCreateView(
        IReadOnlyList<ProductDocument> products,
        IReadOnlyList<RecipeDocument> recipes,
        IReadOnlyList<TestCellState> cells)
    {
        _recipes = recipes;
        _cells = cells;
        BackColor = AppTheme.Background;
        Padding = new Padding(28);
        AutoScroll = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 64 };
        top.Controls.Add(AppTheme.Heading("Job 생성"));
        var cancel = AppTheme.SecondaryButton("전체 작업으로", 130);
        cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => cancel.Left = top.ClientSize.Width - cancel.Width;
        cancel.Click += (_, _) => CancelRequested?.Invoke();
        top.Controls.Add(cancel);

        var card = AppTheme.CardPanel();
        card.Dock = DockStyle.Top;
        card.Height = 585;
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(10)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 6; index++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        AddField(table, 0, "고객명", _customer);
        AddField(table, 1, "고객 의뢰번호", _request);
        AddField(table, 2, "Lot ID", _lot);
        foreach (var product in products) _product.Items.Add(product);
        _product.DisplayMember = nameof(ProductDocument.Name);
        AddField(table, 3, "Product", _product);
        AddField(table, 4, "Recipe", _recipe);
        AddField(table, 5, "Test Cell", _cell);
        _message.Dock = DockStyle.Fill;
        _message.ForeColor = AppTheme.Muted;
        _message.Padding = new Padding(0, 8, 0, 0);
        table.Controls.Add(_message, 1, 6);
        var cellLink = new LinkLabel
        {
            Text = "장비 목록으로 이동",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        cellLink.Click += (_, _) => CellListRequested?.Invoke();
        table.Controls.Add(cellLink, 1, 7);
        _create = AppTheme.PrimaryButton("Job 생성 완료", 160);
        _create.Enabled = false;
        _create.Click += (_, _) => Create();
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        actions.Controls.Add(_create);
        table.Controls.Add(actions, 1, 8);
        card.Controls.Add(table);
        Controls.Add(card);
        Controls.Add(top);

        _product.SelectedIndexChanged += (_, _) => ProductChanged();
        _recipe.SelectedIndexChanged += (_, _) => RecipeChanged();
        _cell.SelectedIndexChanged += (_, _) => ValidateForm();
        _customer.TextChanged += (_, _) => ValidateForm();
        _request.TextChanged += (_, _) => ValidateForm();
        _lot.TextChanged += (_, _) => ValidateForm();
    }

    public event Action? CancelRequested;
    public event Action? CellListRequested;
    public event Action<JobCreationRequest>? CreateRequested;

    private void ProductChanged()
    {
        _recipe.Items.Clear();
        _cell.Items.Clear();
        _cell.Enabled = false;
        if (_product.SelectedItem is not ProductDocument product)
        {
            _recipe.Enabled = false;
            return;
        }
        foreach (var recipe in _recipes.Where(r =>
                     product.AllowedRecipeIds.Contains(r.RecipeId, StringComparer.OrdinalIgnoreCase)))
            _recipe.Items.Add(recipe);
        _recipe.DisplayMember = nameof(RecipeDocument.Name);
        _recipe.Enabled = _recipe.Items.Count > 0;
        _message.Text = _recipe.Items.Count == 0
            ? "이 Product와 호환되는 Recipe가 없습니다."
            : $"{product.WaferDiameterMm} mm · Die {product.DieWidthMm:0.##}×{product.DieHeightMm:0.##} mm · 합격 수율 {product.AcceptanceYieldPercent:0.##}%";
        ValidateForm();
    }

    private void RecipeChanged()
    {
        _cell.Items.Clear();
        if (_recipe.SelectedItem is not RecipeDocument recipe ||
            _product.SelectedItem is not ProductDocument product)
        {
            _cell.Enabled = false;
            ValidateForm();
            return;
        }

        foreach (var item in _cells.Where(x =>
                     x.ConnectionStatus == ConnectionStatus.Connected &&
                     recipe.CompatibleTestCellIds.Contains(x.Definition.Id, StringComparer.OrdinalIgnoreCase) &&
                     x.Definition.SupportedWaferDiametersMm.Contains(product.WaferDiameterMm) &&
                     recipe.RequiredCapabilities.All(capability =>
                         x.Definition.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))))
            _cell.Items.Add(new CellChoice(item));
        _cell.Enabled = _cell.Items.Count > 0;
        _message.Text = _cell.Items.Count == 0
            ? "호환되고 연결된 Test Cell이 없습니다. 장비 목록에서 Cell을 연결하세요."
            : "사용 중인 Test Cell도 배정할 수 있으며 유휴 상태가 된 뒤 사용자가 EDS를 시작합니다.";
        ValidateForm();
    }

    private void ValidateForm() => _create.Enabled =
        !string.IsNullOrWhiteSpace(_customer.Text) &&
        !string.IsNullOrWhiteSpace(_request.Text) &&
        !string.IsNullOrWhiteSpace(_lot.Text) &&
        _product.SelectedItem is ProductDocument &&
        _recipe.SelectedItem is RecipeDocument &&
        _cell.SelectedItem is CellChoice;

    private void Create()
    {
        if (_product.SelectedItem is not ProductDocument product ||
            _recipe.SelectedItem is not RecipeDocument recipe ||
            _cell.SelectedItem is not CellChoice cell)
            return;
        CreateRequested?.Invoke(new(
            _customer.Text.Trim(),
            _request.Text.Trim(),
            _lot.Text.Trim(),
            product,
            recipe,
            cell.State));
    }

    private static void AddField(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            ForeColor = AppTheme.Muted
        }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 8, 0, 8);
        table.Controls.Add(control, 1, row);
    }

    private sealed class CellChoice(TestCellState state)
    {
        public TestCellState State { get; } = state;
        public override string ToString() =>
            $"{State.Definition.Name} · {State.Definition.Tester.Model} / {State.Definition.Prober.Model} · {AppTheme.CellStatusText(State)}";
    }
}

internal sealed class JobDetailView : UserControl
{
    private readonly InspectionJob _job;
    private readonly TestCellState? _cell;
    private readonly Button _start;
    private readonly Button _configure;
    private readonly Label _startReason;
    private readonly Label _cellStatus;
    private readonly DataGridView _runs;

    public JobDetailView(InspectionJob job, TestCellState? cell)
    {
        _job = job;
        _cell = cell;
        BackColor = AppTheme.Background;
        Padding = new Padding(28);
        AutoScroll = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 64 };
        top.Controls.Add(AppTheme.Heading($"Job 상세 · {job.LotId}"));
        var back = AppTheme.SecondaryButton("전체 작업으로", 130);
        back.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => back.Left = top.ClientSize.Width - back.Width;
        back.Click += (_, _) => BackRequested?.Invoke();
        top.Controls.Add(back);

        var info = AppTheme.CardPanel();
        info.Dock = DockStyle.Top;
        info.Height = 285;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 6 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 6; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        AddInfo(table, 0, "Job ID", job.JobId);
        AddInfo(table, 1, "상태", AppTheme.JobText(job.Status));
        AddInfo(table, 2, "고객명", job.CustomerName);
        AddInfo(table, 3, "의뢰번호", job.RequestNumber);
        AddInfo(table, 4, "Lot ID", job.LotId);
        AddInfo(table, 5, "Product", job.ProductSnapshot.Name);
        AddInfo(table, 6, "Recipe", $"{job.RecipeSnapshot.Name} v{job.RecipeSnapshot.Version}");
        AddInfo(table, 7, "Test Cell", cell?.Definition.Name ?? job.TestCellSnapshot.Name);
        _cellStatus = AddInfo(table, 8, "Cell 상태", "");
        AddInfo(table, 9, "Tester", job.TestCellSnapshot.Tester.Model);
        AddInfo(table, 10, "Prober", job.TestCellSnapshot.Prober.Model);
        AddInfo(table, 11, "Probe Card", job.TestCellSnapshot.ProbeCard.Name);
        info.Controls.Add(table);

        var actionCard = AppTheme.CardPanel();
        actionCard.Dock = DockStyle.Top;
        actionCard.Height = 126;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58 };
        _configure = AppTheme.SecondaryButton("모의 결과 설정", 150);
        _configure.Click += (_, _) => ConfigureRequested?.Invoke();
        _start = AppTheme.PrimaryButton(job.Runs.Count == 0 ? "EDS 시작" : "새 Run 시작", 130);
        _start.Click += (_, _) => StartRequested?.Invoke();
        var delete = AppTheme.DangerButton("Job 삭제", 110);
        delete.Click += (_, _) => DeleteRequested?.Invoke();
        actions.Controls.Add(_configure);
        actions.Controls.Add(_start);
        actions.Controls.Add(delete);
        _startReason = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            ForeColor = AppTheme.Muted,
            Padding = new Padding(7, 0, 0, 0),
            AutoEllipsis = true
        };
        actionCard.Controls.Add(_startReason);
        actionCard.Controls.Add(actions);

        var history = AppTheme.CardPanel();
        history.Dock = DockStyle.Top;
        history.Height = 310;
        var title = AppTheme.Heading("Run 이력", 12F);
        title.Dock = DockStyle.Top;
        title.Height = 38;
        _runs = AppTheme.Grid();
        _runs.Dock = DockStyle.Fill;
        _runs.ReadOnly = true;
        _runs.Columns.Add("RunId", "Run ID");
        _runs.Columns.Add("Status", "상태");
        _runs.Columns.Add("Started", "시작");
        _runs.Columns.Add("Yield", "Lot 수율");
        _runs.Columns.Add("Artifacts", "결과");
        _runs.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _runs.Rows[e.RowIndex].Tag is JobRunSummary run)
                RunActivated?.Invoke(run);
        };
        history.Controls.Add(_runs);
        history.Controls.Add(title);
        Controls.Add(history);
        Controls.Add(actionCard);
        Controls.Add(info);
        Controls.Add(top);
        RefreshView();
    }

    public event Action? BackRequested;
    public event Action? ConfigureRequested;
    public event Action? StartRequested;
    public event Action? DeleteRequested;
    public event Action<JobRunSummary>? RunActivated;

    public void RefreshView()
    {
        var connected = _cell?.ConnectionStatus == ConnectionStatus.Connected;
        var busyByOther = _cell?.IsBusy == true &&
                          !string.Equals(_cell.ActiveJobId, _job.JobId, StringComparison.OrdinalIgnoreCase);
        var hasError = _cell?.HasError == true;
        _cellStatus.Text = _cell is null ? "Cell 정보 없음" : AppTheme.CellStatusText(_cell);
        _cellStatus.ForeColor = _cell?.IsReady == true ? AppTheme.Success :
            hasError ? AppTheme.Danger : AppTheme.Warning;
        _configure.Enabled = _job.Status != JobStatus.Running;
        var blockReason = JobStartValidator.GetBlockReason(_job, _cell);
        _start.Enabled = blockReason is null;
        _startReason.Text = blockReason ?? "EDS Run을 시작할 수 있습니다.";
        _runs.Rows.Clear();
        foreach (var run in _job.Runs.OrderByDescending(x => x.StartedAt))
        {
            var row = _runs.Rows[_runs.Rows.Add(
                run.RunId,
                AppTheme.JobText(run.Status),
                run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                run.LotYieldPercent is null ? "-" : $"{run.LotYieldPercent:0.00}%",
                File.Exists(run.ResultFilePath) ? "더블클릭하여 열기" : "결과 파일 없음")];
            row.Tag = run;
        }
    }

    private static Label AddInfo(TableLayoutPanel table, int index, string title, string value)
    {
        var row = index / 2;
        var col = (index % 2) * 2;
        table.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = AppTheme.Muted,
            Anchor = AnchorStyles.Left
        }, col, row);
        var label = new Label
        {
            Text = value,
            AutoSize = true,
            MaximumSize = new Size(400, 44),
            AutoEllipsis = true,
            ForeColor = AppTheme.Text,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Left
        };
        table.Controls.Add(label, col + 1, row);
        return label;
    }
}

internal sealed class SimulationSettingsView : UserControl
{
    private readonly InspectionJob _job;
    private readonly DataGridView _grid;
    private readonly ComboBox _speed;
    private readonly NumericUpDown _defaultYield;
    private readonly Dictionary<string, NumericUpDown> _distribution = new(StringComparer.OrdinalIgnoreCase);
    private readonly CheckBox _errorEnabled;
    private readonly ComboBox _errorComponent;
    private readonly ComboBox _errorWafer;
    private readonly ComboBox _errorStep;
    private readonly Label _message;

    public SimulationSettingsView(InspectionJob job)
    {
        _job = job;
        BackColor = AppTheme.Background;
        Padding = new Padding(24);
        var current = job.Simulation ?? JobSimulationSettings.CreateDefault(job.RecipeSnapshot);

        var top = new Panel { Dock = DockStyle.Top, Height = 62 };
        top.Controls.Add(AppTheme.Heading("모의 EDS 결과 설정"));
        var cancel = AppTheme.SecondaryButton("Job 상세로", 120);
        cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => cancel.Left = top.ClientSize.Width - cancel.Width;
        cancel.Click += (_, _) => CancelRequested?.Invoke();
        top.Controls.Add(cancel);

        var settingsCard = AppTheme.CardPanel();
        settingsCard.Dock = DockStyle.Top;
        settingsCard.Height = 205;
        var lotTitle = AppTheme.Heading("Lot 기본값", 11F);
        lotTitle.Location = new Point(14, 12);
        settingsCard.Controls.Add(lotTitle);
        settingsCard.Controls.Add(new Label
        {
            Text = "목표 수율",
            AutoSize = true,
            Location = new Point(15, 51),
            ForeColor = AppTheme.Muted
        });
        _defaultYield = NumericPercent(current.DefaultTargetYieldPercent);
        _defaultYield.Location = new Point(92, 46);
        settingsCard.Controls.Add(_defaultYield);
        settingsCard.Controls.Add(new Label
        {
            Text = "실행 속도",
            AutoSize = true,
            Location = new Point(218, 51),
            ForeColor = AppTheme.Muted
        });
        _speed = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(295, 46),
            Width = 100
        };
        _speed.Items.AddRange(["1×", "5×", "10×", "20×"]);
        _speed.SelectedItem = $"{current.SpeedFactor}×";
        if (_speed.SelectedIndex < 0) _speed.SelectedItem = "20×";
        settingsCard.Controls.Add(_speed);

        var x = 15;
        foreach (var bin in job.RecipeSnapshot.FailBins)
        {
            var box = NumericPercent(current.DefaultFailBinDistribution.GetValueOrDefault(bin.Code, 25));
            box.DecimalPlaces = 1;
            box.Width = 82;
            var label = new Label
            {
                Text = bin.Code,
                AutoSize = true,
                Location = new Point(x, 88),
                ForeColor = AppTheme.Muted
            };
            box.Location = new Point(x, 110);
            _distribution[bin.Code] = box;
            settingsCard.Controls.Add(label);
            settingsCard.Controls.Add(box);
            x += 190;
        }

        _errorEnabled = new CheckBox
        {
            Text = "구성품 오류 발생",
            AutoSize = true,
            Location = new Point(15, 160),
            Checked = current.CellError.Enabled
        };
        _errorComponent = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(150, 155),
            Width = 120
        };
        _errorComponent.Items.AddRange(["Tester", "Prober", "Probe Card"]);
        _errorComponent.SelectedItem = ComponentText(current.CellError.Component);
        _errorWafer = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(280, 155),
            Width = 110
        };
        _errorWafer.Items.AddRange(Enumerable.Range(1, 25).Select(i => $"Wafer{i:00}").Cast<object>().ToArray());
        _errorWafer.SelectedItem = current.CellError.WaferId;
        _errorStep = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(400, 155),
            Width = 310
        };
        _errorEnabled.CheckedChanged += (_, _) => UpdateErrorControls();
        _errorComponent.SelectedIndexChanged += (_, _) => UpdateErrorSteps(current.CellError.FailedStepId);
        settingsCard.Controls.Add(_errorEnabled);
        settingsCard.Controls.Add(_errorComponent);
        settingsCard.Controls.Add(_errorWafer);
        settingsCard.Controls.Add(_errorStep);

        var save = AppTheme.PrimaryButton("설정 저장", 120);
        save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        save.Location = new Point(settingsCard.Width - save.Width - 14, 151);
        settingsCard.Resize += (_, _) => save.Left = settingsCard.ClientSize.Width - save.Width - 14;
        save.Click += (_, _) => Save();
        settingsCard.Controls.Add(save);
        _message = new Label
        {
            AutoSize = true,
            ForeColor = AppTheme.Danger,
            Location = new Point(725, 160)
        };
        settingsCard.Controls.Add(_message);

        _grid = AppTheme.Grid();
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Wafer",
            HeaderText = "Wafer",
            ReadOnly = true,
            FillWeight = 65
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "UseDefault",
            HeaderText = "Lot 기본값 사용",
            FillWeight = 85
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Yield",
            HeaderText = "목표 수율 (%)",
            FillWeight = 90
        });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "DominantBin",
            HeaderText = "대표 실패 Final Bin (60%)",
            DataSource = new[] { "-" }.Concat(job.RecipeSnapshot.FailBins.Select(x => x.Code)).ToArray(),
            FillWeight = 150
        });
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex is 1 or 2)
                UpdateWaferRow(_grid.Rows[e.RowIndex]);
        };
        _grid.DataError += (_, _) => { };
        foreach (var wafer in current.Wafers)
        {
            var row = _grid.Rows[_grid.Rows.Add(
                wafer.WaferId,
                wafer.UseLotDefault,
                wafer.TargetYieldPercent?.ToString("0.00") ?? current.DefaultTargetYieldPercent.ToString("0.00"),
                wafer.DominantFailBinCode ?? "-")];
            UpdateWaferRow(row);
        }

        Controls.Add(_grid);
        Controls.Add(settingsCard);
        Controls.Add(top);
        UpdateErrorSteps(current.CellError.FailedStepId);
        UpdateErrorControls();
    }

    public event Action? CancelRequested;
    public event Action<JobSimulationSettings>? SaveRequested;

    private static NumericUpDown NumericPercent(double value) => new()
    {
        Minimum = 0,
        Maximum = 100,
        DecimalPlaces = 2,
        Increment = .1M,
        Width = 105,
        Value = (decimal)Math.Clamp(value, 0, 100)
    };

    private void UpdateWaferRow(DataGridViewRow row)
    {
        var useDefault = row.Cells["UseDefault"].Value is true;
        var targetText = row.Cells["Yield"].Value?.ToString();
        var targetIsHundred = double.TryParse(targetText, out var target) && target >= 100;
        row.Cells["Yield"].ReadOnly = useDefault;
        row.Cells["DominantBin"].ReadOnly = useDefault || targetIsHundred;
        row.Cells["Yield"].Style.BackColor = useDefault ? Color.FromArgb(238, 240, 242) : Color.White;
        row.Cells["DominantBin"].Style.BackColor =
            useDefault || targetIsHundred ? Color.FromArgb(238, 240, 242) : Color.White;
        if (useDefault || targetIsHundred) row.Cells["DominantBin"].Value = "-";
    }

    private void UpdateErrorControls()
    {
        _errorComponent.Enabled = _errorEnabled.Checked;
        _errorWafer.Enabled = _errorEnabled.Checked;
        _errorStep.Enabled = _errorEnabled.Checked;
    }

    private void UpdateErrorSteps(string? preferredStepId)
    {
        var component = ParseComponent(_errorComponent.SelectedItem?.ToString());
        var choices = _job.RecipeSnapshot.Steps
            .Where(x => x.AllowedErrorComponents.Contains(component))
            .ToList();
        _errorStep.Items.Clear();
        foreach (var step in choices) _errorStep.Items.Add(new StepChoice(step));
        var preferred = _errorStep.Items.OfType<StepChoice>()
            .FirstOrDefault(x => string.Equals(x.Step.Id, preferredStepId, StringComparison.OrdinalIgnoreCase));
        if (preferred is not null) _errorStep.SelectedItem = preferred;
        else if (_errorStep.Items.Count > 0) _errorStep.SelectedIndex = 0;
    }

    private void Save()
    {
        _message.Text = string.Empty;
        var distribution = _distribution.ToDictionary(
            x => x.Key,
            x => (double)x.Value.Value,
            StringComparer.OrdinalIgnoreCase);
        if (Math.Abs(distribution.Values.Sum() - 100) > .01)
        {
            _message.Text = "실패 Bin 분포 합계는 100%여야 합니다.";
            return;
        }

        var wafers = new List<WaferSimulationSetting>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var useDefault = row.Cells["UseDefault"].Value is true;
            if (!double.TryParse(row.Cells["Yield"].Value?.ToString(), out var target) ||
                target is < 0 or > 100)
            {
                _message.Text = $"{row.Cells["Wafer"].Value}: 목표 수율은 0~100이어야 합니다.";
                return;
            }
            var dominant = row.Cells["DominantBin"].Value?.ToString();
            if (!useDefault && target < 100 && (string.IsNullOrWhiteSpace(dominant) || dominant == "-"))
            {
                _message.Text = $"{row.Cells["Wafer"].Value}: 대표 Final Bin을 선택하세요.";
                return;
            }
            wafers.Add(new WaferSimulationSetting
            {
                WaferId = row.Cells["Wafer"].Value!.ToString()!,
                UseLotDefault = useDefault,
                TargetYieldPercent = useDefault ? null : target,
                DominantFailBinCode = useDefault || target >= 100 ? null : dominant
            });
        }

        var component = ParseComponent(_errorComponent.SelectedItem?.ToString());
        var step = _errorStep.SelectedItem as StepChoice;
        if (_errorEnabled.Checked && step is null)
        {
            _message.Text = "구성품 오류 단계를 선택하세요.";
            return;
        }
        SaveRequested?.Invoke(new JobSimulationSettings
        {
            SpeedFactor = _speed.SelectedItem?.ToString() switch
            {
                "1×" => 1,
                "5×" => 5,
                "10×" => 10,
                _ => 20
            },
            DefaultTargetYieldPercent = (double)_defaultYield.Value,
            DefaultFailBinDistribution = distribution,
            Wafers = wafers,
            CellError = new CellErrorSimulation
            {
                Enabled = _errorEnabled.Checked,
                Component = component,
                WaferId = _errorWafer.SelectedItem?.ToString() ?? "Wafer01",
                FailedStepId = step?.Step.Id ?? string.Empty
            }
        });
    }

    private static string ComponentText(TestCellComponent component) => component switch
    {
        TestCellComponent.Tester => "Tester",
        TestCellComponent.Prober => "Prober",
        _ => "Probe Card"
    };

    private static TestCellComponent ParseComponent(string? text) => text switch
    {
        "Prober" => TestCellComponent.Prober,
        "Probe Card" => TestCellComponent.ProbeCard,
        _ => TestCellComponent.Tester
    };

    private sealed class StepChoice(RecipeStep step)
    {
        public RecipeStep Step { get; } = step;
        public override string ToString() => $"{Step.Id} · {Step.Name}";
    }
}

internal sealed class JobProgressView : UserControl
{
    private readonly InspectionJob _job;
    private readonly JobRunResult _result;
    private readonly TestCellState _cell;
    private readonly Label _summary;
    private readonly Label _cellStatus;
    private readonly DataGridView _wafers;
    private readonly DataGridView _steps;
    private readonly TextBox _log;
    private readonly System.Windows.Forms.Timer _timer;

    public JobProgressView(InspectionJob job, JobRunResult result, TestCellState cell)
    {
        _job = job;
        _result = result;
        _cell = cell;
        BackColor = AppTheme.Background;
        Padding = new Padding(24);
        var header = new Panel { Dock = DockStyle.Top, Height = 104 };
        header.Controls.Add(AppTheme.Heading($"EDS Lot 진행 · {job.LotId}"));
        _summary = new Label
        {
            Location = new Point(2, 48),
            AutoSize = true,
            ForeColor = AppTheme.Text
        };
        _cellStatus = new Label
        {
            Location = new Point(2, 73),
            AutoSize = true,
            ForeColor = AppTheme.Muted
        };
        header.Controls.Add(_summary);
        header.Controls.Add(_cellStatus);
        var cancel = AppTheme.DangerButton("Run 취소", 110);
        cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cancel.Location = new Point(header.Width - cancel.Width, 5);
        header.Resize += (_, _) => cancel.Left = header.ClientSize.Width - cancel.Width;
        cancel.Click += (_, _) => CancelRequested?.Invoke();
        header.Controls.Add(cancel);

        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = AppTheme.Border };
        AppTheme.KeepSplitRatio(split, .5, 320, 360);
        _wafers = AppTheme.Grid();
        _wafers.Dock = DockStyle.Fill;
        _wafers.ReadOnly = true;
        _wafers.Columns.Add("Wafer", "Wafer");
        _wafers.Columns.Add("Status", "판정/상태");
        _wafers.Columns.Add("Yield", "수율");
        split.Panel1.Controls.Add(_wafers);
        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        AppTheme.KeepSplitRatio(right, .55, 180, 140);
        _steps = AppTheme.Grid();
        _steps.Dock = DockStyle.Fill;
        _steps.ReadOnly = true;
        _steps.Columns.Add("Sequence", "#");
        _steps.Columns.Add("Step", "현재 Wafer Recipe 단계");
        _steps.Columns.Add("Status", "상태");
        foreach (var step in job.RecipeSnapshot.Steps)
            _steps.Rows.Add(step.Sequence, step.Name, "대기");
        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Font = new Font("Consolas", 9F)
        };
        right.Panel1.Controls.Add(_steps);
        right.Panel2.Controls.Add(_log);
        split.Panel2.Controls.Add(right);
        Controls.Add(split);
        Controls.Add(header);
        _timer = new System.Windows.Forms.Timer { Interval = 400 };
        _timer.Tick += (_, _) => RefreshAll();
        _timer.Start();
        RefreshAll();
    }

    public string RunId => _result.RunId;
    public event Action? CancelRequested;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    public void ApplyProgress(LotRunProgress progress)
    {
        for (var index = 0; index < _steps.Rows.Count; index++)
        {
            var step = _job.RecipeSnapshot.Steps[index];
            _steps.Rows[index].Cells[2].Value =
                progress.CompletedStepIds.Contains(step.Id) ? "완료" :
                string.Equals(step.Id, progress.CurrentStepId, StringComparison.OrdinalIgnoreCase)
                    ? "진행 중"
                    : "대기";
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        _wafers.Rows.Clear();
        foreach (var wafer in _result.Wafers)
            _wafers.Rows.Add(
                wafer.WaferId,
                AppTheme.WaferText(wafer),
                wafer.YieldPercent is null ? "-" : $"{wafer.YieldPercent:0.00}%");
        _log.Text = string.Join(Environment.NewLine, _result.Logs);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
        var elapsed = DateTimeOffset.Now - _result.StartedAt;
        _summary.Text =
            $"전체 진행률 {_result.ProgressPercent}% · 현재 {_result.CurrentWaferId} · 경과 {elapsed:hh\\:mm\\:ss} · Passed {_result.PassedWaferCount} · Low Yield {_result.LowYieldWaferCount} · 미완료 {25 - _result.CompletedCount}";
        _cellStatus.Text =
            $"Test Cell  {AppTheme.CellStatusText(_cell)} · Tester {_cell.Definition.Tester.Model} · Prober {_cell.Definition.Prober.Model} · Probe Card 장착/정상";
    }
}

internal sealed class JobResultView : UserControl
{
    private readonly InspectionJob _job;
    private readonly JobRunSummary _summary;
    private readonly JobRunResult _result;
    private readonly ListBox _waferList;
    private readonly WaferMapControl _map;
    private readonly Label _waferSummary;
    private readonly Label _dieSummary = null!;
    private readonly DataGridView _binCounts;
    private readonly ComboBox _logFilter;
    private readonly TextBox _log;

    public JobResultView(InspectionJob job, JobRunSummary summary, JobRunResult result)
    {
        _job = job;
        _summary = summary;
        _result = result;
        BackColor = AppTheme.Background;
        Padding = new Padding(24);
        var top = new Panel { Dock = DockStyle.Top, Height = 68 };
        top.Controls.Add(AppTheme.Heading($"Job 결과 · {job.LotId}"));
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 610,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var back = AppTheme.SecondaryButton("Job 상세로", 120);
        back.Click += (_, _) => BackRequested?.Invoke();
        var saveReport = AppTheme.PrimaryButton("결과 보고서 저장", 155);
        saveReport.Enabled = !string.IsNullOrWhiteSpace(summary.ReportFilePath) &&
                             File.Exists(summary.ReportFilePath);
        saveReport.Click += (_, _) => SaveReportRequested?.Invoke(summary);
        var saveLog = AppTheme.SecondaryButton("로그 파일 저장", 140);
        saveLog.Enabled = File.Exists(summary.LogFilePath);
        saveLog.Click += (_, _) => SaveLogRequested?.Invoke(summary);
        actions.Controls.Add(back);
        actions.Controls.Add(saveReport);
        actions.Controls.Add(saveLog);
        top.Controls.Add(actions);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildSummaryTab());
        var waferTab = new TabPage("Wafer 상세")
        {
            BackColor = AppTheme.Background,
            Padding = new Padding(12)
        };
        var waferSplit = new SplitContainer { Dock = DockStyle.Fill };
        AppTheme.KeepSplitRatio(waferSplit, .18, 145, 600);
        _waferList = new ListBox { Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 10F) };
        foreach (var wafer in result.Wafers.Where(x => x.Status == WaferExecutionStatus.Completed))
            _waferList.Items.Add(wafer);
        _waferList.DisplayMember = nameof(WaferResult.WaferId);
        waferSplit.Panel1.Controls.Add(_waferList);
        var detailSplit = new SplitContainer { Dock = DockStyle.Fill };
        AppTheme.KeepSplitRatio(detailSplit, .62, 360, 300);
        _map = new WaferMapControl
        {
            Dock = DockStyle.Fill,
            Recipe = result.RecipeSnapshot
        };
        _map.DieSelected += die =>
            _dieSummary.Text = die is null || !die.IsValid
                ? "Die를 선택하면 좌표와 Final Bin을 표시합니다."
                : $"선택 Die  Row {die.Row}, Column {die.Column}\r\nFinal Bin  {die.FinalBinCode}";
        detailSplit.Panel1.Controls.Add(_map);
        var details = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = Color.White
        };
        _waferSummary = new Label
        {
            Dock = DockStyle.Top,
            Height = 102,
            ForeColor = AppTheme.Text,
            Font = new Font("맑은 고딕", 10F)
        };
        _dieSummary = new Label
        {
            Dock = DockStyle.Top,
            Height = 62,
            ForeColor = AppTheme.Muted
        };
        _binCounts = AppTheme.Grid();
        _binCounts.Dock = DockStyle.Fill;
        _binCounts.ReadOnly = true;
        _binCounts.Columns.Add("Bin", "Final Bin");
        _binCounts.Columns.Add("Count", "Die 수");
        details.Controls.Add(_binCounts);
        details.Controls.Add(_dieSummary);
        details.Controls.Add(_waferSummary);
        detailSplit.Panel2.Controls.Add(details);
        waferSplit.Panel2.Controls.Add(detailSplit);
        waferTab.Controls.Add(waferSplit);
        tabs.TabPages.Add(waferTab);
        tabs.TabPages.Add(BuildTestSummaryTab());

        var logTab = new TabPage("로그") { BackColor = AppTheme.Background, Padding = new Padding(12) };
        _logFilter = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 34
        };
        _logFilter.Items.Add("전체");
        _logFilter.Items.AddRange(Enumerable.Range(1, 25).Select(i => $"Wafer{i:00}").Cast<object>().ToArray());
        _logFilter.SelectedIndex = 0;
        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F)
        };
        _logFilter.SelectedIndexChanged += (_, _) => RefreshLog();
        logTab.Controls.Add(_log);
        logTab.Controls.Add(_logFilter);
        tabs.TabPages.Add(logTab);
        Controls.Add(tabs);
        Controls.Add(top);
        _waferList.SelectedIndexChanged += (_, _) => RefreshWafer();
        if (_waferList.Items.Count > 0) _waferList.SelectedIndex = 0;
        RefreshLog();
    }

    public event Action? BackRequested;
    public event Action<JobRunSummary>? SaveLogRequested;
    public event Action<JobRunSummary>? SaveReportRequested;

    private TabPage BuildSummaryTab()
    {
        var tab = new TabPage("Lot 요약") { BackColor = AppTheme.Background, Padding = new Padding(12) };
        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 105,
            BackColor = Color.White,
            Padding = new Padding(20, 14, 20, 0),
            Font = new Font("맑은 고딕", 10.5F, FontStyle.Bold),
            Text =
                $"상태  {AppTheme.JobText(_result.Status)}\r\nLot 수율  {_result.LotYieldPercent:0.00}%    Passed {_result.PassedWaferCount}    Low Yield {_result.LowYieldWaferCount}    PASS die {_result.PassDieCount:N0}    FAIL die {_result.FailDieCount:N0}\r\nTest Cell  {_result.TestCellSnapshot.Name}    Recipe  {_result.RecipeSnapshot.Name}"
        };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        AppTheme.KeepSplitRatio(split, .62, 260, 150);
        var grid = AppTheme.Grid();
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.Columns.Add("Wafer", "Wafer");
        grid.Columns.Add("Status", "판정/상태");
        grid.Columns.Add("Yield", "수율");
        grid.Columns.Add("Pass", "PASS die");
        grid.Columns.Add("Fail", "FAIL die");
        grid.Columns.Add("MainBin", "주요 실패 Bin");
        foreach (var wafer in _result.Wafers)
        {
            var main = wafer.BinCounts
                .Where(x => !string.Equals(x.Key, _result.RecipeSnapshot.PassBin.Code, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault() ?? "-";
            grid.Rows.Add(
                wafer.WaferId,
                AppTheme.WaferText(wafer),
                wafer.YieldPercent is null ? "-" : $"{wafer.YieldPercent:0.00}%",
                wafer.PassDieCount,
                Math.Max(0, wafer.ValidDieCount - wafer.PassDieCount),
                main);
        }
        var pareto = new BinParetoControl
        {
            Dock = DockStyle.Fill,
            Recipe = _result.RecipeSnapshot,
            Counts = _result.GetBinCounts()
        };
        split.Panel1.Controls.Add(grid);
        split.Panel2.Controls.Add(pareto);
        tab.Controls.Add(split);
        tab.Controls.Add(header);
        return tab;
    }

    private TabPage BuildTestSummaryTab()
    {
        var tab = new TabPage("Test 요약") { BackColor = AppTheme.Background, Padding = new Padding(12) };
        var grid = AppTheme.Grid();
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.Columns.Add("Sequence", "#");
        grid.Columns.Add("Step", "Recipe 단계");
        grid.Columns.Add("Bin", "연결 Final Bin");
        grid.Columns.Add("Fail", "실패 die");
        grid.Columns.Add("Rate", "전체 die 대비");
        grid.Columns.Add("Wafers", "영향 Wafer");
        var total = Math.Max(1, _result.Wafers.Sum(x => x.ValidDieCount));
        foreach (var step in _result.RecipeSnapshot.Steps)
        {
            var bins = _result.RecipeSnapshot.FailBins
                .Where(x => string.Equals(x.RelatedStepId, step.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var codes = bins.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fail = _result.Wafers.Sum(w => w.Dies.Count(d => d.IsValid && codes.Contains(d.FinalBinCode)));
            var affected = _result.Wafers.Count(w => w.Dies.Any(d => d.IsValid && codes.Contains(d.FinalBinCode)));
            grid.Rows.Add(
                step.Sequence,
                step.Name,
                bins.Count == 0 ? "-" : string.Join(", ", bins.Select(x => x.Code)),
                fail,
                $"{fail * 100d / total:0.000}%",
                affected);
        }
        tab.Controls.Add(grid);
        return tab;
    }

    private void RefreshWafer()
    {
        if (_waferList.SelectedItem is not WaferResult wafer) return;
        _map.Wafer = wafer;
        _waferSummary.Text =
            $"{wafer.WaferId}\r\n판정  {AppTheme.WaferText(wafer)}\r\n수율  {wafer.YieldPercent:0.00}% · PASS {wafer.PassDieCount:N0} / {wafer.ValidDieCount:N0}";
        _dieSummary.Text = "Die를 선택하면 좌표와 Final Bin을 표시합니다.";
        _binCounts.Rows.Clear();
        foreach (var bin in _result.RecipeSnapshot.FinalBins)
            _binCounts.Rows.Add(bin.Code, wafer.BinCounts.GetValueOrDefault(bin.Code));
    }

    private void RefreshLog()
    {
        var filter = _logFilter.SelectedItem?.ToString();
        var entries = filter == "전체"
            ? _result.Logs
            : _result.Logs.Where(x => string.Equals(x.WaferId, filter, StringComparison.OrdinalIgnoreCase)).ToList();
        _log.Text = string.Join(Environment.NewLine, entries);
    }
}

internal sealed class WaferMapControl : Control
{
    private WaferResult? _wafer;
    private readonly Dictionary<RectangleF, DieResult> _hitAreas = [];
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RecipeDocument? Recipe { get; set; }
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public WaferResult? Wafer
    {
        get => _wafer;
        set
        {
            _wafer = value;
            Invalidate();
        }
    }

    public WaferMapControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Cursor = Cursors.Hand;
    }

    public event Action<DieResult?>? DieSelected;

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var die = _hitAreas.FirstOrDefault(x => x.Key.Contains(e.Location)).Value;
        DieSelected?.Invoke(die);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        _hitAreas.Clear();
        if (_wafer is null || _wafer.GridRows == 0 || _wafer.GridColumns == 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var margin = 24f;
        var size = Math.Min(ClientSize.Width - margin * 2, ClientSize.Height - margin * 2);
        if (size <= 20) return;
        var originX = (ClientSize.Width - size) / 2f;
        var originY = (ClientSize.Height - size) / 2f;
        using var waferBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var outline = new Pen(Color.FromArgb(80, 95, 108), 2);
        e.Graphics.FillEllipse(waferBrush, originX, originY, size, size);
        e.Graphics.DrawEllipse(outline, originX, originY, size, size);
        var cellW = size / _wafer.GridColumns;
        var cellH = size / _wafer.GridRows;
        var colors = Recipe?.FinalBins.ToDictionary(
            x => x.Code,
            x => TestCellCatalog.ParseColor(x.ColorHex),
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, Color>();
        using var border = new Pen(Color.FromArgb(80, Color.White), .5f);
        foreach (var die in _wafer.Dies.Where(x => x.IsValid))
        {
            var rect = new RectangleF(
                originX + die.Column * cellW,
                originY + die.Row * cellH,
                Math.Max(1, cellW),
                Math.Max(1, cellH));
            var color = colors.GetValueOrDefault(die.FinalBinCode, Color.Gray);
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, rect);
            if (cellW > 3 && cellH > 3) e.Graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            _hitAreas[rect] = die;
        }
    }
}

internal sealed class BinParetoControl : Control
{
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RecipeDocument? Recipe { get; set; }
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyDictionary<string, int> Counts { get; set; } =
        new Dictionary<string, int>();

    public BinParetoControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Recipe is null) return;
        e.Graphics.DrawString(
            "Final Bin Pareto",
            new Font("맑은 고딕", 10F, FontStyle.Bold),
            new SolidBrush(AppTheme.Text),
            14,
            10);
        var bins = Recipe.FailBins
            .Select(x => (Bin: x, Count: Counts.GetValueOrDefault(x.Code)))
            .OrderByDescending(x => x.Count)
            .ToList();
        var max = Math.Max(1, bins.Max(x => x.Count));
        var top = 42f;
        var rowHeight = Math.Max(22f, (ClientSize.Height - top - 10) / Math.Max(1, bins.Count));
        foreach (var (item, index) in bins.Select((item, index) => (item, index)))
        {
            var y = top + index * rowHeight;
            e.Graphics.DrawString(item.Bin.Code, Font, new SolidBrush(AppTheme.Text), 14, y + 2);
            var barX = 185f;
            var width = Math.Max(1, (ClientSize.Width - barX - 90) * item.Count / max);
            using var brush = new SolidBrush(TestCellCatalog.ParseColor(item.Bin.ColorHex));
            e.Graphics.FillRectangle(brush, barX, y + 3, width, rowHeight - 8);
            e.Graphics.DrawString(item.Count.ToString("N0"), Font, new SolidBrush(AppTheme.Muted), barX + width + 8, y + 2);
        }
    }
}

internal sealed class TestCellListView : UserControl
{
    private readonly FlowLayoutPanel _cards;

    public TestCellListView(IReadOnlyList<TestCellState> cells)
    {
        BackColor = AppTheme.Background;
        Padding = new Padding(28);
        var header = new Panel { Dock = DockStyle.Top, Height = 80 };
        header.Controls.Add(AppTheme.Heading("장비 목록"));
        header.Controls.Add(new Label
        {
            Text = "EDS에 배정 가능한 Test Cell과 작업 상태를 확인합니다.",
            AutoSize = true,
            ForeColor = AppTheme.Muted,
            Location = new Point(0, 42)
        });
        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        foreach (var cell in cells)
        {
            var card = new TestCellSummaryCard(cell);
            card.Activated += () => CellActivated?.Invoke(cell);
            _cards.Controls.Add(card);
        }
        Controls.Add(_cards);
        Controls.Add(header);
    }

    public event Action<TestCellState>? CellActivated;
    public void RefreshStates()
    {
        foreach (var card in _cards.Controls.OfType<TestCellSummaryCard>()) card.RefreshState();
    }
}

internal sealed class TestCellSummaryCard : Panel
{
    private readonly TestCellState _cell;
    private readonly Label _status;
    private readonly Label _current;
    private readonly PictureBox _picture;

    public TestCellSummaryCard(TestCellState cell)
    {
        _cell = cell;
        Width = 370;
        Height = 340;
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 16, 16);
        Cursor = Cursors.Hand;
        _picture = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 155,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(237, 242, 246),
            Image = TestCellImageFactory.Create(cell.Definition)
        };
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(16, 10, 16, 0),
            Text = cell.Definition.Name,
            Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
            AutoEllipsis = true
        };
        var detail = new Label
        {
            Dock = DockStyle.Top,
            Height = 66,
            Padding = new Padding(16, 4, 16, 0),
            ForeColor = AppTheme.Muted,
            AutoEllipsis = true,
            Text = $"{cell.Definition.Line}\r\nTester  {cell.Definition.Tester.Model}\r\nProber  {cell.Definition.Prober.Model}"
        };
        _status = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(16, 5, 16, 0),
            Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold)
        };
        _current = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 3, 16, 0),
            ForeColor = AppTheme.Muted,
            AutoEllipsis = true
        };
        Controls.Add(_current);
        Controls.Add(_status);
        Controls.Add(detail);
        Controls.Add(title);
        Controls.Add(_picture);
        Wire(this);
        RefreshState();
    }

    public event Action? Activated;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _picture.Image?.Dispose();
        base.Dispose(disposing);
    }

    public void RefreshState()
    {
        _status.Text = AppTheme.CellStatusText(_cell);
        _status.ForeColor = _cell.HasError ? AppTheme.Danger :
            _cell.IsBusy ? AppTheme.Primary :
            _cell.ConnectionStatus == ConnectionStatus.Connected ? AppTheme.Success : AppTheme.Warning;
        _current.Text = _cell.IsBusy
            ? $"현재 Job  {_cell.ActiveJobId} · {_cell.CurrentWaferId}"
            : _cell.HasError
                ? _cell.ErrorMessage
                : $"연결  {_cell.ConnectionStatus} · {_cell.Definition.IpAddress}:{_cell.Definition.Port}";
    }

    private void Wire(Control control)
    {
        control.DoubleClick += (_, _) => Activated?.Invoke();
        foreach (Control child in control.Controls) Wire(child);
    }
}

internal sealed class TestCellDetailView : UserControl
{
    private readonly TestCellState _cell;
    private readonly Label _integratedStatus;
    private readonly Label _testerStatus;
    private readonly Label _proberStatus;
    private readonly Label _cardStatus;
    private readonly Label _currentJob;
    private readonly Label _error;
    private readonly Button _toggle;
    private readonly Button _reset;
    private readonly DataGridView _history;
    private readonly IReadOnlyList<InspectionJob> _jobs;

    public TestCellDetailView(TestCellState cell, IReadOnlyList<InspectionJob> jobs)
    {
        _cell = cell;
        _jobs = jobs;
        BackColor = AppTheme.Background;
        Padding = new Padding(24);
        AutoScroll = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 64 };
        top.Controls.Add(AppTheme.Heading(cell.Definition.Name));
        var back = AppTheme.SecondaryButton("장비 목록으로", 130);
        back.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => back.Left = top.ClientSize.Width - back.Width;
        back.Click += (_, _) => BackRequested?.Invoke();
        top.Controls.Add(back);

        var card = AppTheme.CardPanel();
        card.Dock = DockStyle.Top;
        card.Height = 350;
        var picture = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 390,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = TestCellImageFactory.Create(cell.Definition)
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(20, 6, 0, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 9; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 9));
        AddInfo(table, 0, "Line", cell.Definition.Line);
        AddInfo(table, 1, "Tester", cell.Definition.Tester.DisplayName);
        AddInfo(table, 2, "Prober", cell.Definition.Prober.DisplayName);
        AddInfo(table, 3, "Probe Card", cell.Definition.ProbeCard.Name);
        AddInfo(table, 4, "IP / Port", $"{cell.Definition.IpAddress}:{cell.Definition.Port}");
        _integratedStatus = AddInfo(table, 5, "통합 작업 상태", "");
        _testerStatus = AddInfo(table, 6, "Tester 상태", "");
        _proberStatus = AddInfo(table, 7, "Prober 상태", "");
        _cardStatus = AddInfo(table, 8, "Probe Card 상태", "");
        card.Controls.Add(table);
        card.Controls.Add(picture);

        var action = AppTheme.CardPanel();
        action.Dock = DockStyle.Top;
        action.Height = 118;
        _toggle = AppTheme.SecondaryButton("Cell 연결", 120);
        _toggle.Click += (_, _) => ConnectionToggleRequested?.Invoke();
        _reset = AppTheme.DangerButton("오류 리셋", 120);
        _reset.Click += (_, _) => ErrorResetRequested?.Invoke();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 280 };
        actions.Controls.Add(_toggle);
        actions.Controls.Add(_reset);
        _currentJob = new Label
        {
            Dock = DockStyle.Top,
            Height = 35,
            ForeColor = AppTheme.Text,
            Padding = new Padding(8, 6, 0, 0)
        };
        _error = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.Danger,
            Padding = new Padding(8, 4, 0, 0),
            AutoEllipsis = true
        };
        action.Controls.Add(_error);
        action.Controls.Add(_currentJob);
        action.Controls.Add(actions);

        var historyCard = AppTheme.CardPanel();
        historyCard.Dock = DockStyle.Top;
        historyCard.Height = 280;
        var historyTitle = AppTheme.Heading("최근 Run", 12F);
        historyTitle.Dock = DockStyle.Top;
        historyTitle.Height = 38;
        _history = AppTheme.Grid();
        _history.Dock = DockStyle.Fill;
        _history.ReadOnly = true;
        _history.Columns.Add("Job", "Job ID");
        _history.Columns.Add("Lot", "Lot ID");
        _history.Columns.Add("Run", "Run ID");
        _history.Columns.Add("Status", "상태");
        _history.Columns.Add("Started", "시작");
        historyCard.Controls.Add(_history);
        historyCard.Controls.Add(historyTitle);
        Controls.Add(historyCard);
        Controls.Add(action);
        Controls.Add(card);
        Controls.Add(top);
        RefreshView();
    }

    public event Action? BackRequested;
    public event Action? ConnectionToggleRequested;
    public event Action? ErrorResetRequested;

    public void RefreshView()
    {
        _integratedStatus.Text = AppTheme.CellStatusText(_cell);
        _integratedStatus.ForeColor = _cell.HasError ? AppTheme.Danger :
            _cell.IsBusy ? AppTheme.Primary :
            _cell.ConnectionStatus == ConnectionStatus.Connected ? AppTheme.Success : AppTheme.Warning;
        _testerStatus.Text = ComponentStatus(TestCellComponent.Tester);
        _proberStatus.Text = ComponentStatus(TestCellComponent.Prober);
        _cardStatus.Text = _cell.ErrorComponent == TestCellComponent.ProbeCard ? "오류" :
            _cell.IsBusy ? "장착 / 사용 중" : "장착 / 정상";
        _currentJob.Text = _cell.IsBusy
            ? $"현재 Job  {_cell.ActiveJobId} · {_cell.CurrentWaferId} · {_cell.ProgressPercent}%"
            : "현재 작업 없음";
        _error.Text = _cell.HasError ? $"오류 원인  {_cell.ErrorMessage}" : string.Empty;
        _toggle.Text = _cell.ConnectionStatus == ConnectionStatus.Connected ? "Cell 연결 해제" : "Cell 연결";
        _toggle.Enabled = !_cell.IsBusy && _cell.ConnectionStatus != ConnectionStatus.Connecting;
        _reset.Enabled = _cell.HasError && !_cell.IsBusy;
        _history.Rows.Clear();
        foreach (var item in _jobs
                     .Where(x => string.Equals(x.TestCellId, _cell.Definition.Id, StringComparison.OrdinalIgnoreCase))
                     .SelectMany(job => job.Runs.Select(run => (job, run)))
                     .OrderByDescending(x => x.run.StartedAt)
                     .Take(10))
            _history.Rows.Add(
                item.job.JobId,
                item.job.LotId,
                item.run.RunId,
                AppTheme.JobText(item.run.Status),
                item.run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private string ComponentStatus(TestCellComponent component)
    {
        if (_cell.ConnectionStatus != ConnectionStatus.Connected) return "연결 해제";
        if (_cell.ErrorComponent == component) return "오류";
        return _cell.IsBusy ? "작업 중" : "유휴";
    }

    private static Label AddInfo(TableLayoutPanel table, int row, string title, string value)
    {
        table.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = AppTheme.Muted,
            Anchor = AnchorStyles.Left
        }, 0, row);
        var label = new Label
        {
            Text = value,
            AutoSize = true,
            MaximumSize = new Size(650, 44),
            AutoEllipsis = true,
            ForeColor = AppTheme.Text,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Left
        };
        table.Controls.Add(label, 1, row);
        return label;
    }
}
