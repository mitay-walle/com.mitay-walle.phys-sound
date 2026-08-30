# Phys Sound 2

Phys Sound turns Unity 3D collision contacts into positioned impact and sliding audio.

Version 2.0 is a breaking rewrite for Unity 6000.5.0f1 and newer. It removes the legacy database, key, material, audio-container and temporary-audio-object workflow. Configuration lives in one Project Settings page, while playback uses one hidden bounded pool of native spatial `AudioSource` objects.

Imported `AudioClip` assets implement Unity's `IAudioGenerator` API in Unity 6.5 and are assigned through `AudioSource.resource`. Unity continues to handle attenuation, spatialization, mixer routing, Doppler and reverb for every pooled emitter.

## Requirements

- Unity 6000.5.0f1 or newer
- Unity Audio module
- Unity Physics 3D module

The package declares no mandatory UPM dependencies. Audio, Physics 3D, Physics 2D and Terrain are detected through assembly-definition version defines, as in Phys Sound 1.x. The 2.0 runtime currently implements Physics 3D only.

## Installation

Install the package from its Git URL in Unity Package Manager:

```text
https://github.com/mitay-walle/com.mitay-walle.phys-sound.git
```

Then open:

```text
Edit > Project Settings > Phys Sound
```

Press **Create Settings**. This explicit action creates the backing asset at:

```text
Assets/Resources/PhysSound/PhysSoundSettings.asset
```

The system never creates or modifies project assets, scenes, prefabs or collider settings automatically.

## Configuration

### Surfaces

A surface groups one or more standard Unity `PhysicsMaterial` assets under a stable name.

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

An interaction describes an unordered surface pair:

```text
Wood × Concrete == Concrete × Wood
```

Each interaction can contain:

- impact clips;
- an impact impulse range, volume curve, multiplier and random pitch range;
- looping slide clips;
- a slide speed range, volume curve, multiplier and pitch range.

Use `*` as a wildcard surface. Resolution order is:

1. exact pair;
2. either concrete surface paired with `*`;
3. `Default × *`;
4. `Default × Default`;
5. `* × *`.

### Voice pool and spatial audio

The same Project Settings page controls:

- maximum pooled voices;
- impact cooldown per collider pair;
- slide fade, pitch and position smoothing;
- `AudioMixerGroup`;
- spatial blend;
- rolloff mode;
- minimum and maximum distance;
- Doppler, spread, priority and reverb-zone mix.

Impact and slide sounds use separate pooled emitters when their contact positions differ. No `AudioSource` is added to gameplay prefabs.

## Contact backends

The **Contact Backend** setting selects one of two mutually exclusive authoring workflows.

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

## Runtime layout

Visible project entities:

```text
Project Settings > Phys Sound
PhysSoundObject only in Components mode
standard PhysicsMaterial assets
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
- the legacy Physics 2D and Terrain composition implementations.

Existing 1.x scenes and prefabs must be reconfigured through **Project Settings > Phys Sound**. The old `PhysSoundObject` script GUID is retained so existing components resolve to the new lightweight component instead of becoming missing scripts.

## Current scope

Phys Sound 2.0 currently supports:

- Unity Physics 3D collision impacts;
- continuous sliding;
- compound colliders through the actual contact colliders;
- `Rigidbody` and `ArticulationBody` velocities through Unity contact data;
- native Unity 3D audio and mixer routing.

Not yet ported:

- Physics 2D;
- Terrain splat-layer composition;
- trigger sounds;
- rolling-specific audio;
- the old sample scenes.

## License and origin

Phys Sound is distributed under the MIT License. The original PhysSound system was developed by Kevin Somers (`crazymonkey`). This repository's 2.0 line is a new implementation and retains the original license notice.
