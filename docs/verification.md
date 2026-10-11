# Verification — 2026-10-11

## Delivered artifact
dist/MetaRemover-Windows-x64.zip, 163,585,288 bytes (about 156 MiB).
Portable, self-contained Windows x64 WPF app; runtime, ExifTool, shared LGPL FFmpeg/FFprobe, notices, source snapshots and application source ZIP included.

## Final evidence
- Full xUnit suite: 38 passed, 0 failed, 0 skipped; build: 0 warnings, 0 errors.
- Release self-contained publish: succeeded.
- Fresh ZIP extraction into a directory containing spaces: bundled executable launched and processed ten exports (removal and replacement across JPG, PNG, WebP, MP4, MOV).
- Packaged smoke: requested tags verified, location removed in removal mode, source hashes unchanged.
- UI launch/render smoke: exited successfully, real populated screenshot inspected at designs/implemented-D.png.
- Fresh independent read-only review: two Important findings, both fixed with failing/passing regression tests (video UTC conversion and canceled ExifTool sidecar cleanup).
- Additional boundary check rejects multiline metadata arguments to prevent them becoming extra ExifTool options.

## Tested cases
Case-insensitive import deduplication, same basename in different folders, Unicode filenames, recursive/nonrecursive imports, nested output exclusion, mixed media, original hashes, output collisions and reservations, IMG_NNNN rollover, actual extensions, per-format metadata writing, GPS hemispheres, UTC integer video dates, original capture dates, corrupt input, cancellation and owned sidecar cleanup, image/video rotation, audio and silent video, extra-track opt-in, sRGB ICC byte preservation, synthetic 10-bit PQ/BT.2020 AV1 signaling and encoded stream hashes, selected/all export scopes and per-file failure continuation.

## Installer verification (2026-10-11)
- Built `dist/MetaRemover-Setup-x64.exe` using the Authenticode-verified official Inno Setup 7.1.0 compiler. Single installer: 120,897,675 bytes (about 115 MiB).
- Per-user silent installation into a fresh workspace test folder: exit 0.
- Installed application smoke: 10 successful exports across JPG, PNG, WebP, MP4 and MOV; original hashes unchanged; bundled tools ran successfully.
- Silent uninstall: exit 0; installed application executable removed; portable distribution remained intact.
- Installer source: `scripts/installer.iss`; reproducible build wrapper: `scripts/installer.ps1`. Installer is unsigned.

## Version 1.1 changes
- Added height/width splitters, minimum panel sizes and wrapped preview values.
- Added named presets with local persistent save/update/delete and complete supported-settings autofill; destination remains machine-local.
- Full suite: 41 passed. Actual populated native UI rendered successfully to `designs/implemented-D-presets.png` and inspected.
- Fresh read-only review: one Important malformed-data issue fixed using a failing/passing regression. No other actionable findings.
- Rebuilt portable ZIP and v1.1 installer. Installed app passed ten mixed-format exports with original hashes unchanged; uninstall passed.
- Installer: 120,910,313 bytes; SHA256 `605B8A5FE5DB3B7FA748EA32560F3C96404EB86230F932E9F2243138AF56BA6A`.

## Version 1.2 camera metadata
- Camera selector with model-specific hardware values, editable lens fields, aperture, physical/equivalent focal lengths, ISO, shutter, exposure compensation, flash and white balance. All new fields are persisted; older presets load without invented camera fields.
- EXIF fields for photos; supported VideoKeys lens fields for MP4/MOV. Required media properties preserved.
- 52 tests passed, native WPF camera/preset diagnostics passed, populated rendering inspected; independent read-only review found no Important/Critical issues.
- Actual rich metadata was verified on JPG, PNG, WebP, MP4 and MOV generated fixtures. User's sample image was not modified.
- Installer version 1.2: 120,920,405 bytes. Installation and installed UI diagnostics passed; installed processing smoke passed ten exports, unchanged originals and bundled tools.
- Native diagnostic evidence: `.build/rich-ui-check.txt`, `.build/rich-installed-ui-check.txt`. Test suite: `.build/rich-full-tests.log`.
- Hardware references: `docs/camera-catalog-sources.md`.

## Version 1.3 complete autofill
- Built-in phone/camera selection supplies editable daylight examples for all supported form camera fields, software, exposure and missing-date policy. Explicit Autofill button restores defaults; custom saved profiles restore exact values.
- Keep original retains source capture dates. Missing-date policy uses a single batch timestamp; transient timestamp is excluded from saved JSON.
- 61 tests passed, native autofill/model-switch/preset diagnostics passed, populated native render inspected. Independent read-only review: no Important/Critical findings.
- Download checksums for the public release are published as `SHA256SUMS.txt` alongside its installer and portable ZIP.
- Existing installed copy updated to v1.3 with unchanged saved-preset hash. Installed UI checks passed; processing smoke passed ten exports across all five formats, original hashes unchanged.
- Evidence: `.build/auto-full-tests.log`, `.build/auto-installed-ui-check.txt`, `.build/auto-installed-smoke.log`, `designs/implemented-D-auto-profile.png`.

## Practical limits
Supported formats are JPG/JPEG, PNG, WebP, MP4 and MOV; HEIC/HEIF and RAW are outside this version.
Rendering and codec/container properties remain. No guarantee is made that every vendor-specific embedded identifier can be detected or removed.
The ICC/HDR fixtures cover the tested preservation paths; arbitrary Dolby Vision, vendor HDR metadata, malformed profiles and unusual codecs are not exhaustively validated.
The design follows D's geometry and palette with functional file data, filename controls, status and validation. This is a native interface rather than a pixel-identical rendering of a raster mockup.
Native dialog interaction was inspected in source and uses standard Windows dialogs; the UI was verified by actual launch/render and view-model processing, without external mouse automation.
Public source and release downloads: https://github.com/AbulH88/MetaRemover. Tool binaries and generated build artifacts are excluded from Git; complete downloads retain their runtime, tool notices, and source materials.
