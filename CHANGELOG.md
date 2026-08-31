# Changelog

## Unreleased

- Added the guided `Force`, `Impact`, and `Slide` workflow. Every impact force range exposes each source clip separately for waveform markup and auditioning; marked impact regions play directly at runtime without exported slice assets, while export remains optional.
- Added `Materials` and `Mapping` steps for focused surface editing, interaction mapping, validation, and copy-from authoring.
- Added scrollable impact source lists and a `Curves` step backed by a reusable two-point `AnimationCurve` drawer for volume and pitch. Each half of the gradient viewport controls one endpoint with live hover feedback; vertical dragging changes its value, horizontal dragging rotates its inner tangent, and side `Min`/`Max` fields provide exact entry.
- Changed lookup precedence so external subprofiles override entries from the main settings, with later subprofiles overriding earlier ones.
- Migrated all bundled sample profiles to the current impact-range and two-point-curve data model, and added descriptions for both example-scene samples.
- Improved Preview layout stability, scrolling, row-level mapping errors, source-copy controls, and table contrast.

All notable changes to this package are documented in this file.

## [2.0.0]

### Added

- Centralized **Project Settings > Audio > Phys Sound** configuration.
- Named acoustic surfaces mapped from standard Unity `PhysicsMaterial` assets.
- Unordered surface-pair interactions with exact and wildcard fallback resolution.
- Impact selection and scaling from contact impulse.
- Continuous slide volume and pitch from tangential contact velocity.
- A bounded hidden pool of positioned native `AudioSource` emitters.
- A componentless backend based on explicitly enabled `Collider.providesContacts`.
- A component backend with a lightweight `PhysSoundObject`.
- Lazy runtime-only `OnCollisionStay` collection for slide-capable component contacts.
- Physics 2D component callbacks through the shared `PhysSoundObject` component.
- A single interactive Inspector Preview for per-interaction Impact subclips and Slide loop markup, waveform zoom/pan, automatic region detection, auditioning, Undo, and WAV export back into the runtime clip arrays.
- `PHYS_SOUND_DISABLE_2D` support without disabling the Unity Physics 2D module.

### Changed

- Minimum Unity version is now 6000.6.0a7.
- Surface and interaction authoring now uses Unity's native serialized dictionaries.
- Settings can merge surfaces and interactions from ordered external subprofile assets.
- Added separate 3D and 2D starter-pack samples containing profiles, sounds, and standalone Physics Material assets.
- Playback now uses Unity 6 `AudioResource` / `AudioSource.resource`; imported `AudioClip` assets act as built-in generators.
- Package modules remain optional through asmdef version defines.
- Runtime configuration is global instead of being distributed across scene components and multiple ScriptableObject types.

### Removed

- The legacy `PhysSoundDatabase`, `PhysSoundKey`, `PhysSoundMaterial`, `PhysSoundAudioSet` and `PhysSoundAudioContainer` APIs.
- The legacy temporary `AudioSource` pool.
- Serialized `AudioSource` setup on physical objects.
- The legacy Physics 2D architecture, Terrain composition and trigger-sound implementations.

### Migration

Version 2.0 is not API-compatible with 1.x. Configure the new runtime from **Project Settings > Audio > Phys Sound**. The legacy `PhysSoundObject` script GUID is retained so existing serialized component references resolve to the new lightweight component.
