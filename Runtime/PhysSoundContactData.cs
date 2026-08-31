using UnityEngine;

namespace PhysSound
{
	internal struct PhysSoundContactData
	{
		internal Collider FirstCollider;
		internal Collider SecondCollider;
		internal Vector3 Position;
		internal Vector3 Normal;
		internal Vector3 RelativeVelocity;
		internal float Impulse;
	}
}