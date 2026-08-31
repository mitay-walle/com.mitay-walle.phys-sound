using System;
using UnityEngine;

namespace PhysSound
{
	internal readonly struct PhysSoundPairKey : IEquatable<PhysSoundPairKey>
	{
		private readonly EntityId _first;
		private readonly EntityId _second;

		internal PhysSoundPairKey(EntityId first, EntityId second)
		{
			if (first <= second)
			{
				_first = first;
				_second = second;
			}
			else
			{
				_first = second;
				_second = first;
			}
		}

		public bool Equals(PhysSoundPairKey other)
		{
			return _first == other._first && _second == other._second;
		}

		public override bool Equals(object obj)
		{
			return obj is PhysSoundPairKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (_first.GetHashCode() * 397) ^ _second.GetHashCode();
			}
		}
	}

	internal readonly struct PhysSoundContinuousKey : IEquatable<PhysSoundContinuousKey>
	{
		internal PhysSoundContinuousKey(PhysSoundPairKey pairKey, int interactionIndex, PhysSoundEmitterMode mode)
		{
			PairKey = pairKey;
			InteractionIndex = interactionIndex;
			Mode = mode;
		}

		internal PhysSoundPairKey PairKey { get; }
		internal int InteractionIndex { get; }
		internal PhysSoundEmitterMode Mode { get; }

		public bool Equals(PhysSoundContinuousKey other)
		{
			return PairKey.Equals(other.PairKey) &&
			       InteractionIndex == other.InteractionIndex &&
			       Mode == other.Mode;
		}

		public override bool Equals(object obj)
		{
			return obj is PhysSoundContinuousKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((PairKey.GetHashCode() * 397) ^ InteractionIndex) * 397 ^ (int)Mode;
			}
		}
	}
}
