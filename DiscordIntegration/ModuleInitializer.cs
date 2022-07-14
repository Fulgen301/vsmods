// MIT License

// Copyright(c) 2021 George Tokmaji

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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using HarmonyLib;

using Vintagestory.Client.NoObf;

namespace DiscordIntegration
{
	internal static class Initializer
	{
		public const string HarmonyID = "org.github.fulgen301.discordintegration";

        [ModuleInitializer]
		public static void Initialize()
		{
			Debug.AutoFlush = true;
			try
			{
				GameVersion.DetermineSymbolsForCurrentVersion();
#if DEBUG
				Harmony.DEBUG = true;
#endif
				Harmony harmony = new(HarmonyID);
				harmony.Patch(GameVersion.SymbolsForCurrentVersion.StartMainDialog, prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(ModuleInitializer), nameof(StartMainDialogPatch))));
				harmony.Patch(AccessTools.DeclaredMethod(typeof(ClientPlatformWindows), "window_RenderFrame"), prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Initializer), nameof(window_RenderFrame))));
				harmony.PatchAll(Assembly.GetExecutingAssembly());
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				throw;
			}
		}

		private static void StartMainDialogPatch(object __instance)
		{
			DiscordSDK.NewInstance(__instance);
		}

		private static void window_RenderFrame()
		{
			DiscordSDK.RunCallbacks();
		}
	}
}