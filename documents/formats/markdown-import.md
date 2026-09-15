# Tekhnexos Markdown import — contract version 1

This contract is independent of the `.k7m` container version. A change to one is not a change to the other.

K7 Mix reads a **standalone entry** export:

- The level-one heading is the mix title.
- Level-two headings are field names. The value is every line until the next heading of the same or higher level.
- Multiline values are kept. Hexadecimal colours such as `#000000` are field content, not markup.

A **dictionary** export (level-one dictionary title, level-two entry title, level-three fields) is accepted when it contains a single entry. The entry title becomes the mix title.

## Recognised fields

| Heading | Maps to |
| --- | --- |
| Description | Mix description (printed on the insert) |
| Link | Mix source URL |
| Text for side A | Side A label (falls back to the mix title when blank) |
| Text for side B | Side B label (falls back to the mix title when blank) |
| Font | Cassette title face: Cousine, Roboto, Rock Salt, Fixedsys |
| Text color | Title ink, `#RGB` or `#RRGGBB` |
| Tape color | Cassette plastic |
| Sticker color | Cassette paper label |
| License | Mix-level licence information |
| Transitions in csv format | Boundaries from track *i* to *i+1* |
| Tracks in csv format | Ordered tracks (extension; see below) |
| Front artwork | Relative path to front jewel-case art |
| Back artwork | Relative path to back jewel-case art |
| Side B starting track | One-based index of the first track on side B |

Missing optional fields take defaults: Roboto, text `#000000`, tape `#1E1E22`, sticker `#CDBA9E`, all tracks on side A, no artwork, no tracks.

Invalid supplied values produce an error naming the field. Unknown headings are stored on the mix and are never treated as playback instructions. A recognised heading that appears twice is ambiguous and must be corrected.

The imported Markdown is kept as a source snapshot inside the package. After edits in K7 Mix it may be out of date. The manifest is authoritative.

## Tracks in csv format

Header and rows: `filename,title,codename,license,source`. Row order is playback order. `filename` is required and is resolved inside the Markdown file's folder; paths that leave that folder are rejected. Other cells may be empty. CSV quoting covers commas, quotation marks and embedded line breaks.

The supplied Summer bangers export has cassette metadata and no track list. That is valid: import it, then add tracks in K7 Mix.

## Transitions in csv format

Comma-separated tokens, one per boundary. For *N* tracks there are max(*N*−1, 0) boundaries.

Tokens, after trimming:

- `none`
- `crossfade Ds`
- `fadeout Ds`
- `silence Ds`

`D` is a positive duration in seconds and may contain decimals. Empty cells between commas are `none` and keep their position. Missing trailing values default to `none`. Excess trailing values are ignored. Either adjustment is a non-blocking import summary. Unrecognised tokens or invalid durations require correction.

## Local media

Artwork and audio are resolved inside the selected source folder. Missing files are named precisely. Before packaging, the app shows the interpreted metadata, ordered tracks, transitions and validation issues.
