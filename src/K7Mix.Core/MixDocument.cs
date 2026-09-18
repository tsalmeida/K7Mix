namespace K7Mix.Core;

public sealed class MixAppearance
{
    public CassetteFont Font { get; set; } = CassetteFont.Default;
    public HexColor TextColor { get; set; } = new(0, 0, 0);
    public HexColor TapeColor { get; set; } = new(0x1E, 0x1E, 0x22);
    public HexColor StickerColor { get; set; } = new(0xCD, 0xBA, 0x9E);
    public string? FrontArtworkPath { get; set; }
    public string? BackArtworkPath { get; set; }
}

public sealed class MixTrack
{
    public string Id { get; set; } = NewId();
    public string Title { get; set; } = "";
    public string Codename { get; set; } = "";
    public string License { get; set; } = "";
    public string Source { get; set; } = "";
    public string? LicenseDocumentPath { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// The boundary that brings playback <em>into</em> this track. On the first
    /// track it is the loop back from the end of the tape, which is the only time
    /// that boundary is ever crossed.
    /// </summary>
    public TrackTransition TransitionIn { get; set; } = TrackTransition.None;

    /// <summary>
    /// A looping track plays through once more before the tape moves on, so it
    /// occupies twice its duration. The repeat is seamless: the track restarts at
    /// zero with no transition, because a transition is a boundary between two
    /// different tracks and the repeat crosses no such boundary.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>How long this track occupies the tape, counting the repeat.</summary>
    public TimeSpan TapeSpan => Loop ? Duration + Duration : Duration;

    public string? OriginalSourcePath { get; set; }
    public string? PackagedAudioPath { get; set; }
    public string? SourceExtension { get; set; }
    public string? SourceSha256 { get; set; }
    public string EncodedProfile { get; set; } = PlaybackProfile.Id;

    public static string NewId() => "trk_" + Guid.NewGuid().ToString("N");
}

public sealed class MixDocument
{
    /// <summary>
    /// Version 2 moved each transition from the outgoing track to the incoming one
    /// and added the per-track loop flag. Version 1 files are migrated on load.
    /// </summary>
    public const int ManifestVersion = 2;

    public const string FormatName = "k7m";

    public string Title { get; set; } = "Untitled mix";
    public string Description { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string License { get; set; } = "";
    public string? LicenseDocumentPath { get; set; }
    public string SideALabel { get; set; } = "";
    public string SideBLabel { get; set; } = "";
    public int? SideBStartTrack { get; set; }
    public bool Locked { get; set; }
    public MixAppearance Appearance { get; } = new();
    public List<MixTrack> Tracks { get; } = [];
    public string? SourceMarkdownSnapshot { get; set; }
    public Dictionary<string, string> UnknownFields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> LicenseDocuments { get; } = [];

    public string EffectiveSideALabel =>
        string.IsNullOrWhiteSpace(SideALabel) ? Title : SideALabel;

    public string EffectiveSideBLabel =>
        string.IsNullOrWhiteSpace(SideBLabel) ? Title : SideBLabel;

    public MixTrack? TrackByCodename(string codename)
    {
        if (string.IsNullOrWhiteSpace(codename)) return null;
        return Tracks.FirstOrDefault(t =>
            string.Equals(t.Codename, codename, StringComparison.OrdinalIgnoreCase));
    }

    public int SideOf(int trackIndex)
    {
        if (SideBStartTrack is null) return 0;
        var start = SideBStartTrack.Value;
        return trackIndex + 1 >= start ? 1 : 0;
    }

    public string SideLabelOf(int trackIndex) =>
        SideOf(trackIndex) == 0 ? EffectiveSideALabel : EffectiveSideBLabel;

    /// <summary>
    /// The track after this one, wrapping to the first: the tape loops, so the last
    /// track's successor is the first and there is no dead end.
    /// </summary>
    public int NextIndex(int trackIndex) =>
        Tracks.Count == 0 ? 0 : trackIndex + 1 >= Tracks.Count ? 0 : trackIndex + 1;

    public void MoveTrack(int from, int to)
    {
        if (from < 0 || from >= Tracks.Count) return;
        if (to < 0 || to >= Tracks.Count) return;
        if (from == to) return;
        var track = Tracks[from];
        Tracks.RemoveAt(from);
        Tracks.Insert(to, track);
    }

    public MixDocument Clone()
    {
        var copy = new MixDocument
        {
            Title = Title,
            Description = Description,
            SourceUrl = SourceUrl,
            License = License,
            LicenseDocumentPath = LicenseDocumentPath,
            SideALabel = SideALabel,
            SideBLabel = SideBLabel,
            SideBStartTrack = SideBStartTrack,
            Locked = Locked,
            SourceMarkdownSnapshot = SourceMarkdownSnapshot,
        };
        copy.Appearance.Font = Appearance.Font;
        copy.Appearance.TextColor = Appearance.TextColor;
        copy.Appearance.TapeColor = Appearance.TapeColor;
        copy.Appearance.StickerColor = Appearance.StickerColor;
        copy.Appearance.FrontArtworkPath = Appearance.FrontArtworkPath;
        copy.Appearance.BackArtworkPath = Appearance.BackArtworkPath;
        foreach (var kv in UnknownFields) copy.UnknownFields[kv.Key] = kv.Value;
        copy.LicenseDocuments.AddRange(LicenseDocuments);
        foreach (var track in Tracks)
        {
            copy.Tracks.Add(new MixTrack
            {
                Id = track.Id,
                Title = track.Title,
                Codename = track.Codename,
                License = track.License,
                Source = track.Source,
                LicenseDocumentPath = track.LicenseDocumentPath,
                Duration = track.Duration,
                TransitionIn = track.TransitionIn,
                Loop = track.Loop,
                OriginalSourcePath = track.OriginalSourcePath,
                PackagedAudioPath = track.PackagedAudioPath,
                SourceExtension = track.SourceExtension,
                SourceSha256 = track.SourceSha256,
                EncodedProfile = track.EncodedProfile,
            });
        }
        return copy;
    }
}
