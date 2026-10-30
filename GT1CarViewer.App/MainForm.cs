using GT1CarViewer.Core;

namespace GT1CarViewer.App;

public sealed class MainForm : Form
{
    private static readonly Color CanvasColor = Color.FromArgb(13, 19, 27);
    private static readonly Color PanelColor = Color.FromArgb(22, 30, 40);
    private static readonly Color RaisedColor = Color.FromArgb(30, 40, 52);
    private static readonly Color MutedText = Color.FromArgb(151, 166, 182);
    private static readonly Color AccentColor = Color.FromArgb(255, 190, 70);

    private readonly string? _initialPath;
    private readonly CarViewport _viewport = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _archivePicker = new();
    private readonly TextBox _searchBox = new();
    private readonly ListBox _carList = new();
    private readonly ComboBox _colorPicker = new();
    private readonly ComboBox _languagePicker = new();
    private readonly Label _carTitle = new();
    private readonly Label _carMetadata = new();
    private readonly Label _sourceLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _garageCount = new();
    private readonly Label _garageTitle = new();
    private readonly Label _modelsLabel = new();
    private readonly Label _languageLabel = new();
    private readonly Label _uvOffsetLabel = new();
    private readonly CheckBox _textureToggle = new();
    private readonly CheckBox _edgesToggle = new();
    private readonly CheckBox _spinToggle = new();
    private readonly NumericUpDown _verticalUvOffset = new();
    private readonly Button _openButton = new();
    private readonly Button _exportButton = new();
    private CarDataSource? _dataSource;
    private CarArchive? _currentArchive;
    private CarModel? _currentModel;
    private CarTexture? _currentTexture;
    private int _selectionVersion;
    private bool _syncing;

    public MainForm(string? initialPath = null)
    {
        _initialPath = initialPath;
        Text = AppText.Get("WindowTitle");
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MinimumSize = new Size(920, 600);
        Size = new Size(1320, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = CanvasColor;
        ForeColor = Color.FromArgb(232, 238, 245);
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;
        AllowDrop = true;
        BuildLayout();

        Shown += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_initialPath))
            {
                await LoadSourceAsync(_initialPath);
            }
        };
        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
        };
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
            {
                await LoadSourceAsync(paths[0]);
            }
        };
        KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                e.SuppressKeyPress = true;
                await OpenSourceDialogAsync();
            }
            else if (e.Control && e.KeyCode == Keys.E)
            {
                e.SuppressKeyPress = true;
                await ExportCurrentCarAsync();
            }
        };
    }

    private void ClearCurrentCar()
    {
        _selectionVersion++;
        _currentModel = null;
        _currentTexture = null;
        _viewport.SetCar(null, null);
        _colorPicker.Items.Clear();
        _carTitle.Text = AppText.Get(_currentArchive is null ? "ChooseCar" : "NoResults");
        _carMetadata.Text = string.Empty;
        _exportButton.Enabled = false;
        _statusLabel.Text = AppText.Get("Ready");
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = CanvasColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        root.Controls.Add(BuildHeader(), 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1280, 740),
            SplitterWidth = 1,
            SplitterDistance = 346,
            Panel1MinSize = 306,
            Panel2MinSize = 450,
            BackColor = Color.FromArgb(46, 58, 72),
            FixedPanel = FixedPanel.Panel1
        };
        root.Controls.Add(split, 0, 1);
        split.Panel1.Controls.Add(BuildSidebar());
        split.Panel2.Controls.Add(BuildViewportPanel());
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 24, 33),
            Padding = new Padding(22, 0, 22, 0)
        };
        var brand = new TableLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 430,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        var title = StyledLabel("GRAN TURISMO", 16, Color.FromArgb(244, 247, 250), bold: true);
        var subtitle = StyledLabel("ATELIER  •  PLAYSTATION  •  1997", 8.5f, MutedText);
        title.Dock = DockStyle.Fill;
        subtitle.Dock = DockStyle.Fill;
        brand.Controls.Add(title, 0, 0);
        brand.Controls.Add(subtitle, 0, 1);
        title.Margin = new Padding(0, 0, 0, 2);
        subtitle.Margin = new Padding(1, 0, 0, 0);
        header.Controls.Add(brand);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 370,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 17, 0, 0),
            BackColor = Color.Transparent
        };
        _openButton.Text = AppText.Get("OpenImage");
        StyleButton(_openButton, emphasized: false);
        _openButton.Width = 148;
        _openButton.Click += async (_, _) => await OpenSourceDialogAsync();
        _exportButton.Text = AppText.Get("ExportObj");
        StyleButton(_exportButton, emphasized: true);
        _exportButton.Width = 138;
        _exportButton.Enabled = false;
        _exportButton.Click += async (_, _) => await ExportCurrentCarAsync();
        actions.Controls.Add(_openButton);
        actions.Controls.Add(_exportButton);
        header.Controls.Add(actions);
        return header;
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 20, 18, 14),
            ColumnCount = 1,
            RowCount = 8,
            BackColor = PanelColor
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));

        var title = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _garageTitle.Text = AppText.Get("Garage");
        _garageTitle.Font = new Font("Segoe UI", 15, FontStyle.Bold);
        _garageTitle.ForeColor = Color.FromArgb(244, 247, 250);
        _garageTitle.Dock = DockStyle.Fill;
        _garageCount.Text = AppText.Get("GarageHint");
        _garageCount.Font = new Font("Segoe UI", 8.5f);
        _garageCount.ForeColor = MutedText;
        _garageCount.Dock = DockStyle.Fill;
        title.Controls.Add(_garageTitle, 0, 0);
        title.Controls.Add(_garageCount, 0, 1);
        sidebar.Controls.Add(title, 0, 0);

        _archivePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_archivePicker);
        _archivePicker.SelectedIndexChanged += (_, _) => SelectArchive();
        sidebar.Controls.Add(_archivePicker, 0, 1);

        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.BackColor = RaisedColor;
        _searchBox.ForeColor = Color.FromArgb(232, 238, 245);
        _searchBox.Font = new Font("Segoe UI", 9.5f);
        _searchBox.PlaceholderText = AppText.Get("SearchCars");
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Margin = new Padding(0, 5, 0, 0);
        _searchBox.TextChanged += (_, _) => FilterCars();
        sidebar.Controls.Add(_searchBox, 0, 2);

        var listHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _modelsLabel.Text = AppText.Get("Models");
        _modelsLabel.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        _modelsLabel.ForeColor = MutedText;
        _modelsLabel.Dock = DockStyle.Left;
        _modelsLabel.TextAlign = ContentAlignment.MiddleLeft;
        var countLabel = StyledLabel("", 8, MutedText);
        countLabel.Name = "visibleCount";
        countLabel.Dock = DockStyle.Right;
        countLabel.TextAlign = ContentAlignment.MiddleRight;
        listHeader.Controls.Add(countLabel);
        listHeader.Controls.Add(_modelsLabel);
        sidebar.Controls.Add(listHeader, 0, 3);

        _carList.Dock = DockStyle.Fill;
        _carList.BorderStyle = BorderStyle.None;
        _carList.BackColor = PanelColor;
        _carList.ForeColor = Color.FromArgb(216, 225, 235);
        _carList.Font = new Font("Segoe UI", 9.5f);
        _carList.DrawMode = DrawMode.OwnerDrawFixed;
        _carList.ItemHeight = 37;
        _carList.IntegralHeight = false;
        _carList.AccessibleName = AppText.Get("CarList");
        _carList.DrawItem += DrawCarItem;
        _carList.SelectedIndexChanged += async (_, _) => await SelectCarAsync();
        sidebar.Controls.Add(_carList, 0, 4);

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        options.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _textureToggle.Text = AppText.Get("Textures");
        _textureToggle.Checked = true;
        _edgesToggle.Text = AppText.Get("Wireframe");
        _spinToggle.Text = AppText.Get("AutoRotate");
        _spinToggle.Checked = true;
        _textureToggle.CheckedChanged += (_, _) => { _viewport.ShowTexture = _textureToggle.Checked; _viewport.Invalidate(); };
        _edgesToggle.CheckedChanged += (_, _) => { _viewport.ShowEdges = _edgesToggle.Checked; _viewport.Invalidate(); };
        _spinToggle.CheckedChanged += (_, _) => _viewport.AutoRotate = _spinToggle.Checked;
        StyleCheckBox(_textureToggle);
        StyleCheckBox(_edgesToggle);
        StyleCheckBox(_spinToggle);
        _viewport.AutoRotate = _spinToggle.Checked;
        var uvOffsetPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty
        };
        _uvOffsetLabel.Text = "UV ↑";
        _uvOffsetLabel.Font = new Font("Segoe UI", 8.5f);
        _uvOffsetLabel.ForeColor = Color.FromArgb(197, 208, 220);
        _uvOffsetLabel.AutoSize = true;
        _uvOffsetLabel.TextAlign = ContentAlignment.MiddleLeft;
        _uvOffsetLabel.Margin = new Padding(0, 5, 5, 0);
        _verticalUvOffset.Minimum = -CarTexture.MaximumVerticalUvOffset;
        _verticalUvOffset.Maximum = CarTexture.MaximumVerticalUvOffset;
        _verticalUvOffset.Width = 58;
        _verticalUvOffset.TextAlign = HorizontalAlignment.Center;
        _verticalUvOffset.BackColor = RaisedColor;
        _verticalUvOffset.ForeColor = Color.FromArgb(235, 240, 246);
        _verticalUvOffset.BorderStyle = BorderStyle.FixedSingle;
        _verticalUvOffset.Font = new Font("Segoe UI", 8.5f);
        _verticalUvOffset.AccessibleName = AppText.Get("UvCorrection");
        _verticalUvOffset.ValueChanged += (_, _) => _viewport.VerticalUvOffset = (int)_verticalUvOffset.Value;
        uvOffsetPanel.Controls.Add(_uvOffsetLabel);
        uvOffsetPanel.Controls.Add(_verticalUvOffset);
        options.Controls.Add(_textureToggle, 0, 0);
        options.Controls.Add(_edgesToggle, 1, 0);
        options.Controls.Add(_spinToggle, 0, 1);
        options.Controls.Add(uvOffsetPanel, 1, 1);
        sidebar.Controls.Add(options, 0, 5);

        var languagePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _languageLabel.Text = AppText.Get("Language");
        _languageLabel.Font = new Font("Segoe UI", 8.5f);
        _languageLabel.ForeColor = Color.FromArgb(197, 208, 220);
        _languageLabel.Dock = DockStyle.Fill;
        _languageLabel.TextAlign = ContentAlignment.MiddleLeft;
        _languagePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _languagePicker.AccessibleName = AppText.Get("Language");
        StyleCombo(_languagePicker);
        _languagePicker.Items.AddRange(AppText.SupportedLanguages
            .Select(language => new LanguageChoice(language, AppText.GetLanguageName(language)))
            .Cast<object>()
            .ToArray());
        _languagePicker.SelectedIndex = AppText.SupportedLanguages.ToList().IndexOf(AppText.Language);
        _languagePicker.SelectedIndexChanged += (_, _) => ChangeLanguage();
        languagePanel.Controls.Add(_languageLabel, 0, 0);
        languagePanel.Controls.Add(_languagePicker, 1, 0);
        sidebar.Controls.Add(languagePanel, 0, 6);

        _sourceLabel.Text = "BIN • CAR.DAT • CARCADE.DAT";
        _sourceLabel.ForeColor = Color.FromArgb(124, 142, 160);
        _sourceLabel.Font = new Font("Segoe UI", 8);
        _sourceLabel.AutoEllipsis = true;
        _sourceLabel.TextAlign = ContentAlignment.MiddleLeft;
        _sourceLabel.Dock = DockStyle.Fill;
        sidebar.Controls.Add(_sourceLabel, 0, 7);
        return sidebar;
    }

    private Control BuildViewportPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = CanvasColor,
            Padding = new Padding(22, 20, 22, 18)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        _carTitle.Text = AppText.Get("ChooseCar");
        _carTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        _carTitle.ForeColor = Color.FromArgb(244, 247, 250);
        _carTitle.Dock = DockStyle.Fill;
        _carTitle.TextAlign = ContentAlignment.BottomLeft;
        _carMetadata.Text = AppText.Get("CarMetadataHint");
        _carMetadata.Font = new Font("Segoe UI", 8.5f);
        _carMetadata.ForeColor = MutedText;
        _carMetadata.Dock = DockStyle.Fill;
        _carMetadata.TextAlign = ContentAlignment.MiddleLeft;
        _colorPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_colorPicker);
        _colorPicker.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        _colorPicker.Width = 172;
        _colorPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_colorPicker.SelectedIndex >= 0)
            {
                _viewport.ColorIndex = _colorPicker.SelectedIndex;
                _viewport.Invalidate();
            }
        };
        details.Controls.Add(_carTitle, 0, 0);
        details.Controls.Add(_carMetadata, 0, 1);
        details.Controls.Add(_colorPicker, 1, 1);
        panel.Controls.Add(details, 0, 0);

        _viewport.Margin = Padding.Empty;
        _viewport.AccessibleName = AppText.Get("ViewportName");
        _viewport.AccessibleDescription = AppText.Get("ViewportDescription");
        panel.Controls.Add(_viewport, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 10, 0, 0) };
        _statusLabel.Text = AppText.Get("Ready");
        _statusLabel.Font = new Font("Segoe UI", 8.5f);
        _statusLabel.ForeColor = MutedText;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Dock = DockStyle.Fill;
        footer.Controls.Add(_statusLabel);
        panel.Controls.Add(footer, 0, 2);
        return panel;
    }

    private async Task OpenSourceDialogAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = AppText.Get("OpenDialogTitle"),
            Filter = AppText.Get("OpenFilter"),
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadSourceAsync(dialog.FileName);
        }
    }

    private async Task LoadSourceAsync(string path)
    {
        _selectionVersion++;
        _currentArchive = null;
        _currentModel = null;
        _currentTexture = null;
        _dataSource = null;
        _viewport.SetCar(null, null);
        _archivePicker.Items.Clear();
        _carList.Items.Clear();
        _exportButton.Enabled = false;
        _statusLabel.Text = AppText.Get("ReadingImage");
        UseWaitCursor = true;
        _sourceLabel.Text = Path.GetFileName(path);

        try
        {
            CarDataSource source = await Task.Run(() => CarDataSource.Open(path));
            _dataSource = source;
            _archivePicker.Items.Clear();
            foreach (CarArchive archive in source.Archives)
            {
                _archivePicker.Items.Add(new ArchiveChoice(archive));
            }

            if (_archivePicker.Items.Count == 0)
            {
                throw new InvalidDataException(AppText.Get("NoArchivesFound"));
            }

            _archivePicker.SelectedIndex = 0;
            _sourceLabel.Text = Path.GetFileName(path);
            _statusLabel.Text = AppText.Get("ImageReady", _archivePicker.Items.Count);
            _viewport.Focus();
        }
        catch (Exception error)
        {
            _dataSource = null;
            _statusLabel.Text = AppText.Get("OpenFailed");
            MessageBox.Show(this, error.Message, AppText.Get("OpenFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void SelectArchive()
    {
        if (_syncing || _archivePicker.SelectedItem is not ArchiveChoice selected)
        {
            return;
        }

        _currentArchive = selected.Archive;
        _syncing = true;
        _searchBox.Clear();
        _carList.BeginUpdate();
        _carList.Items.Clear();
        for (int index = 0; index < selected.Archive.CarCount; index++)
        {
            _carList.Items.Add(new CarChoice(index));
        }

        _carList.EndUpdate();
        _garageCount.Text = AppText.Get("CarsInArchive", selected.Archive.CarCount, selected.Archive.Name);
        _syncing = false;
        if (_carList.Items.Count > 0)
        {
            _carList.SelectedIndex = 0;
        }
        else
        {
            ClearCurrentCar();
        }

        UpdateVisibleCount();
    }

    private void FilterCars()
    {
        if (_syncing || _currentArchive is null)
        {
            return;
        }

        string query = _searchBox.Text.Trim();
        _syncing = true;
        _carList.BeginUpdate();
        _carList.Items.Clear();
        for (int index = 0; index < _currentArchive.CarCount; index++)
        {
            var item = new CarChoice(index);
            if (query.Length == 0 ||
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.SearchIndex.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _carList.Items.Add(item);
            }
        }

        if (_carList.Items.Count > 0)
        {
            _carList.SelectedIndex = 0;
        }

        _carList.EndUpdate();
        _syncing = false;
        UpdateVisibleCount();
        if (_carList.Items.Count > 0)
        {
            _ = SelectCarAsync();
        }
        else
        {
            ClearCurrentCar();
        }
    }

    private async Task SelectCarAsync()
    {
        if (_syncing || _currentArchive is null || _carList.SelectedItem is not CarChoice choice)
        {
            return;
        }

        int version = ++_selectionVersion;
        CarArchive archive = _currentArchive;
        _statusLabel.Text = AppText.Get("DecodingCar", choice.DisplayNumber);
        _exportButton.Enabled = false;

        try
        {
            (CarModel model, CarTexture texture) = await Task.Run(() =>
            {
                CarAsset asset = archive.ReadCar(choice.Index);
                return (CarModel.Parse(asset.ModelData), CarTexture.Parse(asset.TextureData));
            });
            if (version != _selectionVersion || IsDisposed)
            {
                return;
            }

            _currentModel = model;
            _currentTexture = texture;
            _viewport.SetCar(model, texture);
            _colorPicker.BeginUpdate();
            _colorPicker.Items.Clear();
            foreach (byte id in texture.ColorIds)
            {
                _colorPicker.Items.Add(AppText.Get("ColorNumber", id));
            }

            _colorPicker.EndUpdate();
            _colorPicker.SelectedIndex = 0;
            int faceCount = model.Lods[0].Faces.Count;
            int skipped = model.Lods.Sum(lod => lod.SkippedFaceCount);
            _carTitle.Text = choice.Name;
            _carMetadata.Text = AppText.Get("LodMetadata", model.Lods[0].Vertices.Count, faceCount) +
                                (skipped > 0 ? AppText.Get("SkippedFaces", skipped) : string.Empty);
            _exportButton.Enabled = true;
            _statusLabel.Text = AppText.Get("ModelStatus", archive.Name, texture.ColorCount, model.Lods.Count, model.DeclaredLodCount);
        }
        catch (Exception error)
        {
            if (version != _selectionVersion || IsDisposed)
            {
                return;
            }

            _currentModel = null;
            _currentTexture = null;
            _viewport.SetCar(null, null);
            _carTitle.Text = choice.Name;
            _carMetadata.Text = AppText.Get("ModelDecodeFailed");
            _statusLabel.Text = AppText.Get("ModelReadFailed");
            MessageBox.Show(this, error.Message, AppText.Get("ModelReadFailedTitle", choice.Name), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExportCurrentCarAsync()
    {
        if (_currentModel is null || _currentTexture is null || _carList.SelectedItem is not CarChoice choice)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = AppText.Get("ExportDialogTitle"),
            Filter = AppText.Get("ObjFilter"),
            DefaultExt = "obj",
            AddExtension = true,
            FileName = $"{choice.FileStem}.obj",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var export = ObjExporter.Create(
                _currentModel,
                _currentTexture,
                choice.FileStem,
                $"{choice.FileStem}_palette.bmp",
                colorIndex: Math.Clamp(_viewport.ColorIndex, 0, _currentTexture.ColorCount - 1),
                verticalUvOffset: _viewport.VerticalUvOffset);
            string directory = Path.GetDirectoryName(dialog.FileName)!;
            string modelPath = dialog.FileName;
            string materialPath = Path.Combine(directory, export.MaterialFileName);
            string texturePath = Path.Combine(directory, export.TextureFileName);

            await Task.Run(() =>
            {
                File.WriteAllText(modelPath, export.ObjText);
                File.WriteAllText(materialPath, export.MtlText);
                File.WriteAllBytes(texturePath, export.TextureBitmap);
            });

            _statusLabel.Text = AppText.Get("ExportProgress", directory);
            MessageBox.Show(this, AppText.Get("ExportSuccess", directory), AppText.Get("ExportSuccessTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error)
        {
            _statusLabel.Text = AppText.Get("ExportFailed");
            MessageBox.Show(this, error.Message, AppText.Get("ExportFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DrawCarItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index >= 0 && e.Index < _carList.Items.Count && _carList.Items[e.Index] is CarChoice choice)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Color.FromArgb(48, 61, 77) : PanelColor;
            Color foreground = selected ? Color.FromArgb(255, 231, 184) : Color.FromArgb(216, 225, 235);
            using var brush = new SolidBrush(background);
            using var textBrush = new SolidBrush(foreground);
            e.Graphics.FillRectangle(brush, e.Bounds);
            var marker = new Rectangle(e.Bounds.X + 11, e.Bounds.Y + 12, 4, 13);
            using var markerBrush = new SolidBrush(selected ? AccentColor : RaisedColor);
            e.Graphics.FillRectangle(markerBrush, marker);
            e.Graphics.DrawString(choice.DisplayNumber, Font, textBrush, e.Bounds.X + 26, e.Bounds.Y + 9);
            using var smallBrush = new SolidBrush(MutedText);
            e.Graphics.DrawString("GT1", Font, smallBrush, e.Bounds.Right - 38, e.Bounds.Y + 10);
        }

        e.DrawFocusRectangle();
    }

    private void UpdateVisibleCount()
    {
        if (Controls.Count == 0)
        {
            return;
        }

        Label? label = Controls.Find("visibleCount", searchAllChildren: true).OfType<Label>().FirstOrDefault();
        if (label is not null)
        {
            label.Text = _currentArchive is null
                ? string.Empty
                : $"{_carList.Items.Count:N0} / {_currentArchive.CarCount:N0}";
        }
    }

    private static Button CreateButton(string text, bool emphasized)
    {
        var button = new Button { Text = text };
        StyleButton(button, emphasized);
        return button;
    }

    private static void StyleButton(Button button, bool emphasized)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = emphasized ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(63, 76, 91);
        button.BackColor = emphasized ? AccentColor : RaisedColor;
        button.ForeColor = emphasized ? Color.FromArgb(30, 35, 40) : Color.FromArgb(230, 235, 240);
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Height = 36;
        button.Margin = new Padding(8, 0, 0, 0);
        button.UseVisualStyleBackColor = false;
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = RaisedColor;
        combo.ForeColor = Color.FromArgb(235, 240, 246);
        combo.Font = new Font("Segoe UI", 9);
        combo.FlatStyle = FlatStyle.Flat;
        combo.Dock = DockStyle.Fill;
        combo.Margin = new Padding(0, 5, 0, 4);
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.Dock = DockStyle.Fill;
        checkBox.ForeColor = Color.FromArgb(197, 208, 220);
        checkBox.Font = new Font("Segoe UI", 8.5f);
        checkBox.Margin = new Padding(0, 2, 0, 0);
        checkBox.Cursor = Cursors.Hand;
    }

    private static Label StyledLabel(string text, float size, Color color, bool bold = false) =>
        new()
        {
            Text = text,
            Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color,
            AutoEllipsis = true
        };

    private void ChangeLanguage()
    {
        if (_languagePicker.SelectedItem is not LanguageChoice choice || choice.Language == AppText.Language)
        {
            return;
        }

        AppText.SetLanguage(choice.Language);
        ApplyLanguage();
        try
        {
            AppText.SavePreference();
        }
        catch (IOException error)
        {
            MessageBox.Show(this, $"{AppText.Get("LanguageSaveFailed")}\n{error.Message}",
                AppText.Get("LanguageSaveFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException error)
        {
            MessageBox.Show(this, $"{AppText.Get("LanguageSaveFailed")}\n{error.Message}",
                AppText.Get("LanguageSaveFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyLanguage()
    {
        Text = AppText.Get("WindowTitle");
        _openButton.Text = AppText.Get("OpenImage");
        _exportButton.Text = AppText.Get("ExportObj");
        _garageTitle.Text = AppText.Get("Garage");
        _searchBox.PlaceholderText = AppText.Get("SearchCars");
        _modelsLabel.Text = AppText.Get("Models");
        _carList.AccessibleName = AppText.Get("CarList");
        _textureToggle.Text = AppText.Get("Textures");
        _edgesToggle.Text = AppText.Get("Wireframe");
        _spinToggle.Text = AppText.Get("AutoRotate");
        _verticalUvOffset.AccessibleName = AppText.Get("UvCorrection");
        _languageLabel.Text = AppText.Get("Language");
        _languagePicker.AccessibleName = AppText.Get("Language");
        _viewport.AccessibleName = AppText.Get("ViewportName");
        _viewport.AccessibleDescription = AppText.Get("ViewportDescription");
        _viewport.Invalidate();

        int archiveIndex = _archivePicker.SelectedIndex;
        _syncing = true;
        _archivePicker.BeginUpdate();
        _archivePicker.Items.Clear();
        if (_dataSource is not null)
        {
            foreach (CarArchive archive in _dataSource.Archives)
            {
                _archivePicker.Items.Add(new ArchiveChoice(archive));
            }
        }

        if (archiveIndex >= 0 && archiveIndex < _archivePicker.Items.Count)
        {
            _archivePicker.SelectedIndex = archiveIndex;
        }

        _archivePicker.EndUpdate();
        _syncing = false;

        if (_currentArchive is null)
        {
            _garageCount.Text = AppText.Get("GarageHint");
            _carTitle.Text = AppText.Get("ChooseCar");
            _statusLabel.Text = AppText.Get("Ready");
        }
        else
        {
            _garageCount.Text = AppText.Get("CarsInArchive", _currentArchive.CarCount, _currentArchive.Name);
            if (_currentModel is not null && _currentTexture is not null &&
                _carList.SelectedItem is CarChoice car)
            {
                _carTitle.Text = car.Name;
                int selectedColor = _colorPicker.SelectedIndex;
                int faceCount = _currentModel.Lods[0].Faces.Count;
                int skipped = _currentModel.Lods.Sum(lod => lod.SkippedFaceCount);
                _carMetadata.Text = AppText.Get("LodMetadata", _currentModel.Lods[0].Vertices.Count, faceCount) +
                                    (skipped > 0 ? AppText.Get("SkippedFaces", skipped) : string.Empty);
                _colorPicker.BeginUpdate();
                _colorPicker.Items.Clear();
                foreach (byte id in _currentTexture.ColorIds)
                {
                    _colorPicker.Items.Add(AppText.Get("ColorNumber", id));
                }

                _colorPicker.EndUpdate();
                _colorPicker.SelectedIndex = Math.Clamp(selectedColor, 0, _colorPicker.Items.Count - 1);
                _statusLabel.Text = AppText.Get(
                    "ModelStatus",
                    _currentArchive.Name,
                    _currentTexture.ColorCount,
                    _currentModel.Lods.Count,
                    _currentModel.DeclaredLodCount);
            }
            else
            {
                _carTitle.Text = _carList.SelectedItem is CarChoice selectedCar
                    ? selectedCar.Name
                    : AppText.Get("NoResults");
                _carMetadata.Text = AppText.Get("CarMetadataHint");
                _statusLabel.Text = AppText.Get("Ready");
            }
        }

        _carList.Invalidate();
        UpdateVisibleCount();
    }

    private sealed record LanguageChoice(AppLanguage Language, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ArchiveChoice(CarArchive Archive)
    {
        public override string ToString() => AppText.Get("ArchiveCaption", Archive.Name, Archive.CarCount);
    }

    private sealed record CarChoice(int Index)
    {
        public string DisplayNumber => AppText.Get("CarNumber", Index + 1);
        public string Name => DisplayNumber;
        public string FileStem => $"GT1_{Index + 1:000}";
        public string SearchIndex => $"{Index + 1:000}";
        public override string ToString() => DisplayNumber;
    }
}
