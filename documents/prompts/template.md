# Title

## Introduction

We are working on K7 Mix, a Tekhnexos companion desktop app built in C# and Avalonia.

K7 Mix authors cassette-style mixes and packages them as unencrypted `.k7m` files (ZIP + versioned JSON manifest + Ogg Vorbis audio + artwork). Encoding converts audio into the playback profile. Locking tells K7 Mix and compatible players not to change the published mix. Both kinds of file contain encoded audio and can be played. Exporting a locked copy leaves the editable original in place. The lock is not encryption.

The project lives at:

```text
L:\Library\Coding\CSharp\K7Mix
```

The look follows the Tekhnexos companion design standard at:

```text
L:\Library\Coding\Laravel\Tekhnexos\documents\companion-design-standard.md
```

Use the shared app resources copied from Elfwida: `src\K7Mix\Design\Design.axaml`, `Palette.cs`, `Typography.cs`, and embedded Roboto fonts under `src\K7Mix\Assets\Fonts`. New UI should use the existing classes (`page`, `section`, `section bar`, `pill`, area colour classes, `caption`, `prose`, `subheading`, `heading`, `title`, `mono`, `positive`, `negative`) instead of ad-hoc styling. Rose `#ec003f` is the identity colour and belongs on the icon only.

Playback, the `.k7m` format, Markdown import and duration math live in `src\K7Mix.Core` and must stay independent of Avalonia. Skip always honours the configured transition; that cannot be disabled. See `documents/formats/`.

Packaging music does not grant permission to distribute it. Preserve licence, source and attribution text.

## Working on the app

Build and publish from the repo root:

```powershell
dotnet build K7Mix.slnx -c Release
dotnet test  K7Mix.slnx -c Release
dotnet publish src\K7Mix\K7Mix.csproj -c Release -r win-x64 --self-contained true -o artifacts\publish
```

Install the app where it is actually launched:

```powershell
.\install.bat
```

`install.bat` wraps `install.ps1`, which **closes a running K7 Mix by itself**, publishes, rewrites the Start Menu shortcut and cleans the user PATH. Do not stop to ask whether to close it. Once the new build is installed, **relaunch it**, so the user is not left on a stale executable.

That publishes to:

```text
C:\TKHN\K7Mix\K7Mix.exe
```

**Do not "simplify" it back to `%LOCALAPPDATA%\Programs\K7Mix`, and do not publish onto `L:\Apps`.** Both are retired. The office workstation enforces Microsoft Defender ASR rule `01443614-CD74-433A-B99E-2ECDC07BFC25` in Block mode. `C:\TKHN` is the shared root for the companion family, catalogued in `L:\Sync\companion-family.json`. The app's *data* stays on `L:` or wherever the user saved a mix.

Never drive the running app with synthetic clicks or `SendKeys`. Screenshots via per-window `PrintWindow` are welcome instead.

Encoding packaged audio needs `ffmpeg` with libvorbis. Printing the jewel-case insert needs `typst`.

## Finishing a piece of work

When the work is done, built, installed, committed and pushed, report honestly what was completed and what remains. If no Git remote exists yet, commit locally and say that push is blocked by the missing remote instead of pretending the work was pushed.

Use direct, scoped commits. The message should be a plain sentence saying what changed, with a body explaining why.

The repo is `git@github.com:tsalmeida/K7Mix.git` and work lands on `main` directly.

## Database prompts

+At the start of every session, **before working on the main request in `## Request` below**, check this database for extra prompts for this project.

Process **only** database rows whose `datapoint_2` is exactly `Pending`; never alter rows marked `Finished`, `Postponed`, or `Draft`. This Pending-only restriction applies **only to rows read from this database**. It does not suppress, replace, or limit the main request supplied below: after completing any applicable Pending database work (or finding none), also carry out the main `## Request`.

In addition to the request below, there might be extra requests stored in:

```text
L:\Library\Coding\project-ai-prompts_package.sqlite
```

Find K7 Mix prompts by querying for `datapoint_1='K7Mix'` or `datapoint_1='K7 Mix'` and `datapoint_2='Pending'`. Finish each prompt properly before marking it `Finished`: implemented, built, installed, committed, and pushed where possible. If a task fails or ends half-done, leave its row `Pending`.

## Current situation

The live briefing for this project is the Current Situation field of its row in All My Projects (`L:\Vault\Obras\all-my-projects_package.sqlite`, column `longtext_3`). This row is titled "K7 Mix" (id `62420`). Tiago almost never reads it. Models do. Read it at the start of work on this project. It is the mirror of what is happening, what is waiting, what to do next, and what the intended direction is.

**Keep it true without asking.** Do not wait for confirmation. Do not open a second current-situation note with a new timestamp, and do not store a second copy of this briefing. Edit that field in place.

When anything material changes, do both of these in the same turn:

1. **Rewrite the present.** Change the snapshot and the do-now, waiting, and next lines so they match today. Delete or recast sentences that would be false if read tomorrow. A stale now line is a bug.
2. **Add what is new.** Put new facts in the section they belong to. If a plan changes, replace the old plan. Do not leave two competing plans.

**History.** Keep a short block of only what still explains the present. Move a fact there when it is no longer current but still explains a lock or a do-not-redo. Do not grow a diary. Detailed dated notes stay in their own files.

Do not copy whole emails or whole prompts into it. Do not leave advice that was already decided.

Write the field with one small `UPDATE` of `longtext_3`, commit immediately, and stamp `updated_at` (UTC, `YYYY-MM-DDTHH:MM:SS.000000Z`). Open a short-lived connection with pooling off. Re-read the row just before changing it. Never rewrite the database file wholesale. TKHN Sync carries the file.

After editing, stamp **Last rewritten** at the top (the date, and the one-line reason).

## After the work

When you have implemented the requested changes, write a brief report of what was done into Autarktos (`L:\Library\Coding\project-ai-prompts_package.sqlite`), in `longtext_2` of the entry this prompt came from. The commit message is fine as that report.

Set `datapoint_4` ("AI Model family that processed it") to exactly one of: GPT, Grok, Claude, Gemini, Other — according to which model family processed this prompt.

If a Dispatcher's note names the entry `id`, use that. If you processed rows from the Database prompts section, write the report on those rows. Do not pick up other Pending rows from the database; that remains the Database prompts section, used only for manual one-project prompting. If this session has no Autarktos entry, skip the write.

Write those fields with one small `UPDATE` at a time, commit immediately, and stamp `updated_at` (UTC, `YYYY-MM-DDTHH:MM:SS.000000Z`). The sqlite file is kept in sync by TKHN Sync; never rewrite it wholesale.

## Request

*template_focus*
