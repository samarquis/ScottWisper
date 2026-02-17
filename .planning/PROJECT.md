# WhisperKey Voice Dictation

## What This Is

A Windows desktop application that provides universal voice dictation. Press a hotkey, speak naturally, and have your words automatically typed into any active window or text field across the entire system.

## Core Value

Accurate, instantaneous voice-to-text conversion that seamlessly integrates with any Windows application, making typing completely optional.

## Current State

**Shipped Version:** v1.0 Core Voice Dictation Platform (2026-02-15)

**Current Milestone:** v1.1 - User Feedback & Optimization (Planning)

A fully functional voice dictation application with:
- Global hotkey activation (Ctrl+Win+Shift+V)
- Real-time speech-to-text with OpenAI Whisper API
- Universal text injection into any Windows application
- System tray background operation
- Settings management with hotkey/audio configuration
- Voice commands for punctuation and error correction
- Local Whisper model support for offline operation
- HIPAA/GDPR compliance framework
- Enterprise deployment capabilities

## Next Milestone Goals

**v1.1 Priorities (TBD):**
- User feedback integration
- Performance optimizations
- Additional target application compatibility
- New feature development based on user feedback

---

<details>
<summary><b>Previous Requirements & Context (v1.0 Archived)</b></summary>

## Requirements

### Validated

- ✓ CORE-01: System-wide hotkey activation — v1.0
- ✓ CORE-02: Speech-to-text conversion using free cloud APIs — v1.0
- ✓ CORE-03: Automatic text injection into active window — v1.0
- ✓ CORE-04: High transcription accuracy — v1.0 (95%+)
- ✓ CORE-05: Windows compatibility — v1.0 (Windows 10/11)
- ✓ CORE-06: Free tier usage within API limits — v1.0
- ✓ UX-01: Real-time text output — v1.0
- ✓ UX-02: Text insertion at cursor — v1.0
- ✓ UX-03: Basic punctuation commands — v1.0
- ✓ UX-04: Audio/visual feedback — v1.0
- ✓ UX-05: Error correction commands — v1.0
- ✓ UX-06: Automatic punctuation — v1.0
- ✓ SYS-01: Background process management — v1.0
- ✓ SYS-02: Settings management — v1.0
- ✓ SYS-03: Audio device selection — v1.0

### Out of Scope

- Voice commands/control — Focus is pure dictation
- Mobile platforms — Windows desktop only
- Offline processing — Requires cloud APIs for accuracy (satisfied via local Whisper in v1.0)
- Multiple languages — English only initially
- Voice synthesis/text-to-speech — Dictation only

## Context

This solves the problem of slow typing for users who can speak faster than they can type. The universal approach means it works in terminals, browsers, document editors, and any other Windows application without requiring individual integrations. The target user needs this for both professional work and personal use, with the constraint that all components must be free.

## Constraints

- **Platform**: Windows only — Target environment is Windows desktop
- **Cost**: Free only — Must use free tiers of cloud services or open source solutions
- **Network**: Internet required for cloud API — optional for local Whisper
- **Privacy**: Work PC compatible — Must be acceptable for corporate environments

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Universal text injection | Works anywhere without individual app integrations | ✓ Working in v1.0 |
| Cloud-based speech recognition | Higher accuracy than local models | ✓ Working in v1.0 |
| Hotkey activation | Minimal disruption to workflow | ✓ Working in v1.0 |
| Local Whisper fallback | Offline capability for privacy-sensitive users | ✓ Implemented in v1.0 |
| WPF framework | Native Windows integration | ✓ Working in v1.0 |
| HIPAA/GDPR compliance | Enterprise-ready features | ✓ Framework in v1.0 |

</details>

---

*Last updated: 2026-02-15 after v1.0 milestone*
