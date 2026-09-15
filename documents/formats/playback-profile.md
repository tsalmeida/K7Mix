# Playback profile

Inspected against Running Wild `frontend/sdl/music.c` and `music.h`.

The game opens an SDL audio device at **44100 Hz, stereo, signed 16-bit**. Shipped soundtrack files are Ogg Vorbis, decoded on every platform by vendored **stb_vorbis**. Seek math multiplies seconds by `RWM_SAMPLE_RATE` (44100), so a packaged file at any other rate would play at the wrong speed. Media Foundation, used for MP3/WAV/FLAC on Windows, is configured to the same 44100/stereo target.

K7 Mix therefore encodes every packaged track as:

| | |
| --- | --- |
| Container | Ogg |
| Codec | Vorbis (libvorbis) |
| Sample rate | 44100 Hz |
| Channel layout | stereo |
| Quality | q5 (`ffmpeg -q:a 5`) |
| Profile id | `ogg-vorbis-44100-stereo-q5` |

This is gapless under stb_vorbis. MP3 is not used as the packaged format.

## Import decoders

A matching extension is not a promise that the file will import.

| Extension | Decoder |
| --- | --- |
| `.wav` | PCM / IEEE float via NAudio |
| `.mp3` | MPEG Layer III via Windows Media Foundation |
| `.flac` | FLAC via Media Foundation when present |
| `.ogg` / `.oga` | Ogg Vorbis via NVorbis. Copied without re-encoding when already 44100 Hz stereo |
| `.mid` / `.midi` | Standard MIDI File, rendered by the bundled GM-style synthesizer, then encoded |

Not accepted: Opus-in-Ogg, Speex, MP4/AAC as a packaged format, WMA, tracker modules, encrypted files.

MIDI rendering honours GM program changes and channel-10 percussion. SysEx, SMF type 2, SMPTE time division, pitch bend and unlisted controllers are reported and not guessed. MIDI is never stored in the package; only the render is.

Original source files stay on disk. Re-saving a mix does not re-encode a packaged track that is already on this profile and whose source has not changed.
