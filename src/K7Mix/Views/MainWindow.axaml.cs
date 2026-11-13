using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using K7Mix.Core;

namespace K7Mix.Views;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private MixDocument _mix = new();
    private MixPlayer? _player;
    private string? _packagePath;
    private int _suppressDepth;
    private string? _extractFolder;
    private readonly ObservableCollection<TrackRow> _trackRows = [];

    private readonly DispatcherTimer _tick = new()
    {
        Interval = TimeSpan.FromMilliseconds(100),
    };

    /// <summary>
    /// The track the right-hand editors currently hold the text of. Avalonia raises
    /// TextChanged from a queued dispatcher operation, so a field edit can arrive
    /// after the selection has already moved on; writing back through this instead
    /// of the live selection keeps the text with the track it was typed for.
    /// </summary>
    private MixTrack? _editingTrack;

    private bool Suppressed => _suppressDepth > 0;

    /// <summary>
    /// Suppresses the editor write-back handlers for the duration of the scope.
    /// Must nest: these blocks re-enter each other, and a plain bool cleared on
    /// exit would strip the guard from the caller that is still running.
    /// </summary>
    private IDisposable Suppress()
    {
        _suppressDepth++;
        return new SuppressScope(this);
    }

    private sealed class SuppressScope(MainWindow owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            owner._suppressDepth--;
        }
    }

    public MainWindow() : this([]) { }

    public MainWindow(string?[] args)
    {
        InitializeComponent();
#if COMPANIONKIT
        Tekhnexos.CompanionKit.Vim.SetEnable(DescriptionBox, true);
#endif
        VersionText.Text = $"v{Brand.Version}";
        FontBox.ItemsSource = CassetteFont.All.Select(f => f.DisplayName).ToList();
        TransitionKindBox.ItemsSource = new[] { "none", "crossfade", "fadeout", "silence" };
        TrackList.ItemsSource = _trackRows;
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
        _tick.Tick += OnTick;
        Closed += (_, _) =>
        {
            _tick.Stop();
            _player?.Dispose();
        };
    }

    private void Bind()
    {
        using (Suppress())
        {
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
        }
        UpdateStatus();
        BindSelectedTrack();
        UpdateNowPlaying();
    }

    private void RebuildTracks()
    {
        // Refresh the rows in place. Assigning TrackList.ItemsSource here would reset
        // the selection model and raise SelectionChanged, which lands in
        // BindSelectedTrack, rewrites the editors, and comes back through TextChanged
        // to whichever handler called this — a loop that pegs the UI thread and leaks
        // until the process is killed.
        var selected = TrackList.SelectedIndex;
        while (_trackRows.Count > _mix.Tracks.Count)
            _trackRows.RemoveAt(_trackRows.Count - 1);
        for (var i = 0; i < _mix.Tracks.Count; i++)
        {
            var text = FormatTrackRow(i);
            if (i < _trackRows.Count)
                _trackRows[i].Text = text;
            else
                _trackRows.Add(new TrackRow(text));
        }
        if (selected >= 0 && selected < _mix.Tracks.Count && TrackList.SelectedIndex != selected)
            TrackList.SelectedIndex = selected;
    }

    private string FormatTrackRow(int i)
    {
        var t = _mix.Tracks[i];
        var side = _mix.SideOf(i) == 0 ? "A" : "B";
        var code = string.IsNullOrWhiteSpace(t.Codename) ? "" : $" · {t.Codename}";
        var loop = t.Loop ? " ×2" : "";
        // The transition is drawn pointing in, because that is the track it belongs
        // to. On the first track the arrow curls back: it is the loop to the start.
        var arrow = i == 0 ? "↩" : "←";
        return $"{i + 1}. [{side}] {t.Title}{code}{loop}  " +
               $"{MixValidator.FormatMinutes(t.Duration)}  {arrow} {t.TransitionIn.ToToken()}";
    }

    private MixTrack? SelectedTrack()
    {
        var i = TrackList.SelectedIndex;
        if (i < 0 || i >= _mix.Tracks.Count) return null;
        return _mix.Tracks[i];
    }

    private void BindSelectedTrack()
    {
        using var _ = Suppress();
        var track = SelectedTrack();
        _editingTrack = track;
        TrackTitleBox.Text = track?.Title ?? "";
        TrackCodenameBox.Text = track?.Codename ?? "";
        TrackLicenseBox.Text = track?.License ?? "";
        TrackSourceBox.Text = track?.Source ?? "";
        TrackAudioPathText.Text = track?.OriginalSourcePath is { } p
            ? Path.GetFileName(p)
            : track is null ? "" : "(no audio file set)";
        var audioPath = track?.OriginalSourcePath;
        var audioExists = audioPath is not null && File.Exists(audioPath);
        AudioOpenButton.IsEnabled = audioExists;
        AudioFolderButton.IsEnabled = audioExists;
        TrackLoopBox.IsChecked = track?.Loop ?? false;
        TransitionKindBox.SelectedItem = track?.TransitionIn.Kind switch
        {
            TransitionKind.Crossfade => "crossfade",
            TransitionKind.FadeOut => "fadeout",
            TransitionKind.Silence => "silence",
            _ => "none",
        };
        TransitionDurationBox.Text = track is null || track.TransitionIn.IsInstant
            ? ""
            : track.TransitionIn.Duration.TotalSeconds.ToString("0.###");
        TransitionHintText.Text = track is null
            ? ""
            : TrackList.SelectedIndex == 0
                ? "This is the loop back to the start, played when the tape reaches the end of the last track."
                : "Played when the previous track hands over to this one.";
    }

    private void ReadMetadata()
    {
        if (Suppressed) return;
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
        if (Suppressed) return;
        if (FontBox.SelectedItem is string name && CassetteFont.TryParse(name, out var font))
        {
            _mix.Appearance.Font = font;
            TapeView.Apply(_mix);
        }
    }

    private void OnTrackSelected(object? sender, SelectionChangedEventArgs e) => BindSelectedTrack();

    private void OnTrackFieldsChanged(object? sender, TextChangedEventArgs e)
    {
        if (Suppressed) return;
        var track = _editingTrack;
        if (track is null) return;
        track.Title = TrackTitleBox.Text ?? "";
        track.Codename = TrackCodenameBox.Text ?? "";
        track.License = TrackLicenseBox.Text ?? "";
        track.Source = TrackSourceBox.Text ?? "";
        RebuildTracks();
        UpdateStatus();
    }

    private void OnTrackLoopChanged(object? sender, RoutedEventArgs e)
    {
        if (Suppressed) return;
        var track = _editingTrack;
        if (track is null) return;
        track.Loop = TrackLoopBox.IsChecked == true;
        RebuildTracks();
        UpdateStatus();
        UpdateNowPlaying();
    }

    private void OnTransitionChanged(object? sender, SelectionChangedEventArgs e) => ApplyTransition();

    private void OnTransitionDurationChanged(object? sender, TextChangedEventArgs e) => ApplyTransition();

    private void ApplyTransition()
    {
        if (Suppressed) return;
        var track = _editingTrack;
        if (track is null) return;
        var index = _mix.Tracks.IndexOf(track);
        if (index < 0) return; // the edited track was removed before this event arrived
        var kindToken = TransitionKindBox.SelectedItem as string ?? "none";
        var durationText = TransitionDurationBox.Text ?? "";
        try
        {
            if (kindToken == "none")
                track.TransitionIn = TrackTransition.None;
            else
                track.TransitionIn = TransitionParser.Parse($"{kindToken} {durationText}s", index);
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

    private async void OnSetAudioFileClick(object? sender, RoutedEventArgs e)
    {
        if (_mix.Locked) { StatusText.Text = "Locked mixes cannot be edited."; return; }
        var track = SelectedTrack();
        if (track is null) { StatusText.Text = "Select a track first."; return; }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Set audio file for selected track",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.mp3", "*.flac", "*.ogg", "*.oga", "*.mid", "*.midi"] },
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        if (!FfmpegAudioEncoder.IsSupportedExtension(path))
        {
            StatusText.Text = $"Unsupported file: {Path.GetFileName(path)}";
            return;
        }
        try
        {
            track.OriginalSourcePath = path;
            track.SourceExtension = Path.GetExtension(path);
            track.Duration = FfmpegAudioEncoder.ProbeDuration(path);
            if (string.IsNullOrWhiteSpace(track.Title))
                track.Title = Path.GetFileNameWithoutExtension(path);
            BindSelectedTrack();
            RebuildTracks();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{Path.GetFileName(path)}: {ex.Message}";
        }
    }

    private void OnOpenTrackAudioClick(object? sender, RoutedEventArgs e)
    {
        var path = SelectedTrack()?.OriginalSourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            ShellOpen.Path(path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Opening audio file", ex);
            StatusText.Text = ex.Message;
        }
    }

    private void OnOpenTrackAudioFolderClick(object? sender, RoutedEventArgs e)
    {
        var path = SelectedTrack()?.OriginalSourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            ShellOpen.ContainingFolder(path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Opening audio file folder", ex);
            StatusText.Text = ex.Message;
        }
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
        _tick.Start();
    }

    /// <summary>
    /// Drives the progress bar. MixPlayer raises Changed only when the track or the
    /// transition state changes, which is right for a player but leaves the bar
    /// frozen for the length of a track and then jumping when playback is paused.
    /// Polling the transport from the UI thread keeps the bar moving without
    /// posting twenty cross-thread updates a second from the audio clock.
    /// </summary>
    private void OnTick(object? sender, EventArgs e)
    {
        if (_player?.Transport.IsPlaying != true) return;
        UpdateNowPlaying();
    }

    private void OnPlayerChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateNowPlaying);

    private void OnProgressSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateProgress();

    private void UpdateNowPlaying()
    {
        PlayPauseIcon.Text = _player?.Transport.IsPlaying == true ? Icons.Pause : Icons.Play;
        if (_player is null)
        {
            NowPlayingTitle.Text = "Nothing playing";
            NowPlayingText.Text = "Stopped.";
            UpdateProgress();
            return;
        }
        if (!string.IsNullOrWhiteSpace(_player.LastError))
        {
            NowPlayingTitle.Text = "Nothing playing";
            NowPlayingText.Text = _player.LastError;
            UpdateProgress();
            return;
        }

        var t = _player.Transport;
        var track = t.CurrentTrack;
        if (track is null)
        {
            NowPlayingTitle.Text = "Nothing playing";
            NowPlayingText.Text = "Stopped.";
            UpdateProgress();
            return;
        }

        var side = t.DisplayedSide == 0 ? "A" : "B";
        var loop = track.Loop
            ? t.OnLoopRepeat ? " · repeat" : " · plays twice"
            : "";
        var extra = t.IsTransitioning ? " · transition running" : "";
        NowPlayingTitle.Text = track.Title;
        NowPlayingText.Text =
            $"Side {side} · {MixValidator.FormatMinutes(t.PositionInSpan)} " +
            $"of {MixValidator.FormatMinutes(t.CurrentTrackSpan)}{loop}{extra}";
        UpdateProgress();
    }

    /// <summary>
    /// Draws the playhead across the whole span the current track occupies, so a
    /// looping track fills one bar over both passes and the mark shows where the
    /// repeat begins.
    /// </summary>
    private void UpdateProgress()
    {
        var width = ProgressArea.Bounds.Width;
        var t = _player?.Transport;
        if (t?.CurrentTrack is null || width <= 0)
        {
            ProgressFill.Width = 0;
            LoopMark.IsVisible = false;
            return;
        }

        var span = t.CurrentTrackSpan;
        var fraction = span > TimeSpan.Zero
            ? Math.Clamp(t.PositionInSpan.TotalSeconds / span.TotalSeconds, 0, 1)
            : 0;
        ProgressFill.Width = width * fraction;

        if (t.LoopMarker is { } marker)
        {
            LoopMark.IsVisible = true;
            LoopMark.Margin = new Thickness(Math.Clamp(width * marker - 1, 0, width - 2), 0, 0, 0);
        }
        else
        {
            LoopMark.IsVisible = false;
        }
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
