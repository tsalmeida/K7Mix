namespace K7Mix.Core;

public static class LegalNotice
{
    public const string PackagingDoesNotGrantPermission =
        "Packaging, encoding or saving music as a .k7m file does not grant permission " +
        "to copy, perform or distribute that music. The licence of each track and of " +
        "the mix, as supplied by the author, is the only statement of what is allowed. " +
        "K7 Mix preserves that information; it does not evaluate or replace it.";

    public const string LockIsNotEncryption =
        "A locked .k7m file tells K7 Mix and compatible players not to change the published " +
        "mix. The lock is application behaviour. It is not encryption, DRM, or a guarantee " +
        "against external modification or extraction.";

    public const string SkipHonoursTransition =
        "Skipping a track always honours the configured transition, including a zero-duration " +
        "'none' transition. This cannot be disabled.";
}
