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

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VSMods.TemporalStormGear
{
    public class SystemTemporalStormGear : ModSystem
    {
        public const string HarmonyID = "org.github.fulgen301.vsmods.temporalstormgear";

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            Harmony.DEBUG = true;
            new Harmony(HarmonyID).PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            new Harmony(HarmonyID).UnpatchAll();
            base.Dispose();
        }

        public static float GetTemporalStormFactor(ICoreClientAPI capi)
        {
            var stormData = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData;
            if (stormData.nowStormActive)
            {
                double activeDaysLeft = stormData.stormActiveTotalDays - capi.World.Calendar.TotalDays;

                if (activeDaysLeft < 0.02)
                {
                    return (float) (activeDaysLeft / 0.02);
                }

                return 1.0f;
            }

            double nextStormDaysLeft = stormData.nextStormTotalDays - capi.World.Calendar.TotalDays;
            if (nextStormDaysLeft >= 0.35)
            {
                return 0.0f;
            }

            return (float) ((0.35 - nextStormDaysLeft) / 0.35);
        }
    }

    [HarmonyPatch(typeof(HudHotbar), "renderGear")]
    public static class HudHotbarPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            int dupCounter = 0;
            foreach (var instruction in codeInstructions)
            {
                if (instruction.opcode == OpCodes.Dup)
                {
                    if (dupCounter == 0)
                    {
                        ++dupCounter;
                    }
                    else if (dupCounter == 1)
                    {
                        yield return new CodeInstruction(OpCodes.Dup);
                        yield return new CodeInstruction(OpCodes.Ldstr, "temporalStormFactor");
                        //yield return CodeInstruction.LoadField(typeof(ShaderPrograms), nameof(ShaderPrograms.Guigear));
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return CodeInstruction.LoadField(typeof(GuiDialog), "capi");
                        yield return CodeInstruction.Call(typeof(SystemTemporalStormGear), nameof(SystemTemporalStormGear.GetTemporalStormFactor));
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ShaderProgramGuigear), "Uniform", new[] { typeof(string), typeof(float) }));

                        ++dupCounter;
                    }
                }

                yield return instruction;
            }
        }
    }
}