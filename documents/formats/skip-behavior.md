# Skip behaviour

This is a product rule. It is not optional and there is no setting to disable it.

When Next is pressed, K7 Mix (and any compatible player, including Running Wild once it loads `.k7m` files) starts the **configured transition** from the current track to the next one.

- `none` is a zero-duration transition. The next track begins immediately. No decoder gap is added. Silence already in the recording is kept.
- `crossfade Ds` starts immediately and lasts D seconds. The displayed current track and side change when that crossfade finishes.
- `fadeout Ds` fades the outgoing track over D seconds, then the next track begins.
- `silence Ds` inserts D seconds of silence, then the next track begins.

While a transition is running, further Next requests are **ignored**, not queued. Play/Pause and Stop remain available. If Next is pressed during an automatic end-of-track transition, that transition is allowed to finish; it is not restarted.

Listener controls in the first version: Play/Pause, Stop, Next. No Previous, no seeking, no direct track selection.

After the final track, playback ends. It does not loop.

## Game-driven codenames

`RequestCodename` is the same rule as Next. A nonempty codename is unique within a mix. Asking for `desert` while `deep-forest` is playing starts the configured transition from the current track toward that request. It is never treated as an instantaneous skip. A request received while a transition is already running is ignored.
