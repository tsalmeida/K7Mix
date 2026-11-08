# K7 Mix

**Cassette-style mixes for games, packaged as unencrypted `.k7m` files.**

K7 Mix makes mixtapes the way a cassette made them: two sides, a running order, a transition into every track, a tape colour, a sticker and a hand-picked title face, and a jewel-case insert to print. The result is one self-contained `.k7m` file that K7 Mix plays like a tape, and that a game can play as its soundtrack. The first consumer is Running Wild.

Rose icon (`#ec003f`, Tailwind rose-600). A Tekhnexos companion app for Windows, C# and Avalonia, open source under the MIT licence.

Encoding is not locking. Encoding converts audio into the playback profile. Locking tells K7 Mix and compatible players not to change the published mix. Both editable and locked packages contain encoded audio and can be played. Exporting a locked copy leaves the editable original in place. The lock is application behaviour, not encryption, and not a guarantee against external modification.

Packaging, encoding or saving music as a `.k7m` file does not grant permission to copy, perform or distribute that music.

---

## Part of the Tekhnexos ecosystem

The look follows the Tekhnexos companion design standard (kept with Tekhnexos, not in this repository). Roboto throughout, embedded in `src/K7Mix/Assets/Fonts/`. The rose identity colour (`#ec003f`, Tailwind rose-600) lives on the icon and nowhere else inside the app.

`src/K7Mix/Design/` is copied from Elfwida, the reference implementation.

---

## Playback profile

Chosen to match Running Wild's music device (`frontend/sdl/music.c`: `RWM_SAMPLE_RATE 44100`, stereo S16, stb_vorbis):

| | |
| --- | --- |
| Container | Ogg |
| Codec | Vorbis |
| Sample rate | 44100 Hz |
| Channels | stereo |
| Quality | q5 (ffmpeg `-q:a 5`) |
| Decode target | signed 16-bit PCM |

Every packaged track uses that profile. Original sources are kept; MIDI is rendered only while authoring. Unchanged packaged tracks are not re-encoded when they already match.

Import is by decoder, not by extension. See `documents/formats/playback-profile.md`.

---

## Cassette appearance

Each mix has:

- a tape (plastic) colour
- a sticker colour
- a title colour
- a title, drawn in one of four bundled faces: **Cousine**, **Roboto**, **Rock Salt**, **Fixedsys**
- optional front and back artwork, cropped to the cassette-face proportion (100.4 × 63.8 mm, IEC 60094-1)

Those identifiers, font files and measurements are shared with Running Wild so a title looks the same in the app, in the game, and on the printed insert. After the four bundled faces (Cousine, Roboto, Rock Salt, Fixedsys) the Font list holds every standard Logarktos font from `L:\Sync\logarktos-fonts.json`, installed for the user by `L:\Sync\Install-LogarktosFonts.ps1`, when that catalog is present; without it the list is the four bundled faces. Those are set by family name on screen and on the insert, the package records the family beside the id so the mix opens on a computer without the catalog, and a game that does not carry the face shows the title in Roboto. The cassette sticker is not the jewel-case insert.

Print **Print insert** for a Typst PDF at actual size, with crop marks, folds, spine, safe areas, J-card and the two cassette labels.

---

## Duration and transitions

Preferred maximum 90 minutes, hard maximum 120 minutes, for the whole mix. Exceeding 90 minutes is a non-blocking notice; exceeding 120 minutes blocks export. The limits never change audio quality.

Each boundary is `none`, `crossfade Ds`, `fadeout Ds` or `silence Ds`. Skip always honours that boundary, including a zero-duration `none`. Further skip requests while a transition is running are ignored. Play/Pause and Stop remain available. After the last track, playback ends. There is no Previous, seeking, or direct track selection, and no way to disable transition-honouring skip.

---

## Install (Tekhnexos workstations)

```powershell
cd L:\Library\Coding\CSharp\K7Mix
.\install.bat
```

That publishes a self-contained `K7Mix.exe` to `C:\TKHN\K7Mix\K7Mix.exe`, rewrites the Start Menu shortcut and puts the folder on the user `PATH`. Mixes stay wherever you save them.

Not `%LOCALAPPDATA%\Programs` and not `L:\Apps`. `install.bat` is the Tekhnexos family's installer and needs that family's scripts; anyone else builds with `dotnet publish` (below).

## Build

```powershell
dotnet build K7Mix.slnx -c Release
dotnet test  K7Mix.slnx -c Release
```

```powershell
dotnet publish src\K7Mix\K7Mix.csproj -c Release -r win-x64 --self-contained true -o publish
```

Requirements: [.NET 9 SDK](https://dotnet.microsoft.com/download). Encoding packaged audio needs `ffmpeg` with libvorbis on `PATH`. Printing the insert needs `typst` on `PATH`.

---

## Layout

| Path | Role |
| --- | --- |
| `src/K7Mix.Core` | Format, Markdown import, encoding, transport — no Avalonia |
| `src/K7Mix` | Companion UI |
| `native/k7m_playback.h` | C surface Running Wild should grow toward |
| `documents/formats/` | Markdown contract and `.k7m` container, versioned separately |
| `fixtures/` | Representative Tekhnexos exports |
| `documents/game-integration/` | How a game plays `.k7m` tapes; the games' own assets and code stay with the games |

---

## Credits

Roboto, Roboto Slab, Roboto Condensed, Roboto Mono and Cousine (SIL Open Font License 1.1); Rock Salt (Apache License 2.0); Fixedsys Excelsior (public domain / CC0); Font Awesome Free 6 (font under the SIL OFL 1.1, icons under CC BY 4.0). Each notice sits beside its font. Audio through [NAudio](https://github.com/naudio/NAudio) and [NVorbis](https://github.com/NVorbis/NVorbis); encoding through `ffmpeg`, printing through [Typst](https://typst.app), both installed separately.

The software is open source; the music you put in a mix is not covered by it. Keep each track's licence, source and attribution with the mix (see `documents/formats/legal.md`).

## Licence

MIT. See [LICENSE](LICENSE). Contributions are welcome: see [CONTRIBUTING.md](CONTRIBUTING.md).
