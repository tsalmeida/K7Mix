# K7 Mix in games: the standard way to play `.k7m` tapes

How a video game plays K7 Mix packages, written from the two games that do it: **Running Wild** (*Earthbound Gladiator Rally*, `L:\Library\Coding\C89\RunningWild`) and **Knife and Card** (the Valliberum collections, `L:\Library\Coding\C89\KnifeAndCard`). Read this before adding K7 Mix tapes to a new game. The next game should follow it, not re-derive it.

Written 2026-09-18, when Knife and Card was brought back to its original soundtrack on K7 Mix tapes (see the case study at the end). Beside this file, on the author's machine, sit copies of what that work used. They belong to the games, which are not released, so they are kept out of the public repository (`.gitignore` lists them) and the paths below resolve only on that machine:

| Folder | What it holds |
| --- | --- |
| `assets/walkman/` | The Walkman: the painted plate (`TT-Sony-Walkman-plate.png`), its transparent cut for the table (`walkman.png`), the close-up's deck (`walkman-deck.png`), THE ONE key (`walkman-key-bezel.png` + `walkman-key-cap.png`), the ten stamped legends (`legends/`), and the cassette plate K7 Mix itself tints (`TT-Cassette-Tape.png`). |
| `assets/fonts/` | The four K7 Mix title faces (Cousine, Roboto, Rock Salt, Fixedsys Excelsior; the same files as `src/K7Mix/Assets/TitleFonts/`) and DSEG14 Classic, the Walkman's LCD face, each with its licence. |
| `assets/screenshots/` | What it looks like when it is right. |
| `tools/` | The generators: `generate_walkman_plates.py` (deck, key, legends), `make_walkman_art.py` (the table cut), `build_k7m_tapes.py` (a game's official-tape builder), `kac_midi_synth.py` (rendering MIDI with a game's own synth). |
| `reference/` | Knife and Card's C89 implementation: `kac_k7m.[ch]` (package reader), `kac_tapes.[ch]` (the engine), `kac_walkman.[ch]` (the Walkman). They need SDL2, SDL2_ttf, cJSON and `stb_vorbis.c` (both games vendor it). |

The container itself is specified in `../formats/k7m-container.md`, the audio in `../formats/playback-profile.md`, skip behaviour in `../formats/skip-behavior.md`. This document is about **playing** tapes in a game: the engine, how the game drives it, and the Walkman the player sees.

---

## 1. The model: one Walkman, one tape in it

- **All the game's music is on cassette tapes, and one Sony Walkman plays them.** Whatever tape is in the Walkman is the only music that sounds. There is no second music system beside it.
- **A tape is:**
  1. every `.k7m` file under the game's **music root**, a loose, player-visible folder (`music/` beside the exe, `open-assets/music/` in Running Wild). The game ships its **official** tapes in `music/tapes/`; a player who drops more `.k7m` files in has more tapes;
  2. **the Mixtape**: all the loose audio files under the music root (any subfolder) together make one more tape, played straight through as one continuous side, resumed across sessions, deaf to the game. Supported: `.ogg`, `.mp3`, `.m4a`, `.wav`, `.flac`, `.mid`.
- **EJECT takes the tape out and puts the next one in**, cycling through all of them (the `.k7m` tapes sorted by name, then the Mixtape). The tape in the deck is remembered.
- **A first boot** puts the game's default official tape in. A saved tape that has vanished falls back to the default, then to the first tape.
- **Music off** (a game's Music option) silences the Walkman; it does not unload the tape.

## 2. How the game drives the tape

A tape in the deck is always in one of two states.

### 2.1 Following the game (until the player touches it)

While it follows the game, **the game decides what plays**, and every change goes through the **boundary belonging to the track being moved into** (`transitionIn`: `none`, `crossfade Ds`, `fadeout Ds`, `silence Ds`), never an instant cut the tape did not ask for. A change requested while a transition runs **waits** and is asked again next frame. It is not queued and not dropped.

There are two ways to follow the game. A game picks one:

**A. By codename (Running Wild).** Each moment of the game has a slug (`menu`, an environment, an ending...). The game says which moment is on every frame; the tape plays the track whose `codename` matches, **looping** it. A moment the tape has no track for keeps the tape rolling. Official tapes must cover every moment, and no codename may repeat within a tape. Use this when the music should score *what is happening*.

**B. By shuffle (Knife and Card).** The game says which **table** is up every frame (`"launcher"`, or the game's title). The tape plays a **random track to its end**, and the boundary belonging to the track it picks next carries it there, **never the same one twice in a row**. Every change of table puts a fresh random track on. Codenames are ignored. Use this when the music is a **playlist**, the same everywhere, which is what Knife and Card always had.

### 2.2 Off the game: the player's tape

**The moment the player presses any transport key except volume** (play/pause, stop, previous, next, previous/next side, on the Walkman or on the keyboard's media keys), the tape stops following the game and plays by **K7 Mix's own rules**:

- each track in order from where it is, and **twice** when it is marked `loop`;
- every boundary through its configured transition, fired at the exact frame it needs (a crossfade starts its length before the outgoing track ends; `none` is gapless);
- a skip pressed during a transition is **ignored**;
- after the last track the tape **loops back to the first**, through the first track's own transition;
- previous: back to the top of the track if more than 3 s in, else the previous track;
- previous/next side: jump to the top of side A / side B (`sideBStartTrack`).

**Ejecting the tape and putting it back** makes it follow the game again. Volume never breaks it free.

## 3. The engine

Both games run the same engine (Running Wild `frontend/sdl/music.c`, Knife and Card `kac_core/kac_tapes.c`; `reference/`). Build yours on it rather than on a per-frame "play file" call:

- **Its own audio device and one decoder thread**, separate from the sound effects. The thread mixes **two decks** into a **ring buffer** that the SDL callback drains, so the game loop never stalls on a decode and a crossfade is just two decks with gain ramps.
- **The main thread posts orders** (a short queue, so two orders in one frame both land); a **busy** flag clears only when no order is pending and both decks have settled. That is what makes "a request during a transition waits" reliable.
- **A free-running tape arms a cue**: the next boundary, fired by the decoder thread at the exact frame (lead time = the crossfade/fadeout length, one block for `none`/`silence`).
- **Tracks stream straight out of the package.** Stored ZIP entries are read as file sections; deflated ones are inflated in memory. **No extraction cache.** (Running Wild's old cache let a re-extract clobber a file a deck still held open.)
- **`gainDb` is a playback level**, applied per deck. Licensed recordings are never re-encoded to fix loudness.
- **Size the reader for the tapes you ship.** K7 Mix sets no track count; Running Wild's reader holds 64 tracks and **silently dropped 25 of Knife and Card's 89** until the cap was raised to 256 and made to log. Your self-test must count the tracks it loaded against the tape's manifest.
- **Persist** (per user, never in the install folder): the tape in the deck, volume, the Music on/off, and the Mixtape's file and position.

## 4. The Walkman

The Walkman is the one music machine, so it is **an object on the game's table**, not a menu.

### 4.1 On the table: real size, partly in view

- **It lies in the top-right corner of every screen that has music**, drawn after the game, under nothing but modal overlays.
- **Real size.** It is drawn to scale against the game's own objects. A compact cassette is **100.4 x 63.8 mm** (IEC 60094). The plate's cassette window spans **65.7%** of the body's width, which puts the Walkman at about **152 mm across**. Beside a **63 x 88 mm** playing card that is 2.4 cards wide and about one card tall. The game reports its on-screen card width every frame (Knife and Card: `KacGameInterface.get_card_screen_width`); Running Wild uses its table's mm-to-px scale.
- **Only part of it is on screen.** It hangs off the top and right edges and shows its lower-left part (about 56% of its width and 62% of its height, capped to a share of the short screen edge) so it does not take the table over. The visible part is the click target.
- **The cassette's shell colour shows through the window. Its title does not.** The title is read in the close-up.
- **Art:** `assets/walkman/walkman.png`, cut from `TT-Sony-Walkman-plate.png` by `tools/make_walkman_art.py` (flood-fills the plate's black background from the edges, soft 1 px edge, crop). Keep a procedural fallback so the Walkman can never be missing.

*Screenshot, kept with the games: The Walkman at the top right of the Knife and Card launcher (`assets/screenshots/table-launcher.png`).*

*Screenshot, kept with the games: The same Walkman in a game, at that game's card size (`assets/screenshots/table-in-game.png`).*

### 4.2 The close-up: computer-drawn, not the photograph

Clicking the Walkman lifts it toward the camera (a short ease out of the corner, the table dimmed behind it). The close-up is **drawn**, not the photograph, and it is modal: it takes every click and key. Esc, Backspace or a click outside puts it back.

**Geometry**, in deck units (the rects the art and the code share):

| Part | Rect (x, y, w, h) |
| --- | --- |
| Deck body | 0, 0, 650, 432 |
| Cassette window | 45, 28, 560, 132 |
| LCD strip | 45, 172, 560, 58 |
| Keys, row 1 | x = 30 + i x 96, y 256, 86 x 58: previous side, previous, play/pause, stop, next, next side |
| Keys, row 2 | volume down 247, 334, 70 x 44; volume up 333, 334, 70 x 44; EJECT 550, 334, 70 x 44 |

Scale the whole deck uniformly to the window (Knife and Card: 60% of the width or 70% of the height, whichever is smaller).

**The deck** is `walkman-deck.png` (Sony blue, lit from above, brushed rim, anodised top bevel, every well empty and near-black) drawn to the exact deck rect, so its wells land under what the code draws.

**The cassette in the window** is the tape in the deck, drawn from its package's `appearance`. This is what makes a tape look the same in K7 Mix, in the game and on the printed insert:

1. **Shell** in `tapeColor`: rect (53, 34, 544, 120), radius 6. Lit top bevel (59, 38, 532, 4) at `tape/2 + 128`; shaded lip (59, 148, 532, 3) at `tape x 2/5`.
2. **Paper label** in `stickerColor`: (69, 46, 512, 30), radius 3.
3. **Reels** (radius 34) at (141, 104) and (509, 104): a dark hub ring (35, 30, 26), a (87, 78, 71) face, a dark centre and six spokes, spinning while it plays (one radian per 320 ms). The tape band between them: (141, 78, 368, 6), dark.
4. **Title** on the label, in `font` and `textColor`, centred in (73, 46, 504, 30), shaved onto a `..` tail when too wide (paper does not stretch). Faces: `cousine`, `roboto`, `fixedsys` at 16 units (Fixedsys on its 8 px grid); `rock-salt` at 20 units, top-anchored **12 units above** the label, because Rock Salt's glyph box carries a tall empty ascender band.
5. Everything above is **clipped to the window**: the cassette lives under the transparent door.
6. **EJECT** lifts the cassette 10 units for a moment (still inside the window) and draws the open door's edge across the bottom.
7. **The Mixtape** is always hot fuchsia (181, 70, 126) on a cream label (211, 204, 199), with what is playing handwritten in Rock Salt.
8. **No tape at all**: an empty deck and "no music found" written on the door.

**The LCD** is a real 14-segment display, in DSEG14 Classic at 13 units, green (39, 188, 93):

- every character sits in a **fixed cell** as wide as the all-segments glyph `~`, and a **ghost row** of `~` at alpha 34 fills the whole window (the unlit segments);
- top row, in thirds: `TRK nn/NN`, `VOL nn` (or `MUTE`), and the state `PLAY` / `PAUSE` / `STOP` / `EJECT` (Knife and Card shows `SHUF` while the table shuffles the tape);
- bottom row: `m:ss/m:ss` in (55, 202, 178, 22), and a progress bar (235, 208, 358, 8).

**The keys** are all one key (`generate_walkman_plates.py`, after Running Wild's arcade-button doctrine):

- the **bezel** (`walkman-key-bezel.png`) is hardware and never tinted;
- the **cap** (`walkman-key-cap.png`, white) is tinted the deck's cream keycap plastic (214, 210, 198), **never the tape's colour**: hardware does not change colour with the cassette in it. It lights (240, 236, 226) on hover or keyboard focus and darkens while pressed;
- the **symbol** (`legends/`, a contour band 4.5% of a 256 px canvas wide) is **stamped** into the cap, sized 3/5 of the key's short edge, twice: a lit lip (255, 250, 238, alpha 105) one pixel down-right, then the groove (38, 34, 30, alpha 235) on top. The light comes from the upper left. Play shows pause while playing.

**Input in the close-up.** Left click presses a key on release (press and release on the same key). The wheel is volume. Arrows/WASD move the keyboard focus to the nearest key in that direction, Enter presses it, Space is play/pause, E is EJECT. The game's fullscreen keys pass through.

*Screenshot, kept with the games: Memory Challenges' tape in the close-up (`assets/screenshots/closeup-memory-challenges.png`).*

*Screenshot, kept with the games: Battle Mechanism's tape: Rock Salt on red (`assets/screenshots/closeup-battle-mechanism.png`).*

*Screenshot, kept with the games: The Mixtape (`assets/screenshots/closeup-mixtape.png`).*

Running Wild's own close-up, for comparison (same deck, keys, readout):

*Screenshot, kept with the games: Running Wild (`assets/screenshots/running-wild-closeup.png`).*

### 4.3 The keyboard's media keys

- **They work everywhere, open or not, and before any game logic sees them.**
- **Transport keys** drive the Walkman: play/pause, stop, next and previous (SDL `SDLK_AUDIOPLAY`, `AUDIOSTOP`, `AUDIONEXT`, `AUDIOPREV`). They count as transport keys: a following tape comes off the game.
- **Volume up, volume down and mute** (`SDLK_VOLUMEUP`, `VOLUMEDOWN`, `AUDIOMUTE` / `MUTE`) set the **whole game's volume**, sound effects and tape alike, applied on top of the Walkman's own VOL. Steps of 5%; mute toggles; a volume key unmutes. Persist the volume; let mute last a session.
- **Confirm each key** briefly. The Walkman's own readout is off-screen in the corner, so Knife and Card shows a small dark slip at the top of the table: `Volume 95%`, `Sound muted`, `Playing · <track>`.
- **Swallow the key-up too**, so no game reacts to it.
- **Testing:** never post the real media keys to the game window from a script. Windows also routes them to the system volume and to every other media player. Push a synthetic SDL key event through the real event loop instead (Knife and Card: `KAC_SHOT_KEY=AudioNext`).

*Screenshot, kept with the games: The media-key slip (`assets/screenshots/media-key-slip.png`).*

## 5. Official tapes

- **Built by a script in the game's repo, never by hand, and never checked in.** `.k7m` output is gitignored; the builder is the tracked source (`tools/build_k7m_tapes.py`). It is deterministic: stable track ids, a fixed ZIP timestamp, every entry STORED, `manifest.json` first, a `licenses/NOTICE.txt`, atomic replace.
- **The playback profile** is Ogg Vorbis, 44.1 kHz, stereo, q5 (`ffmpeg -ac 2 -ar 44100 -c:a libvorbis -q:a 5`). A licensed recording already on the profile is copied byte for byte. Level is matched with a per-track `gainDb` (Running Wild: -19 LUFS, true peak at most -1 dBTP), never by re-encoding.
- **Music the game renders itself** (MIDI) is rendered with **the game's own synth**, ported faithfully (`tools/kac_midi_synth.py`), so the tape sounds exactly as the game always did. Its level may be set in the audio, since it is the game's own render.
- **Licensed music is found by catalogue title** in Phonotek's package and read from where the row says. The tape's use is recorded by ticking the game's project in the row's `tags_1`, and the tag is removed again when a tape stops using the cue.
- **K7 Mix's duration rule** (preferred 90, hard 120 minutes) is enforced by K7 Mix's **export** validator. A game's builder should honour it. When a request says otherwise, the builder says so loudly and the game's docs record the exception (the Knife and Card tapes, below).
- **Ship**: `PackageRelease` copies exactly the release's official tapes into `music/tapes/` and throws if one is missing. Nothing else in the music folder ships. Licensed music ships only inside tapes, never as loose files. A `.k7m` is not encrypted by design: the format's legal note travels with it.
- **Credits**: every composer or pack that asks for a credit gets it in the game's notices.

## 6. Verification

Music is heard, not seen, and it changes over minutes, so screenshots are not enough. Both games have the same two harnesses:

- **Self-test** (`KAC_TEST_K7M=1` / `RW_TEST_K7M=1`): every tape opens; every track decodes out of the package at 44.1 kHz stereo, at its start and after a mid-track seek; official tapes are present; their track count matches the playlist; codenames are unique (and, in codename mode, cover every moment). It writes a report and returns the number of problems.
- **Live timeline**: drive the real engine through a script and log the deck four times a second. For codename mode: moment changes, a change arriving mid-transition that waits, NEXT breaking free, a later moment change that must NOT move the tape, the harness sending the track to 4 s before its end so its own boundary fires, EJECT. For shuffle mode: table changes bring new random pieces, a shuffled piece's end leads to another random piece, NEXT takes it off the shuffle (track + 1), a table change that must not move it, the free tape's end leads to track + 1, EJECT puts it back on the shuffle.
- **Screenshots**: the corner object in the launcher and in a game, the close-up for each official tape's font, the Mixtape, and the media-key slip.

## 7. Case study: Knife and Card, 2026-09-18

The request, in the user's words:

> We had a collection of midi tracks and they would play randomly as we played either game. Let's go back to that, however, using the k7m format. We should have one k7m file per compiled game -- meaning one for Valliberum Cards, another for Valliberum Battle Mechanism.

What was there before tapes: 89 Bach MIDI files (Mutopia Project) played by the game's piano synth, a random piece at start, a new random piece on every launch of or return from a game and whenever one ended, never the same twice in a row, the same playlist in every game and in both collections. The first K7 Mix port (2026-09-17) had given each game its own codenamed tracks, and the battle collection licensed cues instead. The user rejected that.

What it became:

- **One tape per Steam release**: `valliberum-memory-challenges.k7m` (Cousine, ink #191410 on a #F2E9D3 label, #334D99 shell) and `valliberum-battle-mechanism.k7m` (Rock Salt, #191410 on #E0CDAB, #722424 shell). Both hold the same 89 pieces, one per track, in playlist order, with side B starting at track 56. Every boundary is `none`, because the synth render keeps its two-second tail. That is exactly how one piece used to follow the next.
- **Shuffle mode** (2.1 B) reproduces the old playlist exactly. The player's keys take the tape off the shuffle, as in Running Wild.
- **148 minutes**: over K7 Mix's 120-minute authoring maximum, knowingly, because the request was the whole old playlist on one tape. The K7 Mix app would refuse to re-export these tapes. Dropping pieces from the builder's `PIECES` list is the switch if the rule should win.
- **The track cap** in the shared reader went from 64 to 256 (see 3).
- **The Walkman** became real-size and partly in view on the table with no title (4.1), and the close-up became Running Wild's drawn deck with the tape drawn from its manifest (4.2). The media keys and the whole-game volume were added (4.3).

## 8. Checklist for the next game

1. Vendor `stb_vorbis.c`, port `kac_k7m` + `kac_tapes` (or Running Wild's `k7m.c` + `music.c`), and size `K7M_TRACK_MAX` for your tapes.
2. Pick codename mode or shuffle mode; call the engine every frame with the moment or the table.
3. Write the builder; build the official tapes; gitignore the output; make packaging copy exactly them.
4. Put the Walkman on the table at real scale, partly in view, no title; the drawn close-up from `generate_walkman_plates.py`; the four title faces and DSEG14.
5. Route the media keys before the game; whole-game volume and mute; a confirmation slip.
6. Credits; Phonotek tags for licensed cues.
7. Self-test, live timeline, screenshots. Record the soundtrack in the game's plan.
