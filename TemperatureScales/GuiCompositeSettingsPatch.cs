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

using Mono.Cecil.Cil;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Config;

namespace VSMods.TemperatureScales
{
    public static class GuiCompositeSettingsPatchHelpers
    {
        public static GuiComposer OnInterfaceOptions(GuiComposer composer)
        {
            int elementKey = composer.CurrentElementKey;
            ElementBounds textBounds = composer.GetStaticText($"element-{elementKey - 2}").Bounds;
            ElementBounds dropDownBounds = composer.GetDropDown($"element-{elementKey}").Bounds;

            string[] values = Enum.GetNames(typeof(TemperatureScale));

            composer
                .AddStaticText(Lang.Get("temperaturescales:settings-name-temperaturescale"), CairoFont.WhiteSmallishText(), textBounds = textBounds.BelowCopy(0, 2))
                .AddHoverText(Lang.Get("temperaturescales:settings-hover-temperaturescale"), CairoFont.WhiteSmallText(), 250, textBounds = textBounds.FlatCopy().WithFixedHeight(25.0))
                .AddDropDown(values, values, Array.IndexOf(values, ClientSettings.Inst.String[SystemTemperatureScales.SettingsKey]), OnTemperatureScaleChanged, dropDownBounds.BelowCopy(0, 14).WithFixedSize(300, 30));

            return composer;
        }

        private static void OnTemperatureScaleChanged(string newScale, bool _)
        {
            ClientSettings.Inst.String[SystemTemperatureScales.SettingsKey] = newScale;
        }
    }

    [HarmonyPatch(typeof(GuiCompositeSettings))]
    public static class GuiCompositeSettingsPatch
    {
        [HarmonyPatch("OnInterfaceOptions")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> OnInterfaceOptionsTranspiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var enumerator = codeInstructions.GetEnumerator();

            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
                if (enumerator.Current.operand as string == "setting-name-language")
                {
                    break;
                }
            }

            do
            {
                enumerator.MoveNext();
                yield return enumerator.Current;
            }
            while ((enumerator.Current.operand as MethodInfo)?.Name != "AddDropDown");

            yield return CodeInstruction.Call(typeof(GuiCompositeSettingsPatchHelpers), nameof(GuiCompositeSettingsPatchHelpers.OnInterfaceOptions));

            while (enumerator.MoveNext())
            {
                /*if (enumerator.Current.OperandIs(29.0))
                {
                    enumerator.Current.operand = 4.0;
                }
                else if (enumerator.Current.OperandIs(41.0))
                {
                    enumerator.Current.operand = 16.0;
                }*/

                yield return enumerator.Current;
            }
        }
    }
}