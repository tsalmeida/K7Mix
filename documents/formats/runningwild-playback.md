# Running Wild playback interface

Playback logic lives in `K7Mix.Core` (`MixTransport`, `MixPlayer`, `IMixPlayback`) and must stay independent of Avalonia.

Running Wild today plays loose files through `frontend/sdl/music.c`: two decks, stb_vorbis for Ogg, Media Foundation for other compressed formats, a sine-voice MIDI synth. A `.k7m` package is a ZIP of those Ogg files plus a manifest. The C header `native/k7m_playback.h` is the surface to grow toward:

- `rw_k7m_load` / `rw_k7m_unload`
- metadata: title, colours, font id, artwork paths
- `rw_k7m_play_pause`, `rw_k7m_stop`, `rw_k7m_next`
- `rw_k7m_request_codename`
- `rw_k7m_is_playing`, `rw_k7m_is_transitioning`, `rw_k7m_track_index`, `rw_k7m_side`

`rw_k7m_next` and `rw_k7m_request_codename` follow `documents/formats/skip-behavior.md`. They are never instantaneous jumps.

Until that C loader exists, a packaged mix can still be verified with Running Wild's decoder: `native/k7m_stb_vorbis_probe.c` compiles against `frontend/sdl/third_party/stb_vorbis.c` and must print `rate=44100 channels=2` for every packaged track.

Cassette colours, title font identifiers and the noun-cassette silhouette (`assets/icons/noun-cassette-7416933.svg`) are the same in both projects.
