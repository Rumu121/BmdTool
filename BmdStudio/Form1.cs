using System.Drawing;
using BMDEditor.Bmd;

namespace BMDEditor;

public partial class Form1 : Form
{
    private readonly Random _random = new();
    private BmdDocument _document = new();
    private string? _gameRootPath;
    private Color[] _palette = PaletteCodec.CreateDefault();
    private bool _updatingEditor;
    private bool _updatingAutoPaletteVariant;
    private List<PaletteCandidate> _autoPaletteVariants = new();
    private readonly PictureBox _paletteGridBox = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new();
    private bool _isAnimationPlaying;
    private int _animationFrameCursor;
    private const int PaletteColumns = 16;
    private const int PaletteRows = 16;
    private const int PaletteCellSize = 23;
    private const int PaletteCellGap = 2;

    public Form1()
    {
        InitializeComponent();
        _animationTimer.Tick += AnimationTimerOnTick;
        UpdateAnimationTimerInterval();
        BuildPaletteButtons();
        CreateNewDocument();
    }

    private int SelectedFrameIndex => _framesList.SelectedIndices.Count > 0 ? _framesList.SelectedIndices[0] : -1;
    private int SelectedFrameCount => _framesList.SelectedIndices.Count;
    private bool HasSingleFrameSelection => SelectedFrameCount == 1;
    private BmdFrame? SelectedFrame => SelectedFrameIndex >= 0 && SelectedFrameIndex < _document.Frames.Count
        ? _document.Frames[SelectedFrameIndex]
        : null;

    private void CreateNewDocument()
    {
        CreateNewDocument(BmdGameFormat.Cultures2);
    }

    private void CreateNewDocument(BmdGameFormat format)
    {
        StopAnimation(restorePreview: false);
        _document = new BmdDocument
        {
            GameFormat = format
        };
        _document.Frames.Add(new BmdFrame
        {
            Type = GetDefaultFrameType(_document.GameFormat),
            Pixels = new[] { new[] { new IndexedPixel() } }
        });
        _document.RenumberFrames();
        SyncFormatControls();
        RefreshFrameList(0);
        SetStatus("Utworzono nowy dokument.");
    }

    private void OpenBmd()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Otworz plik BMD",
            Filter = "BMD (*.bmd)|*.bmd"
        };
        if (!string.IsNullOrWhiteSpace(_gameRootPath))
        {
            var defaultBobsDir = Path.Combine(_gameRootPath, "Data", "engine2d", "bin", "bobs");
            dialog.InitialDirectory = Directory.Exists(defaultBobsDir) ? defaultBobsDir : _gameRootPath;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            StopAnimation(restorePreview: false);
            _document = BmdCodec.Load(dialog.FileName);
            SyncFormatControls();
            RefreshFrameList(_document.Frames.Count > 0 ? 0 : -1);
            var status = $"Wczytano {Path.GetFileName(dialog.FileName)}.";
            if (TryAutoLoadPaletteForBmd(dialog.FileName, out var autoPaletteMessage))
            {
                status += $" {autoPaletteMessage}";
            }

            SetStatus(status);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void SaveDocument()
    {
        if (string.IsNullOrWhiteSpace(_document.SourcePath))
        {
            SaveDocumentAs();
            return;
        }

        SaveToPath(_document.SourcePath!);
    }

    private void SaveDocumentAs()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz plik BMD",
            Filter = "BMD (*.bmd)|*.bmd"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SaveToPath(dialog.FileName);
    }

    private void SaveToPath(string path)
    {
        try
        {
            BmdCodec.Save(_document, path);
            _document.SourcePath = path;
            SetStatus($"Zapisano {Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void BrowsePalettePath()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz plik palety PCX",
            Filter = "PCX (*.pcx)|*.pcx"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _palettePathTextBox.Text = dialog.FileName;
    }

    private void LoadPalette()
    {
        if (string.IsNullOrWhiteSpace(_palettePathTextBox.Text) || !File.Exists(_palettePathTextBox.Text))
        {
            BrowsePalettePath();
        }

        if (string.IsNullOrWhiteSpace(_palettePathTextBox.Text) || !File.Exists(_palettePathTextBox.Text))
        {
            return;
        }

        try
        {
            _palette = PaletteCodec.LoadFromPcx(_palettePathTextBox.Text);
            ClearAutoPaletteVariants();
            SyncPaletteButtons();
            RefreshPreview();
            SetStatus($"Wczytano palete 256 kolorow z {Path.GetFileName(_palettePathTextBox.Text)}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void RandomizePalette()
    {
        _palette = PaletteCodec.CreateRandom(_random);
        ClearAutoPaletteVariants();
        SyncPaletteButtons();
        RefreshPreview();
        SetStatus("Wylosowano palete 256 kolorow.");
    }

    private void LoadSelectedFrameIntoEditor()
    {
        var frame = HasSingleFrameSelection ? SelectedFrame : null;
        _updatingEditor = true;
        try
        {
            _gameFormatInput.SelectedItem = _document.GameFormat;
            RefreshFrameTypeChoices();

            if (frame is null)
            {
                _frameTypeInput.SelectedItem = GetDefaultFrameType(_document.GameFormat);
                _offsetXInput.Value = 0;
                _offsetYInput.Value = 0;
                _widthValueLabel.Text = "-";
                _heightValueLabel.Text = "-";
                _previewBox.Image?.Dispose();
                _previewBox.Image = null;
            }
            else
            {
                _frameTypeInput.SelectedItem = frame.Type == BmdFrameType.Empty ? GetDefaultFrameType(_document.GameFormat) : frame.Type;
                _offsetXInput.Value = frame.OffsetX;
                _offsetYInput.Value = frame.OffsetY;
                _widthValueLabel.Text = frame.Width.ToString();
                _heightValueLabel.Text = frame.Height.ToString();
            }
        }
        finally
        {
            _updatingEditor = false;
        }

        RefreshSelectionStatus();
        RefreshPreview();
    }

    private void UpdateSelectedFrameProperties()
    {
        if (_updatingEditor || !HasSingleFrameSelection)
        {
            return;
        }

        var frame = SelectedFrame;
        if (frame is null)
        {
            return;
        }

        if (_frameTypeInput.SelectedItem is BmdFrameType selectedType)
        {
            frame.Type = selectedType;
        }
        frame.OffsetX = decimal.ToInt32(_offsetXInput.Value);
        frame.OffsetY = decimal.ToInt32(_offsetYInput.Value);

        RefreshFrameList(_framesList.SelectedIndex);
        RefreshPreview();
    }

    private void UpdateGameFormat()
    {
        if (_updatingEditor || _gameFormatInput.SelectedItem is not BmdGameFormat selectedFormat)
        {
            return;
        }

        _document.GameFormat = selectedFormat;
        RefreshFrameTypeChoices();

        var frame = HasSingleFrameSelection ? SelectedFrame : null;
        if (frame is not null && !GetAllowedTypes(selectedFormat).Contains(frame.Type))
        {
            frame.Type = GetDefaultFrameType(selectedFormat);
        }

        RefreshFrameList(Math.Max(_framesList.SelectedIndex, 0));
        SetStatus($"Ustawiono format {selectedFormat}.");
    }

    private void RefreshFrameList(int selectIndex)
    {
        _document.RenumberFrames();
        _framesList.BeginUpdate();
        _framesList.Items.Clear();
        foreach (var frame in _document.Frames)
        {
            _framesList.Items.Add(frame);
        }

        _framesList.EndUpdate();

        if (_framesList.Items.Count == 0)
        {
            _previewBox.Image?.Dispose();
            _previewBox.Image = null;
            RefreshSelectionStatus();
            return;
        }

        _framesList.SelectedIndex = Math.Clamp(selectIndex, 0, _framesList.Items.Count - 1);
        RefreshSelectionStatus();
    }

    private void RefreshPreview()
    {
        if (_isAnimationPlaying)
        {
            return;
        }

        var frame = SelectedFrameCount > 0 ? SelectedFrame : null;
        _previewBox.Image?.Dispose();
        if (frame is null)
        {
            _previewBox.Image = null;
            return;
        }

        _previewBox.Image = frame.ToBitmap(_palette, _useAlphaCheck.Checked, _showAnchorCheck.Checked);
    }

    private void AddFrameFromPng()
    {
        using var dialog = CreatePngDialog("Import PNG");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using var image = new Bitmap(dialog.FileName);
            using var copy = new Bitmap(image);
            var frame = new BmdFrame
            {
                Type = GetDefaultFrameType(_document.GameFormat),
                OffsetX = 0,
                OffsetY = 0
            };
            frame.LoadFromBitmap(copy, _palette);
            _document.Frames.Add(frame);
            RefreshFrameList(_document.Frames.Count - 1);
            SetStatus($"Dodano klatke z {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ReplaceFrameFromPng()
    {
        var frame = HasSingleFrameSelection ? SelectedFrame : null;
        if (frame is null)
        {
            return;
        }

        using var dialog = CreatePngDialog("Podmien zaznaczona klatke");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using var image = new Bitmap(dialog.FileName);
            using var copy = new Bitmap(image);
            frame.LoadFromBitmap(copy, _palette);
            RefreshFrameList(_framesList.SelectedIndex);
            SetStatus($"Podmieniono klatke {frame.Number}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ExportCurrentFrame()
    {
        var frame = HasSingleFrameSelection ? SelectedFrame : null;
        if (frame is null || frame.IsEmpty)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Eksport PNG",
            Filter = "PNG (*.png)|*.png",
            FileName = $"{frame.Number:D4}.png"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using var bitmap = frame.ToBitmap(_palette, _useAlphaCheck.Checked);
            bitmap.Save(dialog.FileName);
            SetStatus($"Wyeksportowano klatke {frame.Number}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ExportWorkspace()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Wybierz katalog eksportu"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            BmdCodec.ExportWorkspace(_document, dialog.SelectedPath, _palette, _useAlphaCheck.Checked);
            SetStatus("Wykonano masowy eksport.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ImportWorkspace()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Wybierz katalog z metadata.csv i PNG"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _document = BmdCodec.ImportWorkspace(dialog.SelectedPath, _palette, _document.GameFormat);
            SyncFormatControls();
            RefreshFrameList(_document.Frames.Count > 0 ? 0 : -1);
            SetStatus("Wykonano masowy import.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void DeleteSelectedFrame()
    {
        var selected = _framesList.SelectedIndices.Cast<int>().OrderByDescending(x => x).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        var nextIndex = selected.Min();
        foreach (var index in selected)
        {
            _document.Frames.RemoveAt(index);
        }

        if (_document.Frames.Count == 0)
        {
            CreateNewDocument();
            return;
        }

        RefreshFrameList(Math.Min(nextIndex, _document.Frames.Count - 1));
        SetStatus(selected.Length == 1 ? "Usunieto zaznaczona klatke." : $"Usunieto {selected.Length} zaznaczone klatki.");
    }

    private void DeleteAllFrames()
    {
        CreateNewDocument(_document.GameFormat);
        SetStatus("Usunieto wszystkie klatki.");
    }

    private void MoveSelectedFramesUp()
    {
        var selected = GetSelectedFrameIndices();
        if (selected.Length == 0 || selected[0] <= 0)
        {
            return;
        }

        foreach (var index in selected)
        {
            SwapFrames(index, index - 1);
        }

        var newSelection = selected.Select(x => x - 1).ToArray();
        RefreshFrameListWithSelection(newSelection);
        SetStatus("Przesunieto zaznaczone klatki w gore.");
    }

    private void MoveSelectedFramesDown()
    {
        var selected = GetSelectedFrameIndices();
        if (selected.Length == 0 || selected[^1] >= _document.Frames.Count - 1)
        {
            return;
        }

        for (var i = selected.Length - 1; i >= 0; i--)
        {
            var index = selected[i];
            SwapFrames(index, index + 1);
        }

        var newSelection = selected.Select(x => x + 1).ToArray();
        RefreshFrameListWithSelection(newSelection);
        SetStatus("Przesunieto zaznaczone klatki w dol.");
    }

    private void SetAnchorFromPreviewClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || SelectedFrameCount == 0 || _previewBox.Image is null || _isAnimationPlaying)
        {
            return;
        }

        var referenceFrame = SelectedFrame;
        if (referenceFrame is null || referenceFrame.IsEmpty)
        {
            return;
        }

        var image = _previewBox.Image;
        var imageLeft = (_previewBox.ClientSize.Width - image.Width) / 2;
        var imageTop = (_previewBox.ClientSize.Height - image.Height) / 2;
        var localX = e.X - imageLeft;
        var localY = e.Y - imageTop;

        if (localX < 0 || localY < 0 || localX >= image.Width || localY >= image.Height)
        {
            return;
        }

        var newOffsetX = -localX;
        var newOffsetY = -localY;
        var selectedIndices = GetSelectedFrameIndices();
        foreach (var selectedIndex in selectedIndices)
        {
            if (selectedIndex < 0 || selectedIndex >= _document.Frames.Count)
            {
                continue;
            }

            _document.Frames[selectedIndex].OffsetX = newOffsetX;
            _document.Frames[selectedIndex].OffsetY = newOffsetY;
        }

        if (HasSingleFrameSelection)
        {
            _updatingEditor = true;
            try
            {
                _offsetXInput.Value = newOffsetX;
                _offsetYInput.Value = newOffsetY;
            }
            finally
            {
                _updatingEditor = false;
            }
        }

        if (selectedIndices.Length > 1)
        {
            RefreshFrameListWithSelection(selectedIndices);
            SetStatus($"Ustawiono anchor masowo ({selectedIndices.Length} klatek): X={newOffsetX}, Y={newOffsetY}.");
            return;
        }

        RefreshFrameList(SelectedFrameIndex);
        SetStatus($"Ustawiono anchor: X={newOffsetX}, Y={newOffsetY}.");
    }

    private void SwapFrames(int firstIndex, int secondIndex)
    {
        (_document.Frames[firstIndex], _document.Frames[secondIndex]) = (_document.Frames[secondIndex], _document.Frames[firstIndex]);
    }

    private void BatchChangeAnchors()
    {
        if (_document.Frames.Count == 0)
        {
            return;
        }

        if (!TryGetBatchAnchorChange(out var mode, out var xValue, out var yValue))
        {
            return;
        }

        foreach (var frame in _document.Frames)
        {
            if (mode == BatchAnchorMode.Set)
            {
                frame.OffsetX = xValue;
                frame.OffsetY = yValue;
                continue;
            }

            frame.OffsetX += xValue;
            frame.OffsetY += yValue;
        }

        var selectedIndex = _framesList.SelectedIndex;
        RefreshFrameList(selectedIndex >= 0 ? selectedIndex : 0);
        SetStatus($"Zmieniono anchory masowo ({(mode == BatchAnchorMode.Set ? "Ustaw" : "Dodaj")} X={xValue}, Y={yValue}).");
    }

    private bool TryGetBatchAnchorChange(out BatchAnchorMode mode, out int xValue, out int yValue)
    {
        mode = BatchAnchorMode.Add;
        xValue = 0;
        yValue = 0;

        using var dialog = new Form
        {
            Text = "Masowa zmiana anchor",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(320, 170)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var modeBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        modeBox.Items.Add("Dodaj");
        modeBox.Items.Add("Ustaw");
        modeBox.SelectedIndex = 0;

        var xInput = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = -4096,
            Maximum = 4096
        };

        var yInput = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = -4096,
            Maximum = 4096
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Width = 90
        };
        var cancelButton = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Width = 90
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        layout.Controls.Add(new Label { Text = "Tryb", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(modeBox, 1, 0);
        layout.Controls.Add(new Label { Text = "Anchor X", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        layout.Controls.Add(xInput, 1, 1);
        layout.Controls.Add(new Label { Text = "Anchor Y", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        layout.Controls.Add(yInput, 1, 2);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);

        dialog.Controls.Add(layout);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        mode = modeBox.SelectedIndex == 1 ? BatchAnchorMode.Set : BatchAnchorMode.Add;
        xValue = decimal.ToInt32(xInput.Value);
        yValue = decimal.ToInt32(yInput.Value);
        return true;
    }

    private void BuildPaletteButtons()
    {
        _palettePanel.SuspendLayout();
        _palettePanel.Controls.Clear();

        _paletteGridBox.SizeMode = PictureBoxSizeMode.Normal;
        _paletteGridBox.Width = (PaletteColumns * PaletteCellSize) + ((PaletteColumns - 1) * PaletteCellGap) + 2;
        _paletteGridBox.Height = (PaletteRows * PaletteCellSize) + ((PaletteRows - 1) * PaletteCellGap) + 2;
        _paletteGridBox.Margin = new Padding(2);
        _paletteGridBox.BackColor = Color.White;
        _paletteGridBox.BorderStyle = BorderStyle.FixedSingle;
        _paletteGridBox.MouseClick -= PaletteGridBoxOnMouseClick;
        _paletteGridBox.MouseClick += PaletteGridBoxOnMouseClick;

        _palettePanel.Controls.Add(_paletteGridBox);
        _palettePanel.ResumeLayout();
        RedrawPaletteGrid();
    }

    private void SyncPaletteButtons()
    {
        RedrawPaletteGrid();
    }

    private void RedrawPaletteGrid()
    {
        var width = _paletteGridBox.Width;
        var height = _paletteGridBox.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);

        for (var i = 0; i < Math.Min(PaletteCodec.PaletteSize, _palette.Length); i++)
        {
            var row = i / PaletteColumns;
            var column = i % PaletteColumns;
            var x = 1 + column * (PaletteCellSize + PaletteCellGap);
            var y = 1 + row * (PaletteCellSize + PaletteCellGap);

            using var fillBrush = new SolidBrush(_palette[i]);
            graphics.FillRectangle(fillBrush, x, y, PaletteCellSize, PaletteCellSize);
            graphics.DrawRectangle(Pens.DimGray, x, y, PaletteCellSize, PaletteCellSize);
        }

        var old = _paletteGridBox.Image;
        _paletteGridBox.Image = bitmap;
        old?.Dispose();
    }

    private void PaletteGridBoxOnMouseClick(object? sender, MouseEventArgs e)
    {
        var slotWidth = PaletteCellSize + PaletteCellGap;
        var slotHeight = PaletteCellSize + PaletteCellGap;

        var col = (e.X - 1) / slotWidth;
        var row = (e.Y - 1) / slotHeight;
        if (col < 0 || col >= PaletteColumns || row < 0 || row >= PaletteRows)
        {
            return;
        }

        var inCellX = (e.X - 1) % slotWidth;
        var inCellY = (e.Y - 1) % slotHeight;
        if (inCellX < 0 || inCellX >= PaletteCellSize || inCellY < 0 || inCellY >= PaletteCellSize)
        {
            return;
        }

        var index = row * PaletteColumns + col;
        if (index < 0 || index >= _palette.Length)
        {
            return;
        }

        using var colorDialog = new ColorDialog { Color = _palette[index] };
        if (colorDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _palette[index] = colorDialog.Color;
        RedrawPaletteGrid();
        RefreshPreview();
        SetStatus($"Zmieniono kolor palety #{index}.");
    }

    private void RefreshSelectionStatus()
    {
        _selectedCountLabel.Text = $"Zaznaczono: {SelectedFrameCount}";

        _replaceFrameButton.Enabled = HasSingleFrameSelection;
        _exportFrameButton.Enabled = HasSingleFrameSelection && SelectedFrame is { IsEmpty: false };
        _deleteFrameButton.Enabled = SelectedFrameCount > 0;

        var selected = GetSelectedFrameIndices();
        _moveUpButton.Enabled = selected.Length > 0 && selected[0] > 0;
        _moveDownButton.Enabled = selected.Length > 0 && selected[^1] < _document.Frames.Count - 1;

        RefreshAnimationControls();
    }

    private void SyncFormatControls()
    {
        _updatingEditor = true;
        try
        {
            _gameFormatInput.SelectedItem = _document.GameFormat;
            RefreshFrameTypeChoices();
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void RefreshFrameTypeChoices()
    {
        var selected = _frameTypeInput.SelectedItem as BmdFrameType?;
        _frameTypeInput.Items.Clear();
        foreach (var type in GetAllowedTypes(_document.GameFormat))
        {
            _frameTypeInput.Items.Add(type);
        }

        if (selected.HasValue && _frameTypeInput.Items.Contains(selected.Value))
        {
            _frameTypeInput.SelectedItem = selected.Value;
        }
        else if (_frameTypeInput.Items.Count > 0)
        {
            _frameTypeInput.SelectedItem = GetDefaultFrameType(_document.GameFormat);
        }
    }

    private static IReadOnlyList<BmdFrameType> GetAllowedTypes(BmdGameFormat format)
    {
        return format switch
        {
            BmdGameFormat.Cultures1 => new[] { BmdFrameType.Normal, BmdFrameType.Shadow, BmdFrameType.Build },
            _ => new[] { BmdFrameType.Normal, BmdFrameType.Shadow, BmdFrameType.Extended }
        };
    }

    private static BmdFrameType GetDefaultFrameType(BmdGameFormat format)
    {
        return format == BmdGameFormat.Cultures1 ? BmdFrameType.Normal : BmdFrameType.Extended;
    }

    private static OpenFileDialog CreatePngDialog(string title)
    {
        return new OpenFileDialog
        {
            Title = title,
            Filter = "PNG (*.png)|*.png"
        };
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
    }

    private void SelectGameFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Wybierz folder glownego katalogu gry/moda",
            SelectedPath = !string.IsNullOrWhiteSpace(_gameRootPath) && Directory.Exists(_gameRootPath)
                ? _gameRootPath
                : string.Empty
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _gameRootPath = dialog.SelectedPath;
        SetStatus($"Ustawiono folder gry: {_gameRootPath}");
    }

    private bool TryAutoLoadPaletteForBmd(string bmdPath, out string message)
    {
        message = string.Empty;
        try
        {
            var candidates = PaletteAutoResolver.ResolvePaletteCandidates(bmdPath, _gameRootPath);
            if (candidates.Count == 0)
            {
                ClearAutoPaletteVariants();
                return false;
            }

            if (candidates.Count == 1)
            {
                ClearAutoPaletteVariants();
                LoadPaletteCandidate(candidates[0]);
                message = $"Auto-paleta: {candidates[0].Name}.";
                return true;
            }

            SetAutoPaletteVariants(candidates);
            if (_autoPaletteVariantInput.SelectedIndex < 0)
            {
                return false;
            }

            ApplySelectedAutoPaletteVariant();
            var selected = _autoPaletteVariants[_autoPaletteVariantInput.SelectedIndex];
            message = candidates.Count > 1
                ? $"Auto-paleta: {selected.Name} ({candidates.Count} warianty)."
                : $"Auto-paleta: {selected.Name}.";
            return true;
        }
        catch
        {
            ClearAutoPaletteVariants();
            return false;
        }
    }

    private void SetAutoPaletteVariants(IReadOnlyList<PaletteCandidate> candidates)
    {
        _autoPaletteVariants = candidates.ToList();
        _updatingAutoPaletteVariant = true;
        try
        {
            _autoPaletteVariantInput.Items.Clear();
            foreach (var candidate in _autoPaletteVariants)
            {
                _autoPaletteVariantInput.Items.Add($"Auto: {candidate.Name}");
            }

            _autoPaletteVariantInput.Enabled = _autoPaletteVariants.Count > 1;
            SetAutoPaletteVariantVisibility(_autoPaletteVariants.Count > 1);
            _autoPaletteVariantInput.SelectedIndex = _autoPaletteVariants.Count > 0 ? 0 : -1;
        }
        finally
        {
            _updatingAutoPaletteVariant = false;
        }
    }

    private void ClearAutoPaletteVariants()
    {
        _autoPaletteVariants.Clear();
        _updatingAutoPaletteVariant = true;
        try
        {
            _autoPaletteVariantInput.Items.Clear();
            _autoPaletteVariantInput.SelectedIndex = -1;
            _autoPaletteVariantInput.Enabled = false;
            SetAutoPaletteVariantVisibility(false);
        }
        finally
        {
            _updatingAutoPaletteVariant = false;
        }
    }

    private void ApplySelectedAutoPaletteVariant()
    {
        if (_updatingAutoPaletteVariant)
        {
            return;
        }

        var index = _autoPaletteVariantInput.SelectedIndex;
        if (index < 0 || index >= _autoPaletteVariants.Count)
        {
            return;
        }

        try
        {
            var selected = _autoPaletteVariants[index];
            LoadPaletteCandidate(selected);
            SetStatus($"Wybrano wariant palety: {selected.Name}.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void LoadPaletteCandidate(PaletteCandidate candidate)
    {
        _palette = PaletteCodec.LoadFromPcx(candidate.PcxPath);
        _palettePathTextBox.Text = candidate.PcxPath;
        SyncPaletteButtons();
        RefreshPreview();
    }

    private void SetAutoPaletteVariantVisibility(bool isVisible)
    {
        _autoPaletteVariantInput.Visible = isVisible;
        if (_autoPaletteVariantInput.Parent is not TableLayoutPanel layout)
        {
            return;
        }

        var row = layout.GetRow(_autoPaletteVariantInput);
        if (row < 0 || row >= layout.RowStyles.Count)
        {
            return;
        }

        layout.RowStyles[row].SizeType = SizeType.Absolute;
        layout.RowStyles[row].Height = isVisible ? 30 : 0;
    }

    private void ShowError(Exception ex)
    {
        MessageBox.Show(this, ex.Message, "BMD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        SetStatus("Operacja zakonczona bledem.");
    }

    private void ToggleAnimationPlayback()
    {
        if (_isAnimationPlaying)
        {
            StopAnimation(restorePreview: false);
            return;
        }

        StartAnimation();
    }

    private void StartAnimation()
    {
        if (!CanShowAnimationControls())
        {
            return;
        }

        _animationFrameCursor = 0;
        _isAnimationPlaying = true;
        _animationToggleButton.Text = "Stop";
        UpdateAnimationTimerInterval();
        _animationTimer.Start();
        RenderNextAnimationFrame();
    }

    private void StopAnimation(bool restorePreview)
    {
        _isAnimationPlaying = false;
        _animationTimer.Stop();
        _animationToggleButton.Text = "Play";
        _animationFrameCursor = 0;
        if (restorePreview)
        {
            RefreshPreview();
        }
    }

    private void UpdateAnimationTimerInterval()
    {
        var fps = Math.Max(1, decimal.ToInt32(_animationFpsInput.Value));
        _animationTimer.Interval = Math.Max(1, 1000 / fps);
    }

    private void AnimationTimerOnTick(object? sender, EventArgs e)
    {
        RenderNextAnimationFrame();
    }

    private void RenderNextAnimationFrame()
    {
        if (!_isAnimationPlaying)
        {
            return;
        }

        var selectedIndices = GetSelectedFrameIndices();
        if (selectedIndices.Length < 2 || !CanAnimateCurrentDocument())
        {
            StopAnimation(restorePreview: true);
            return;
        }

        if (_animationFrameCursor >= selectedIndices.Length)
        {
            _animationFrameCursor = 0;
        }

        var frameIndex = selectedIndices[_animationFrameCursor++];
        if (frameIndex < 0 || frameIndex >= _document.Frames.Count)
        {
            return;
        }

        var frame = _document.Frames[frameIndex];
        var oldImage = _previewBox.Image;
        _previewBox.Image = CreateAnchoredAnimationBitmap(frame, selectedIndices);
        oldImage?.Dispose();
    }

    private Bitmap CreateAnchoredAnimationBitmap(BmdFrame frame, IReadOnlyList<int> selectedIndices)
    {
        var minLeft = int.MaxValue;
        var minTop = int.MaxValue;
        var maxRight = int.MinValue;
        var maxBottom = int.MinValue;

        foreach (var index in selectedIndices)
        {
            if (index < 0 || index >= _document.Frames.Count)
            {
                continue;
            }

            var selected = _document.Frames[index];
            if (selected.IsEmpty)
            {
                continue;
            }

            minLeft = Math.Min(minLeft, selected.OffsetX);
            minTop = Math.Min(minTop, selected.OffsetY);
            maxRight = Math.Max(maxRight, selected.OffsetX + selected.Width);
            maxBottom = Math.Max(maxBottom, selected.OffsetY + selected.Height);
        }

        if (minLeft == int.MaxValue || minTop == int.MaxValue || maxRight <= minLeft || maxBottom <= minTop)
        {
            return frame.ToBitmap(_palette, _useAlphaCheck.Checked, _showAnchorCheck.Checked);
        }

        var canvasWidth = Math.Max(1, maxRight - minLeft);
        var canvasHeight = Math.Max(1, maxBottom - minTop);
        var canvas = new Bitmap(canvasWidth, canvasHeight);

        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.Transparent);

        using var frameBitmap = frame.ToBitmap(_palette, _useAlphaCheck.Checked, _showAnchorCheck.Checked);
        var drawX = frame.OffsetX - minLeft;
        var drawY = frame.OffsetY - minTop;
        graphics.DrawImageUnscaled(frameBitmap, drawX, drawY);

        return canvas;
    }

    private void RefreshAnimationControls()
    {
        var show = CanShowAnimationControls();
        _animationPanel.Visible = show;
        _animationToggleButton.Enabled = show;
        _animationFpsInput.Enabled = show;

        if (_animationPanel.Parent is TableLayoutPanel layout)
        {
            var row = layout.GetRow(_animationPanel);
            if (row >= 0 && row < layout.RowStyles.Count)
            {
                layout.RowStyles[row].SizeType = SizeType.Absolute;
                layout.RowStyles[row].Height = show ? 42 : 0;
            }
        }

        if (!show && _isAnimationPlaying)
        {
            StopAnimation(restorePreview: true);
        }
    }

    private bool CanShowAnimationControls()
    {
        return SelectedFrameCount > 1 && CanAnimateCurrentDocument();
    }

    private bool CanAnimateCurrentDocument()
    {
        return true;
    }

    private int[] GetSelectedFrameIndices()
    {
        return _framesList.SelectedIndices.Cast<int>().OrderBy(x => x).ToArray();
    }

    private void RefreshFrameListWithSelection(IReadOnlyList<int> selectedIndices)
    {
        _document.RenumberFrames();
        _framesList.BeginUpdate();
        _framesList.Items.Clear();
        foreach (var frame in _document.Frames)
        {
            _framesList.Items.Add(frame);
        }

        _framesList.ClearSelected();
        foreach (var selectedIndex in selectedIndices)
        {
            if (selectedIndex >= 0 && selectedIndex < _framesList.Items.Count)
            {
                _framesList.SetSelected(selectedIndex, true);
            }
        }

        _framesList.EndUpdate();
        RefreshSelectionStatus();
        RefreshPreview();
    }

    private enum BatchAnchorMode
    {
        Add,
        Set
    }
}
