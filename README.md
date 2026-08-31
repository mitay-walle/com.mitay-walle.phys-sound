# Phys Sound 2

Phys Sound turns Unity 3D and 2D collision contacts into positioned impact and sliding audio. Version 2.0 is a breaking rewrite for Unity 6000.6.0a7 and newer, built around one settings asset and a bounded pool of native spatial `AudioSource` objects.

![Phys Sound settings](Documentation/Screenshot_1.png)

## Requirements

- Unity 6000.6.0a7 or newer
- Unity Audio and Physics 3D modules
- Physics 2D module only for 2D contacts

## Installation

Install the Git URL through Unity Package Manager:

```text
https://github.com/mitay-walle/com.mitay-walle.phys-sound.git
```

Then:

1. Open **Edit > Project Settings > Audio > Phys Sound**.
2. Press **Create Settings**.
3. Add the **Starter Pack 3D** or **Starter Pack 2D** sample, or configure your own surfaces and interactions.
4. Choose a contact backend for Physics 3D.

Settings and the pooled emitter prefab are created under `Assets/Resources/PhysSound`. Phys Sound does not automatically modify scenes, prefabs, colliders, or Physics Materials.

## Configuration

Select the settings asset or a `PhysSoundSubprofile` to use the editor at the bottom of the Inspector.

### Materials

Create named surfaces and assign their `PhysicsMaterial` or `PhysicsMaterial2D` assets. Unmapped materials use `Default`.

![Materials tab](Documentation/Preview_Materials.png)

### Mapping

Map unordered surface pairs to interactions. Empty **Surface B** acts as a wildcard; the separate **Default Interaction** is the final fallback.

![Mapping tab](Documentation/Preview_Mapping.png)

### Force

Split the impact-force axis into ranges and audition each range.

![Force tab](Documentation/Preview_Force.png)

### Impact

Assign impact clips and mark the playable waveform region for the selected force range.

![Impact tab](Documentation/Preview_Impact.png)

### Slide

Assign a looping slide clip and mark its loop region.

![Slide tab](Documentation/Preview_Slide.png)

### Curves

Adjust volume and pitch response for impact and slide sounds.

![Curves tab](Documentation/Preview_Curves.png)

Waveform tools include playback, Undo, zoom, pan, automatic region detection, and optional WAV export. External subprofiles are applied in order; later subprofiles override earlier ones.

## Physics 3D backends

- **Provides Contacts**: enable Unity's **Provides Contacts** flag on participating colliders. No Phys Sound scene component is required.
- **Components**: add `PhysSoundObject` to the root that receives collision callbacks, normally the `Rigidbody` GameObject.

Physics 2D always uses `PhysSoundObject`. Enable **Force Disable Physics 2D** in the settings when the package should omit its 2D runtime code.

## Audio and samples

Configure mixer routing, spatial blend, rolloff, distance, Doppler, spread, priority, and reverb on the referenced emitter prefab. Pool size, impact cooldown, and slide smoothing are configured in the Phys Sound settings.

The package includes Example Scene and Starter Pack samples for both 3D and 2D.

## Migrating from 1.x

Phys Sound 2.0 is not API-compatible with 1.x. Reconfigure existing content through **Project Settings > Audio > Phys Sound**. The old `PhysSoundObject` script GUID is retained so existing components do not become missing scripts.

## License

Phys Sound is distributed under the MIT License. The original PhysSound system was developed by Kevin Somers (`crazymonkey`).
