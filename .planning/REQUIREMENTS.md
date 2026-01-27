# ScottWisper Voice Dictation Requirements

## v1 Requirements

### AUTH - Authentication & Usage (not applicable - no user accounts)
*This project uses anonymous usage with API key management*

### CORE - Core Dictation Functionality
- **CORE-01**: System-wide hotkey activation - User can trigger dictation from any application using a global hotkey
- **CORE-02**: Speech-to-text conversion using free cloud APIs - Real-time audio transcription with high accuracy
- **CORE-03**: Automatic text injection into active window - Transcribed text appears at cursor position in current application
- **CORE-04**: High transcription accuracy - 95%+ accuracy for clear speech using cloud APIs
- **CORE-05**: Windows compatibility - Works on Windows 10/11 with standard microphone hardware
- **CORE-06**: Free tier usage within API limits - Sustainable cost model for moderate daily usage

### UX - User Experience Features  
- **UX-01**: Real-time text output - Transcription appears as user speaks, not after completion
- **UX-02**: Text insertion at cursor - Precise text placement in any Windows application
- **UX-03**: Basic punctuation commands - Voice commands for period, comma, question mark, etc.
- **UX-04**: Audio/visual feedback - Clear indication when dictation is active/recording/processing
- **UX-05**: Error correction commands - Voice commands to delete words, undo transcription
- **UX-06**: Automatic punctuation - Intelligent punctuation insertion based on speech patterns

### SYS - System Integration
- **SYS-01**: Background process management - System tray application with minimal resource usage
- **SYS-02**: Settings management - User configuration for hotkey, API preferences, audio devices
- **SYS-03**: Audio device selection - Support for multiple microphones with default selection
- **SYS-04**: Usage monitoring - Track API usage and provide warnings before limits exceeded

## Requirements Count
**Total v1 Requirements:** 15
- CORE: 6 requirements
- UX: 6 requirements  
- SYS: 3 requirements

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CORE-01 | Phase 1 | Complete |
| CORE-02 | Phase 1 | Complete |
| CORE-03 | Phase 2 | Complete |
| CORE-04 | Phase 1 | Complete |
| CORE-05 | Phase 1 | Complete |
| CORE-06 | Phase 1 | Complete |
| UX-01 | Phase 1 | Complete |
| UX-02 | Phase 2 | Complete |
| UX-03 | Phase 3 | Pending |
| UX-04 | Phase 2 | Complete |
| UX-05 | Phase 3 | Pending |
| UX-06 | Phase 3 | Pending |
| SYS-01 | Phase 2 | Complete |
| SYS-02 | Phase 2 | Complete |
| SYS-03 | Phase 2 | Complete |

---
*Requirements defined: January 26, 2026*
*Ready for roadmap: yes*