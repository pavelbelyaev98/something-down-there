# 068 — Odin editor tooling

**Status:** complete. Odin Inspector/Validator 4.0.2.4 configured in Editor Only mode with a focused MainGame/content validation profile that runs before builds; setup and license notes live in `unity/readme.md`.

## Objective
Finish the user-approved Odin installation and make Inspector/Validator useful in the existing developer workflow.

## Concept reference
Developer tooling only; gameplay and the concept remain unchanged.

## Current implementation
- Inspector and Serializer 4.0.2.4 are imported under `Assets/Plugins/Sirenix`; Unity compiles successfully.
- Matching Validator 4.0.2.4 required a separate import; the user completed Unity's unsigned-package prompt.
- MainGame and project-owned content are the validation targets. The full BK demo import and recovery scene are not game validation targets.
- Existing persistence owns current-format saves; installing Odin does not justify replacing it.

## Changes
- Import the purchased Validator through the live Editor, preserving publisher paths and metadata.
- Verify the installed API and run a read-only scan of game content; use the vendor's existing validation workflow rather than duplicate it.
- Store `MainGameValidation.asset` under project-owned Editor assets and select it in Odin's built-in build automation; complete scans before builds, stop on errors, log warnings, never auto-fix.
- Enable the publisher's Editor Only mode because no game code uses Odin serialization.
- Record approved versions, license/source, relevant menus and practical use in local tooling documentation and repository guidance.
- Keep vendor binaries in Git LFS and text/metadata in ordinary Git; no license credentials belong in source control.

## Edge cases
- Do not open, modify or save the recovery scene or overwrite unsaved user scene edits.
- Do not mass-fix vendor diagnostics or treat unused demo warnings as game failures.
- Preserve current save validation and recovery; add no older-save compatibility or speculative serializer migration.
- Validator import may trigger domain reloads; confirm readiness before subsequent commands.

## Acceptance criteria
- Inspector, Serializer and Validator are present at the matching approved version; Unity compiles without new errors or warnings.
- A targeted game-content validation scan runs and its material findings are handled or accurately reported.
- The Windows player builds successfully with the plugins installed.
- Documentation explains how future work should use the tooling and retain the existing persistence owner.

## Verification
- Both products report 4.0.2.4; Editor Only mode is enabled and the game has no Odin serialization consumers.
- The focused profile returned 14,489 valid results and no errors or warnings. This is asset setup coverage, not proof of gameplay correctness.
- Build automation uses this profile explicitly, completes validation and stops builds on errors; warnings are logged without automatic fixes.
- Unity upgraded five publisher plugin metadata files to its current format with GUIDs unchanged; Inspector/Validator/Serializer binaries were not patched.
- The Windows player builds successfully with no errors or compiler warnings; only the existing warning that Pipeline is disabled in players remains. Its managed output excludes Odin Serializer, Utilities and editor assemblies.
- MainGame remains unmodified and clean; the recovery scene was not opened or edited. Source and documentation checks passed.
