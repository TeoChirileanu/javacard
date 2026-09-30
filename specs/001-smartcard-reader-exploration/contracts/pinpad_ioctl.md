# Contract: PINpad IOCTL

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30

Feature 001 collects no PIN and exposes no VERIFY command and no PC/SC Part 10 PIN IOCTL.

Constitution v1.0.1, Principle V: on-reader PIN entry (`FEATURE_VERIFY_PIN_DIRECT`, IOCTL `0x00313538`) is mandatory only for a feature that collects a PIN. This feature MUST NOT add a PIN prompt in order to satisfy that principle.

A later feature that collects a PIN writes its own contract here. Until then this file is the record that 001 has no PINpad surface.
