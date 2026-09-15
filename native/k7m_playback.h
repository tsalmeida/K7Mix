/* k7m_playback.h — the C surface RunningWild should grow toward.
 *
 * Playback logic is independent of Avalonia. A .k7m file is a ZIP with
 * manifest.json (authoritative) and audio/*.ogg encoded as:
 *   Ogg Vorbis, 44100 Hz, stereo, quality q5
 * which stb_vorbis already decodes. Do not add a gap between tracks when the
 * transition is 'none'; keep silence that is already in the recording.
 *
 * Skip / Next ALWAYS honours the configured transition, including a zero-
 * duration 'none'. This cannot be disabled. Further Next requests while a
 * transition is running are ignored, not queued. Play/Pause and Stop remain
 * available. After the final track, playback ends without looping. There is
 * no Previous, seeking or direct track selection in the first version.
 *
 * rw_k7m_request_codename is the game-driven lookup. It is not an instantaneous
 * skip. If a transition is already running the request is ignored.
 */
#ifndef K7M_PLAYBACK_H
#define K7M_PLAYBACK_H

#ifdef __cplusplus
extern "C" {
#endif

int  rw_k7m_load(const char *path);
void rw_k7m_unload(void);
const char *rw_k7m_title(void);
const char *rw_k7m_tape_color(void);     /* "#RRGGBB" */
const char *rw_k7m_sticker_color(void);
const char *rw_k7m_text_color(void);
const char *rw_k7m_font_id(void);        /* cousine | roboto | rock-salt | fixedsys */
const char *rw_k7m_front_artwork(void);  /* extracted path, or "" */
const char *rw_k7m_back_artwork(void);

void rw_k7m_play_pause(void);
void rw_k7m_stop(void);
int  rw_k7m_next(void);                  /* 1 if a transition started, 0 if ignored */
int  rw_k7m_request_codename(const char *codename);
int  rw_k7m_is_playing(void);
int  rw_k7m_is_transitioning(void);
int  rw_k7m_track_index(void);           /* displayed track, 0-based */
int  rw_k7m_side(void);                  /* 0 = A, 1 = B */
int  rw_k7m_locked(void);

#ifdef __cplusplus
}
#endif
#endif /* K7M_PLAYBACK_H */
