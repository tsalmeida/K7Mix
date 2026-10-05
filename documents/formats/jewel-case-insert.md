# Jewel-case insert

The cassette **sticker** (the paper label on the shell) is not the jewel-case **insert** (the J-card that slides into the Norelco box).

## Shared measurements

| Piece | Size |
| --- | --- |
| Cassette shell (IEC 60094-1) | 100.4 × 63.8 × 12 mm |
| Jewel case (Norelco) | 109.5 × 69.8 × 16.5 mm |
| Front artwork crop | 100 / 64 = 1.563:1, the front panel |
| Back artwork crop | 70 / 64 ≈ 1.094:1, the back panel |
| J-card front | 100 × 64 mm |
| J-card spine | 12.5 × 64 mm |
| J-card back | 70 × 64 mm |
| Top / bottom flaps | 10 mm |
| Bleed | 3 mm |
| Safe area | 3 mm inside folds and trim |
| Cassette labels | 89 × 43 mm, side A and side B |

## Cropping

Artwork is never adjusted by hand and the user is never asked to frame it. Whatever picture is supplied is trimmed equally on its two longer sides until it matches the proportion of the panel it prints on, keeping the middle. A picture that is too wide loses its left and right edges; one that is too tall loses its top and bottom; a square loses a little of each panel's shorter dimension. Supplying a picture already at the target proportion is how someone takes control of the framing, because then there is nothing to trim.

The front and back panels are different widths, so each is cropped to its own proportion rather than to a shared one. Cropping happens when the mix is packaged, so the `.k7m` carries artwork already at the right shape and every reader of the package agrees on the framing; cropping again on re-save is a no-op. The on-screen preview and the PDF apply the same centre crop, so what the preview shows is what prints.

The Typst PDF is exported at actual size with crop marks. Print at 100 %, no "fit to page". Without artwork the insert uses the sticker colour, title and font, with the tape colour on the spine.

Title font identifiers are the same four used on the on-screen cassette and in Running Wild: `cousine`, `roboto`, `rock-salt`, `fixedsys`. Any other identifier is a standard Logarktos font; the insert sets it by the `fontFamily` the data carries, with Roboto behind it.
