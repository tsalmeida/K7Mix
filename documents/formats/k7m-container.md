# `.k7m` container — format version 1

A `.k7m` file is a ZIP archive. It is not encrypted. It is self-contained: after the source folder is removed, the package still plays.

```
manifest.json          authoritative for playback and editing
audio/trk_<id>.ogg     one file per track, playback profile
artwork/front.png      optional
artwork/back.png       optional
source/entry.md        imported Markdown snapshot (may be stale)
licenses/NOTICE.txt    packaging is not a licence grant
licenses/…             accompanying licence documents
```

Internal paths and track ids are stable across re-saves. Track ids are assigned when a track is added and survive reordering.

`manifest.json` names format `k7m` and `formatVersion` 1. An unsupported version is rejected with a clear message. Saving writes a temporary file and replaces the destination only after success; a previous file is preserved if saving fails.

`locked: true` instructs K7 Mix and compatible players not to change the mix. It is not DRM.

See `playback-profile.md` for the audio inside `audio/`.
