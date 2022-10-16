# PhysSound UPM-Package
[Here's the license](https://forum.unity.com/threads/open-source-physsound-physics-audio-system.334297/page-2#post-4399633)  
[Here's the download from the author](https://forum.unity.com/threads/open-source-physsound-physics-audio-system.334297/page-2#post-4399633)  

The PhysSound system adds the ability to bring your physics to life through the use of impact and sliding
sounds. The system works with both 2D and 3D physics.

Install [by Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html)

## Added
- 2D Samples Scene
- 2D Trigger sample
- surface type is recognized by PhysicMaterial
- disabling Physics2D, Physics, Terrain Unity modules strip code through custom scripting define Symbols
- [ArticluationBody](https://docs.unity3d.com/Manual/class-ArticulationBody.html) support, samples
- delay for impact sound
## Changed
- Converted to UPM package
- Demo converted to UPM package Samples
- Custom Inspectors removed
- codegen PhysMaterialType (int + string) replaced with PhysSoundKey (ScriptableObject)
- Fallback now PhysSoundMaterial, and became recursive
- collision events placed to separated components
## Fixed
- optimization distance check -> sqr distance check
- Update() replaced with FixedUpdate() to fix KinematicVelocity became 0,0,0

There are 4 parts of the system: 
- PhysSoundDatabase : ScriptableObject

![](https://github.com/mitay-walle/com.scruffy-rules.phys-sound/blob/master/Documentation/Screenshot_2.png)
- PhysSoundKey : ScriptableObject

![](https://github.com/mitay-walle/com.scruffy-rules.phys-sound/blob/master/Documentation/Screenshot_4.png)
- PhysSoundMaterial : ScriptableObject

![](https://github.com/mitay-walle/com.scruffy-rules.phys-sound/blob/master/Documentation/Screenshot_1.png)

- PhysSoundObject : MonoBehaviour

![](https://github.com/mitay-walle/com.scruffy-rules.phys-sound/blob/master/Documentation/Screenshot_3.png)
