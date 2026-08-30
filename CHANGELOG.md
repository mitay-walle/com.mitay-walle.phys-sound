# Changelog

All notable changes to this package are documented in this file.

## [2.0.0]

### Added

- Centralized **Project Settings > Phys Sound** configuration.
- Named acoustic surfaces mapped from standard Unity `PhysicsMaterial` assets.
- Unordered surface-pair interactions with exact and wildcard fallback resolution.
- Impact selection and scaling from contact impulse.
- Continuous slide volume and pitch from tangential contact velocity.
- A bounded hidden pool of positioned native `AudioSource` emitters.
- A componentless backend based on explicitly enabled `Collider.providesContacts`.
- A component backend with a lightweight `PhysSoundObject`.
- Lazy runtime-only `OnCollisionStay` collection for slide-capable component contacts.

### Changed

- Minimum Unity version is now 6000.5.0f1.
- Playback now uses Unity 6 `AudioResource` / `AudioSource.resource`; imported `AudioClip` assets act as built-in generators.
- Package modules remain optional through asmdef version defines.
- Runtime configuration is global instead of being distributed across scene components and multiple ScriptableObject types.

### Removed

- The legacy `PhysSoundDatabase`, `PhysSoundKey`, `PhysSoundMaterial`, `PhysSoundAudioSet` and `PhysSoundAudioContainer` APIs.
- The legacy temporary `AudioSource` pool.
- Serialized `AudioSource` setup on physical objects.
- Physics 2D, Terrain composition and trigger-sound implementations.
- Legacy sample entries from the package manifest.

### Migration

Version 2.0 is not API-compatible with 1.x. Configure the new runtime from **Project Settings > Phys Sound**. The legacy `PhysSoundObject` script GUID is retained so existing serialized component references resolve to the new lightweight component.
