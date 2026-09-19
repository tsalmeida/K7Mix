using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace K7Mix.Core;

public static class K7mPackage
{
    public const string ManifestName = "manifest.json";
    public const string SourceSnapshotName = "source/entry.md";
    public const int CurrentVersion = MixDocument.ManifestVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Save(MixDocument mix, string path, bool lockedCopy, IAudioEncoder encoder)
    {
        if (mix.Locked && !lockedCopy)
            throw new MixValidationException("This mix is locked. Export a locked copy from the editable original, or open an editable file.");

        if (!MixValidator.CanExport(mix, out var issues))
            throw new MixValidationException(issues.ToString());

        var working = mix.Clone();
        working.Locked = lockedCopy || mix.Locked;

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(directory))
            throw new MixValidationException("The output path could not be resolved.");
        Directory.CreateDirectory(directory);

        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var backup = File.Exists(path) ? path + ".bak-" + Guid.NewGuid().ToString("N") : null;
        try
        {
            if (File.Exists(path) && backup is not null)
                File.Copy(path, backup, overwrite: false);

            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                PackAssets(working, zip, encoder);
                var manifest = ManifestDto.From(working);
                var json = JsonSerializer.Serialize(manifest, JsonOptions);
                WriteText(zip, ManifestName, json);
                if (!string.IsNullOrWhiteSpace(working.SourceMarkdownSnapshot))
                    WriteText(zip, SourceSnapshotName, working.SourceMarkdownSnapshot);
                WriteText(zip, "licenses/NOTICE.txt",
                    LegalNotice.PackagingDoesNotGrantPermission + Environment.NewLine + Environment.NewLine +
                    LegalNotice.LockIsNotEncryption + Environment.NewLine);
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        catch
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { /* keep the previous file */ }
            }
            if (backup is not null && File.Exists(backup) && !File.Exists(path))
                File.Move(backup, path);
            throw;
        }
        finally
        {
            if (backup is not null && File.Exists(backup) && File.Exists(path))
            {
                try { File.Delete(backup); } catch { /* leftover backup is harmless */ }
            }
        }
    }

    public static MixDocument Load(string path, string extractDirectory)
    {
        if (!File.Exists(path))
            throw new MixValidationException($"Package not found: {path}");

        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(path, extractDirectory, overwriteFiles: true);
        var manifestPath = Path.Combine(extractDirectory, ManifestName);
        if (!File.Exists(manifestPath))
            throw new MixValidationException("This file is not a .k7m package: manifest.json is missing.");

        var json = File.ReadAllText(manifestPath, Encoding.UTF8);
        var dto = JsonSerializer.Deserialize<ManifestDto>(json, JsonOptions)
                  ?? throw new MixValidationException("manifest.json could not be read.");
        if (!string.Equals(dto.Format, MixDocument.FormatName, StringComparison.OrdinalIgnoreCase))
            throw new MixValidationException($"Unsupported format '{dto.Format}'.");
        if (dto.FormatVersion < 1 || dto.FormatVersion > CurrentVersion)
        {
            throw new MixValidationException(
                $"Unsupported .k7m format version {dto.FormatVersion}. " +
                $"This app reads versions 1 to {CurrentVersion}.");
        }

        var mix = dto.ToDocument(extractDirectory);
        var snapshot = Path.Combine(extractDirectory, SourceSnapshotName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(snapshot))
            mix.SourceMarkdownSnapshot = File.ReadAllText(snapshot, Encoding.UTF8);
        ValidateRequiredFiles(mix, extractDirectory);
        foreach (var track in mix.Tracks)
        {
            if (track.Duration > TimeSpan.Zero) continue;
            var audio = track.OriginalSourcePath
                ?? (string.IsNullOrWhiteSpace(track.PackagedAudioPath)
                    ? null
                    : Path.Combine(extractDirectory, track.PackagedAudioPath.Replace('/', Path.DirectorySeparatorChar)));
            if (string.IsNullOrWhiteSpace(audio) || !File.Exists(audio)) continue;
            try { track.Duration = FfmpegAudioEncoder.ProbeDuration(audio); }
            catch { /* leave zero; the player will report it */ }
        }
        return mix;
    }

    private static void ValidateRequiredFiles(MixDocument mix, string root)
    {
        foreach (var track in mix.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.PackagedAudioPath))
                throw new MixValidationException($"Track '{track.Title}' has no packaged audio path.");
            var audio = Path.Combine(root, track.PackagedAudioPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(audio))
                throw new MixValidationException($"Packaged audio missing: {track.PackagedAudioPath}");
        }
    }

    private static void PackAssets(MixDocument mix, ZipArchive zip, IAudioEncoder encoder)
    {
        for (var i = 0; i < mix.Tracks.Count; i++)
        {
            var track = mix.Tracks[i];
            var dest = $"audio/{track.Id}{PlaybackProfile.FileExtension}";
            var source = track.OriginalSourcePath ?? AbsoluteFrom(null, track.PackagedAudioPath);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new MixValidationException($"Track '{track.Title}' has no audio file to package.");

            using var output = new MemoryStream();
            encoder.EncodeToProfile(source, output, track);
            WriteBytes(zip, dest, output.ToArray());
            track.PackagedAudioPath = dest;
            track.EncodedProfile = PlaybackProfile.Id;
        }

        mix.Appearance.FrontArtworkPath = PackOptionalImage(
            zip, mix.Appearance.FrontArtworkPath, "artwork/front.png", JewelCaseLayout.FrontArtAspect);
        mix.Appearance.BackArtworkPath = PackOptionalImage(
            zip, mix.Appearance.BackArtworkPath, "artwork/back.png", JewelCaseLayout.BackArtAspect);

        if (!string.IsNullOrWhiteSpace(mix.LicenseDocumentPath) && File.Exists(mix.LicenseDocumentPath))
        {
            var name = "licenses/mix" + Path.GetExtension(mix.LicenseDocumentPath);
            WriteBytes(zip, name, File.ReadAllBytes(mix.LicenseDocumentPath));
            mix.LicenseDocuments.Add(name);
        }

        foreach (var track in mix.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.LicenseDocumentPath) || !File.Exists(track.LicenseDocumentPath))
                continue;
            var name = $"licenses/{track.Id}{Path.GetExtension(track.LicenseDocumentPath)}";
            WriteBytes(zip, name, File.ReadAllBytes(track.LicenseDocumentPath));
            mix.LicenseDocuments.Add(name);
        }
    }

    /// <summary>
    /// Packs artwork already cropped to the panel it prints on, so the package and
    /// the printed insert agree and nothing downstream has to guess at the framing.
    /// Cropping an image that is already at the target proportion keeps all of it,
    /// which is what makes re-saving a package leave the artwork alone.
    /// </summary>
    private static string? PackOptionalImage(ZipArchive zip, string? path, string dest, double aspect)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        WriteBytes(zip, dest, ArtworkCrop.CropToPng(path, aspect));
        return dest;
    }

    private static string? AbsoluteFrom(string? root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        if (Path.IsPathRooted(relative)) return relative;
        if (string.IsNullOrWhiteSpace(root)) return relative;
        return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void WriteText(ZipArchive zip, string name, string text) =>
        WriteBytes(zip, name, Encoding.UTF8.GetBytes(text));

    private static void WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

internal sealed class ManifestDto
{
    public string Format { get; set; } = MixDocument.FormatName;
    public int FormatVersion { get; set; } = MixDocument.ManifestVersion;
    public bool Locked { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
    public string? License { get; set; }
    public string? SideALabel { get; set; }
    public string? SideBLabel { get; set; }
    public int? SideBStartTrack { get; set; }
    public AppearanceDto Appearance { get; set; } = new();
    public List<TrackDto> Tracks { get; set; } = [];
    public List<string> LicenseDocuments { get; set; } = [];
    public Dictionary<string, string> UnknownFields { get; set; } = new();
    public string LegalNotice { get; set; } = Core.LegalNotice.PackagingDoesNotGrantPermission;

    public static ManifestDto From(MixDocument mix) => new()
    {
        Format = MixDocument.FormatName,
        FormatVersion = MixDocument.ManifestVersion,
        Locked = mix.Locked,
        Title = mix.Title,
        Description = mix.Description,
        SourceUrl = mix.SourceUrl,
        License = mix.License,
        SideALabel = mix.SideALabel,
        SideBLabel = mix.SideBLabel,
        SideBStartTrack = mix.SideBStartTrack,
        Appearance = new AppearanceDto
        {
            Font = mix.Appearance.Font.Id,
            TextColor = mix.Appearance.TextColor.ToHex(),
            TapeColor = mix.Appearance.TapeColor.ToHex(),
            StickerColor = mix.Appearance.StickerColor.ToHex(),
            FrontArtwork = mix.Appearance.FrontArtworkPath,
            BackArtwork = mix.Appearance.BackArtworkPath,
        },
        Tracks = mix.Tracks.Select(t => new TrackDto
        {
            Id = t.Id,
            Title = t.Title,
            Codename = string.IsNullOrWhiteSpace(t.Codename) ? null : t.Codename,
            License = t.License,
            Source = t.Source,
            Audio = t.PackagedAudioPath,
            DurationSeconds = t.Duration.TotalSeconds,
            SourceExtension = t.SourceExtension,
            SourceSha256 = t.SourceSha256,
            EncodedProfile = t.EncodedProfile,
            TransitionIn = t.TransitionIn.ToToken(),
            Loop = t.Loop,
        }).ToList(),
        LicenseDocuments = [.. mix.LicenseDocuments],
        UnknownFields = new Dictionary<string, string>(mix.UnknownFields, StringComparer.OrdinalIgnoreCase),
    };

    public MixDocument ToDocument(string root)
    {
        var mix = new MixDocument
        {
            Title = Title,
            Description = Description ?? "",
            SourceUrl = SourceUrl ?? "",
            License = License ?? "",
            SideALabel = SideALabel ?? "",
            SideBLabel = SideBLabel ?? "",
            SideBStartTrack = SideBStartTrack,
            Locked = Locked,
        };
        mix.Appearance.Font = CassetteFont.Parse(Appearance.Font);
        mix.Appearance.TextColor = HexColor.Parse(Appearance.TextColor ?? "#000000", "textColor");
        mix.Appearance.TapeColor = HexColor.Parse(Appearance.TapeColor ?? "#1E1E22", "tapeColor");
        mix.Appearance.StickerColor = HexColor.Parse(Appearance.StickerColor ?? "#CDBA9E", "stickerColor");
        mix.Appearance.FrontArtworkPath = Combine(root, Appearance.FrontArtwork);
        mix.Appearance.BackArtworkPath = Combine(root, Appearance.BackArtwork);
        foreach (var kv in UnknownFields) mix.UnknownFields[kv.Key] = kv.Value;
        mix.LicenseDocuments.AddRange(LicenseDocuments);
        foreach (var t in Tracks)
        {
            mix.Tracks.Add(new MixTrack
            {
                Id = string.IsNullOrWhiteSpace(t.Id) ? MixTrack.NewId() : t.Id,
                Title = t.Title ?? "",
                Codename = t.Codename ?? "",
                License = t.License ?? "",
                Source = t.Source ?? "",
                Duration = TimeSpan.FromSeconds(t.DurationSeconds),
                TransitionIn = TransitionParser.Parse(t.TransitionIn, mix.Tracks.Count),
                Loop = t.Loop,
                PackagedAudioPath = t.Audio,
                OriginalSourcePath = Combine(root, t.Audio),
                SourceExtension = t.SourceExtension,
                SourceSha256 = t.SourceSha256,
                EncodedProfile = t.EncodedProfile ?? PlaybackProfile.Id,
            });
        }
        if (FormatVersion < 2) MigrateTransitionsToVersion2(mix);
        return mix;
    }

    /// <summary>
    /// Version 1 stored a boundary on the outgoing track, so track <em>i</em> held
    /// the transition into track <em>i+1</em> and the last track's value was never
    /// used. Version 2 stores it on the incoming track, which frees the first
    /// track's slot for the loop back to the start. Shift each value one place
    /// forward and leave the loop back at none: a version 1 tape stopped at the end
    /// rather than looping, so it has no such boundary to carry over.
    /// </summary>
    private void MigrateTransitionsToVersion2(MixDocument mix)
    {
        for (var i = mix.Tracks.Count - 1; i >= 1; i--)
            mix.Tracks[i].TransitionIn = TransitionParser.Parse(Tracks[i - 1].TransitionOut, i);
        if (mix.Tracks.Count > 0)
            mix.Tracks[0].TransitionIn = TrackTransition.None;
    }

    private static string? Combine(string root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}

internal sealed class AppearanceDto
{
    public string? Font { get; set; }
    public string? TextColor { get; set; }
    public string? TapeColor { get; set; }
    public string? StickerColor { get; set; }
    public string? FrontArtwork { get; set; }
    public string? BackArtwork { get; set; }
}

internal sealed class TrackDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Codename { get; set; }
    public string? License { get; set; }
    public string? Source { get; set; }
    public string? Audio { get; set; }
    public double DurationSeconds { get; set; }
    public string? SourceExtension { get; set; }
    public string? SourceSha256 { get; set; }
    public string? EncodedProfile { get; set; }

    /// <summary>The boundary that brings playback into this track.</summary>
    public string? TransitionIn { get; set; }

    /// <summary>Play this track through twice before moving on.</summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Read only, for format version 1, which stored the boundary on the outgoing
    /// track. Never written: leaving it null keeps it out of the manifest.
    /// </summary>
    public string? TransitionOut { get; set; }
}
