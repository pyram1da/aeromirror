# AeroMirror 0.12.23 — archived rejected test plan

## Status

This test plan is closed without acceptance. The 4:5 outer-window behavior was
rejected as the wrong product boundary and removed. Physical testing of that
layout is no longer requested.

## Invalidated evidence

Any earlier 0.12.23 build, package, Setup, hash, or passing geometry contract
belongs to the rejected implementation. It must not be used to qualify 0.12.24,
offered for installation, attached to GitHub, or treated as proof that Photos
is fixed.

## Replacement investigation

Use the [0.12.24 test plan](../0.12.24/TEST_PLAN.md). It keeps the PC window
untouched, changes one display-negotiation value, and retains privacy-safe
sender geometry plus sink-local CAPS, pixel-aspect-ratio, and CropMeta timelines
alongside the physical iPhone sequence without claiming a one-to-one mapping.
