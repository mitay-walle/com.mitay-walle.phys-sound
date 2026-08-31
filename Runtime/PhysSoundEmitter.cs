using UnityEngine;

namespace PhysSound
{
	internal struct PhysSoundEmitter
	{
		internal AudioSource Source;
		internal PhysSoundEmitterMode Mode;
		internal PhysSoundContinuousKey ContinuousKey;
		internal int InteractionIndex;
		internal Vector3 TargetPosition;
		internal float TargetVolume;
		internal float TargetPitch;
		internal float LastSeenAt;
		internal double ImpactEndDspTime;
		internal bool Stopping;
	}
}
