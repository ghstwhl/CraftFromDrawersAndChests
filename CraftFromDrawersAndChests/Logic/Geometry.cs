using System;

namespace CraftFromDrawersAndChests.Logic
{
    /// <summary>
    /// A minimal, dependency-free 3D vector. It exists so range maths can be unit-tested
    /// outside the game: this type (and everything else under <c>Logic/</c>) must never
    /// reference UnityEngine, Valheim, BepInEx or Jotunn.
    /// </summary>
    public readonly struct Vec3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Squared distance to <paramref name="other"/>; no square root, for cheap range checks.</summary>
        public float SqrDistanceTo(Vec3 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            float dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>Euclidean distance to <paramref name="other"/>.</summary>
        public float DistanceTo(Vec3 other)
        {
            return (float)Math.Sqrt(SqrDistanceTo(other));
        }
    }
}
