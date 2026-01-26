# Feature Research

**Domain:** Voice Dictation (Windows Desktop Application)
**Researched:** January 26, 2026
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Global hotkey activation | Universal standard across voice dictation tools (Windows+H, Ctrl+Alt+R) | LOW | Must work in any application, system-level hooking required |
| Real-time text output | Users expect to see words appear as they speak | MEDIUM | Requires low-latency API calls and text insertion |
| Basic punctuation commands | "comma", "period", "question mark" commands are industry standard | LOW | Simple text replacement, core to usable dictation |
| Audio feedback/visual indicators | Microphone status, listening/processing states | LOW | Visual indicators for recording state, audio cues for start/stop |
| Text insertion at cursor | Must insert where user is currently typing | MEDIUM | Requires Windows API calls to detect active text field |
| Error correction via voice | "scratch that", "undo" commands are expected | MEDIUM | Buffer management and text replacement logic |
| Internet connectivity requirement | Users expect cloud-based services to need connection | LOW | Free tier APIs require internet, offline capability is differentiator |
| Basic accuracy (>90%) | Modern users expect reasonable accuracy from any dictation tool | MEDIUM | Depends on API choice (Whisper/Azure/etc.) |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Free tier with generous limits | Competes against paid Dragon ($300-700) and limited free tiers | HIGH | API cost management, requires usage tracking |
| Universal Windows integration | Works in ANY application (browsers, IDEs, legacy apps) | HIGH | More comprehensive than Windows Voice Typing limitations |
| Offline capability | Privacy advantage over cloud-only solutions like Wispr Flow | VERY HIGH | Requires local model deployment, significant storage/RAM |
| Automatic punctuation | Removes need to say "comma", "period" out loud | MEDIUM | AI-powered punctuation insertion, user preference |
| Custom vocabulary/learning | Adapts to user's terminology, industry-specific words | HIGH | Model fine-tuning or personal dictionary management |
| Context-aware insertion | Understands application context (email vs code vs document) | VERY HIGH | Window detection + specialized formatting |
| Multiple language support | Broad language support vs Dragon's limited languages | MEDIUM | API-dependent, Whisper supports 99+ languages |
| Voice commands for formatting | "bold that", "heading", "bullet list" | MEDIUM | Beyond basic text insertion, requires rich text support |
| Noise cancellation | Works better in cafes, offices, real-world environments | MEDIUM | Audio preprocessing, real-time filtering |
| Speed advantage (3x typing) | Core value proposition - faster than manual typing | LOW | Inherent to voice dictation, measurable performance metric |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Real-time continuous streaming | "Always listening" like Alexa | Privacy nightmare, resource intensive, regulatory risk | Push-to-talk hotkey model with clear recording states |
| Screenshot capture for context | Wispr Flow-style window analysis | Massive privacy concerns, bandwidth usage, user distrust | Application detection via window titles/process names |
| Cloud-only processing | Simpler deployment | No offline use, latency, privacy concerns | Hybrid model: online for accuracy, offline for privacy |
| Unlimited free tier | User acquisition magnet | Unsustainable API costs (Whisper: $0.006/min) | Generous but limited free tier with clear upgrade path |
| Complex UI/interfaces | Feature checkbox for competitive reviews | Breaks "universal" goal, adds cognitive load | Minimal system tray interface, hotkey-driven |
| Biometric voice security | "Only my voice works" | False positives/negatives, accessibility issues | Optional device encryption instead |
| Recording and playback | Review what was said | Storage overhead, privacy concerns | Text-based undo/redo instead |
| Advanced AI formatting | Smart document restructuring | Unpredictable results, loss of user control | Simple voice commands for explicit formatting |

## Feature Dependencies

```
[Global Hotkey Activation]
    └──requires──> [Audio Feedback System]
                   └──requires──> [Text Insertion Engine]
                                  └──requires──> [API Integration (Whisper/Azure)]

[Real-time Text Output] ──enhances──> [Global Hotkey Activation]
[Punctuation Commands] ──enhances──> [Text Insertion Engine]
[Error Correction] ──requires──> [Text Buffer Management]

[Offline Capability] ──conflicts──> [Cloud API Processing]
[Custom Vocabulary] ──enhances──> [API Integration]
[Context-Aware Insertion] ──requires──> [Window Detection API]
```

### Dependency Notes

- **Global Hotkey Activation requires Audio Feedback System:** Users need visual/audio confirmation that recording started
- **Text Insertion Engine requires API Integration:** Core transcription capability from cloud/local API
- **Real-time Text Output enhances Global Hotkey Activation:** Live output makes the hotkey interaction feel responsive
- **Offline Capability conflicts with Cloud API Processing:** Must choose between local model deployment and cloud API
- **Context-Aware Insertion requires Window Detection API:** Need Windows APIs to detect active application context

## MVP Definition

### Launch With (v1)

Minimum viable product — what's needed to validate the concept.

- [ ] **Global Hotkey Activation** — Core interaction model, universal access
- [ ] **Real-time Text Output** — Basic transcription capability  
- [ ] **Text Insertion at Cursor** — Works in any Windows application
- [ ] **Basic Punctuation Commands** — Usable for real writing tasks
- [ ] **Free Tier with Limits** — Validates market, manages costs

### Add After Validation (v1.x)

Features to add once core is working.

- [ ] **Error Correction Commands** — User experience improvement
- [ ] **Automatic Punctuation** — Reduces cognitive load
- [ ] **Audio/Visual Feedback** — Polish and reliability
- [ ] **Custom Vocabulary** — Personalization for power users

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] **Offline Capability** — Privacy differentiator, high implementation cost
- [ ] **Context-Aware Insertion** — Advanced universal integration
- [ ] **Multiple Language Support** — Market expansion
- [ ] **Voice Commands for Formatting** — Advanced productivity features

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Global Hotkey Activation | HIGH | LOW | P1 |
| Real-time Text Output | HIGH | MEDIUM | P1 |
| Text Insertion at Cursor | HIGH | MEDIUM | P1 |
| Basic Punctuation Commands | HIGH | LOW | P1 |
| Free Tier with Limits | HIGH | HIGH | P1 |
| Error Correction Commands | MEDIUM | MEDIUM | P2 |
| Automatic Punctuation | MEDIUM | MEDIUM | P2 |
| Audio/Visual Feedback | MEDIUM | LOW | P2 |
| Custom Vocabulary | MEDIUM | HIGH | P3 |
| Offline Capability | HIGH | VERY HIGH | P3 |
| Context-Aware Insertion | HIGH | VERY HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible  
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Windows Voice Typing | Dragon Professional | Wispr Flow | ScottWisper Approach |
|---------|--------------------|---------------------|------------|---------------------|
| Global Hotkey | Windows+H (limited) | Custom configurable | Custom configurable | Custom configurable |
| Universal App Support | Limited | Windows apps only | macOS only | Full Windows universal |
| Pricing | Free | $300-700 | $144/year | Freemium model |
| Offline Capability | No | No | No | Future differentiator |
| API-based | Azure only | Local models | Cloud only | Flexible (Whisper/Azure) |
| Privacy | Microsoft data | Local processing | Screenshots to cloud | User choice model |
| Accuracy | Good | Excellent (trained) | Good | API-dependent (excellent) |

## Sources

- Wirecutter Dictation Software Review (2025) - Industry analysis and user expectations
- Dragon NaturallySpeaking Feature Matrix - Professional feature comparison
- OpenAI Whisper API Documentation (2025) - Current capabilities and pricing
- Windows 11 Voice Access Support Docs - Built-in limitations and features  
- Wispr Flow Alternatives Analysis (2026) - Privacy concerns and user complaints
- Voice Access Q&A Forums - Real user issues with Windows built-in tools
- Zapier Dictation Software Guide (2025) - Market landscape and pricing models
- Dictation Daddy Blog - Windows built-in capabilities and alternatives

---
*Feature research for: Voice Dictation (Windows Desktop Application)*
*Researched: January 26, 2026*