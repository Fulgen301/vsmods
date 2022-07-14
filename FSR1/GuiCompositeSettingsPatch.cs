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

using System;
using System.Collections.Generic;
using System.Text;

using Vintagestory.Client;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace VSMods.FSR1
{
    public static class GuiCompositeSettingsPatchHelpers
    {
        public static GuiComposer OnGraphicsOptions(GuiComposer composer)
        {
            ElementBounds elementBounds = composer.GetSlider("resolutionSlider").Bounds;
            int elementKey = composer.CurrentElementKey;
            ElementBounds textBounds = composer.GetHoverText($"element-{elementKey}").Bounds;

            composer
                .AddStaticText(Lang.Get("setting-name-easu"), CairoFont.WhiteSmallishText(), textBounds = textBounds.BelowCopy())
                .AddHoverText(Lang.Get("setting-hover-easu"), CairoFont.WhiteSmallText(), 250, textBounds.FlatCopy())
                .AddSwitch(OnEasuChanged, elementBounds = elementBounds.BelowCopy(0, 10), "easuSwitch")

                .AddStaticText(Lang.Get("setting-name-rcas"), CairoFont.WhiteSmallishText(), textBounds = textBounds.BelowCopy())
                .AddHoverText(Lang.Get("setting-hover-rcas"), CairoFont.WhiteSmallText(), 250, textBounds.FlatCopy())
                .AddSwitch(OnRcasChanged, elementBounds = elementBounds.BelowCopy(0, 10), "rcasSwitch");

            composer.GetSwitch("easuSwitch").SetValue(ClientSettings.Inst.Bool["easu"]);
            composer.GetSwitch("rcasSwitch").SetValue(ClientSettings.Inst.Bool["rcas"]);
            return composer;
        }

        private static void OnEasuChanged(bool value)
        {
            ClientSettings.Inst.Bool["easu"] = value;
        }

        private static void OnRcasChanged(bool value)
        {
            ClientSettings.Inst.Bool["rcas"] = value;
        }
    }

    [HarmonyPatch(typeof(GuiCompositeSettings))]
    public static class GuiCompositeSettingsPatch
    {
        [HarmonyTranspiler]
        [HarmonyPatch("ComposerHeader")]
        static IEnumerable<CodeInstruction> ComposerHeaderTranspiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            bool changed = false;
            foreach (var instruction in codeInstructions)
            {
                if (!changed && instruction.OperandIs(665))
                {
                    changed = true;
                    yield return new CodeInstruction(OpCodes.Ldc_R8, 665.0 + 80);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch("OnGraphicsOptions")]
        static IEnumerable<CodeInstruction> OnGraphicsOptionsTranspiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var endChildElements = AccessTools.DeclaredMethod(typeof(GuiComposer), nameof(GuiComposer.EndChildElements));
            foreach (var instruction in codeInstructions)
            {
                if (instruction.Calls(endChildElements))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(GuiCompositeSettingsPatchHelpers), nameof(GuiCompositeSettingsPatchHelpers.OnGraphicsOptions)));
                }

                yield return instruction;
            }
        }
    }
}
