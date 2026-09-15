# Legal notes

K7 Mix stores licence, source and attribution text, and can include accompanying licence documents in the package. It does not decide whether a use is allowed.

## What the app will not claim

Packaging, encoding, locking or saving a mix does not grant a right to copy, perform, stream or ship the music. Buying a file, pointing K7 Mix at it, or putting the result in a game folder does not change the licence.

The `.k7m` lock is application behaviour. It is not encryption and not a guarantee against extraction.

## What authors still have to do

- Confirm that each track's licence covers the intended game use and distribution **before** shipping the package.
- Keep attribution in the mix, on the insert, and in the game where the licence requires it (Creative Commons BY, many royalty-free libraries).
- Do not ship a mix marked **Non-Commercial Use Only** with a commercial game unless a separate licence says otherwise.
- Keep licence documents with the mix. K7 Mix copies them into `licenses/` when asked; stripping them is a choice the author would have to make elsewhere.
- Title fonts (Cousine, Roboto, Rock Salt, Fixedsys) have their own notices in `src/K7Mix/Assets/TitleFonts/`. Bundling those faces in the app and in a printed insert follows those notices. It does not license the music.
- MIDI input is rendered through a bundled synthesizer. That render is a new audio file. The MIDI composition is still under its own licence; the synthesizer does not replace it.

## Other requirements worth keeping in view

- Public-domain or CC0 recordings are the straightforward path for a game soundtrack that must ship.
- A URL in **Link** (a Spotify playlist, a store page) is a pointer, not a licence.
- The Markdown snapshot inside a package can go stale after edits. The manifest is what a player reads; the snapshot is evidence of the import, not a live contract.
- If a third party modifies a `.k7m` file, K7 Mix cannot stop them. Do not describe the format as protected.
