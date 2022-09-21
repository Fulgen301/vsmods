// MIT License

// Copyright(c) 2022 George Tokmaji

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#if !NET5
namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit
    {
    }
}
#endif

namespace VSMods.TemporalTech
{
    public interface ITemporalStabilityModifier
    {
        public class Comparer : IComparer<ITemporalStabilityModifier>
        {
            public int Compare(ITemporalStabilityModifier x, ITemporalStabilityModifier y)
            {
                return x.Priority.CompareTo(y.Priority);
            }
        }

        double Priority { get; }

        public float ModifyTemporalStability(double x, double y, double z, float stability);
    }

    public abstract class AreaModifier
    {
        public float Interpolate(double x, double y, double z, float stability1, float stability2)
        {
            return stability1 + InterpolationFactor(x, y, z) * (stability2 - stability1);
        }

        public abstract float InterpolationFactor(double x, double y, double z);
    }

    public class SphereModifier : AreaModifier
    {
        private readonly BlockPos pos;
        private readonly double radiusSquared;

        public SphereModifier(BlockPos pos, double radius)
        {
            this.pos = pos;
            radiusSquared = radius * radius;
        }

        public override float InterpolationFactor(double x, double y, double z)
        {
            return (float) GameMath.Clamp(1.0 - pos.DistanceSqTo(x, y, z) / radiusSquared, 0.0, 1.0);
        }
    }

    public class SystemTemporalTech : ModSystem
    {
        public const string HarmonyID = "org.github.fulgen301.vsmods.temporaltech";

        private SortedSet<ITemporalStabilityModifier> modifiers;

        public override void Start(ICoreAPI api)
        {
            modifiers = new(new ITemporalStabilityModifier.Comparer());

            Harmony harmony = new(HarmonyID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void AddTemporalStabilityModifier(ITemporalStabilityModifier modifier)
        {
            modifiers.Add(modifier);
        }

        public void RemoveTemporalStabilityModifier(ITemporalStabilityModifier modifier)
        {
            modifiers.Remove(modifier);
        }

        public void ModifyTemporalStability(double x, double y, double z, ref float stability)
        {
            foreach (var modifier in modifiers)
            {
                stability = GameMath.Clamp(modifier.ModifyTemporalStability(x, y, z, stability), 0.0f, 1.5f);
            }
        }
    }

    [HarmonyPatch(typeof(SystemTemporalStability), nameof(SystemTemporalStability.GetTemporalStability), new[] { typeof(double), typeof(double), typeof(double) })]
    public static class SystemTemporalStabilityPatch
    {
        public static void Postfix(double x, double y, double z, ref float __result, ICoreAPI ___api)
        {
            ___api.ModLoader.GetModSystem<SystemTemporalTech>().ModifyTemporalStability(x, y, z, ref __result);
        }
    }
}