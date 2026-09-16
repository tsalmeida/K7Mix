using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using K7Mix.Core;

namespace K7Mix.Views;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private MixDocument _mix = new();
    private MixPlayer? _player;
    private string? _packagePath;
    private bool _suppress;
    private string? _extractFolder;

    public MainWindow() : this([]) { }

    public MainWindow(string?[] args)
    {
        InitializeComponent();
        VersionText.Text = $"v{Brand.Version}";
        FontBox.ItemsSource = CassetteFont.All.Select(f => f.DisplayName).ToList();
        TransitionKindBox.ItemsSource = new[] { "none", "crossfade", "fadeout", "silence" };
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Opened += (_, _) =>
        {
            var startup = args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            if (startup is not null && File.Exists(startup))
            {
                if (startup.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    ImportMarkdown(startup);
                else if (startup.EndsWith(".k7m", StringComparison.OrdinalIgnoreCase))
                    OpenPackage(startup);
            }
            Bind();
        };
        Closed += (_, _) =>
        {
            _player?.Dispose();
        };
    }

    private void Bind()
    {
        _suppress = true;
        TitleBox.Text = _mix.Title;
        DescriptionBox.Text = _mix.Description;
        SourceBox.Text = _mix.SourceUrl;
        LicenseBox.Text = _mix.License;
        SideABox.Text = _mix.SideALabel;
        SideBBox.Text = _mix.SideBLabel;
        SideBStartBox.Text = _mix.SideBStartTrack?.ToString() ?? "";
        FontBox.SelectedItem = _mix.Appearance.Font.DisplayName;
        TapeColorBox.Text = _mix.Appearance.TapeColor.ToHex();
        StickerColorBox.Text = _mix.Appearance.StickerColor.ToHex();
        TextColorBox.Text = _mix.Appearance.TextColor.ToHex();
        RebuildTracks();
        TapeView.Apply(_mix);
        CaseView.Apply(_mix);
        _suppress = false;
        UpdateStatus();
        BindSelectedTrack();
    }

    private void RebuildTracks()
    {
        var selected = TrackList.SelectedIndex;
        TrackList.ItemsSource = _mix.Tracks.Select((t, i) =>
        {
            var side = _mix.SideOf(i) == 0 ? "A" : "B";
            var trans = i < _mix.Tracks.Count - 1 ? t.TransitionOut.ToToken() : "end";
            var code = string.IsNullOrWhiteSpace(t.Codename) ? "" : $" · {t.Codename}";
            return $"{i + 1}. [{side}] {t.Title}{code}  {MixValidator.FormatMinutes(t.Duration)}  → {trans}";
        }).ToList();
        if (selected >= 0 && selected < _mix.Tracks.Count)
            TrackList.SelectedIndex = selected;
    }

    private MixTrack? SelectedTrack()
    {
        var i = TrackList.SelectedIndex;
        if (i < 0 || i >= _mix.Tracks.Count) return null;
        return _mix.Tracks[i];
    }

    private void BindSelectedTrack()
    {
        _suppress = true;
        var track = SelectedTrack();
        TrackTitleBox.Text = track?.Title ?? "";
        TrackCodenameBox.Text = track?.Codename ?? "";
        TrackLicenseBox.Text = track?.License ?? "";
        TrackSourceBox.Text = track?.Source ?? "";
        var kind = track?.TransitionOut.Kind.ToString().ToLowerInvariant() ?? "none";
        if (kind == "fadeout") kind = "fadeout";
        TransitionKindBox.SelectedItem = track?.TransitionOut.Kind switch
        {
            TransitionKind.Crossfade => "crossfade",
            TransitionKind.FadeOut => "fadeout",
            TransitionKind.Silence => "silence",
            _ => "none",
        };
        TransitionDurationBox.Text = track is null || track.TransitionOut.IsInstant
            ? ""
            : track.TransitionOut.Duration.TotalSeconds.ToString("0.###");
        _suppress = false;
    }

    private void ReadMetadata()
    {
        if (_suppress) return;
        _mix.Title = TitleBox.Text ?? "";
        _mix.Description = DescriptionBox.Text ?? "";
        _mix.SourceUrl = SourceBox.Text ?? "";
        _mix.License = LicenseBox.Text ?? "";
        _mix.SideALabel = SideABox.Text ?? "";
        _mix.SideBLabel = SideBBox.Text ?? "";
        if (int.TryParse(SideBStartBox.Text, out var n) && n >= 1)
            _mix.SideBStartTrack = n;
        else
            _mix.SideBStartTrack = null;
        if (HexColor.TryParse(TapeColorBox.Text, out var tape)) _mix.Appearance.TapeColor = tape;
        if (HexColor.TryParse(StickerColorBox.Text, out var sticker)) _mix.Appearance.StickerColor = sticker;
        if (HexColor.TryParse(TextColorBox.Text, out var text)) _mix.Appearance.TextColor = text;
        TapeView.Apply(_mix);
        RebuildTracks();
        UpdateStatus();
    }

    private void OnMetadataChanged(object? sender, TextChangedEventArgs e) => ReadMetadata();

    private void OnFontChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (FontBox.SelectedItem is string name && CassetteFont.TryParse(name, out var font))
        {
            _mix.Appearance.Font = font;
            TapeView.Apply(_mix);
        }
    }

    private void OnTrackSelected(object? sender, SelectionChangedEventArgs e) => BindSelectedTrack();

    private void OnTrackFieldsChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        var track = SelectedTrack();
        if (track is null) return;
        track.Title = TrackTitleBox.Text ?? "";
        track.Codename = TrackCodenameBox.Text ?? "";
        track.License = TrackLicenseBox.Text ?? "";
        track.Source = TrackSourceBox.Text ?? "";
        RebuildTracks();
        UpdateStatus();
    }

    private void OnTransitionChanged(object? sender, SelectionChangedEventArgs e) => ApplyTransition();

    private void OnTransitionDurationChanged(object? sender, TextChangedEventArgs e) => ApplyTransition();

    private void ApplyTransition()
    {
        if (_suppress) return;
        var track = SelectedTrack();
        if (track is null) return;
        var kindToken = TransitionKindBox.SelectedItem as string ?? "none";
        var durationText = TransitionDurationBox.Text ?? "";
        try
        {
            if (kindToken == "none")
                track.TransitionOut = TrackTransition.None;
            else
                track.TransitionOut = TransitionParser.Parse($"{kindToken} {durationText}s", TrackList.SelectedIndex);
        }
        catch (MixValidationException)
        {
            // Keep typing.
        }
        RebuildTracks();
        UpdateStatus();
    }

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        _player?.Dispose();
        _player = null;
        _mix = new MixDocument();
        _packagePath = null;
        Bind();
        StatusText.Text = "New mix. Add tracks or import a Tekhnexos Markdown export.";
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Tekhnexos Markdown",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null) ImportMarkdown(path);
    }

    private void ImportMarkdown(string path)
    {
        try
        {
            var folder = Path.GetDirectoryName(path) ?? "";
            var result = MarkdownImporter.Import(File.ReadAllText(path), folder);
            foreach (var track in result.Document.Tracks)
            {
                try { track.Duration = FfmpegAudioEncoder.ProbeDuration(track.OriginalSourcePath!); }
                catch (Exception ex)
                {
                    result.Issues.Error($"Could not read '{track.Title}': {ex.Message}");
                }
            }
            _player?.Dispose();
            _player = null;
            _mix = result.Document;
            _packagePath = null;
            Bind();
            StatusText.Text = result.Issues.Items.Count == 0
                ? $"Imported {Path.GetFileName(path)}."
                : result.Issues.ToString().Replace(Environment.NewLine, " · ");
        }
        catch (Exception ex)
        {
            AppLog.Error("Import Markdown", ex);
            StatusText.Text = ex.Message;
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open .k7m",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("K7 Mix") { Patterns = ["*.k7m"] }],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null) OpenPackage(path);
    }

    private void OpenPackage(string path)
    {
        try
        {
            _extractFolder = Path.Combine(AppPaths.UserDataDirectory, "open", Guid.NewGuid().ToString("N"));
            _player?.Dispose();
            _player = null;
            _mix = K7mPackage.Load(path, _extractFolder);
            _packagePath = path;
            _settings.Remember(path);
            Bind();
            StatusText.Text = _mix.Locked
                ? $"Opened locked mix {Path.GetFileName(path)} — {_mix.Tracks.Count} track(s). Play is available; editing is not."
                : $"Opened {Path.GetFileName(path)} — {_mix.Tracks.Count} track(s).";
        }
        catch (Exception ex)
        {
            AppLog.Error("Open package", ex);
            StatusText.Text = ex.Message;
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked)
        {
            StatusText.Text = "This mix is locked. Use Save locked copy from an editable original, or start a new mix.";
            return;
        }
        await SaveAsync(lockedCopy: false);
    }

    private async void OnSaveLockedClick(object? sender, RoutedEventArgs e)
    {
        await SaveAsync(lockedCopy: true);
    }

    private async Task SaveAsync(bool lockedCopy)
    {
        ReadMetadata();
        if (!MixValidator.CanExport(_mix, out var issues))
        {
            StatusText.Text = issues.ToString().Replace(Environment.NewLine, " · ");
            return;
        }

        var suggested = lockedCopy
            ? Path.GetFileNameWithoutExtension(_packagePath ?? _mix.Title) + ".locked.k7m"
            : Path.GetFileName(_packagePath) ?? Sanitize(_mix.Title) + ".k7m";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = lockedCopy ? "Save locked copy" : "Save mix",
            SuggestedFileName = suggested,
            FileTypeChoices = [new FilePickerFileType("K7 Mix") { Patterns = ["*.k7m"] }],
        });
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        try
        {
            foreach (var track in _mix.Tracks)
            {
                if (track.Duration <= TimeSpan.Zero && track.OriginalSourcePath is not null)
                    track.Duration = FfmpegAudioEncoder.ProbeDuration(track.OriginalSourcePath);
            }
            K7mPackage.Save(_mix, path, lockedCopy, new FfmpegAudioEncoder());
            if (!lockedCopy)
            {
                _packagePath = path;
                _settings.Remember(path);
            }
            StatusText.Text = lockedCopy
                ? $"Wrote locked copy {Path.GetFileName(path)}. The editable mix is unchanged."
                : $"Saved {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Save", ex);
            StatusText.Text = ex.Message;
        }
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        ReadMetadata();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Printable jewel-case insert",
            SuggestedFileName = Sanitize(_mix.Title) + "-insert.pdf",
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        try
        {
            var templates = TemplateSeed.Ensure();
            var typ = Path.Combine(templates, "jewel-case-insert.typ");
            var data = InsertRenderer.BuildTypstData(
                _mix, _mix.Appearance.FrontArtworkPath, _mix.Appearance.BackArtworkPath);
            InsertRenderer.Compile(typ, data, path, Path.Combine(templates, "fonts"));
            StatusText.Text = $"Wrote {Path.GetFileName(path)} at actual size.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Print insert", ex);
            StatusText.Text = ex.Message;
        }
    }

    private async void OnAddTracksClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked) { StatusText.Text = "Locked mixes cannot be edited."; return; }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add audio",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.mp3", "*.flac", "*.ogg", "*.oga", "*.mid", "*.midi"] },
            ],
        });
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is null) continue;
            if (!FfmpegAudioEncoder.IsSupportedExtension(path))
            {
                StatusText.Text = $"Unsupported file: {Path.GetFileName(path)}";
                continue;
            }
            try
            {
                var duration = FfmpegAudioEncoder.ProbeDuration(path);
                _mix.Tracks.Add(new MixTrack
                {
                    Title = Path.GetFileNameWithoutExtension(path),
                    Duration = duration,
                    OriginalSourcePath = path,
                    SourceExtension = Path.GetExtension(path),
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"{Path.GetFileName(path)}: {ex.Message}";
            }
        }
        RebuildTracks();
        UpdateStatus();
    }

    private void OnMoveUpClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked) return;
        var i = TrackList.SelectedIndex;
        if (i <= 0) return;
        _mix.MoveTrack(i, i - 1);
        RebuildTracks();
        TrackList.SelectedIndex = i - 1;
        UpdateStatus();
    }

    private void OnMoveDownClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked) return;
        var i = TrackList.SelectedIndex;
        if (i < 0 || i >= _mix.Tracks.Count - 1) return;
        _mix.MoveTrack(i, i + 1);
        RebuildTracks();
        TrackList.SelectedIndex = i + 1;
        UpdateStatus();
    }

    private void OnRemoveTrackClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked) return;
        var i = TrackList.SelectedIndex;
        if (i < 0) return;
        _mix.Tracks.RemoveAt(i);
        RebuildTracks();
        UpdateStatus();
    }

    private void OnSideBHereClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked) return;
        var i = TrackList.SelectedIndex;
        if (i < 0) return;
        _mix.SideBStartTrack = i + 1;
        SideBStartBox.Text = _mix.SideBStartTrack.ToString();
        RebuildTracks();
    }

    private async void OnFrontArtClick(object? sender, RoutedEventArgs e) =>
        await PickArt(p => _mix.Appearance.FrontArtworkPath = p);

    private async void OnBackArtClick(object? sender, RoutedEventArgs e) =>
        await PickArt(p => _mix.Appearance.BackArtworkPath = p);

    private async Task PickArt(Action<string> assign)
    {
        if (_mix.Locked) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Artwork",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        assign(path);
        Bind();
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_mix.Tracks.Count == 0)
            {
                StatusText.Text = "This mix has no tracks to play.";
                return;
            }
            EnsurePlayer();
            _player?.PlayPause();
            if (!string.IsNullOrWhiteSpace(_player?.LastError))
                StatusText.Text = _player!.LastError;
            UpdateNowPlaying();
        }
        catch (Exception ex)
        {
            AppLog.Error("Play", ex);
            StatusText.Text = ex.Message;
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        _player?.Stop();
        UpdateNowPlaying();
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        EnsurePlayer();
        if (_player is not null && !_player.Next())
            StatusText.Text = _player.Transport.IsTransitioning
                ? "A transition is already running. Next is ignored until it finishes."
                : "No next track.";
        UpdateNowPlaying();
    }

    private void EnsurePlayer()
    {
        _player ??= new MixPlayer(_mix);
        _player.Changed -= OnPlayerChanged;
        _player.Changed += OnPlayerChanged;
    }

    private void OnPlayerChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateNowPlaying);

    private void UpdateNowPlaying()
    {
        if (_player is null)
        {
            NowPlayingText.Text = "Stopped.";
            return;
        }
        var t = _player.Transport;
        var track = t.CurrentTrack;
        var side = t.DisplayedSide == 0 ? "A" : "B";
        var extra = t.IsTransitioning ? " · transition running" : "";
        if (!string.IsNullOrWhiteSpace(_player.LastError))
        {
            NowPlayingText.Text = _player.LastError;
            return;
        }
        NowPlayingText.Text = track is null
            ? "Stopped."
            : $"Side {side} · {track.Title} · {MixValidator.FormatMinutes(t.PositionOnTrack)}{extra}";
    }

    private void UpdateStatus()
    {
        var issues = MixValidator.Validate(_mix);
        var timeline = MixValidator.FormatMinutes(DurationMath.Timeline(_mix));
        DurationText.Text = $"Playback length {timeline} (preferred 90:00, hard 120:00). {_mix.Tracks.Count} track(s).";
        var first = issues.Items.FirstOrDefault(i => i.Severity != IssueSeverity.Info);
        StatusText.Text = first?.Message
            ?? (string.IsNullOrWhiteSpace(_packagePath) ? "Unsaved mix." : Path.GetFileName(_packagePath));
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasK7m(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var path = FirstK7m(e);
        if (path is not null) OpenPackage(path);
        e.Handled = true;
    }

    private static bool HasK7m(DragEventArgs e) => FirstK7m(e) is not null;

    private static string? FirstK7m(DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return null;
        foreach (var item in e.Data.GetFiles() ?? [])
        {
            var path = item.TryGetLocalPath();
            if (path is not null && path.EndsWith(".k7m", StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return null;
    }

    private static string Sanitize(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "mix" : cleaned;
    }
}
