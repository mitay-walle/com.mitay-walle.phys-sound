# Phys Sound 2

Phys Sound turns Unity 3D and 2D collision contacts into positioned impact and sliding audio.

Version 2.0 is a breaking rewrite for Unity 6000.6.0a7 and newer. It removes the legacy database, key, material, audio-container and temporary-audio-object workflow. Configuration lives in one Project Settings page, while playback uses one hidden bounded pool of native spatial `AudioSource` objects.

Imported `AudioClip` assets implement Unity's `IAudioGenerator` API in Unity 6.5 and are assigned through `AudioSource.resource`. Unity continues to handle attenuation, spatialization, mixer routing, Doppler and reverb for every pooled emitter.

## Requirements

- Unity 6000.6.0a7 or newer
- Unity Audio module
- Unity Physics 3D module

The package declares no mandatory UPM dependencies. Audio, Physics 3D, Physics 2D and Terrain are detected through assembly-definition version defines, as in Phys Sound 1.x. Physics 2D support is optional.

Enable **Force Disable Physics 2D** in the Phys Sound settings to add the `PHYS_SOUND_DISABLE_2D` scripting define for the active build target. This removes Phys Sound's 2D runtime code without disabling Unity Physics 2D for the rest of the project. Serialized 2D material fields remain in the asset layout but are hidden in the Inspector, so switching the define does not discard their references.

## Installation

Install the package from its Git URL in Unity Package Manager:

```text
https://github.com/mitay-walle/com.mitay-walle.phys-sound.git
```

Then open:

```text
Edit > Project Settings > Audio > Phys Sound
```

Press **Create Settings**. This explicit action creates the settings and emitter prefab at:

```text
Assets/Resources/PhysSound/PhysSoundSettings.asset
Assets/Resources/PhysSound/PhysSoundAudioSource.prefab
```

The system never creates or modifies project assets, scenes, prefabs or collider settings automatically.

## Configuration

### Surfaces

The surfaces dictionary maps each stable surface name to one or more standard Unity `PhysicsMaterial` or `PhysicsMaterial2D` assets.

Example:

```text
Wood
  Wood
  WoodSlippery

Metal
  Metal
  MetalBouncy
```

A collider whose material is not mapped resolves to `Default`.

### Interactions

The interactions dictionary maps an unordered surface pair to its audio configuration:

```text
Wood × Concrete == Concrete × Wood
```

Each interaction can contain:

- impact clips;
- an impact impulse range, volume curve, multiplier and random pitch range;
- looping slide clips;
- a slide speed range, volume curve, multiplier and pitch range.

The **Default Interaction** is configured separately and is not an element of the interactions list.
Leave **Surface B** empty on a listed interaction to use a wildcard. Resolution order is:

1. exact pair;
2. either concrete surface paired with an empty Surface B;
3. the separate **Default Interaction**.

### External subprofiles

The main settings can reference an ordered array of external `PhysSoundSubprofile` assets. Each subprofile can define surfaces and interactions for a reusable content set. Later subprofiles override earlier entries, and dictionaries authored directly in the main settings override all external subprofiles. The separate **Default Interaction** remains owned by the main settings.

The package also includes separate `Starter Pack 3D` and `Starter Pack 2D` samples. Each starter pack contains a ready-to-use profile, sounds, and standalone Physics Material assets.

### Interactive audio preview and markup

Select the settings asset or any Phys Sound subprofile and open Unity's Preview panel at the bottom of the Inspector. Use **Slice Force** to split the nonlinear impact-force line into editable ranges, then use **Slice Impact Clips** to select every source clip in a range and mark its waveform. Each valid marked region becomes a random runtime impact variation and plays directly from the source recording without creating sliced assets. **Slice Slide Clips** provides the corresponding loop workflow. Use the mouse wheel to zoom around the cursor, middle-drag or Alt + left-drag to pan, and **Fit** to restore the full clip. **Auto Detect** replaces the current markup with detected regions; compact MinMaxSliders control the accepted sound/pause volume and duration ranges. Region edits support Unity Undo. **Export** remains available when standalone WAV AudioClips are useful.

### Voice pool and spatial audio

The settings reference one `AudioSource` prefab. Configure mixer routing, spatial blend, rolloff, distance, Doppler, spread, priority, reverb and other source properties directly on that prefab.

The same Project Settings page controls:

- maximum pooled voices;
- impact cooldown per collider pair;
- slide fade, pitch and position smoothing;
- the emitter prefab used by every pooled voice.

Impact and slide sounds use separate pooled emitters when their contact positions differ. No `AudioSource` is added to gameplay prefabs.

## Contact backends

The **Contact Backend** setting selects one of two mutually exclusive Unity Physics 3D authoring workflows. Physics 2D always uses `PhysSoundObject` collision callbacks because `Physics2D` has no global equivalent of `Physics.ContactEvent`.

### Provides Contacts

This is the componentless workflow.

Enable the standard **Provides Contacts** flag manually on every collider that should opt into Phys Sound. A collision is processed when at least one collider in the pair has the flag enabled.

Phys Sound:

- subscribes to `Physics.ContactEvent`;
- explicitly filters out pairs where neither collider has opted in;
- never scans scenes or prefabs;
- never changes `Collider.providesContacts`;
- does not provide a bulk toggle command.

This mode is useful when you want no Phys Sound components in scenes and prefer Unity's centralized contact stream.

### Components

Add `PhysSoundObject` to the root that receives collision callbacks, normally the same GameObject as the `Rigidbody`.

`PhysSoundObject` permanently declares only `OnCollisionEnter`. It does not declare `OnCollisionStay`.

When an entered surface interaction contains slide audio, Phys Sound adds one hidden runtime-only continuous-contact receiver to that object. The receiver exists only while at least one slide-capable contact is active and is destroyed after the last such contact exits. Impact-only objects therefore do not pay for continuous callbacks.

If both colliding objects contain `PhysSoundObject`, the runtime deduplicates the impact and assigns continuous tracking to one side.

`Collider.providesContacts` is ignored by this backend.

### Physics 2D components

Add `PhysSoundObject` to a GameObject that receives `OnCollisionEnter2D`, `OnCollisionStay2D` and `OnCollisionExit2D`. The same component supports both physics backends, and the runtime deduplicates pairs when both colliders contain it. The 3D **Contact Backend** selection does not affect this path.

## Runtime layout

Visible project entities:

```text
Project Settings > Audio > Phys Sound
PhysSoundObject only in Components mode
PhysSoundObject for Physics 2D contacts
standard PhysicsMaterial assets
standard PhysicsMaterial2D assets
standard Collider.providesContacts only in Provides Contacts mode
```

Runtime-only implementation details:

```text
one hidden persistent host
one bounded pool of hidden AudioSources
one persistent slide emitter per active collider pair
one-shot impact emitters from the same pool
a lazy hidden continuous receiver in Components mode
```

## Migration from 1.x

Phys Sound 2.0 is not API-compatible with 1.x.

Removed concepts include:

- `PhysSoundDatabase`;
- `PhysSoundKey`;
- `PhysSoundMaterial`;
- `PhysSoundAudioSet`;
- `PhysSoundAudioContainer`;
- manually configured impact and slide `AudioSource` components;
- the legacy temporary-audio pool;
- the legacy Physics 2D architecture and Terrain composition implementation.

Existing 1.x scenes and prefabs must be reconfigured through **Project Settings > Audio > Phys Sound**. The old `PhysSoundObject` script GUID is retained so existing components resolve to the new lightweight component instead of becoming missing scripts.

## Current scope

Phys Sound 2.0 currently supports:

- Unity Physics 3D collision impacts;
- Unity Physics 2D collision impacts through `PhysSoundObject`;
- continuous sliding;
- compound colliders through the actual contact colliders;
- `Rigidbody` and `ArticulationBody` velocities through Unity contact data;
- native Unity 3D audio and mixer routing.

Not yet ported:

- Terrain splat-layer composition;
- trigger sounds;
- rolling-specific audio;
- triggers and rolling-specific audio.

## License and origin

Phys Sound is distributed under the MIT License. The original PhysSound system was developed by Kevin Somers (`crazymonkey`). This repository's 2.0 line is a new implementation and retains the original license notice.
