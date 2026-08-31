# Changelog

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
