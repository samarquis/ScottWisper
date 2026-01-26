# ScottWisper Voice Dictation Roadmap

## Overview

ScottWisper is a Windows desktop voice dictation application that provides universal voice-to-text conversion with system-wide hotkey activation and cloud-based speech recognition. This roadmap delivers a production-ready application in 3 phases, starting with core technology validation and progressing through Windows integration to competitive features.

## Phases

### Phase 1: Core Technology Validation

**Goal:** Establish working speech-to-text pipeline with real-time transcription capability and sustainable cost model.

**Dependencies:** None - Foundation phase

**Requirements:**
- CORE-01: System-wide hotkey activation
- CORE-02: Speech-to-text conversion using free cloud APIs  
- CORE-04: High transcription accuracy (95%+)
- CORE-05: Windows compatibility
- CORE-06: Free tier usage within API limits
- UX-01: Real-time text output

**Success Criteria:**
1. User can activate dictation using a global hotkey combination while any Windows application is active
2. User sees real-time transcription appearing on screen within 100ms of speaking
3. Transcription accuracy exceeds 95% for clear English speech in quiet environments
4. Application runs stably on Windows 10/11 without crashes during continuous dictation sessions
5. API usage stays within free tier limits for moderate daily usage (2-3 hours)

**Plans:** 4 plans in 3 waves

**Plan List:**
- [x] 01-01-PLAN.md — Foundation setup with WPF and global hotkey registration ✓
- [x] 01-02-PLAN.md — OpenAI Whisper API integration with real-time audio capture ✓
- [x] 01-03-PLAN.md — Real-time transcription display and cost tracking implementation ✓
- [x] 01-04-PLAN.md — Comprehensive validation and performance testing ✓

---

### Phase 2: Windows Integration & User Experience

**Goal:** Deliver universally compatible text injection and system-level integration for seamless use across all Windows applications.

**Dependencies:** Phase 1 completion - Requires working transcription pipeline

**Requirements:**
- CORE-03: Automatic text injection into active window
- UX-02: Text insertion at cursor
- UX-04: Audio/visual feedback
- SYS-01: Background process management
- SYS-02: Settings management
- SYS-03: Audio device selection

**Success Criteria:**
1. Transcribed text appears at exact cursor position in any Windows application (browser, IDE, document editor)
2. User receives clear visual and audio feedback indicating dictation status (ready, recording, processing, complete)
3. Application runs as background system tray process with minimal CPU/memory usage
4. User can configure hotkey, API settings, and audio device preferences through settings interface
5. Application gracefully handles microphone permission requests and device changes
6. Text injection works reliably across target applications (web browsers, Visual Studio, Office, Notepad++, terminal)

---

### Phase 3: Competitive Features & Polish

**Goal:** Differentiate from basic Windows Voice Typing with intelligent dictation features and production-ready polish.

**Dependencies:** Phase 2 completion - Requires universal integration working reliably

**Requirements:**
- UX-03: Basic punctuation commands
- UX-05: Error correction commands  
- UX-06: Automatic punctuation

**Success Criteria:**
1. User can insert punctuation using voice commands ("period", "comma", "question mark", "new paragraph")
2. User can correct transcription errors using voice commands ("delete last word", "undo", "replace [word] with [word]")
3. Application automatically inserts appropriate punctuation based on speech patterns and pauses
4. Dictation workflow feels natural and efficient compared to typing, reducing cognitive overhead
5. Error correction commands work reliably and improve overall transcription accuracy
6. Features demonstrate clear advantages over built-in Windows Voice Typing justifying adoption

---

## Progress Tracking

| Phase | Status | Completion | Notes |
|-------|--------|------------|-------|
| Phase 1: Core Technology Validation | ✅ Complete | 100% | All 4 plans executed, Phase goal verified ✓ |
| Phase 2: Windows Integration & User Experience | 📋 Planned | 0% | Depends on Phase 1 |
| Phase 3: Competitive Features & Polish | ⏸️ Not Started | 0% | Depends on Phase 2 |

**Overall Project Progress:** [███░░░░░░] 33% - Phase 1 complete, ready for Phase 2

## Milestone Timeline

- **Phase 1:** Foundation - Core transcription pipeline with real-time capabilities
- **Phase 2:** Integration - Universal Windows compatibility and user experience
- **Phase 3:** Polish - Competitive differentiators and production readiness

## Critical Path Focus

The critical path focuses on:
1. **Real-time latency optimization** - Sub-100ms end-to-end performance
2. **Universal text injection reliability** - Compatibility across Windows applications  
3. **Sustainable cost model** - API usage optimization for free tier viability

These risks are addressed early in Phase 1 and validated throughout Phase 2 before adding competitive features in Phase 3.

---
*Roadmap created: January 26, 2026*
*Depth: Comprehensive (3 phases for 15 requirements)*
*Coverage: 100% - All v1 requirements mapped*