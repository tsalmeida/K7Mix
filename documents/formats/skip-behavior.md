# Skip behaviour

This is a product rule. It is not optional and there is no setting to disable it.

A transition belongs to the track it leads **into**, not the one it leaves. Track *i*'s transition is the boundary from track *i*−1 to track *i*, so it stays with that track through a reorder. The first track's transition is the **loop back to the start**: the only time it is crossed is when the tape reaches the end.

When Next is pressed, K7 Mix (and any compatible player, including Running Wild once it loads `.k7m` files) starts the transition belonging to the track being moved into.

- `none` is a zero-duration transition. The next track begins immediately. No decoder gap is added. Silence already in the recording is kept.
- `crossfade Ds` starts immediately and lasts D seconds. The displayed current track and side change when that crossfade finishes.
- `fadeout Ds` fades the outgoing track over D seconds, then the next track begins.
- `silence Ds` inserts D seconds of silence, then the next track begins.

While a transition is running, further Next requests are **ignored**, not queued. Play/Pause and Stop remain available. If Next is pressed during an automatic end-of-track transition, that transition is allowed to finish; it is not restarted.

Listener controls in the first version: Play/Pause, Stop, Next. No Previous, no seeking, no direct track selection.

## The end of the tape

After the final track the tape **loops back to the first**, crossing the first track's own transition. Playback does not stop and does not need to be restarted. Next on the last track takes the same boundary, so it wraps rather than being ignored.

## Looping tracks

A track marked `loop` plays through **twice** and then moves on. The repeat is seamless: the track restarts at zero with no transition, because a transition is a boundary between two different tracks and the repeat crosses none. A looping track therefore occupies twice its duration on the tape, and both its incoming transition and the one leaving it apply once, at the start of the first pass and the end of the second.

Next during the first pass abandons the repeat and takes the boundary normally.

## Game-driven codenames

`RequestCodename` is the same rule as Next. A nonempty codename is unique within a mix. Asking for `desert` while `deep-forest` is playing starts the transition belonging to `desert`, the track being moved into. It is never treated as an instantaneous skip. A request received while a transition is already running is ignored.
