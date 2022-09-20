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
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.Gui;
using Vintagestory.Client.NoObf;

namespace VSMods.TemperatureScales
{
    [DefaultValue(Celsius)]
    public enum TemperatureScale
    {
        Celsius,
        Fahrenheit,
        Kelvin
    }

    public class SystemTemperatureScales : ModSystem
    {
        public const string HarmonyID = "org.github.fulgen301.vsmods.temperaturescales";
        public const string SettingsKey = $"{HarmonyID}.scale";

        private const float AbsoluteZeroInKelvin = 273.15f;

        public static SystemTemperatureScales Instance { get; private set; }

        public TemperatureScale Scale { get; private set; }

        private readonly Regex temperatureRegex = new(@"(-?\d+(?:\.|,)?\d*)( ?)°([CFK])");
        private CultureInfo cultureInfo;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            Instance = this;

            string scale = ClientSettings.Inst.GetStringSetting(SettingsKey);
            if (scale is not null)
            {
                TemperatureScaleChanged(scale);
            }
            else
            {
                ClientSettings.Inst.String[SettingsKey] = TemperatureScale.Celsius.ToString();
                Scale = TemperatureScale.Celsius;
            }

            ClientSettings.Inst.String.AddWatcher(SettingsKey, TemperatureScaleChanged);

            CreateCultureInfo(ClientSettings.Language);
            ClientSettings.Inst.String.AddWatcher("language", CreateCultureInfo);

#if DEBUG
            Harmony.DEBUG = true;
#endif

            Harmony harmony = new(HarmonyID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }


        public override void Dispose()
        {
            new Harmony(HarmonyID).UnpatchAll();

            ClientSettings.Inst.String.RemoveWatcher("language", CreateCultureInfo);
            ClientSettings.Inst.String.RemoveWatcher(SettingsKey, TemperatureScaleChanged);
            Instance = null;
        }

        private void TemperatureScaleChanged(string newScale)
        {
            Scale = (TemperatureScale) Enum.Parse(typeof(TemperatureScale), newScale, true);
        }

        private void CreateCultureInfo(string language)
        {
            try
            {
                cultureInfo = CultureInfo.CreateSpecificCulture(language);
            }
            catch (CultureNotFoundException)
            {
                cultureInfo = CultureInfo.InvariantCulture;
            }
        }

        private static float CelsiusToFahrenheit(float celsius)
        {
            return celsius * (9.0f / 5.0f) + 32;
        }

        private static float FahrenheitToCelsius(float fahrenheit)
        {
            return fahrenheit - 32 * (5.0f / 9.0f);
        }

        public string ReplaceTemperatureScale(string text)
        {
            return temperatureRegex.Replace(text, match =>
            {
                TemperatureScale sourceScale = match.Groups[3].Value.Normalize() switch
                {
                    "C" => TemperatureScale.Celsius,
                    "F" => TemperatureScale.Fahrenheit,
                    "K" => TemperatureScale.Kelvin,
                    _ => throw new InvalidOperationException()
                };

                if (sourceScale == Scale)
                {
                    return match.Value;
                }

                if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float | NumberStyles.AllowThousands, cultureInfo, out float temperature))
                {
                    try
                    {
                        temperature = float.Parse(match.Groups[1].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new InvalidOperationException("Cannot parse a floating point number using the current language culture or the invariant culture.", e);
                    }
                }

                temperature = temperature switch
                {
                    _ when sourceScale == TemperatureScale.Celsius && Scale == TemperatureScale.Fahrenheit => CelsiusToFahrenheit(temperature),
                    _ when sourceScale == TemperatureScale.Celsius && Scale == TemperatureScale.Kelvin => temperature + AbsoluteZeroInKelvin,
                    _ when sourceScale == TemperatureScale.Fahrenheit && Scale == TemperatureScale.Celsius => FahrenheitToCelsius(temperature),
                    _ when sourceScale == TemperatureScale.Fahrenheit && Scale == TemperatureScale.Kelvin => FahrenheitToCelsius(temperature) + AbsoluteZeroInKelvin,
                    _ when sourceScale == TemperatureScale.Kelvin && Scale == TemperatureScale.Celsius => temperature - AbsoluteZeroInKelvin,
                    _ when sourceScale == TemperatureScale.Kelvin && Scale == TemperatureScale.Fahrenheit => CelsiusToFahrenheit(temperature - AbsoluteZeroInKelvin),
                    _ => throw new NotImplementedException()
                };

                return temperature.ToString(cultureInfo) + match.Groups[2] + Scale switch
                {
                    TemperatureScale.Celsius => "°C",
                    TemperatureScale.Fahrenheit => "°F",
                    TemperatureScale.Kelvin => "K",
                    _ => throw new InvalidOperationException()
                };
            });
        }

        internal static void ReplaceTemperatureScaleInLines(ICoreClientAPI api, TextLine[] lines)
        {
            if (api is MainMenuAPI) return;

            var modSystem = api.ModLoader.GetModSystem<SystemTemperatureScales>();
            foreach (var line in lines)
            {
                line.Text = modSystem.ReplaceTemperatureScale(line.Text);
            }
        }
    }

    [HarmonyPatch(typeof(TextDrawUtil), nameof(TextDrawUtil.DrawTextLine))]
    public static class TextDrawUtilPatch
    {
        public static void Prefix(ref string text)
        {
            text = SystemTemperatureScales.Instance.ReplaceTemperatureScale(text);
        }
    }

    /*[HarmonyPatch(typeof(GuiElementTextBase))]
    public static class GuiElementTextBasePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, new[] { typeof(ICoreClientAPI), typeof(string), typeof(CairoFont), typeof(ElementBounds)})]
        public static void ConstructorPostfix(ICoreClientAPI capi, string text, ref string ___text)
        {
            ___text = capi.ModLoader.GetModSystem<SystemTemperatureScales>().ReplaceTemperatureScale(text);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(GuiElementTextBase.DrawMultilineTextAt))]
        public static IEnumerable<CodeInstruction> DrawMultilineTextAtTranspiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            foreach (var instruction in codeInstructions)
            {
                yield return instruction;

                if (instruction.opcode == OpCodes.Stloc_0)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.LoadField(typeof(GuiElement), "api");

                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return CodeInstruction.Call(typeof(SystemTemperatureScales), nameof(SystemTemperatureScales.ReplaceTemperatureScaleInLines));
                }
            }
        }
    }*/
}