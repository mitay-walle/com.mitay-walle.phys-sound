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
}