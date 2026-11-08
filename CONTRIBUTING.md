# Contributing to K7 Mix

Thank you for helping. Two rules come before anything else:

1. **Never commit music you may not redistribute.** No `.k7m` packages, no audio, no artwork you did not make. Tests build their audio in code (see `tests/K7Mix.Tests`), and `.gitignore` refuses `.k7m` files, but it is not a guarantee. Packaging a track as a `.k7m` file does not grant anyone the right to share it.
2. **Keep licence, source and attribution text.** K7 Mix carries a track's licence and attribution through import, the manifest and the printed insert. A change must never drop or rewrite that text.

## Working on it

```powershell
dotnet build K7Mix.slnx -c Release
dotnet test  K7Mix.slnx -c Release
```

Encoding needs `ffmpeg` with libvorbis on `PATH`; printing the insert needs `typst`.

- The format, Markdown import, encoding, duration math and the transport belong in `src/K7Mix.Core`, with a test. It stays free of Avalonia; the window (`src/K7Mix`) stays thin.
- The `.k7m` container and the Markdown contract are specified in `documents/formats/`. A change to either one is a change to that document first, and it keeps old packages readable.
- Skip always honours the configured transition into the next track. That is the point of a tape, and it is not an option to add.
- The look follows the Tekhnexos companion design standard: Roboto, Tailwind colours, the classes in `src/K7Mix/Design/Design.axaml` (`page`, `section`, `pill`, `caption`, ...) rather than ad-hoc styling, and pills with a Font Awesome icon in the pill's hue. `src/K7Mix/Design` is copied from the reference implementation and is not edited here. Rose (`#ec003f`) belongs on the icon only.
- Only Font Awesome **Free** glyphs may be used; the Pro face is not redistributable. Check a new codepoint exists in `src/K7Mix/Assets/Icons/fa-solid-900.ttf`.
- A new title face must come with a licence that allows embedding it in the app and in a printed PDF, and its notice goes beside the file.
- Write commit messages as a plain sentence saying what changed, then why.

## Reporting a bug

Attach the mix's Markdown (with the track names replaced if you like) or describe the tracks' formats, lengths and transitions. Please do not attach the audio itself.
