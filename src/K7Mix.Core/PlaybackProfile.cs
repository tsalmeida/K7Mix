namespace K7Mix.Core;

/// <summary>
/// The one playback profile every packaged track is encoded to. Chosen to match
/// RunningWild's music device: S16 stereo at 44100 Hz, decoded by stb_vorbis.
/// </summary>
public static class PlaybackProfile
{
    public const string Id = "ogg-vorbis-44100-stereo-q5";
    public const string Container = "Ogg";
    public const string Codec = "Vorbis";
    public const int SampleRate = 44100;
    public const int Channels = 2;
    public const string ChannelLayout = "stereo";
    public const string SampleFormat = "S16";
    public const int VorbisQuality = 5;
    public const string FileExtension = ".ogg";

    public const double PreferredMaximumMinutes = 90;
    public const double HardMaximumMinutes = 120;

    public static TimeSpan PreferredMaximum => TimeSpan.FromMinutes(PreferredMaximumMinutes);
    public static TimeSpan HardMaximum => TimeSpan.FromMinutes(HardMaximumMinutes);

    public static string Summary =>
        $"{Container} {Codec}, {SampleRate} Hz, {ChannelLayout}, quality q{VorbisQuality} (VBR). " +
        "Decoded as signed 16-bit PCM. Matching RunningWild (stb_vorbis, RWM_SAMPLE_RATE 44100).";
}
