# Changelog

All notable changes to NoBarrelShadow are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/); versions follow [SemVer](https://semver.org/).

## [Unreleased]

## [1.1.0] - 2026-07-06
### Added
- Per-light F12 sliders — fine-tune range and intensity of every flashlight individually
- Native support for every vanilla flashlight plus all WTT ContentBackport lights, no setup needed
- Learns the real name of unrecognized native lights the first time they're seen in raid, saved permanently
- Public API for other mod authors to hook their own lights into the slider system

## [1.0.1] - 2026-07-01
### Fixed
- AI flashlights and dropped guns with lights on no longer cast shadows through walls and geometry
### Added
- Debug logging toggle in F12 settings (for bug reporting)
### Known issues
- MP7A1/MP7A2: switching the flashlight to a different rail mid-raid makes the shadow reappear — toggle the light off and on to fix

## [1.0.0] - 2026-06-27
### Added
- Initial release — removes flashlight barrel shadows on all tactical devices
