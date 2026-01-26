# ScottWisper Voice Dictation

## What This Is

A Windows desktop application that provides universal voice dictation. Press a hotkey, speak naturally, and have your words automatically typed into any active window or text field across the entire system.

## Core Value

Accurate, instantaneous voice-to-text conversion that seamlessly integrates with any Windows application, making typing completely optional.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] System-wide hotkey activation
- [ ] Speech-to-text conversion using free cloud APIs
- [ ] Automatic text injection into active window
- [ ] High transcription accuracy
- [ ] Windows compatibility
- [ ] Free tier usage within API limits

### Out of Scope

- Voice commands/control — Focus is pure dictation
- Mobile platforms — Windows desktop only
- Offline processing — Requires cloud APIs for accuracy
- Multiple languages — English only initially
- Voice synthesis/text-to-speech — Dictation only

## Context

This solves the problem of slow typing for users who can speak faster than they can type. The universal approach means it works in terminals, browsers, document editors, and any other Windows application without requiring individual integrations. The target user needs this for both professional work and personal use, with the constraint that all components must be free.

## Constraints

- **Platform**: Windows only — Target environment is Windows desktop
- **Cost**: Free only — Must use free tiers of cloud services or open source solutions
- **Network**: Internet required — Cloud-based speech recognition for accuracy
- **Privacy**: Work PC compatible — Must be acceptable for corporate environments

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Universal text injection | Works anywhere without individual app integrations | — Pending |
| Cloud-based speech recognition | Higher accuracy than local models | — Pending |
| Hotkey activation | Minimal disruption to workflow | — Pending |

---
*Last updated: 2026-01-26 after initialization*