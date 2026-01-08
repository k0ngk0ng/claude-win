# Changelog

All notable changes to Claude Code for Windows will be documented in this file.

## [0.1.31] - 2024-01-08

### Fixed
- Stop button now properly updates UI state after stopping Claude Code
- Added OnProcessExited event trigger when manually stopping

## [0.1.30] - 2024-01-08

### Added
- Two installer versions: Full (bundled) and Lite (downloads during install)
  - **Full**: Includes Node.js + PortableGit (~350MB), works offline
  - **Lite**: Downloads dependencies during installation (~15MB)

### Fixed
- Use PortableGit instead of MinGit (MinGit doesn't include bash.exe)

## [0.1.29] - 2024-01-08

### Fixed
- Use PortableGit instead of MinGit (MinGit doesn't include bash.exe)
- Git Bash now properly bundled in installer

## [0.1.28] - 2024-01-08

### Added
- Version number display in UI header (shows git tag or assembly version)
- Git Bash status check in welcome message

### Changed
- Improved startup diagnostics to show Git Bash detection result

## [0.1.27] - 2024-01-08

### Added
- Bundled MinGit (Git for Windows) in installer - no separate Git installation required
- Git Bash auto-detection for Claude Code runtime requirement
- Git Bash path configuration in settings (CLAUDE_CODE_GIT_BASH_PATH)
- Settings UI shows auto-detected Git Bash path
- Debug output now shows Git Bash path

### Fixed
- "Claude Code on Windows requires git-bash" error by bundling Git and auto-setting environment variable

## [0.1.26] - 2024-01-08

### Added
- Bundled MinGit (Git for Windows) in installer - no separate Git installation required
- Git Bash auto-detection for Claude Code runtime requirement
- Git Bash path configuration in settings (CLAUDE_CODE_GIT_BASH_PATH)
- Settings UI shows auto-detected Git Bash path
- Debug output now shows Git Bash path

### Fixed
- "Claude Code on Windows requires git-bash" error by bundling Git and auto-setting environment variable

## [0.1.25] - 2024-01-08

### Changed
- Image upload button style improved (fixed vertical line display issue)

### Fixed
- "node is not recognized" error when running Claude Code
- Now uses node.exe directly to run CLI.js instead of claude.cmd

## [0.1.24] - 2024-01-08

### Added
- CHANGELOG.md for version history tracking
- User npm directory installation to avoid permission issues

### Changed
- npm packages now install to user AppData directory (avoids Program Files EPERM errors)
- Improved process cleanup on application close
- Installer now silently handles process checks (no "process not found" messages)

### Fixed
- npm EPERM error when auto-installing Claude Code CLI
- Orphaned node processes not being killed on close
- Process tree cleanup now correctly tracks PID before disposal

## [0.1.23] - 2024-01-08

### Added
- Custom ConPTY implementation using Windows native APIs
- No external PTY library dependencies

### Changed
- Replaced Pty.Net with custom P/Invoke implementation
- Requires Windows 10 1809 (Build 17763) or later

### Fixed
- Build failures due to Pty.Net package version issues

## [0.1.22] - 2024-01-08

### Fixed
- Attempted to fix Pty.Net package version (still had API mismatch)

## [0.1.21] - 2024-01-08

### Added
- PTY (Pseudo Terminal) support for proper TUI rendering
- Process cleanup on application close
- Close confirmation dialog when Claude is running

### Changed
- Switched from stdin/stdout redirection to PTY mode

## [0.1.20] - 2024-01-08

### Added
- Close confirmation dialog when Claude Code is running
- Process tree cleanup on application close

### Fixed
- Residual processes after closing the application

## [0.1.19] - 2024-01-08

### Added
- Terminal environment variables (TERM, FORCE_COLOR, COLORTERM)

### Fixed
- Claude Code TUI output display issues

## [0.1.18] - 2024-01-08

### Changed
- Image upload button icon changed from "+" to paperclip

## [0.1.17] - 2024-01-08

### Changed
- Switched to persistent process mode (session context maintained)

### Fixed
- Line break handling issues

## [0.1.16] - 2024-01-08

### Added
- GUI debug toggle in settings (default: off)

## [0.1.15] - 2024-01-07

### Added
- Image upload and clipboard paste support
- Slash command toolbar
- JSON configuration import

### Fixed
- Chinese character encoding issues

## [0.1.0] - 2024-01-06

### Added
- Initial release
- WPF native Windows client
- Bundled Node.js runtime
- NSIS installer
- Environment variable configuration UI
- Auto-install Claude Code CLI
