#nullable enable
using System.Drawing;
using BMDEditor.Bmd;

namespace BMDEditor;

partial class Form1
{
    private System.ComponentModel.IContainer? components;
    private MenuStrip _menuStrip = null!;
    private ToolStripMenuItem _fileMenuItem = null!;
    private ToolStripMenuItem _framesMenuItem = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ListBox _framesList = null!;
    private Label _selectedCountLabel = null!;
    private CheckBox _useAlphaCheck = null!;
    private CheckBox _showAnchorCheck = null!;
    private Button _importPngButton = null!;
    private Button _batchImportButton = null!;
    private Button _replaceFrameButton = null!;
    private Button _exportFrameButton = null!;
    private Button _deleteFrameButton = null!;
    private Button _deleteAllButton = null!;
    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private Panel _animationPanel = null!;
    private Button _animationToggleButton = null!;
    private NumericUpDown _animationFpsInput = null!;
    private Button _batchAnchorButton = null!;
    private Button _batchExportButton = null!;
    private TextBox _palettePathTextBox = null!;
    private Button _browsePaletteButton = null!;
    private Button _loadPaletteButton = null!;
    private Button _randomPaletteButton = null!;
    private ComboBox _autoPaletteVariantInput = null!;
    private FlowLayoutPanel _palettePanel = null!;
    private ComboBox _gameFormatInput = null!;
    private ComboBox _frameTypeInput = null!;
    private NumericUpDown _offsetXInput = null!;
    private NumericUpDown _offsetYInput = null!;
    private Label _widthValueLabel = null!;
    private Label _heightValueLabel = null!;
    private PictureBox _previewBox = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _menuStrip = new MenuStrip();
        _fileMenuItem = new ToolStripMenuItem();
        _framesMenuItem = new ToolStripMenuItem();
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        _framesList = new ListBox();
        _selectedCountLabel = new Label();
        _useAlphaCheck = new CheckBox();
        _showAnchorCheck = new CheckBox();
        _importPngButton = new Button();
        _batchImportButton = new Button();
        _replaceFrameButton = new Button();
        _exportFrameButton = new Button();
        _deleteFrameButton = new Button();
        _deleteAllButton = new Button();
        _moveUpButton = new Button();
        _moveDownButton = new Button();
        _animationPanel = new Panel();
        _animationToggleButton = new Button();
        _animationFpsInput = new NumericUpDown();
        _batchAnchorButton = new Button();
        _batchExportButton = new Button();
        _palettePathTextBox = new TextBox();
        _browsePaletteButton = new Button();
        _loadPaletteButton = new Button();
        _randomPaletteButton = new Button();
        _autoPaletteVariantInput = new ComboBox();
        _palettePanel = new FlowLayoutPanel();
        _gameFormatInput = new ComboBox();
        _frameTypeInput = new ComboBox();
        _offsetXInput = new NumericUpDown();
        _offsetYInput = new NumericUpDown();
        _widthValueLabel = new Label();
        _heightValueLabel = new Label();
        _previewBox = new PictureBox();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Control;
        ClientSize = new Size(1450, 900);
        MinimumSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "BMDEditor";

        BuildMenu();
        BuildLayout();

        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Dock = DockStyle.Bottom;
        _statusLabel.Text = "Gotowe.";

        Controls.Add(_statusStrip);
        Controls.Add(_menuStrip);

        ResumeLayout(false);
        PerformLayout();
    }

    private void BuildMenu()
    {
        _fileMenuItem.Text = "Plik";
        _fileMenuItem.DropDownItems.Add("Nowy", null, (_, _) => CreateNewDocument());
        _fileMenuItem.DropDownItems.Add("Otworz BMD...", null, (_, _) => OpenBmd());
        _fileMenuItem.DropDownItems.Add("Wybierz folder gry...", null, (_, _) => SelectGameFolder());
        _fileMenuItem.DropDownItems.Add("Zapisz", null, (_, _) => SaveDocument());
        _fileMenuItem.DropDownItems.Add("Zapisz jako...", null, (_, _) => SaveDocumentAs());

        _framesMenuItem.Text = "Klatki";
        _framesMenuItem.DropDownItems.Add("Import PNG...", null, (_, _) => AddFrameFromPng());
        _framesMenuItem.DropDownItems.Add("Podmien zaznaczona...", null, (_, _) => ReplaceFrameFromPng());
        _framesMenuItem.DropDownItems.Add("Eksport PNG...", null, (_, _) => ExportCurrentFrame());
        _framesMenuItem.DropDownItems.Add("Masowy import...", null, (_, _) => ImportWorkspace());
        _framesMenuItem.DropDownItems.Add("Masowy eksport...", null, (_, _) => ExportWorkspace());

        _menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenuItem, _framesMenuItem });
        _menuStrip.Dock = DockStyle.Top;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 0),
            ColumnCount = 3,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildFramesColumn(), 0, 0);
        root.Controls.Add(BuildPaletteColumn(), 1, 0);
        root.Controls.Add(BuildPreviewPanel(), 2, 0);

        Controls.Add(root);
    }

    private Control BuildFramesColumn()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 12
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var title = new Label
        {
            Text = "Klatki",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        _framesList.Dock = DockStyle.Fill;
        _framesList.BorderStyle = BorderStyle.FixedSingle;
        _framesList.SelectionMode = SelectionMode.MultiExtended;
        _framesList.SelectedIndexChanged += (_, _) => LoadSelectedFrameIntoEditor();

        _selectedCountLabel.Text = "Zaznaczono: 0";
        _selectedCountLabel.Dock = DockStyle.Fill;
        _selectedCountLabel.TextAlign = ContentAlignment.MiddleLeft;

        _useAlphaCheck.Text = "Uzywaj alpha";
        _useAlphaCheck.Dock = DockStyle.Fill;
        _useAlphaCheck.CheckedChanged += (_, _) => RefreshPreview();

        _showAnchorCheck.Text = "Pokaz zaczep";
        _showAnchorCheck.Dock = DockStyle.Fill;
        _showAnchorCheck.CheckedChanged += (_, _) => RefreshPreview();

        var row1 = TwoButtonRow(_importPngButton, _batchImportButton, "Import PNG", "Masowy import");
        _importPngButton.Click += (_, _) => AddFrameFromPng();
        _batchImportButton.Click += (_, _) => ImportWorkspace();

        var row2 = TwoButtonRow(_replaceFrameButton, _exportFrameButton, "Zamien zaznaczona", "Eksport PNG");
        _replaceFrameButton.Click += (_, _) => ReplaceFrameFromPng();
        _exportFrameButton.Click += (_, _) => ExportCurrentFrame();

        var row3 = TwoButtonRow(_deleteFrameButton, _deleteAllButton, "Usun zaznaczona", "Usun wszystkie");
        _deleteFrameButton.Click += (_, _) => DeleteSelectedFrame();
        _deleteAllButton.Click += (_, _) => DeleteAllFrames();

        var row4 = TwoButtonRow(_moveUpButton, _moveDownButton, "Przesun w gore", "Przesun w dol");
        _moveUpButton.Click += (_, _) => MoveSelectedFramesUp();
        _moveDownButton.Click += (_, _) => MoveSelectedFramesDown();

        BuildAnimationPanel();

        _batchExportButton.Text = "Masowy eksport";
        _batchExportButton.Dock = DockStyle.Fill;
        _batchExportButton.Height = 38;
        _batchExportButton.Click += (_, _) => ExportWorkspace();

        _batchAnchorButton.Text = "Masowa zmiana anchor";
        _batchAnchorButton.Dock = DockStyle.Fill;
        _batchAnchorButton.Height = 38;
        _batchAnchorButton.Click += (_, _) => BatchChangeAnchors();

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_framesList, 0, 1);
        panel.Controls.Add(_selectedCountLabel, 0, 2);
        panel.Controls.Add(_useAlphaCheck, 0, 3);
        panel.Controls.Add(_showAnchorCheck, 0, 4);
        panel.Controls.Add(row1, 0, 5);
        panel.Controls.Add(row2, 0, 6);
        panel.Controls.Add(row3, 0, 7);
        panel.Controls.Add(row4, 0, 8);
        panel.Controls.Add(_animationPanel, 0, 9);
        panel.Controls.Add(_batchAnchorButton, 0, 10);
        panel.Controls.Add(_batchExportButton, 0, 11);

        return panel;
    }

    private Control BuildPaletteColumn()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        var paletteGroup = new GroupBox
        {
            Text = "Paleta",
            Dock = DockStyle.Fill
        };

        var paletteLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(8)
        };
        paletteLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paletteLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
        paletteLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        paletteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        paletteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        paletteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        paletteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _palettePathTextBox.Dock = DockStyle.Fill;
        _browsePaletteButton.Text = "...";
        _browsePaletteButton.Dock = DockStyle.Fill;
        _browsePaletteButton.Click += (_, _) => BrowsePalettePath();

        var paletteButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        paletteButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        paletteButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _loadPaletteButton.Text = "Wczytaj PCX";
        _loadPaletteButton.Dock = DockStyle.Fill;
        _loadPaletteButton.Click += (_, _) => LoadPalette();

        _randomPaletteButton.Text = "Losuj palete";
        _randomPaletteButton.Dock = DockStyle.Fill;
        _randomPaletteButton.Click += (_, _) => RandomizePalette();

        paletteButtons.Controls.Add(_loadPaletteButton, 0, 0);
        paletteButtons.Controls.Add(_randomPaletteButton, 1, 0);

        _autoPaletteVariantInput.Dock = DockStyle.Fill;
        _autoPaletteVariantInput.DropDownStyle = ComboBoxStyle.DropDownList;
        _autoPaletteVariantInput.Enabled = false;
        _autoPaletteVariantInput.SelectedIndexChanged += (_, _) => ApplySelectedAutoPaletteVariant();

        _palettePanel.Dock = DockStyle.Fill;
        _palettePanel.AutoScroll = true;
        _palettePanel.WrapContents = true;
        _palettePanel.Padding = new Padding(4);
        _palettePanel.BackColor = Color.White;
        _palettePanel.BorderStyle = BorderStyle.FixedSingle;

        paletteLayout.Controls.Add(_palettePathTextBox, 0, 0);
        paletteLayout.Controls.Add(new Panel(), 1, 0);
        paletteLayout.Controls.Add(_browsePaletteButton, 2, 0);
        paletteLayout.Controls.Add(paletteButtons, 0, 1);
        paletteLayout.SetColumnSpan(paletteButtons, 3);
        paletteLayout.Controls.Add(_autoPaletteVariantInput, 0, 2);
        paletteLayout.SetColumnSpan(_autoPaletteVariantInput, 3);
        paletteLayout.Controls.Add(_palettePanel, 0, 3);
        paletteLayout.SetColumnSpan(_palettePanel, 3);
        paletteGroup.Controls.Add(paletteLayout);

        var propertiesGroup = new GroupBox
        {
            Text = "Wlasciwosci klatki",
            Dock = DockStyle.Fill
        };

        var propertiesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(12)
        };
        propertiesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        propertiesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _gameFormatInput.DropDownStyle = ComboBoxStyle.DropDownList;
        _gameFormatInput.Items.AddRange(new object[]
        {
            BmdGameFormat.Cultures1,
            BmdGameFormat.Cultures2
        });
        _gameFormatInput.SelectedIndexChanged += (_, _) => UpdateGameFormat();

        _frameTypeInput.DropDownStyle = ComboBoxStyle.DropDownList;
        _frameTypeInput.SelectedIndexChanged += (_, _) => UpdateSelectedFrameProperties();

        ConfigureNumeric(_offsetXInput);
        ConfigureNumeric(_offsetYInput);
        _offsetXInput.ValueChanged += (_, _) => UpdateSelectedFrameProperties();
        _offsetYInput.ValueChanged += (_, _) => UpdateSelectedFrameProperties();

        propertiesLayout.Controls.Add(NewLabel("Gra"), 0, 0);
        propertiesLayout.Controls.Add(_gameFormatInput, 1, 0);
        propertiesLayout.Controls.Add(NewLabel("Typ"), 0, 1);
        propertiesLayout.Controls.Add(_frameTypeInput, 1, 1);
        propertiesLayout.Controls.Add(NewLabel("Anchor X"), 0, 2);
        propertiesLayout.Controls.Add(_offsetXInput, 1, 2);
        propertiesLayout.Controls.Add(NewLabel("Anchor Y"), 0, 3);
        propertiesLayout.Controls.Add(_offsetYInput, 1, 3);
        propertiesLayout.Controls.Add(NewLabel("Szerokosc"), 0, 5);
        propertiesLayout.Controls.Add(_widthValueLabel, 1, 5);
        propertiesLayout.Controls.Add(NewLabel("Wysokosc"), 0, 6);
        propertiesLayout.Controls.Add(_heightValueLabel, 1, 6);

        _widthValueLabel.Text = "-";
        _heightValueLabel.Text = "-";
        _widthValueLabel.Dock = DockStyle.Fill;
        _heightValueLabel.Dock = DockStyle.Fill;
        _widthValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        _heightValueLabel.TextAlign = ContentAlignment.MiddleLeft;

        propertiesGroup.Controls.Add(propertiesLayout);

        panel.Controls.Add(paletteGroup, 0, 0);
        panel.Controls.Add(propertiesGroup, 0, 1);
        return panel;
    }

    private Control BuildPreviewPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(176, 176, 176),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(4)
        };

        _previewBox.Dock = DockStyle.Fill;
        _previewBox.SizeMode = PictureBoxSizeMode.CenterImage;
        _previewBox.BackColor = Color.FromArgb(176, 176, 176);
        _previewBox.MouseClick += (_, e) => SetAnchorFromPreviewClick(e);
        panel.Controls.Add(_previewBox);
        return panel;
    }

    private static Label NewLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Panel TwoButtonRow(Button left, Button right, string leftText, string rightText)
    {
        left.Text = leftText;
        right.Text = rightText;
        left.Dock = DockStyle.Fill;
        right.Dock = DockStyle.Fill;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 4, 0, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.Controls.Add(left, 0, 0);
        panel.Controls.Add(right, 1, 0);
        return new Panel { Dock = DockStyle.Fill, Controls = { panel } };
    }

    private static void ConfigureNumeric(NumericUpDown input)
    {
        input.Minimum = -4096;
        input.Maximum = 4096;
        input.Dock = DockStyle.Fill;
    }

    private void BuildAnimationPanel()
    {
        _animationPanel.Dock = DockStyle.Fill;
        _animationPanel.Visible = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));

        _animationToggleButton.Text = "Play";
        _animationToggleButton.Dock = DockStyle.Fill;
        _animationToggleButton.Click += (_, _) => ToggleAnimationPlayback();

        var fpsLabel = new Label
        {
            Text = "FPS",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _animationFpsInput.Minimum = 1;
        _animationFpsInput.Maximum = 60;
        _animationFpsInput.Value = 12;
        _animationFpsInput.Dock = DockStyle.Fill;
        _animationFpsInput.ValueChanged += (_, _) => UpdateAnimationTimerInterval();

        layout.Controls.Add(_animationToggleButton, 0, 0);
        layout.Controls.Add(fpsLabel, 1, 0);
        layout.Controls.Add(_animationFpsInput, 2, 0);
        _animationPanel.Controls.Add(layout);
    }
}
