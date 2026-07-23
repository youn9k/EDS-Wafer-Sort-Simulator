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

    public static Button PrimaryButton(string text, int width = 120) => Button(text, width, Primary, Color.White, 0);
    public static Button SecondaryButton(string text, int width = 110) => Button(text, width, Color.White, Text, 1);
    public static Button DangerButton(string text, int width = 110) => Button(text, width, Danger, Color.White, 0);

    private static Button Button(string text, int width, Color back, Color fore, int border)
    {
        var button = new Button
        {
            Text = text, Width = width, Height = 38, MinimumSize = new Size(width, 38),
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 0, 14, 0), BackColor = back, ForeColor = fore,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold), Margin = new Padding(6)
        };
        button.FlatAppearance.BorderSize = border;
        button.FlatAppearance.BorderColor = Border;
        return button;
    }

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
    }

    public static Label Heading(string text, float size = 17F) => new()
    {
        Text = text, AutoSize = true, ForeColor = Text,
        Font = new Font("맑은 고딕", size, FontStyle.Bold)
    };

    public static Panel CardPanel() => new()
    {
        BackColor = Surface, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 14),
        BorderStyle = BorderStyle.FixedSingle
    };

    public static DataGridView Grid() => new()
    {
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeight = 34,
        RowTemplate = { Height = 30 }
    };
}

internal static class EquipmentImageFactory
{
    public static Bitmap Create(EquipmentDefinition equipment, int width = 260, int height = 145)
    {
        if (!string.IsNullOrWhiteSpace(equipment.ImagePath))
        {
            try
            {
                using var stream = File.OpenRead(equipment.ImagePath);
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // 실행 중 이미지가 삭제되거나 손상돼도 장비 카드는 계속 표시한다.
            }
        }

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(235, 240, 244));
        using var shadow = new SolidBrush(Color.FromArgb(25, Color.Black));
        using var body = new SolidBrush(Color.FromArgb(218, 225, 231));
        using var front = new SolidBrush(Color.FromArgb(247, 249, 251));
        using var accent = new SolidBrush(equipment.AccentColor);
        using var dark = new SolidBrush(Color.FromArgb(54, 65, 75));
        using var glass = new SolidBrush(Color.FromArgb(96, 173, 207));
        using var pen = new Pen(Color.FromArgb(148, 160, 171), 2);

        FillRoundedRectangle(graphics, shadow, new Rectangle(42, 25, 176, 111), 10);
        FillRoundedRectangle(graphics, body, new Rectangle(36, 17, 176, 111), 10);
        graphics.FillRectangle(front, 51, 30, 146, 83);
        graphics.FillRectangle(accent, 51, 30, 146, 9);
        graphics.FillEllipse(dark, 82, 50, 83, 54);
        graphics.FillEllipse(glass, 93, 58, 61, 38);
        graphics.DrawEllipse(pen, 93, 58, 61, 38);
        graphics.FillRectangle(dark, 59, 47, 13, 47);
        graphics.FillEllipse(accent, 61, 52, 9, 9);
        graphics.FillRectangle(accent, 176, 48, 10, 48);
        graphics.FillRectangle(dark, 53, 117, 22, 11);
        graphics.FillRectangle(dark, 173, 117, 22, 11);

        using var font = new Font("Segoe UI", 8F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(70, 80, 90));
        graphics.DrawString(equipment.Model, font, textBrush, new PointF(169, 104));
        return bitmap;
    }

    private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}

internal sealed class JobCard : Panel
{
    private readonly InspectionJob _job;
    private readonly Func<string, EquipmentState?> _equipment;
    private readonly Label _status;
    private readonly Label _progress;

    public JobCard(InspectionJob job, Func<string, EquipmentState?> equipment)
    {
        _job = job; _equipment = equipment;
        Width = 340; Height = 260; BackColor = Color.White; BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 16, 16); Padding = new Padding(18); Cursor = Cursors.Hand;

        var title = new Label
        {
            Text = job.LotId, Dock = DockStyle.Top, Height = 34, Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
            ForeColor = AppTheme.Text, AutoEllipsis = true
        };
        _status = new Label
        {
            Dock = DockStyle.Top, Height = 28, Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
        };
        var detail = new Label
        {
            Dock = DockStyle.Top, Height = 128, ForeColor = AppTheme.Muted,
            Text = $"Job ID  {job.JobId}\r\n고객  {job.CustomerName}\r\n의뢰번호  {job.RequestNumber}\r\n제품  {job.ProductSnapshot.Name}\r\n레시피  {job.RecipeSnapshot.Name}\r\n장비  {EquipmentName()}"
        };
        _progress = new Label { Dock = DockStyle.Bottom, Height = 28, ForeColor = AppTheme.Text };
        Controls.Add(_progress); Controls.Add(detail); Controls.Add(_status); Controls.Add(title);
        Wire(this); RefreshState();
    }

    public event Action? Activated;
    public void RefreshState()
    {
        var suffix = _job.Status == JobStatus.Completed && _job.HasNgWafers ? " · NG 포함" : string.Empty;
        _status.Text = $"{JobText(_job.Status)}{suffix}";
        _status.ForeColor = _job.Status switch
        {
            JobStatus.Completed when !_job.HasNgWafers => AppTheme.Success,
            JobStatus.Running => AppTheme.Primary,
            JobStatus.Pending => AppTheme.Warning,
            _ => AppTheme.Danger
        };
        var last = _job.Runs.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        _progress.Text = _job.Status == JobStatus.Running
            ? $"진행률 {_job.ProgressPercent}%  ·  {_job.CurrentWaferId}"
            : last is null ? "아직 실행되지 않음" : $"최근 실행  {last.StartedAt:yyyy-MM-dd HH:mm}";
    }

    private string EquipmentName() => _equipment(_job.EquipmentId)?.Definition.Name ?? _job.EquipmentId;
    private void Wire(Control control)
    {
        control.Click += (_, _) => Activated?.Invoke();
        foreach (Control child in control.Controls) Wire(child);
    }

    internal static string JobText(JobStatus status) => status switch
    {
        JobStatus.Pending => "대기", JobStatus.Running => "진행 중", JobStatus.Completed => "완료",
        JobStatus.Failed => "실패", JobStatus.Canceled => "취소", _ => "중단"
    };
}

internal sealed class CreateJobCard : Panel
{
    public CreateJobCard()
    {
        Width = 340; Height = 260; BackColor = Color.FromArgb(236, 244, 252);
        BorderStyle = BorderStyle.FixedSingle; Margin = new Padding(0, 0, 16, 16); Cursor = Cursors.Hand;
        var label = new Label
        {
            Dock = DockStyle.Fill, Text = "＋\r\nJob 생성", TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = AppTheme.Primary, Font = new Font("맑은 고딕", 15F, FontStyle.Bold), Cursor = Cursors.Hand
        };
        label.Click += (_, _) => Activated?.Invoke();
        Controls.Add(label);
        Click += (_, _) => Activated?.Invoke();
    }
    public event Action? Activated;
}

internal sealed class JobListView : UserControl
{
    private readonly FlowLayoutPanel _cards;
    private readonly TextBox _search;
    private readonly ComboBox _filter;
    private IReadOnlyList<InspectionJob> _jobs = [];
    private Func<string, EquipmentState?> _equipment = _ => null;

    public JobListView()
    {
        BackColor = AppTheme.Background; Padding = new Padding(28);
        var header = new Panel { Dock = DockStyle.Top, Height = 96 };
        header.Controls.Add(AppTheme.Heading("전체 작업"));
        _search = new TextBox { PlaceholderText = "고객명, 의뢰번호, Lot ID 검색", Width = 300, Location = new Point(0, 50) };
        _filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Location = new Point(315, 48) };
        _filter.Items.AddRange(["전체", "대기", "진행", "완료", "실패·중단·취소"]);
        _filter.SelectedIndex = 0;
        _search.TextChanged += (_, _) => Render();
        _filter.SelectedIndexChanged += (_, _) => Render();
        header.Controls.Add(_search); header.Controls.Add(_filter);
        _cards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(0, 10, 0, 0) };
        Controls.Add(_cards); Controls.Add(header);
    }

    public event Action? CreateRequested;
    public event Action<InspectionJob>? JobActivated;
    public void SetJobs(IReadOnlyList<InspectionJob> jobs, Func<string, EquipmentState?> equipment)
    { _jobs = jobs; _equipment = equipment; Render(); }

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
        foreach (Control item in _cards.Controls) item.Dispose();
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
            var card = new JobCard(job, _equipment);
            card.Activated += () => JobActivated?.Invoke(job);
            _cards.Controls.Add(card);
        }
        _cards.ResumeLayout();
    }
}

internal sealed record JobCreationRequest(
    string CustomerName, string RequestNumber, string LotId,
    ProductDocument Product, RecipeDocument Recipe, EquipmentState Equipment);

internal sealed class JobCreateView : UserControl
{
    private readonly TextBox _customer = new();
    private readonly TextBox _request = new();
    private readonly TextBox _lot = new();
    private readonly ComboBox _product = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _recipe = new() { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly ComboBox _equipment = new() { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly Label _message = new();
    private readonly Button _create;
    private readonly IReadOnlyList<RecipeDocument> _recipes;
    private readonly IReadOnlyList<EquipmentState> _equipmentItems;

    public JobCreateView(IReadOnlyList<ProductDocument> products, IReadOnlyList<RecipeDocument> recipes, IReadOnlyList<EquipmentState> equipment)
    {
        _recipes = recipes; _equipmentItems = equipment;
        BackColor = AppTheme.Background; Padding = new Padding(28); AutoScroll = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 62 };
        top.Controls.Add(AppTheme.Heading("Job 생성"));
        var cancel = AppTheme.SecondaryButton("전체 작업으로", 120);
        cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => cancel.Left = top.ClientSize.Width - cancel.Width;
        cancel.Click += (_, _) => CancelRequested?.Invoke();
        top.Controls.Add(cancel);

        var card = AppTheme.CardPanel(); card.Dock = DockStyle.Top; card.Height = 570;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new Padding(10) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 6; index++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        AddField(table, 0, "고객명", _customer); AddField(table, 1, "고객 의뢰번호", _request); AddField(table, 2, "Lot ID", _lot);
        foreach (var product in products) _product.Items.Add(product);
        _product.DisplayMember = nameof(ProductDocument.Name);
        AddField(table, 3, "검사 대상", _product); AddField(table, 4, "레시피", _recipe); AddField(table, 5, "장비", _equipment);
        _message.Dock = DockStyle.Fill; _message.ForeColor = AppTheme.Muted; _message.Padding = new Padding(0, 8, 0, 0);
        table.Controls.Add(_message, 1, 6);
        var equipmentLink = new LinkLabel { Text = "장비 목록으로 이동", AutoSize = true, Anchor = AnchorStyles.Left };
        equipmentLink.Click += (_, _) => EquipmentListRequested?.Invoke();
        table.Controls.Add(equipmentLink, 1, 7);
        _create = AppTheme.PrimaryButton("Job 생성 완료", 150); _create.Enabled = false;
        _create.Click += (_, _) => Create();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.Add(_create); table.Controls.Add(actions, 1, 8);
        card.Controls.Add(table);
        Controls.Add(card); Controls.Add(top);

        _product.SelectedIndexChanged += (_, _) => ProductChanged();
        _recipe.SelectedIndexChanged += (_, _) => RecipeChanged();
        _equipment.SelectedIndexChanged += (_, _) => ValidateForm();
        _customer.TextChanged += (_, _) => ValidateForm(); _request.TextChanged += (_, _) => ValidateForm(); _lot.TextChanged += (_, _) => ValidateForm();
    }

    public event Action? CancelRequested;
    public event Action? EquipmentListRequested;
    public event Action<JobCreationRequest>? CreateRequested;

    private void ProductChanged()
    {
        _recipe.Items.Clear(); _equipment.Items.Clear(); _equipment.Enabled = false;
        if (_product.SelectedItem is not ProductDocument product) { _recipe.Enabled = false; return; }
        foreach (var recipe in _recipes.Where(r => product.AllowedRecipeIds.Contains(r.RecipeId, StringComparer.OrdinalIgnoreCase)))
            _recipe.Items.Add(recipe);
        _recipe.DisplayMember = nameof(RecipeDocument.Name);
        _recipe.Enabled = _recipe.Items.Count > 0;
        _message.Text = _recipe.Items.Count == 0 ? "이 검사 대상과 호환되는 레시피가 없습니다." :
            $"{product.WaferDiameterMm} mm · {product.Material} · 합격 수율 {product.AcceptanceYieldPercent:0.##}%";
        ValidateForm();
    }

    private void RecipeChanged()
    {
        _equipment.Items.Clear();
        if (_recipe.SelectedItem is not RecipeDocument recipe) { _equipment.Enabled = false; ValidateForm(); return; }
        foreach (var item in _equipmentItems.Where(x =>
                     x.ConnectionStatus == ConnectionStatus.Connected &&
                     recipe.CompatibleEquipmentModels.Contains(x.Definition.Model, StringComparer.OrdinalIgnoreCase)))
            _equipment.Items.Add(new EquipmentChoice(item));
        _equipment.Enabled = _equipment.Items.Count > 0;
        _message.Text = _equipment.Items.Count == 0
            ? "호환되고 연결된 장비가 없습니다. 장비 목록에서 장비를 연결하세요."
            : "사용 중인 장비도 배정할 수 있으며 장비가 유휴 상태일 때 사용자가 검사를 시작합니다.";
        ValidateForm();
    }

    private void ValidateForm() => _create.Enabled =
        !string.IsNullOrWhiteSpace(_customer.Text) && !string.IsNullOrWhiteSpace(_request.Text) &&
        !string.IsNullOrWhiteSpace(_lot.Text) && _product.SelectedItem is ProductDocument &&
        _recipe.SelectedItem is RecipeDocument && _equipment.SelectedItem is EquipmentChoice;

    private void Create()
    {
        if (_product.SelectedItem is not ProductDocument product || _recipe.SelectedItem is not RecipeDocument recipe ||
            _equipment.SelectedItem is not EquipmentChoice equipment) return;
        CreateRequested?.Invoke(new(_customer.Text.Trim(), _request.Text.Trim(), _lot.Text.Trim(), product, recipe, equipment.State));
    }

    private static void AddField(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = AppTheme.Muted }, 0, row);
        control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 8, 0, 8); table.Controls.Add(control, 1, row);
    }

    private sealed class EquipmentChoice(EquipmentState state)
    {
        public EquipmentState State { get; } = state;
        public override string ToString() => $"{State.Definition.Name} · {State.Definition.Model}" + (State.IsBusy ? " · 사용 중" : " · 유휴");
    }
}

internal sealed class JobDetailView : UserControl
{
    private readonly InspectionJob _job;
    private readonly EquipmentState? _equipment;
    private readonly Button _start;
    private readonly Button _configure;
    private readonly Label _startReason;
    private readonly Label _equipmentStatus;
    private readonly DataGridView _runs;

    public JobDetailView(InspectionJob job, EquipmentState? equipment)
    {
        _job = job; _equipment = equipment;
        BackColor = AppTheme.Background; Padding = new Padding(28); AutoScroll = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 62 };
        top.Controls.Add(AppTheme.Heading($"Job 상세 · {job.LotId}"));
        var back = AppTheme.SecondaryButton("전체 작업으로", 120);
        back.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => back.Left = top.ClientSize.Width - back.Width;
        back.Click += (_, _) => BackRequested?.Invoke(); top.Controls.Add(back);

        var info = AppTheme.CardPanel(); info.Dock = DockStyle.Top; info.Height = 260;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 5; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        AddInfo(table, 0, "Job ID", job.JobId); AddInfo(table, 1, "상태", JobCard.JobText(job.Status));
        AddInfo(table, 2, "고객명", job.CustomerName); AddInfo(table, 3, "의뢰번호", job.RequestNumber);
        AddInfo(table, 4, "Lot ID", job.LotId); AddInfo(table, 5, "제품", job.ProductSnapshot.Name);
        AddInfo(table, 6, "레시피", $"{job.RecipeSnapshot.Name} v{job.RecipeSnapshot.Version}");
        AddInfo(table, 7, "배정 장비", equipment?.Definition.Name ?? job.EquipmentId);
        _equipmentStatus = AddInfo(table, 8, "장비 상태", "");
        AddInfo(table, 9, "Wafer", "Wafer01 ~ Wafer25");
        info.Controls.Add(table);

        var actionCard = AppTheme.CardPanel(); actionCard.Dock = DockStyle.Top; actionCard.Height = 120;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55 };
        _configure = AppTheme.SecondaryButton("모의 결과 설정", 145);
        _configure.Click += (_, _) => ConfigureRequested?.Invoke();
        _start = AppTheme.PrimaryButton(job.Runs.Count == 0 ? "테스트 시작" : "새 Run 시작", 130);
        _start.Click += (_, _) => StartRequested?.Invoke();
        var delete = AppTheme.DangerButton("Job 삭제", 110);
        delete.Click += (_, _) => DeleteRequested?.Invoke();
        actions.Controls.Add(_configure); actions.Controls.Add(_start); actions.Controls.Add(delete);
        _startReason = new Label { Dock = DockStyle.Bottom, Height = 34, ForeColor = AppTheme.Muted, Padding = new Padding(7, 0, 0, 0) };
        actionCard.Controls.Add(_startReason); actionCard.Controls.Add(actions);

        var history = AppTheme.CardPanel(); history.Dock = DockStyle.Top; history.Height = 300;
        var title = AppTheme.Heading("실행 이력", 12F); title.Dock = DockStyle.Top; title.Height = 36;
        _runs = AppTheme.Grid(); _runs.Dock = DockStyle.Fill; _runs.ReadOnly = true;
        _runs.Columns.Add("RunId", "Run ID"); _runs.Columns.Add("Status", "상태"); _runs.Columns.Add("Started", "시작");
        _runs.Columns.Add("Yield", "Lot 수율"); _runs.Columns.Add("Artifacts", "결과");
        _runs.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _runs.Rows[e.RowIndex].Tag is JobRunSummary run) RunActivated?.Invoke(run);
        };
        history.Controls.Add(_runs); history.Controls.Add(title);
        Controls.Add(history); Controls.Add(actionCard); Controls.Add(info); Controls.Add(top);
        RefreshView();
    }

    public event Action? BackRequested;
    public event Action? ConfigureRequested;
    public event Action? StartRequested;
    public event Action? DeleteRequested;
    public event Action<JobRunSummary>? RunActivated;

    public void RefreshView()
    {
        var connected = _equipment?.ConnectionStatus == ConnectionStatus.Connected;
        var busyByOther = _equipment?.IsBusy == true && !string.Equals(_equipment.ActiveJobId, _job.JobId, StringComparison.OrdinalIgnoreCase);
        _equipmentStatus.Text = _equipment is null ? "장비 정보 없음" :
            !connected ? "연결 해제" : _equipment.IsBusy ? $"진행 중 ({_equipment.ActiveJobId})" : "유휴";
        _equipmentStatus.ForeColor = connected && !busyByOther ? AppTheme.Success : AppTheme.Warning;
        _configure.Enabled = _job.Status != JobStatus.Running;
        _start.Enabled = _job.Status != JobStatus.Running && _job.Simulation is not null && connected && !busyByOther;
        _startReason.Text = _job.Simulation is null ? "모의 결과 설정을 먼저 저장하세요." :
            !connected ? "배정 장비가 연결되어 있지 않습니다." :
            busyByOther ? "배정 장비가 다른 Job을 실행 중입니다." :
            _job.Status == JobStatus.Running ? "현재 Run이 진행 중입니다." : "검사를 시작할 수 있습니다.";
        _runs.Rows.Clear();
        foreach (var run in _job.Runs.OrderByDescending(x => x.StartedAt))
        {
            var row = _runs.Rows[_runs.Rows.Add(run.RunId, JobCard.JobText(run.Status), run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                run.LotYieldPercent is null ? "-" : $"{run.LotYieldPercent:0.00}%",
                File.Exists(run.ResultFilePath) ? "더블클릭하여 열기" : "결과 파일 없음")];
            row.Tag = run;
        }
    }

    private static Label AddInfo(TableLayoutPanel table, int index, string title, string value)
    {
        var row = index / 2; var col = (index % 2) * 2;
        table.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = AppTheme.Muted, Anchor = AnchorStyles.Left }, col, row);
        var label = new Label { Text = value, AutoSize = true, ForeColor = AppTheme.Text, Font = new Font("맑은 고딕", 9F, FontStyle.Bold), Anchor = AnchorStyles.Left };
        table.Controls.Add(label, col + 1, row); return label;
    }
}

internal sealed class SimulationSettingsView : UserControl
{
    private readonly InspectionJob _job;
    private readonly DataGridView _grid;
    private readonly ComboBox _speed;
    private readonly Label _error;

    public SimulationSettingsView(InspectionJob job)
    {
        _job = job;
        BackColor = AppTheme.Background; Padding = new Padding(28);
        var top = new Panel { Dock = DockStyle.Top, Height = 62 };
        top.Controls.Add(AppTheme.Heading("모의 결과 설정"));
        var cancel = AppTheme.SecondaryButton("Job 상세로", 110);
        cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => cancel.Left = top.ClientSize.Width - cancel.Width;
        cancel.Click += (_, _) => CancelRequested?.Invoke(); top.Controls.Add(cancel);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White, Padding = new Padding(14, 10, 14, 8) };
        toolbar.Controls.Add(new Label { Text = "실행 속도", AutoSize = true, Location = new Point(12, 18), ForeColor = AppTheme.Muted });
        _speed = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(95, 13), Width = 100 };
        _speed.Items.AddRange(["1×", "5×", "10×"]);
        _speed.SelectedItem = $"{_job.Simulation?.SpeedFactor ?? 10}×";
        toolbar.Controls.Add(_speed);
        var save = AppTheme.PrimaryButton("설정 저장", 110); save.Dock = DockStyle.Right; save.Click += (_, _) => Save();
        toolbar.Controls.Add(save);
        _error = new Label { AutoSize = true, ForeColor = AppTheme.Danger, Location = new Point(225, 18) };
        toolbar.Controls.Add(_error);

        _grid = AppTheme.Grid(); _grid.Dock = DockStyle.Fill; _grid.MultiSelect = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wafer", HeaderText = "Wafer", ReadOnly = true, FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Outcome", HeaderText = "예상 결과", DataSource = new[] { "정상", "NG", "장비 오류" } });
        _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Level", HeaderText = "결함 수준", DataSource = new[] { "-", "낮음", "중간", "높음" } });
        _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Step", HeaderText = "오류 단계", DataSource = new[] { "-" }.Concat(job.RecipeSnapshot.Steps.Select(x => $"{x.Id} · {x.Name}")).ToArray() });
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 1) UpdateRow(_grid.Rows[e.RowIndex]); };
        _grid.DataError += (_, _) => { };

        var settings = _job.Simulation ?? JobSimulationSettings.CreateDefault();
        foreach (var wafer in settings.Wafers)
        {
            var outcome = wafer.Outcome switch { SimulationOutcome.Ng => "NG", SimulationOutcome.EquipmentError => "장비 오류", _ => "정상" };
            var level = wafer.DefectLevel switch { DefectLevel.Low => "낮음", DefectLevel.Medium => "중간", DefectLevel.High => "높음", _ => "-" };
            var step = wafer.FailedStepId is null ? "-" :
                $"{wafer.FailedStepId} · {job.RecipeSnapshot.Steps.FirstOrDefault(x => x.Id == wafer.FailedStepId)?.Name}";
            var row = _grid.Rows[_grid.Rows.Add(wafer.WaferId, outcome, level, step)];
            UpdateRow(row);
        }
        Controls.Add(_grid); Controls.Add(toolbar); Controls.Add(top);
    }

    public event Action? CancelRequested;
    public event Action<JobSimulationSettings>? SaveRequested;

    private void UpdateRow(DataGridViewRow row)
    {
        var outcome = row.Cells["Outcome"].Value?.ToString();
        row.Cells["Level"].ReadOnly = outcome != "NG";
        row.Cells["Step"].ReadOnly = outcome != "장비 오류";
        row.Cells["Level"].Style.BackColor = outcome == "NG" ? Color.White : Color.FromArgb(238, 240, 242);
        row.Cells["Step"].Style.BackColor = outcome == "장비 오류" ? Color.White : Color.FromArgb(238, 240, 242);
        if (outcome != "NG") row.Cells["Level"].Value = "-";
        else if (row.Cells["Level"].Value?.ToString() == "-") row.Cells["Level"].Value = "낮음";
        if (outcome != "장비 오류") row.Cells["Step"].Value = "-";
        else if (row.Cells["Step"].Value?.ToString() == "-")
            row.Cells["Step"].Value = $"{_job.RecipeSnapshot.Steps[0].Id} · {_job.RecipeSnapshot.Steps[0].Name}";
    }

    private void Save()
    {
        var errors = 0; var wafers = new List<WaferSimulationSetting>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var outcomeText = row.Cells["Outcome"].Value?.ToString() ?? "정상";
            var outcome = outcomeText switch { "NG" => SimulationOutcome.Ng, "장비 오류" => SimulationOutcome.EquipmentError, _ => SimulationOutcome.Normal };
            if (outcome == SimulationOutcome.EquipmentError) errors++;
            var level = row.Cells["Level"].Value?.ToString() switch
            {
                "낮음" => DefectLevel.Low, "중간" => DefectLevel.Medium, "높음" => DefectLevel.High, _ => (DefectLevel?)null
            };
            var stepText = row.Cells["Step"].Value?.ToString();
            var stepId = outcome == SimulationOutcome.EquipmentError && stepText != "-" ? stepText?.Split('·')[0].Trim() : null;
            wafers.Add(new WaferSimulationSetting { WaferId = row.Cells["Wafer"].Value!.ToString()!, Outcome = outcome, DefectLevel = level, FailedStepId = stepId });
        }
        if (errors > 1) { _error.Text = "장비 오류는 최대 한 Wafer에만 지정할 수 있습니다."; return; }
        var speed = _speed.SelectedItem?.ToString() switch { "1×" => 1, "5×" => 5, _ => 10 };
        SaveRequested?.Invoke(new JobSimulationSettings { SpeedFactor = speed, Wafers = wafers });
    }
}

internal sealed class JobProgressView : UserControl
{
    private readonly InspectionJob _job;
    private readonly JobRunResult _result;
    private readonly Label _summary;
    private readonly DataGridView _wafers;
    private readonly DataGridView _steps;
    private readonly TextBox _log;
    private readonly System.Windows.Forms.Timer _timer;

    public JobProgressView(InspectionJob job, JobRunResult result)
    {
        _job = job; _result = result;
        BackColor = AppTheme.Background; Padding = new Padding(24);
        var header = new Panel { Dock = DockStyle.Top, Height = 86 };
        header.Controls.Add(AppTheme.Heading($"Lot 검사 진행 · {job.LotId}"));
        _summary = new Label { Location = new Point(2, 50), AutoSize = true, ForeColor = AppTheme.Text };
        header.Controls.Add(_summary);
        var cancel = AppTheme.DangerButton("검사 취소", 110); cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cancel.Location = new Point(header.Width - cancel.Width, 5); header.Resize += (_, _) => cancel.Left = header.ClientSize.Width - cancel.Width;
        cancel.Click += (_, _) => CancelRequested?.Invoke(); header.Controls.Add(cancel);

        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = AppTheme.Border };
        AppTheme.KeepSplitRatio(split, .55, 300, 300);
        _wafers = AppTheme.Grid(); _wafers.Dock = DockStyle.Fill; _wafers.ReadOnly = true;
        _wafers.Columns.Add("Wafer", "Wafer"); _wafers.Columns.Add("Status", "상태"); _wafers.Columns.Add("Yield", "수율");
        split.Panel1.Controls.Add(_wafers);
        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        AppTheme.KeepSplitRatio(right, .56, 150, 120);
        _steps = AppTheme.Grid(); _steps.Dock = DockStyle.Fill; _steps.ReadOnly = true;
        _steps.Columns.Add("Sequence", "#"); _steps.Columns.Add("Step", "현재 Wafer 단계"); _steps.Columns.Add("Status", "상태");
        foreach (var step in job.RecipeSnapshot.Steps) _steps.Rows.Add(step.Sequence, step.Name, "대기");
        _log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Font = new Font("Consolas", 9F) };
        right.Panel1.Controls.Add(_steps); right.Panel2.Controls.Add(_log); split.Panel2.Controls.Add(right);
        Controls.Add(split); Controls.Add(header);
        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += (_, _) => RefreshSummary();
        _timer.Start(); RefreshAll();
    }

    public string RunId => _result.RunId;
    public event Action? CancelRequested;
    protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }

    public void ApplyProgress(LotRunProgress progress)
    {
        for (var index = 0; index < _steps.Rows.Count; index++)
        {
            var step = _job.RecipeSnapshot.Steps[index];
            _steps.Rows[index].Cells[2].Value = progress.CompletedStepIds.Contains(step.Id) ? "완료" :
                string.Equals(step.Id, progress.CurrentStepId, StringComparison.OrdinalIgnoreCase) ? "진행 중" : "대기";
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        _wafers.Rows.Clear();
        foreach (var wafer in _result.Wafers)
            _wafers.Rows.Add(wafer.WaferId, WaferText(wafer.Status), wafer.YieldPercent is null ? "-" : $"{wafer.YieldPercent:0.00}%");
        _log.Text = string.Join(Environment.NewLine, _result.Logs);
        _log.SelectionStart = _log.TextLength; _log.ScrollToCaret();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var elapsed = DateTimeOffset.Now - _result.StartedAt;
        _summary.Text = $"전체 진행률 {_result.ProgressPercent}%  ·  현재 {_result.CurrentWaferId}  ·  경과 {elapsed:hh\\:mm\\:ss}  ·  정상 {_result.NormalCount}  ·  NG {_result.NgCount}  ·  미완료 {25 - _result.CompletedCount}";
    }

    internal static string WaferText(WaferExecutionStatus status) => status switch
    {
        WaferExecutionStatus.Pending => "대기", WaferExecutionStatus.Running => "진행 중",
        WaferExecutionStatus.Normal => "정상", WaferExecutionStatus.Ng => "NG",
        WaferExecutionStatus.EquipmentError => "장비 오류", _ => "미실행"
    };
}

internal sealed class JobResultView : UserControl
{
    private readonly InspectionJob _job;
    private readonly JobRunSummary _summary;
    private readonly JobRunResult _result;
    private readonly ListBox _waferList;
    private readonly WaferMapControl _map;
    private readonly Label _waferSummary;
    private readonly DataGridView _defects;
    private readonly ComboBox _logFilter;
    private readonly TextBox _log;

    public JobResultView(InspectionJob job, JobRunSummary summary, JobRunResult result)
    {
        _job = job; _summary = summary; _result = result;
        BackColor = AppTheme.Background; Padding = new Padding(24);
        var top = new Panel { Dock = DockStyle.Top, Height = 66 };
        top.Controls.Add(AppTheme.Heading($"Job 결과 · {job.LotId}"));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 560, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var back = AppTheme.SecondaryButton("Job 상세로", 110); back.Click += (_, _) => BackRequested?.Invoke();
        var saveReport = AppTheme.PrimaryButton("결과 보고서 저장", 145);
        saveReport.Enabled = !string.IsNullOrWhiteSpace(summary.ReportFilePath) && File.Exists(summary.ReportFilePath);
        saveReport.Click += (_, _) => SaveReportRequested?.Invoke(summary);
        var saveLog = AppTheme.SecondaryButton("로그 파일 저장", 130); saveLog.Enabled = File.Exists(summary.LogFilePath);
        saveLog.Click += (_, _) => SaveLogRequested?.Invoke(summary);
        actions.Controls.Add(back); actions.Controls.Add(saveReport); actions.Controls.Add(saveLog); top.Controls.Add(actions);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildSummaryTab());
        var waferTab = new TabPage("Wafer 상세") { BackColor = AppTheme.Background, Padding = new Padding(12) };
        var waferSplit = new SplitContainer { Dock = DockStyle.Fill };
        AppTheme.KeepSplitRatio(waferSplit, .2, 150, 500);
        _waferList = new ListBox { Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 10F) };
        foreach (var wafer in result.Wafers.Where(x => x.Status is WaferExecutionStatus.Normal or WaferExecutionStatus.Ng))
            _waferList.Items.Add(wafer);
        _waferList.DisplayMember = nameof(WaferResult.WaferId);
        waferSplit.Panel1.Controls.Add(_waferList);
        var detailSplit = new SplitContainer { Dock = DockStyle.Fill };
        AppTheme.KeepSplitRatio(detailSplit, .6, 280, 260);
        _map = new WaferMapControl { Dock = DockStyle.Fill };
        detailSplit.Panel1.Controls.Add(_map);
        var details = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = Color.White };
        _waferSummary = new Label { Dock = DockStyle.Top, Height = 132, ForeColor = AppTheme.Text, Font = new Font("맑은 고딕", 10F) };
        _defects = AppTheme.Grid(); _defects.Dock = DockStyle.Fill; _defects.ReadOnly = true;
        _defects.Columns.Add("Type", "결함 유형"); _defects.Columns.Add("Count", "개수");
        details.Controls.Add(_defects); details.Controls.Add(_waferSummary); detailSplit.Panel2.Controls.Add(details);
        waferSplit.Panel2.Controls.Add(detailSplit); waferTab.Controls.Add(waferSplit); tabs.TabPages.Add(waferTab);

        var logTab = new TabPage("로그") { BackColor = AppTheme.Background, Padding = new Padding(12) };
        _logFilter = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Height = 34 };
        _logFilter.Items.Add("전체"); foreach (var wafer in result.Wafers) _logFilter.Items.Add(wafer.WaferId);
        _logFilter.SelectedIndex = 0; _logFilter.SelectedIndexChanged += (_, _) => RefreshLog();
        _log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F) };
        logTab.Controls.Add(_log); logTab.Controls.Add(_logFilter); tabs.TabPages.Add(logTab);
        Controls.Add(tabs); Controls.Add(top);
        _waferList.SelectedIndexChanged += (_, _) => RefreshWafer();
        if (_waferList.Items.Count > 0) _waferList.SelectedIndex = 0;
        RefreshLog();
    }

    public event Action? BackRequested;
    public event Action<JobRunSummary>? SaveLogRequested;
    public event Action<JobRunSummary>? SaveReportRequested;

    private TabPage BuildSummaryTab()
    {
        var page = new TabPage("Lot 요약") { BackColor = AppTheme.Background, Padding = new Padding(14) };
        var headline = new Label
        {
            Dock = DockStyle.Top, Height = 132, BackColor = Color.White, Padding = new Padding(18, 14, 18, 14),
            Font = new Font("맑은 고딕", 11F, FontStyle.Bold), ForeColor = AppTheme.Text,
            Text = $"상태  {JobCard.JobText(_result.Status)}\r\nLot 수율  {(_result.LotYieldPercent is null ? "-" : $"{_result.LotYieldPercent:0.00}%")}    정상 {_result.NormalCount}    NG {_result.NgCount}    총 결함 {_result.TotalDefectCount}\r\n장비  {_result.EquipmentName}    레시피  {_result.RecipeName} v{_result.RecipeVersion}"
        };
        var grid = AppTheme.Grid(); grid.Dock = DockStyle.Fill; grid.ReadOnly = true;
        grid.Columns.Add("Wafer", "Wafer"); grid.Columns.Add("Status", "판정"); grid.Columns.Add("Yield", "수율");
        grid.Columns.Add("Level", "결함 수준"); grid.Columns.Add("BadDies", "불량 다이"); grid.Columns.Add("Defects", "결함 수");
        foreach (var wafer in _result.Wafers)
            grid.Rows.Add(wafer.WaferId, JobProgressView.WaferText(wafer.Status),
                wafer.YieldPercent is null ? "-" : $"{wafer.YieldPercent:0.00}%", LevelText(wafer.DefectLevel),
                wafer.ValidDieCount == 0 ? "-" : wafer.ValidDieCount - wafer.PassDieCount, wafer.Defects.Count);
        page.Controls.Add(grid); page.Controls.Add(headline); return page;
    }

    private void RefreshWafer()
    {
        if (_waferList.SelectedItem is not WaferResult wafer) return;
        _map.Wafer = wafer;
        _waferSummary.Text = $"{wafer.WaferId}\r\n판정: {JobProgressView.WaferText(wafer.Status)}\r\n수율: {wafer.YieldPercent:0.00}%\r\n결함 수준: {LevelText(wafer.DefectLevel)}";
        _defects.Rows.Clear();
        foreach (var type in new[] { "Particle", "Scratch", "Pattern", "Edge", "Contamination" })
            _defects.Rows.Add(type, wafer.Defects.Count(x => x.Type == type));
    }

    private void RefreshLog()
    {
        var filter = _logFilter.SelectedItem?.ToString();
        var entries = filter == "전체" ? _result.Logs : _result.Logs.Where(x => x.WaferId == filter);
        _log.Text = string.Join(Environment.NewLine, entries);
    }

    private static string LevelText(DefectLevel? level) => level switch { DefectLevel.Low => "낮음", DefectLevel.Medium => "중간", DefectLevel.High => "높음", _ => "-" };
}

internal sealed class WaferMapControl : Control
{
    private WaferResult? _wafer;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public WaferResult? Wafer { get => _wafer; set { _wafer = value; Invalidate(); } }
    public WaferMapControl() { DoubleBuffered = true; BackColor = Color.White; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_wafer is null) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var size = Math.Min(ClientSize.Width - 40, ClientSize.Height - 40);
        if (size <= 0) return;
        var originX = (ClientSize.Width - size) / 2f; var originY = (ClientSize.Height - size) / 2f;
        var cell = size / (float)LotTestRunner.GridSize;
        using var outline = new Pen(Color.FromArgb(100, 115, 130), 2);
        e.Graphics.DrawEllipse(outline, originX, originY, size, size);
        foreach (var die in _wafer.Dies.Where(x => x.IsValid))
        {
            using var brush = new SolidBrush(die.IsPass ? Color.FromArgb(105, 190, 135) : Color.FromArgb(220, 78, 78));
            var rectangle = new RectangleF(originX + die.Column * cell + 1, originY + die.Row * cell + 1, cell - 2, cell - 2);
            e.Graphics.FillRectangle(brush, rectangle);
        }
    }
}

internal sealed class EquipmentListView : UserControl
{
    private readonly FlowLayoutPanel _cards;

    public EquipmentListView(IReadOnlyList<EquipmentState> equipment)
    {
        BackColor = AppTheme.Background;
        Padding = new Padding(28);
        var header = new Panel { Dock = DockStyle.Top, Height = 88 };
        header.Controls.Add(AppTheme.Heading("장비 목록"));
        header.Controls.Add(new Label
        {
            Text = "연결된 웨이퍼 검사 장비와 작업 상태를 확인합니다.",
            Location = new Point(1, 49),
            AutoSize = true,
            ForeColor = AppTheme.Muted
        });
        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            BackColor = AppTheme.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        foreach (var item in equipment)
        {
            var card = new EquipmentSummaryCard(item);
            card.Activated += () => EquipmentActivated?.Invoke(item);
            _cards.Controls.Add(card);
        }
        Controls.Add(_cards); Controls.Add(header);
    }

    public event Action<EquipmentState>? EquipmentActivated;

    public void RefreshStates()
    {
        foreach (var card in _cards.Controls.OfType<EquipmentSummaryCard>()) card.RefreshState();
    }
}

internal sealed class EquipmentSummaryCard : Panel
{
    private readonly EquipmentState _equipment;
    private readonly Label _overlay;
    private readonly Label _status;
    private readonly ToolTip _toolTip = new();

    public EquipmentSummaryCard(EquipmentState equipment)
    {
        _equipment = equipment;
        Width = 292;
        Height = 248;
        BackColor = AppTheme.Surface;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 18, 18);
        Cursor = Cursors.Hand;

        var image = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 155,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Image = EquipmentImageFactory.Create(equipment.Definition)
        };
        _overlay = new Label
        {
            Parent = image,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Visible = false
        };
        var name = new Label
        {
            Text = equipment.Definition.Name,
            Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(16, 166),
            Size = new Size(260, 26),
            AutoEllipsis = true,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _toolTip.SetToolTip(name, equipment.Definition.Name);
        var model = new Label
        {
            Text = $"{equipment.Definition.Manufacturer} · {equipment.Definition.Model}",
            ForeColor = AppTheme.Muted,
            Location = new Point(17, 193),
            AutoSize = true
        };
        _status = new Label
        {
            Location = new Point(17, 218),
            AutoSize = true,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
        };

        Controls.Add(_status);
        Controls.Add(model);
        Controls.Add(name);
        Controls.Add(image);
        WireActivation(this);
        RefreshState();
    }

    public event Action? Activated;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _toolTip.Dispose();
        base.Dispose(disposing);
    }

    public void RefreshState()
    {
        _status.Text = _equipment.ConnectionStatus switch
        {
            ConnectionStatus.Connected => "●  연결됨",
            ConnectionStatus.Connecting => "●  연결 중...",
            _ => "●  연결 해제됨"
        };
        _status.ForeColor = _equipment.ConnectionStatus == ConnectionStatus.Connected
            ? AppTheme.Success
            : _equipment.ConnectionStatus == ConnectionStatus.Connecting ? AppTheme.Warning : AppTheme.Danger;
        _overlay.Visible = _equipment.IsBusy;
        _overlay.BackColor = _equipment.IsBusy ? Color.FromArgb(145, 12, 20, 28) : Color.Transparent;
        _overlay.Text = _equipment.IsBusy ? $"작업 중 ({_equipment.ProgressPercent}%)" : string.Empty;
    }

    private void WireActivation(Control control)
    {
        control.Click += (_, _) => Activated?.Invoke();
        foreach (Control child in control.Controls) WireActivation(child);
    }

    internal static string ConnectionText(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Connected => "연결됨",
        ConnectionStatus.Connecting => "연결 중",
        _ => "연결 해제"
    };
}

internal sealed class EquipmentDetailView : UserControl
{
    private readonly EquipmentState _equipment;
    private readonly Label _connection;
    private readonly Label _lastConnection;
    private readonly Label _workStatus;
    private readonly Label _currentWork;
    private readonly Button _toggle;

    public EquipmentDetailView(EquipmentState equipment)
    {
        _equipment = equipment;
        BackColor = AppTheme.Background;
        Padding = new Padding(28);
        AutoScroll = true;

        var top = new Panel { Dock = DockStyle.Top, Height = 58 };
        top.Controls.Add(AppTheme.Heading(equipment.Definition.Name));
        var back = AppTheme.SecondaryButton("목록으로", 100);
        back.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        top.Resize += (_, _) => back.Left = top.ClientSize.Width - back.Width;
        back.Click += (_, _) => BackRequested?.Invoke();
        top.Controls.Add(back);

        var info = AppTheme.CardPanel();
        info.Dock = DockStyle.Top;
        info.Height = 295;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < 5; index++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        AddInfo(table, 0, "장비명", equipment.Definition.Name);
        AddInfo(table, 1, "제조사", equipment.Definition.Manufacturer);
        AddInfo(table, 2, "모델", equipment.Definition.Model);
        AddInfo(table, 3, "장비 ID", equipment.Definition.Id);
        AddInfo(table, 4, "IP / 포트", $"{equipment.Definition.IpAddress}:{equipment.Definition.Port}");
        _connection = AddInfo(table, 5, "연결 상태", string.Empty);
        _lastConnection = AddInfo(table, 6, "마지막 연결", string.Empty);
        _workStatus = AddInfo(table, 7, "작업 상태", string.Empty);
        AddInfo(table, 8, "통신 방식", "가상 장비 시뮬레이션");
        info.Controls.Add(table);

        var work = AppTheme.CardPanel();
        work.Dock = DockStyle.Top;
        work.Height = 135;
        var workTitle = new Label
        {
            Text = "현재 작업",
            AutoSize = true,
            Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
            ForeColor = AppTheme.Text
        };
        _currentWork = new Label { Location = new Point(18, 48), AutoSize = true, ForeColor = AppTheme.Muted };
        work.Controls.Add(_currentWork);
        work.Controls.Add(workTitle);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(0, 10, 0, 0) };
        _toggle = AppTheme.SecondaryButton("연결 해제", 120);
        _toggle.Click += (_, _) => ConnectionToggleRequested?.Invoke();
        actions.Controls.Add(_toggle);

        Controls.Add(actions);
        Controls.Add(work);
        Controls.Add(info);
        Controls.Add(top);
        RefreshView();
    }

    public event Action? BackRequested;
    public event Action? ConnectionToggleRequested;

    public void RefreshView()
    {
        _connection.Text = _equipment.ConnectionStatus switch
        {
            ConnectionStatus.Connected => "● 연결됨",
            ConnectionStatus.Connecting => "● 연결 중...",
            _ => "● 연결 해제됨"
        };
        _connection.ForeColor = _equipment.ConnectionStatus == ConnectionStatus.Connected
            ? AppTheme.Success
            : _equipment.ConnectionStatus == ConnectionStatus.Connecting ? AppTheme.Warning : AppTheme.Danger;
        _lastConnection.Text = _equipment.LastConnectedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        _workStatus.Text = _equipment.IsBusy ? $"진행 중 ({_equipment.ProgressPercent}%)" : "유휴";
        _workStatus.ForeColor = _equipment.IsBusy ? AppTheme.Primary : AppTheme.Text;
        _currentWork.Text = _equipment.IsBusy
            ? $"Job ID: {_equipment.ActiveJobId}\r\n현재 Wafer: {_equipment.CurrentWaferId}    진행률: {_equipment.ProgressPercent}%"
            : "현재 실행 중인 Job이 없습니다.";
        _toggle.Enabled = !_equipment.IsBusy && _equipment.ConnectionStatus != ConnectionStatus.Connecting;
        _toggle.Text = _equipment.ConnectionStatus == ConnectionStatus.Connected ? "연결 해제" : "재연결";
    }

    private static Label AddInfo(TableLayoutPanel table, int index, string title, string value)
    {
        var row = index / 2;
        var column = (index % 2) * 2;
        table.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = AppTheme.Muted,
            Anchor = AnchorStyles.Left
        }, column, row);
        var label = new Label
        {
            Text = value,
            AutoSize = true,
            ForeColor = AppTheme.Text,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Left
        };
        table.Controls.Add(label, column + 1, row);
        return label;
    }
}

internal sealed class EquipmentSelectionDialog : Form
{
    private readonly ListBox _list = new();
    private readonly Button _connect;
    public EquipmentSelectionDialog(IReadOnlyList<EquipmentDefinition> equipment)
    {
        Text = "가상 장비 연결"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, 390);
        BackColor = AppTheme.Background; Padding = new Padding(20);
        var title = AppTheme.Heading("연결할 장비 선택", 14F); title.Dock = DockStyle.Top; title.Height = 48;
        _list.Dock = DockStyle.Fill; _list.DisplayMember = nameof(EquipmentDefinition.Name);
        foreach (var item in equipment) _list.Items.Add(item);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft };
        _connect = AppTheme.PrimaryButton("연결", 100); _connect.Enabled = equipment.Count > 0;
        _connect.Click += (_, _) => { SelectedEquipment = _list.SelectedItem as EquipmentDefinition; if (SelectedEquipment is not null) DialogResult = DialogResult.OK; };
        var cancel = AppTheme.SecondaryButton("취소", 90); cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(_connect); footer.Controls.Add(cancel);
        Controls.Add(_list); Controls.Add(footer); Controls.Add(title);
        if (equipment.Count > 0) _list.SelectedIndex = 0;
        AcceptButton = _connect; CancelButton = cancel;
    }
    public EquipmentDefinition? SelectedEquipment { get; private set; }
}
