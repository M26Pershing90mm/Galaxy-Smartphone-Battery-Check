

using System.Diagnostics;
using GalaxyBatteryCheck.Core;

namespace GalaxyBatteryCheck.Gui;

public sealed class MainForm : Form
{
    private static readonly string[] RowNames =
    {
        "model", "android_version", "level_pct", "temperature_c", "max_temperature_c",
        "first_use_date", "cycle_count", "charge_counter_mah", "extrapolated_full_mah",
        "rated_capacity_mah", "rated_capacity_source", "extrapolated_to_rated_pct",
    };

    private string _languageMode = "auto";
    private string _language;
    private Report? _data;
    private bool _busy;
    private BatteryService? _service;
    private string? _servicePath;
    private readonly CancellationTokenSource _lifetime = new();
    private string _statusKey = "idle";
    private object[] _statusArgs = Array.Empty<object>();

    private readonly Dictionary<string, Control> _simple = new();
    private readonly Dictionary<string, (Panel Panel, FlowLayoutPanel Stack, Label Header, Label Value)> _cards = new();
    private readonly System.Windows.Forms.Timer _connectTimer = new() { Interval = 4000 };
    private bool _updatingDeviceList;
    private string? _lastAutoAttemptSerial;
    private DateTimeOffset _lastAutoAttemptAt = DateTimeOffset.MinValue;
    private Label _healthDetail = null!;
    private readonly Dictionary<string, ListViewItem> _tableRows = new();

    private ComboBox _languageCombo = null!;
    private TextBox _adbBox = null!;
    private ComboBox _deviceCombo = null!;
    private TextBox _ratedBox = null!;
    private Button _scanButton = null!;
    private Button _checkButton = null!;
    private Button _browseButton = null!;
    private Button _saveButton = null!;
    private Button _donateButton = null!;
    private LinkLabel _authorLink = null!;
    private Label _statusLabel = null!;
    private ListView _table = null!;

    public MainForm()
    {
        _language = Language.WindowsDisplayLanguage();
        Text = $"{Localization.Get(_language, "heading")} v{AppInfo.Version} · {AppInfo.CopyrightHolder}";
        Width = 920;
        Height = 770;
        MinimumSize = new Size(740, 660);
        BackColor = Color.FromArgb(0xf4, 0xf6, 0xfa);
        Font = new Font("Segoe UI", 9.5f);

        LoadIcon();
        BuildLayout();
        Translate();

        _connectTimer.Tick += async (_, _) => await ScanAsync(background: true);
        Load += async (_, _) =>
        {
            try
            {
                await Task.Delay(350, _lifetime.Token);
                _connectTimer.Start();
                await ScanAsync(background: true);
            }
            catch (OperationCanceledException) { }
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel)
        {
            _connectTimer.Stop();
            _lifetime.Cancel();
            _service?.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectTimer.Stop();
            _connectTimer.Dispose();
            _lifetime.Cancel();
            _service?.Dispose();
        }
        base.Dispose(disposing);
    }

    private BatteryService Service(string path)
    {
        if (_service is null || _service.IsStopped || _servicePath != path)
        {
            _service?.Dispose();
            _service = new BatteryService(path);
            _servicePath = path;
        }
        return _service;
    }

    private void LoadIcon()
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "GalaxyBatteryCheck.ico");
            if (File.Exists(icoPath))
                Icon = new Icon(icoPath);
        }
        catch
        {

        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 13, 18, 8),
            AutoScroll = true,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var headingBox = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        var heading = NewLabel("heading", bold: true, size: 15f);
        var subtitle = NewLabel("subtitle", color: Color.FromArgb(0x52, 0x61, 0x76));
        headingBox.Controls.Add(heading);
        headingBox.Controls.Add(subtitle);

        var languageBox = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Anchor = AnchorStyles.Right };
        var languageLabel = NewLabel("language");
        _languageCombo = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        _languageCombo.SelectedIndexChanged += OnLanguageChanged;
        languageBox.Controls.Add(languageLabel);
        languageBox.Controls.Add(_languageCombo);

        header.Controls.Add(headingBox, 0, 0);
        header.Controls.Add(languageBox, 1, 0);

        var independentLabel = NewLabel("independent", color: Color.FromArgb(0x71, 0x50, 0x24));
        independentLabel.MaximumSize = new Size(850, 0);
        independentLabel.Dock = DockStyle.Top;

        var connection = new GroupBox { Text = "", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(11), Height = 150 };
        _simple["connection"] = connection;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var adbLabel = NewLabel("adb");
        _adbBox = new TextBox { Dock = DockStyle.Fill, Text = FindAdb() };
        _browseButton = new Button { AutoSize = true };
        _simple["browse"] = _browseButton;
        _browseButton.Click += OnBrowseAdb;
        grid.Controls.Add(adbLabel, 0, 0);
        grid.Controls.Add(_adbBox, 1, 0);
        grid.Controls.Add(_browseButton, 2, 0);

        var deviceLabel = NewLabel("device");
        _deviceCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _deviceCombo.SelectedIndexChanged += async (_, _) =>
        {
            if (_updatingDeviceList || _lifetime.IsCancellationRequested) return;
            ClearReport();
            await CheckAsync(automatic: true);
        };
        _scanButton = new Button { AutoSize = true };
        _simple["scan"] = _scanButton;
        _scanButton.Click += async (_, _) => await ScanAsync();
        grid.Controls.Add(deviceLabel, 0, 1);
        grid.Controls.Add(_deviceCombo, 1, 1);
        grid.Controls.Add(_scanButton, 2, 1);

        var ratedLabel = NewLabel("rated");
        _ratedBox = new TextBox { Dock = DockStyle.Fill };
        _checkButton = new Button { AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        _simple["check"] = _checkButton;
        _checkButton.Click += async (_, _) => await CheckAsync();
        grid.Controls.Add(ratedLabel, 0, 2);
        grid.Controls.Add(_ratedBox, 1, 2);
        grid.Controls.Add(_checkButton, 2, 2);

        connection.Controls.Add(grid);

        var summaryLabel = NewLabel("summary", bold: true, size: 11f);
        summaryLabel.Dock = DockStyle.Top;

        var cardsPanel = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, Height = 102 };
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        cardsPanel.Controls.Add(BuildCard("health"), 0, 0);
        cardsPanel.Controls.Add(BuildCard("bsoh"), 1, 0);
        cardsPanel.Controls.Add(BuildCard("asoc"), 2, 0);
        _healthDetail = new Label
        {
            AutoSize = true, Dock = DockStyle.Top,
            Padding = new Padding(2, 5, 2, 10),
            ForeColor = Color.FromArgb(0x52, 0x61, 0x76),
        };

        var detailsLabel = NewLabel("details", bold: true, size: 11f);
        detailsLabel.Dock = DockStyle.Top;

        _table = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = false,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.None,
            Height = 220,
        };
        _table.Columns.Add("", 280);
        _table.Columns.Add("", 320);
        foreach (var key in RowNames)
        {
            var item = new ListViewItem(new[] { "", "—" });
            _tableRows[key] = item;
            _table.Items.Add(item);
        }

        var noticeLabel = NewLabel("notice", color: Color.FromArgb(0x52, 0x61, 0x76));
        noticeLabel.Dock = DockStyle.Top;
        noticeLabel.MaximumSize = new Size(850, 0);
        var privacyLabel = NewLabel("privacy", color: Color.FromArgb(0x52, 0x61, 0x76));
        privacyLabel.Dock = DockStyle.Top;
        privacyLabel.MaximumSize = new Size(850, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        body.Controls.Add(header);
        body.Controls.Add(independentLabel);
        body.Controls.Add(connection);
        body.Controls.Add(summaryLabel);
        body.Controls.Add(cardsPanel);
        body.Controls.Add(_healthDetail);
        body.Controls.Add(detailsLabel);
        body.Controls.Add(_table);
        body.Controls.Add(noticeLabel);
        body.Controls.Add(privacyLabel);
        root.Controls.Add(body, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 1, AutoSize = true, Padding = new Padding(18, 10, 18, 12) };

        var statusLine = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        statusLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _statusLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
        _saveButton = new Button { AutoSize = true, Enabled = false };
        _simple["save"] = _saveButton;
        _saveButton.Click += OnSave;
        statusLine.Controls.Add(_statusLabel, 0, 0);
        statusLine.Controls.Add(_saveButton, 1, 0);

        var actionLine = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        actionLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var supportLabel = NewLabel("support");
        _donateButton = new Button
        {
            AutoSize = true, BackColor = Color.FromArgb(0x14, 0x7a, 0x52), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(18, 9, 18, 9),
        };
        _donateButton.FlatAppearance.BorderSize = 0;
        _simple["donate"] = _donateButton;
        _donateButton.Click += (_, _) => OpenUrl(Language.DonationUrl(_language));
        actionLine.Controls.Add(supportLabel, 0, 0);
        actionLine.Controls.Add(_donateButton, 1, 0);

        var legalLine = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 2,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0), Padding = new Padding(0, 3, 0, 3),
        };
        legalLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        legalLine.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        legalLine.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _authorLink = new LinkLabel
        {
            AutoSize = true, AutoEllipsis = false, Dock = DockStyle.Top,
            Margin = new Padding(0, 2, 0, 2), TextAlign = ContentAlignment.MiddleLeft,
        };
        _authorLink.LinkClicked += (_, _) => OpenUrl(AppInfo.BlogUrl);
        var copyrightLabel = NewLabel("copyright", color: Color.FromArgb(0x52, 0x61, 0x76));
        legalLine.Controls.Add(_authorLink, 0, 0);
        copyrightLabel.Dock = DockStyle.Top;
        copyrightLabel.Margin = new Padding(0, 2, 0, 2);
        legalLine.Controls.Add(copyrightLabel, 0, 1);
        legalLine.SizeChanged += (_, _) => UpdateFooterTextWidths();

        var warningLabel = NewLabel("warning", bold: true, color: Color.FromArgb(0xb9, 0x1c, 0x1c));
        warningLabel.Dock = DockStyle.Top;
        warningLabel.MaximumSize = new Size(850, 0);

        footer.Controls.Add(statusLine);
        footer.Controls.Add(actionLine);
        footer.Controls.Add(legalLine);
        footer.Controls.Add(warningLabel);
        footer.SizeChanged += (_, _) => UpdateFooterTextWidths();
        Controls.Add(footer);
    }

    private void UpdateFooterTextWidths()
    {
        if (_authorLink?.Parent is not Control legalLine) return;
        var width = Math.Max(1, legalLine.ClientSize.Width - legalLine.Padding.Horizontal - _authorLink.Margin.Horizontal);
        var maximum = new Size(width, 0);
        if (_authorLink.MaximumSize != maximum) _authorLink.MaximumSize = maximum;
        if (_simple.TryGetValue("copyright", out var copyright) && copyright.MaximumSize != maximum)
            copyright.MaximumSize = maximum;
        if (_simple.TryGetValue("warning", out var warning) && warning.Parent is Control footer)
        {
            var warningMaximum = new Size(Math.Max(1, footer.ClientSize.Width - footer.Padding.Horizontal - warning.Margin.Horizontal), 0);
            if (warning.MaximumSize != warningMaximum) warning.MaximumSize = warningMaximum;
        }
    }

    private Panel BuildCard(string key)
    {
        var background = Color.FromArgb(0xe9, 0xee, 0xf5);
        var card = new Panel { Dock = DockStyle.Fill, BackColor = background, Padding = new Padding(12, 12, 8, 10), Margin = new Padding(0, 0, 8, 0) };
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, BackColor = background, WrapContents = false };
        var header = new Label { AutoSize = true, ForeColor = Color.FromArgb(0x40, 0x54, 0x6b), Font = new Font(Font, FontStyle.Bold), BackColor = background };
        var value = new Label { AutoSize = true, Text = "—", ForeColor = Color.FromArgb(0x10, 0x36, 0x5e), Font = new Font("Segoe UI", 19f, FontStyle.Bold), BackColor = background };
        stack.Controls.Add(header);
        stack.Controls.Add(value);
        card.Controls.Add(stack);
        _cards[key] = (card, stack, header, value);
        return card;
    }

    private void ColorCard(string key, string grade)
    {
        var background = grade switch
        {
            "good" => Color.FromArgb(0xe0, 0xf5, 0xe6),
            "monitor" => Color.FromArgb(0xff, 0xf2, 0xcf),
            "service" => Color.FromArgb(0xff, 0xe7, 0xe5),
            _ => Color.FromArgb(0xe9, 0xee, 0xf5),
        };
        var foreground = grade switch
        {
            "good" => Color.FromArgb(0x14, 0x78, 0x43),
            "monitor" => Color.FromArgb(0x8a, 0x59, 0x00),
            "service" => Color.FromArgb(0xb4, 0x23, 0x18),
            _ => Color.FromArgb(0x40, 0x54, 0x6b),
        };
        var card = _cards[key];
        card.Panel.BackColor = background;
        card.Stack.BackColor = background;
        card.Header.BackColor = background;
        card.Value.BackColor = background;
        card.Value.ForeColor = foreground;
    }

    private void ClearReport()
    {
        _data = null;
        _saveButton.Enabled = false;
        foreach (var key in _cards.Keys)
        {
            _cards[key].Value.Text = "—";
            ColorCard(key, "unknown");
        }
        _healthDetail.Text = Localization.Get(_language, "health_unavailable_note");
        foreach (var row in _tableRows.Values) row.SubItems[1].Text = "—";
    }

    private Label NewLabel(string key, bool bold = false, float size = 9.5f, Color? color = null)
    {
        var label = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? Color.Black,
        };
        _simple[key] = label;
        return label;
    }

    private static string FindAdb()
    {
        var dir = AppContext.BaseDirectory;
        foreach (var candidate in new[]
                 {
                     Path.Combine(dir, "platform-tools", "adb.exe"),
                     Path.Combine(dir, "adb.exe"),
                 })
        {
            if (File.Exists(candidate))
                return candidate;
        }
        return "adb";
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        var index = _languageCombo.SelectedIndex;
        _languageMode = index switch { 1 => "ko", 2 => "en", 3 => "ja", _ => "auto" };
        _language = _languageMode == "auto" ? Language.WindowsDisplayLanguage() : _languageMode;
        Translate();
    }

    private void Translate()
    {
        Text = $"{Localization.Get(_language, "heading")} v{AppInfo.Version} · {AppInfo.CopyrightHolder}";
        foreach (var (key, control) in _simple)
        {
            if (control is GroupBox or Form)
                continue;
            control.Text = Localization.Get(_language, key);
        }

        var color = _language == "ko" ? Color.FromArgb(0x14, 0x7a, 0x52) : Color.FromArgb(0x1b, 0x5e, 0xc9);
        _donateButton.BackColor = color;

        _languageCombo.SelectedIndexChanged -= OnLanguageChanged;
        _languageCombo.Items.Clear();
        _languageCombo.Items.AddRange(new object[]
        {
            Localization.Get(_language, "auto"), Localization.LanguageNames["ko"],
            Localization.LanguageNames["en"], Localization.LanguageNames["ja"],
        });
        _languageCombo.SelectedIndex = _languageMode == "auto" ? 0 : _languageMode switch { "ko" => 1, "en" => 2, "ja" => 3, _ => 0 };
        _languageCombo.SelectedIndexChanged += OnLanguageChanged;

        foreach (var pair in _cards)
            pair.Value.Header.Text = Localization.Get(_language, pair.Key);
        _ratedBox.PlaceholderText = Localization.Get(_language, "rated_auto_hint");

        foreach (var key in RowNames)
            _tableRows[key].SubItems[0].Text = Localization.Get(_language, key);

        _authorLink.Text = Localization.Get(_language, "author");
        UpdateFooterTextWidths();

        SetStatus(_statusKey, _statusArgs);
        if (_data is not null)
            Render(_data);
        else
            _healthDetail.Text = Localization.Get(_language, "health_unavailable_note");
    }

    private void SetStatus(string key, params object[] args)
    {
        if (IsDisposed || Disposing) return;
        _statusKey = key;
        _statusArgs = args;
        _statusLabel.Text = Localization.Get(_language, key, args.Select(value => value is AdbException error ? (object)AppText.Error(_language, error) : value).ToArray());
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (IsDisposed || Disposing) return;
        _scanButton.Enabled = !busy;
        _checkButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _deviceCombo.Enabled = !busy;
        _adbBox.Enabled = !busy;
        _ratedBox.Enabled = !busy;
    }

    private void OnBrowseAdb(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = Localization.Get(_language, "adb_title"),
            Filter = _language switch { "ko" => "ADB|adb.exe|모든 파일|*.*", "ja" => "ADB|adb.exe|すべてのファイル|*.*", _ => "ADB|adb.exe|All files|*.*" },
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _adbBox.Text = dialog.FileName;
            _ = ScanAsync();
        }
    }

    private async Task ScanAsync(bool background = false)
    {
        if (_busy || _lifetime.IsCancellationRequested) return;
        SetBusy(true);
        if (!background) SetStatus("scanning");
        var adb = string.IsNullOrWhiteSpace(_adbBox.Text) ? "adb" : _adbBox.Text.Trim();
        string? autoSerial = null;
        try
        {
            var devices = await Service(adb).ListDevicesAsync(_lifetime.Token);
            if (_lifetime.IsCancellationRequested) return;
            var connected = devices.Where(d => d.State == "device").Select(d => d.Serial).ToList();
            var denied = devices.Where(d => d.State != "device").Select(d => $"{d.Serial} ({d.State})").ToList();
            var previous = _deviceCombo.SelectedItem as string;
            var candidate = previous is not null && connected.Contains(previous) ? previous
                : connected.Count == 1 ? connected[0] : null;

            _updatingDeviceList = true;
            try
            {
                _deviceCombo.Items.Clear();
                _deviceCombo.Items.AddRange(connected.Cast<object>().ToArray());
                _deviceCombo.SelectedItem = candidate;
            }
            finally { _updatingDeviceList = false; }

            if (candidate is null || !string.Equals(_data?.Device.Serial, candidate, StringComparison.Ordinal))
                ClearReport();

            if (connected.Count == 0)
            {
                _lastAutoAttemptSerial = null;
                if (denied.Count > 0) SetStatus("device_unavailable", string.Join(", ", denied));
                else SetStatus("no_device");
            }
            else if (candidate is null)
            {
                SetStatus("multiple_manual");
            }
            else
            {
                if (!background && _data is not null) SetStatus("found", connected.Count);
                if (_data is null && (candidate != _lastAutoAttemptSerial ||
                    DateTimeOffset.UtcNow - _lastAutoAttemptAt >= TimeSpan.FromSeconds(15)))
                {
                    _lastAutoAttemptSerial = candidate;
                    _lastAutoAttemptAt = DateTimeOffset.UtcNow;
                    autoSerial = candidate;
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (AdbNotFoundException)
        {
            ClearReport();
            SetStatus("no_adb");
        }
        catch (AdbException ex)
        {
            SetStatus("error", ex);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            SetStatus("error", AppText.AdbError("adb_start"));
        }
        finally { SetBusy(false); }

        if (autoSerial is not null && !_lifetime.IsCancellationRequested &&
            string.Equals(_deviceCombo.SelectedItem as string, autoSerial, StringComparison.Ordinal))
            await CheckAsync(automatic: true);
    }

    private async Task CheckAsync(bool automatic = false)
    {
        if (_busy || _lifetime.IsCancellationRequested)
            return;
        var serial = _deviceCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(serial))
        {
            if (automatic) return;
            MessageBox.Show(this, Localization.Get(_language, "missing_device"),
                Localization.Get(_language, "missing_device_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        double? rated = null;
        var ratedText = _ratedBox.Text.Trim();
        if (ratedText.Length > 0)
        {
            if (!double.TryParse(ratedText, out var parsed) || !double.IsFinite(parsed) || parsed is < 100 or > 30000)
            {
                if (automatic) SetStatus("rated_error");
                else MessageBox.Show(this, Localization.Get(_language, "rated_error"),
                    Localization.Get(_language, "rated_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            rated = parsed;
        }

        SetBusy(true);
        SetStatus("reading");
        var adb = string.IsNullOrWhiteSpace(_adbBox.Text) ? "adb" : _adbBox.Text.Trim();
        try
        {
            var report = await Service(adb).CollectAsync(serial, rated, skipSysfs: false, cancellationToken: _lifetime.Token);
            if (_lifetime.IsCancellationRequested) return;
            // Ignore a result from a phone that has since been deselected.
            if (!string.Equals(_deviceCombo.SelectedItem as string, serial, StringComparison.Ordinal)) return;
            _data = report;
            Render(report);
            _saveButton.Enabled = true;
            SetStatus("done");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (AdbNotFoundException)
        {
            SetStatus("no_adb");
        }
        catch (AdbException ex)
        {
            SetStatus("error", ex);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            SetStatus("error", AppText.AdbError("adb_start"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string Fmt(object? value, string unit = "", int digits = 1)
    {
        return value switch
        {
            null => Localization.Get(_language, "none"),
            double d => d.ToString("F" + digits, System.Globalization.CultureInfo.GetCultureInfo(_language)) + unit,
            _ => DisplayText.Clean(value?.ToString() ?? "") + unit,
        };
    }

    private void Render(Report report)
    {
        var b = report.Battery;
        var assessment = b.HealthAssessment ?? BatteryHealth.Assess(b);
        _cards["health"].Value.Text = Localization.Get(_language, "health_" + assessment.Status);
        _cards["bsoh"].Value.Text = Fmt(b.BsohPct, "%", 2);
        _cards["asoc"].Value.Text = Fmt(b.AsocPct, "%", 2);
        ColorCard("health", assessment.Status);
        ColorCard("bsoh", assessment.BsohStatus);
        ColorCard("asoc", assessment.AsocStatus);
        _healthDetail.Text = Localization.Get(_language, "health_basis_" + assessment.BasedOn);
        if (assessment.IndicatorsDiffer)
            _healthDetail.Text += "  " + Localization.Get(_language, "health_disagree");

        void Set(string key, string value) => _tableRows[key].SubItems[1].Text = value;
        Set("model", Fmt(report.Device.Model));
        Set("android_version", Fmt(report.Device.AndroidVersion));
        Set("level_pct", b.LevelPct is int lvl ? $"{lvl}%" : Localization.Get(_language, "none"));
        Set("temperature_c", Fmt(b.TemperatureC, " °C"));
        Set("max_temperature_c", Fmt(b.MaxTemperatureC, " °C"));
        Set("first_use_date", Fmt(b.FirstUseDate));
        Set("cycle_count", Fmt(b.CycleCount, " " + Localization.Get(_language, "cycles"), 2));
        Set("charge_counter_mah", Fmt(b.ChargeCounterMAh, " mAh", 2));
        Set("extrapolated_full_mah", Fmt(b.ExtrapolatedFullMAh, " mAh"));
        Set("rated_capacity_mah", Fmt(b.RatedCapacityMAh, " mAh"));
        Set("rated_capacity_source", b.RatedCapacitySource is null ? Localization.Get(_language, "none") :
            Localization.Get(_language, b.RatedCapacitySource == "manual" ? "rated_manual" : "rated_device"));
        Set("extrapolated_to_rated_pct", Fmt(b.ExtrapolatedToRatedPct, "%"));
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_data is null)
            return;
        using var dialog = new SaveFileDialog
        {
            Title = Localization.Get(_language, "save_title"),
            Filter = "JSON|*.json",
            FileName = "galaxy battery report.json",
            DefaultExt = ".json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _data.Application.DonationUrl = Language.DonationUrl(_language);
            ReportFile.Save(dialog.FileName, ReportJson.Serialize(_data));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, Localization.Get(_language, "save_error"), Localization.Get(_language, "save_failed"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        SetStatus("saved", Path.GetFileName(dialog.FileName));
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(this, Localization.Get(_language, "browser_error", url),
                Localization.Get(_language, "browser_error_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
