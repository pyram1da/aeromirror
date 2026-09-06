# AeroMirror 0.12.28 — audit and refactoring candidate

Local review candidate. Not published; physical acceptance is pending.

## Summary

Corrects a reproduced shell-window ownership defect behind the black initial
normal viewer. A child video-window SHOW event could receive saved desktop
coordinates and move the actual picture outside its visible parent. The shell
now restores and fits only the outer window. See [AUDIT.md](AUDIT.md).

## Should I update?

Use the local 0.12.28 installer to retest the reported black-start regression
after its automated build gates pass. No settings reset is required. This is
not an updater-visible GitHub Release or a claim of completed iPhone acceptance.

## Changes

- Guard top-level ownership at the SHOW hook, renderer lookup, saved-placement
  and aspect-fit boundaries. Keep the accepted gallery, native core, codecs,
  dimensions and fullscreen behavior unchanged.
- Refactor manual-update callback delivery and downloaded-file ownership so
  form/handle disposal cancels pending work and cleans up unclaimed downloads.
- Retain failed Windows taskbar/Z-order policy changes for retry instead of
  marking them successful. Dispose the settings tooltip with its form.
- Add behavior tests against both installed 0.12.27 and the corrected shell,
  plus 250 update-disposal races and real hidden-window policy checks.

## Known limitations

The code-level regression is reproduced and corrected, but untouched physical
fresh starts still need testing. Mixed-window Z-order, minimize/restore and
stopped-Bonjour recovery retain their independent physical gates. Broader
native/installer decomposition remains staged work, not a completed rewrite.
