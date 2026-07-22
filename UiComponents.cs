using System.Diagnostics;

namespace RecipeTestProject;

internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(242, 245, 248);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 221, 229);
    public static readonly Color Primary = Color.FromArgb(32, 104, 179);
    public static readonly Color PrimaryDark = Color.FromArgb(24, 78, 134);
    public static readonly Color Text = Color.FromArgb(35, 45, 55);
    public static readonly Color Muted = Color.FromArgb(101, 113, 126);
    public static readonly Color Success = Color.FromArgb(35, 150, 83);
    public static readonly Color Danger = Color.FromArgb(205, 62, 62);
    public static readonly Color Warning = Color.FromArgb(218, 143, 24);

    public static Button PrimaryButton(string text, int width = 120)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            BackColor = Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
            Margin = new Padding(6)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    public static Button SecondaryButton(string text, int width = 110)
    {
        var button = PrimaryButton(text, width);
        button.BackColor = Color.White;
        button.ForeColor = Text;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        return button;
    }

    public static Label Heading(string text, float size = 17F) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Text,
        Font = new Font("맑은 고딕", size, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 8)
    };

    public static Panel CardPanel() => new()
    {
        BackColor = Surface,
        Padding = new Padding(18),
        Margin = new Padding(0, 0, 0, 14),
        BorderStyle = BorderStyle.FixedSingle
    };
}

internal static class EquipmentImageFactory
{
    public static Bitmap Create(EquipmentDefinition equipment, int width = 260, int height = 145)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(235, 240, 244));

        using var shadow = new SolidBrush(Color.FromArgb(25, Color.Black));
        using var body = new SolidBrush(Color.FromArgb(218, 225, 231));
        using var front = new SolidBrush(Color.FromArgb(247, 249, 251));
        using var accent = new SolidBrush(equipment.AccentColor);
        using var dark = new SolidBrush(Color.FromArgb(54, 65, 75));
        using var gray = new SolidBrush(Color.Gray);
        using var glass = new SolidBrush(Color.FromArgb(96, 173, 207));
        using var pen = new Pen(Color.FromArgb(148, 160, 171), 2);

        graphics.FillRoundedRectangle(shadow, new Rectangle(42, 25, 176, 111), 10);
        graphics.FillRoundedRectangle(body, new Rectangle(36, 17, 176, 111), 10);
        graphics.FillRectangle(front, 51, 30, 146, 83);
        graphics.FillRectangle(accent, 51, 30, 146, 9);
        graphics.FillEllipse(dark, 82, 50, 83, 54);
        graphics.FillEllipse(glass, 93, 58, 61, 38);
        graphics.DrawEllipse(pen, 93, 58, 61, 38);
        graphics.FillRectangle(dark, 59, 47, 13, 47);
        graphics.FillEllipse(accent, 61, 52, 9, 9);
        graphics.FillEllipse(gray, 61, 68, 9, 9);
        graphics.FillRectangle(accent, 176, 48, 10, 48);
        graphics.FillRectangle(dark, 53, 117, 22, 11);
        graphics.FillRectangle(dark, 173, 117, 22, 11);

        using var font = new Font("Segoe UI", 8F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(70, 80, 90));
        graphics.DrawString(equipment.Model, font, textBrush, new PointF(169, 104));
        return bitmap;
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
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

internal sealed class EquipmentCard : Panel
{
    private readonly EquipmentState _equipment;
    private readonly PictureBox _image;
    private readonly Label _overlay;
    private readonly Label _status;

    public EquipmentCard(EquipmentState equipment)
    {
        _equipment = equipment;
        Width = 292;
        Height = 248;
        BackColor = AppTheme.Surface;
        BorderStyle = BorderStyle.FixedSingle;
        Margin = new Padding(0, 0, 18, 18);
        Cursor = Cursors.Hand;

        _image = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 155,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = EquipmentImageFactory.Create(equipment.Definition)
        };
        _overlay = new Label
        {
            Parent = _image,
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
            AutoSize = true
        };
        var model = new Label
        {
            Text = equipment.Definition.Model,
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
        Controls.Add(_image);
        WireDoubleClick(this);
        RefreshState();
    }

    public event EventHandler? Activated;

    public void RefreshState()
    {
        var connected = _equipment.ConnectionStatus == ConnectionStatus.Connected;
        var stateText = _equipment.ConnectionStatus switch
        {
            ConnectionStatus.Connected => "●  연결됨",
            ConnectionStatus.Connecting => "●  연결 중...",
            _ => "●  연결 해제됨"
        };
        _status.Text = stateText;
        _status.ForeColor = connected ? AppTheme.Success : _equipment.ConnectionStatus == ConnectionStatus.Connecting ? AppTheme.Warning : AppTheme.Danger;

        _overlay.Visible = _equipment.TestStatus != TestStatus.Idle;
        _overlay.BackColor = _equipment.TestStatus == TestStatus.Running ? Color.FromArgb(145, 12, 20, 28) : Color.FromArgb(112, 12, 20, 28);
        _overlay.Text = _equipment.TestStatus switch
        {
            TestStatus.Running => $"테스트 중 ({_equipment.ProgressPercent}%)",
            TestStatus.Succeeded => "테스트 완료 (100%)",
            TestStatus.Failed => $"테스트 실패 ({_equipment.ProgressPercent}%)",
            TestStatus.Canceled => $"테스트 취소 ({_equipment.ProgressPercent}%)",
            _ => string.Empty
        };
    }

    private void WireDoubleClick(Control control)
    {
        control.DoubleClick += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
        foreach (Control child in control.Controls) WireDoubleClick(child);
    }
}

internal sealed class EquipmentListView : UserControl
{
    private readonly FlowLayoutPanel _cards;
    private readonly Label _empty;

    public EquipmentListView()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.Background;
        Padding = new Padding(28);

        var header = new Panel { Dock = DockStyle.Top, Height = 88 };
        header.Controls.Add(AppTheme.Heading("장비 목록"));
        var caption = new Label
        {
            Text = "연결된 웨이퍼 검사 장비를 확인하고 관리합니다.",
            ForeColor = AppTheme.Muted,
            Location = new Point(1, 49),
            AutoSize = true
        };
        header.Controls.Add(caption);

        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            BackColor = AppTheme.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        _empty = new Label
        {
            Text = "연결된 장비가 없습니다.\r\n상단의 ‘장비 > 장비 연결’을 선택해 장비를 추가하세요.",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = AppTheme.Muted,
            Font = new Font("맑은 고딕", 11F),
            Dock = DockStyle.Fill
        };
        Controls.Add(_cards);
        Controls.Add(_empty);
        Controls.Add(header);
    }

    public event Action<EquipmentState>? EquipmentActivated;

    public void SetEquipment(IEnumerable<EquipmentState> equipment)
    {
        _cards.SuspendLayout();
        foreach (Control control in _cards.Controls) control.Dispose();
        _cards.Controls.Clear();
        var items = equipment.ToList();
        foreach (var item in items)
        {
            var card = new EquipmentCard(item);
            card.Activated += (_, _) => EquipmentActivated?.Invoke(item);
            _cards.Controls.Add(card);
        }
        _empty.Visible = items.Count == 0;
        _cards.Visible = items.Count > 0;
        _cards.ResumeLayout();
    }

    public void RefreshStates()
    {
        foreach (var card in _cards.Controls.OfType<EquipmentCard>()) card.RefreshState();
    }
}

internal sealed class EquipmentSelectionDialog : Form
{
    private readonly ListBox _list;
    private readonly Label _detail;
    private readonly Button _connect;
    private readonly IReadOnlyList<EquipmentDefinition> _equipment;

    public EquipmentSelectionDialog(IReadOnlyList<EquipmentDefinition> equipment)
    {
        _equipment = equipment;
        Text = "가상 장비 연결";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(570, 430);
        MinimumSize = new Size(520, 380);
        BackColor = AppTheme.Background;
        Font = new Font("맑은 고딕", 9F);

        var header = new Label
        {
            Text = "연결할 웨이퍼 검사 장비를 선택하세요.",
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(20, 20, 0, 0),
            Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
            ForeColor = AppTheme.Text
        };
        _list = new ListBox
        {
            Dock = DockStyle.Left,
            Width = 260,
            BorderStyle = BorderStyle.None,
            Font = new Font("맑은 고딕", 10F),
            IntegralHeight = false
        };
        foreach (var item in equipment) _list.Items.Add(item);
        _list.DisplayMember = nameof(EquipmentDefinition.Name);
        _list.SelectedIndexChanged += (_, _) => UpdateDetail();

        _detail = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ForeColor = AppTheme.Text,
            Font = new Font("맑은 고딕", 10F)
        };

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 12) };
        var listHost = new Panel { Dock = DockStyle.Left, Width = 266, BackColor = Color.White, Padding = new Padding(6) };
        listHost.Controls.Add(_list);
        body.Controls.Add(_detail);
        body.Controls.Add(listHost);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 8, 14, 8),
            BackColor = Color.White
        };
        _connect = AppTheme.PrimaryButton("연결", 100);
        _connect.Enabled = false;
        _connect.Click += (_, _) => { SelectedEquipment = _list.SelectedItem as EquipmentDefinition; DialogResult = DialogResult.OK; };
        var cancel = AppTheme.SecondaryButton("취소", 90);
        cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(_connect);
        footer.Controls.Add(cancel);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
        AcceptButton = _connect;
        CancelButton = cancel;
        if (_equipment.Count > 0) _list.SelectedIndex = 0;
    }

    public EquipmentDefinition? SelectedEquipment { get; private set; }

    private void UpdateDetail()
    {
        if (_list.SelectedItem is not EquipmentDefinition selected)
        {
            _detail.Text = "선택 가능한 장비가 없습니다.";
            _connect.Enabled = false;
            return;
        }
        _detail.Text = $"{selected.Name}\r\n\r\n모델\r\n  {selected.Model}\r\n\r\n장비 ID\r\n  {selected.Id}\r\n\r\n통신 주소\r\n  {selected.IpAddress}:{selected.Port}\r\n\r\n연결에는 약 1초가 소요됩니다.";
        _connect.Enabled = true;
    }
}

internal sealed class SimulationDialog : Form
{
    private readonly ComboBox _result;
    private readonly ComboBox _failedStep;
    private readonly IReadOnlyList<RecipeStep> _steps;

    public SimulationDialog(RecipeDocument recipe)
    {
        _steps = recipe.Steps.OrderBy(x => x.Sequence).ToList();
        Text = "모의 테스트 결과 설정";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppTheme.Background;
        Font = new Font("맑은 고딕", 9F);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 10),
            ColumnCount = 2,
            RowCount = 4
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(new Label { Text = recipe.Name, AutoSize = true, Font = new Font("맑은 고딕", 12F, FontStyle.Bold), ForeColor = AppTheme.Text }, 0, 0);
        content.SetColumnSpan(content.GetControlFromPosition(0, 0)!, 2);
        content.Controls.Add(new Label { Text = "예상 결과", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _result = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _result.Items.AddRange(["성공", "실패"]);
        _result.SelectedIndex = 0;
        content.Controls.Add(_result, 1, 1);
        content.Controls.Add(new Label { Text = "실패 단계", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _failedStep = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = false };
        _result.SelectedIndexChanged += (_, _) => _failedStep.Enabled = _result.SelectedIndex == 1;
        foreach (var step in _steps) _failedStep.Items.Add($"{step.Sequence}. {step.Name}");
        if (_failedStep.Items.Count > 0) _failedStep.SelectedIndex = 0;
        content.Controls.Add(_failedStep, 1, 2);
        content.Controls.Add(new Label
        {
            Text = "선택한 결과에 따라 실제 단계 시간만큼 대기한 뒤 모의 응답을 생성합니다.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.Muted
        }, 0, 3);
        content.SetColumnSpan(content.GetControlFromPosition(0, 3)!, 2);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 8, 16, 8), BackColor = Color.White };
        var start = AppTheme.PrimaryButton("테스트 시작", 120);
        start.Click += (_, _) => { Settings = new TestSimulationSettings(_result.SelectedIndex == 1, _result.SelectedIndex == 1 ? _steps[_failedStep.SelectedIndex].Id : null); DialogResult = DialogResult.OK; };
        var cancel = AppTheme.SecondaryButton("취소", 90);
        cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(start);
        footer.Controls.Add(cancel);
        Controls.Add(content);
        Controls.Add(footer);
        AcceptButton = start;
        CancelButton = cancel;
    }

    public TestSimulationSettings? Settings { get; private set; }
}

internal sealed class EquipmentDetailView : UserControl
{
    private readonly EquipmentState _equipment;
    private readonly Label _connection;
    private readonly Label _testStatus;
    private readonly Label _lastConnection;
    private readonly Button _connectionButton;
    private readonly Button _testButton;
    private readonly Label _resultSummary;

    public EquipmentDetailView(EquipmentState equipment)
    {
        _equipment = equipment;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = AppTheme.Background;
        Padding = new Padding(28);

        var top = new Panel { Dock = DockStyle.Top, Height = 58 };
        top.Controls.Add(AppTheme.Heading(equipment.Definition.Name));
        var close = AppTheme.SecondaryButton("목록으로", 100);
        close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        close.Location = new Point(top.Width - close.Width, 0);
        top.Resize += (_, _) => close.Left = top.ClientSize.Width - close.Width;
        close.Click += (_, _) => CloseRequested?.Invoke();
        top.Controls.Add(close);

        var info = AppTheme.CardPanel();
        info.Dock = DockStyle.Top;
        info.Height = 255;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 4; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        AddInfo(table, 0, "장비명", equipment.Definition.Name);
        AddInfo(table, 1, "모델", equipment.Definition.Model);
        AddInfo(table, 2, "장비 ID", equipment.Definition.Id);
        AddInfo(table, 3, "IP / 포트", $"{equipment.Definition.IpAddress}:{equipment.Definition.Port}");
        _connection = AddInfo(table, 4, "연결 상태", string.Empty);
        _lastConnection = AddInfo(table, 5, "마지막 연결", string.Empty);
        _testStatus = AddInfo(table, 6, "테스트 상태", string.Empty);
        AddInfo(table, 7, "통신 방식", "가상 장비 시뮬레이션");
        info.Controls.Add(table);

        var result = AppTheme.CardPanel();
        result.Dock = DockStyle.Top;
        result.Height = 135;
        var resultTitle = new Label { Text = "최근 테스트", AutoSize = true, Font = new Font("맑은 고딕", 11F, FontStyle.Bold), ForeColor = AppTheme.Text };
        _resultSummary = new Label { Location = new Point(18, 48), AutoSize = true, ForeColor = AppTheme.Muted };
        result.Controls.Add(_resultSummary);
        result.Controls.Add(resultTitle);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(0, 10, 0, 0), FlowDirection = FlowDirection.LeftToRight };
        _testButton = AppTheme.PrimaryButton("테스트 시작", 130);
        _testButton.Click += (_, _) => TestRequested?.Invoke();
        _connectionButton = AppTheme.SecondaryButton("연결 해제", 120);
        _connectionButton.Click += (_, _) => ConnectionToggleRequested?.Invoke();
        actions.Controls.Add(_testButton);
        actions.Controls.Add(_connectionButton);

        Controls.Add(actions);
        Controls.Add(result);
        Controls.Add(info);
        Controls.Add(top);
        RefreshView();
    }

    public event Action? CloseRequested;
    public event Action? TestRequested;
    public event Action? ConnectionToggleRequested;

    public void RefreshView()
    {
        _connection.Text = _equipment.ConnectionStatus switch { ConnectionStatus.Connected => "● 연결됨", ConnectionStatus.Connecting => "● 연결 중...", _ => "● 연결 해제됨" };
        _connection.ForeColor = _equipment.ConnectionStatus == ConnectionStatus.Connected ? AppTheme.Success : _equipment.ConnectionStatus == ConnectionStatus.Connecting ? AppTheme.Warning : AppTheme.Danger;
        _lastConnection.Text = _equipment.LastConnectedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        _testStatus.Text = _equipment.TestStatus switch
        {
            TestStatus.Running => $"테스트 중 ({_equipment.ProgressPercent}%)",
            TestStatus.Succeeded => "테스트 완료",
            TestStatus.Failed => $"테스트 실패 ({_equipment.ProgressPercent}%)",
            TestStatus.Canceled => $"테스트 취소 ({_equipment.ProgressPercent}%)",
            _ => "대기"
        };
        _testButton.Enabled = _equipment.ConnectionStatus == ConnectionStatus.Connected;
        _testButton.Text = _equipment.IsRunning ? "진행 화면 보기" : _equipment.LastResult is null ? "테스트 시작" : "새 테스트 시작";
        _connectionButton.Enabled = !_equipment.IsRunning && _equipment.ConnectionStatus != ConnectionStatus.Connecting;
        _connectionButton.Text = _equipment.ConnectionStatus == ConnectionStatus.Connected ? "연결 해제" : "재연결";
        _resultSummary.Text = _equipment.LastResult is null
            ? "아직 수행된 테스트가 없습니다."
            : $"결과: {StatusText(_equipment.LastResult.Status)}    실행 시간: {_equipment.LastResult.Duration:mm\\:ss}\r\n" +
              (_equipment.LastResult.Status == TestStatus.Failed ? $"실패 단계: {_equipment.LastResult.FailedStepName}\r\n원인: {_equipment.LastResult.FailureReason}" : "로그는 결과 화면에서 확인할 수 있습니다.");
    }

    private static Label AddInfo(TableLayoutPanel table, int index, string title, string value)
    {
        var row = index / 2;
        var col = (index % 2) * 2;
        table.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = AppTheme.Muted, Anchor = AnchorStyles.Left }, col, row);
        var label = new Label { Text = value, AutoSize = true, ForeColor = AppTheme.Text, Font = new Font("맑은 고딕", 9F, FontStyle.Bold), Anchor = AnchorStyles.Left };
        table.Controls.Add(label, col + 1, row);
        return label;
    }

    private static string StatusText(TestStatus status) => status switch { TestStatus.Succeeded => "성공", TestStatus.Failed => "실패", TestStatus.Canceled => "취소", TestStatus.Running => "진행 중", _ => "대기" };
}

internal sealed class TestRunView : UserControl
{
    private readonly EquipmentState _equipment;
    private readonly TestRun _run;
    private readonly SplitContainer _split;
    private readonly Label _title;
    private readonly Label _stepLabel;
    private readonly Label _percent;
    private readonly ProgressBar _progress;
    private readonly DataGridView _stepGrid;
    private readonly Button _cancel;
    private bool _splitInitialized;

    public TestRunView(EquipmentState equipment, TestRun run)
    {
        _equipment = equipment;
        _run = run;
        Dock = DockStyle.Fill;
        BackColor = AppTheme.Background;
        Padding = new Padding(24);

        var header = new Panel { Dock = DockStyle.Top, Height = 58 };
        _title = AppTheme.Heading($"{equipment.Definition.Name} · 테스트 진행");
        header.Controls.Add(_title);
        var close = AppTheme.SecondaryButton("목록으로", 100);
        close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        close.Location = new Point(header.Width - close.Width, 0);
        header.Resize += (_, _) => close.Left = header.ClientSize.Width - close.Width;
        close.Click += (_, _) => CloseRequested?.Invoke();
        header.Controls.Add(close);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = AppTheme.Border
        };
        _split.SizeChanged += (_, _) =>
        {
            if (!_splitInitialized && _split.Width > 700)
            {
                _split.SplitterDistance = (int)(_split.Width * .64);
                _split.Panel1MinSize = 300;
                _split.Panel2MinSize = 240;
                _splitInitialized = true;
            }
        };

        var left = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(22) };
        _stepLabel = new Label { Text = "테스트를 준비하고 있습니다.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 13F, FontStyle.Bold), ForeColor = AppTheme.Text };
        _percent = new Label { Text = "0%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 23F, FontStyle.Bold), ForeColor = AppTheme.Primary };
        _progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        var progressSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 130,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty
        };
        progressSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        progressSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        progressSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        progressSummary.Controls.Add(_stepLabel, 0, 0);
        progressSummary.Controls.Add(_percent, 0, 1);
        progressSummary.Controls.Add(_progress, 0, 2);
        _cancel = AppTheme.SecondaryButton("테스트 취소", 120);
        _cancel.ForeColor = AppTheme.Danger;
        _cancel.Click += (_, _) => CancelRequested?.Invoke();
        var cancelHost = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        cancelHost.Controls.Add(_cancel);

        _stepGrid = CreateGrid();
        _stepGrid.Dock = DockStyle.Fill;
        _stepGrid.Columns.Add("Sequence", "순서");
        _stepGrid.Columns.Add("Name", "단계");
        _stepGrid.Columns.Add("Command", "명령");
        _stepGrid.Columns.Add("Duration", "시간");
        _stepGrid.Columns.Add("Status", "상태");
        _stepGrid.Columns[0].Width = 55;
        _stepGrid.Columns[2].Width = 115;
        _stepGrid.Columns[3].Width = 65;
        _stepGrid.Columns[4].Width = 80;
        _stepGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        foreach (var step in run.Recipe.Steps.OrderBy(x => x.Sequence))
            _stepGrid.Rows.Add(step.Sequence, step.Name, step.Command, $"{step.DurationSeconds}초", "대기");
        var spacer = new Panel { Dock = DockStyle.Top, Height = 14 };
        left.Controls.Add(_stepGrid);
        left.Controls.Add(spacer);
        left.Controls.Add(progressSummary);
        left.Controls.Add(cancelHost);

        _split.Panel1.Controls.Add(left);
        _split.Panel2.Controls.Add(BuildRecipePanel(run.Recipe));
        Controls.Add(_split);
        Controls.Add(header);
    }

    public event Action? CloseRequested;
    public event Action? CancelRequested;
    public event Action? NewTestRequested;
    public event Action<TestResult>? SaveLogRequested;
    public string RunId => _run.RunId;

    public void UpdateProgress(TestProgress update)
    {
        if (IsDisposed) return;
        _stepLabel.Text = update.LogMessage;
        _percent.Text = $"{update.Percent}%";
        _progress.Value = Math.Clamp(update.Percent, 0, 100);
        foreach (DataGridViewRow row in _stepGrid.Rows)
        {
            var step = _run.Recipe.Steps[row.Index];
            var status = update.FailedStepId == step.Id ? "실패" : update.CompletedStepIds.Contains(step.Id) ? "완료" : update.CurrentStepId == step.Id ? "진행 중" : "대기";
            row.Cells[4].Value = status;
            row.DefaultCellStyle.ForeColor = status switch { "실패" => AppTheme.Danger, "완료" => AppTheme.Success, "진행 중" => AppTheme.Primary, _ => AppTheme.Text };
        }
    }

    public void ShowResult(TestResult result)
    {
        _title.Text = $"{_equipment.Definition.Name} · 테스트 결과";
        _split.Panel1.Controls.Clear();
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(28) };
        var resultColor = result.Status == TestStatus.Succeeded ? AppTheme.Success : result.Status == TestStatus.Failed ? AppTheme.Danger : AppTheme.Warning;
        var resultText = result.Status switch { TestStatus.Succeeded => "테스트 성공", TestStatus.Failed => "테스트 실패", _ => "테스트 취소" };
        var heading = new Label { Text = resultText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 20F, FontStyle.Bold), ForeColor = resultColor };
        var summary = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 12),
            Font = new Font("맑은 고딕", 10F),
            ForeColor = AppTheme.Text,
            Text = $"최종 진행률: {result.FinalProgressPercent}%\r\n실행 시간: {result.Duration:mm\\:ss}" +
                   (result.Status == TestStatus.Failed ? $"\r\n실패 단계: {result.FailedStepName}\r\n원인: {result.FailureReason}" : string.Empty)
        };
        var logTitle = new Label { Text = "로그 미리보기", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 10F, FontStyle.Bold), ForeColor = AppTheme.Text };
        var log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(247, 249, 251),
            Font = new Font("Consolas", 9F),
            Text = result.LogText
        };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0), WrapContents = false };
        var save = AppTheme.SecondaryButton("로그 파일 저장", 165);
        save.Click += (_, _) => SaveLogRequested?.Invoke(result);
        var restart = AppTheme.PrimaryButton("새 테스트 시작", 165);
        restart.Click += (_, _) => NewTestRequested?.Invoke();
        actions.Controls.Add(restart);
        actions.Controls.Add(save);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(summary, 0, 1);
        layout.Controls.Add(logTitle, 0, 2);
        layout.Controls.Add(log, 0, 3);
        layout.Controls.Add(actions, 0, 4);
        panel.Controls.Add(layout);
        _split.Panel1.Controls.Add(panel);
    }

    private static DataGridView CreateGrid() => new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        ReadOnly = true,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        ColumnHeadersHeight = 34,
        RowTemplate = { Height = 32 }
    };

    private static Control BuildRecipePanel(RecipeDocument recipe)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, AutoScroll = true };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(20, 18, 20, 24),
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = new Label { Text = "레시피", AutoSize = true, Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 13F, FontStyle.Bold), ForeColor = AppTheme.Text, Margin = new Padding(0, 0, 0, 12) };
        var meta = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = AppTheme.Text,
            Margin = new Padding(0, 0, 0, 18),
            Text = $"레시피명  {recipe.Name}\r\nID  {recipe.RecipeId}\r\n버전  {recipe.Version}\r\n대상 모델  {recipe.TargetEquipmentModel}\r\n작성자  {recipe.Author}\r\n작성일  {recipe.CreatedAt:yyyy-MM-dd HH:mm}\r\n\r\n웨이퍼 크기  {recipe.Wafer.DiameterMm} mm\r\n재질  {recipe.Wafer.Material}\r\nLot ID  {recipe.Wafer.LotId}\r\n\r\n스캔 모드  {recipe.Inspection.ScanMode}\r\n해상도  {recipe.Inspection.ResolutionMicrometer} μm\r\n결함 임계값  {recipe.Inspection.DefectThresholdMicrometer} μm\r\n에지 제외  {recipe.Inspection.EdgeExclusionMm} mm"
        };
        var parametersTitle = new Label { Text = "단계 및 파라미터", AutoSize = true, Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 10F, FontStyle.Bold), ForeColor = AppTheme.Text, Margin = new Padding(0, 0, 0, 10) };

        AddRecipeRow(content, title);
        AddRecipeRow(content, meta);
        AddRecipeRow(content, parametersTitle);
        foreach (var step in recipe.Steps.OrderBy(x => x.Sequence))
        {
            var stepPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(247, 249, 251),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };
            stepPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stepPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stepPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stepPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var stepTitle = new Label
            {
                Text = $"{step.Sequence}. {step.Name}",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = AppTheme.Text,
                Padding = new Padding(2, 2, 2, 6)
            };
            var command = new Label
            {
                Text = $"명령  {step.Command}    실행 시간  {step.DurationSeconds}초",
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.Muted,
                Padding = new Padding(2, 4, 2, 6)
            };
            var parameterText = step.Parameters.Count == 0
                ? "파라미터 없음"
                : string.Join(Environment.NewLine, step.Parameters.Select(parameter => $"• {parameter.Name}: {parameter.Value} {parameter.Unit}".TrimEnd()));
            var parameters = new Label
            {
                Text = parameterText,
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = AppTheme.Text,
                Padding = new Padding(2, 5, 2, 2)
            };
            stepPanel.Controls.Add(stepTitle, 0, 0);
            stepPanel.Controls.Add(command, 0, 1);
            stepPanel.Controls.Add(parameters, 0, 2);
            AddRecipeRow(content, stepPanel);
        }
        host.Controls.Add(content);
        return host;
    }

    private static void AddRecipeRow(TableLayoutPanel layout, Control control)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(control, 0, row);
    }
}

internal sealed class TaskStatusRow : Panel
{
    private readonly Label _label;
    private readonly ProgressBar _bar;

    public TaskStatusRow(EquipmentState equipment)
    {
        Height = 46;
        Dock = DockStyle.Top;
        Padding = new Padding(12, 8, 12, 6);
        BackColor = Color.White;
        _label = new Label { Text = equipment.Definition.Name, Dock = DockStyle.Left, Width = 210, TextAlign = ContentAlignment.MiddleLeft, ForeColor = AppTheme.Text };
        _bar = new ProgressBar { Dock = DockStyle.Fill, Maximum = 100 };
        Controls.Add(_bar);
        Controls.Add(_label);
        UpdateState(equipment);
    }

    public void UpdateState(EquipmentState equipment)
    {
        _label.Text = $"{equipment.Definition.Name}   {equipment.ProgressPercent}%";
        _bar.Value = Math.Clamp(equipment.ProgressPercent, 0, 100);
    }
}
