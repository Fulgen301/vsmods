// MIT License

// Copyright(c) 2026 George Tokmaji

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
using System.Linq;
using System.Reflection;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

[assembly: ModInfo("CookingPotLock", "cookingpotlock", Authors = new[] {"Fulgen"}, Description = "Prevents the cooking pot from being taken out of a firepit if it contains cooking ingredients.", Version = "1.0.0", Side = "Universal")]

#if !NET5
namespace System.Runtime.CompilerServices
{
	public static class IsExternalInit
	{
	}
}
#endif

namespace VSMods.CookingPotLock
{
	public class SystemCookingPotLock : ModSystem
	{
		public const string HarmonyID = "org.github.fulgen301.vsmods.cookingpotlock";

		public override void Start(ICoreAPI api)
		{
			Harmony harmony = new(HarmonyID);
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

	}

	[HarmonyPatch(typeof(ItemSlotInput), nameof(ItemSlotInput.CanTake))]
	public static class ItemSlotInputPatch
	{
		public static bool Prefix(ItemSlotInput __instance, ref bool __result)
		{
			if (__instance.IsCookingContainer)
			{
				bool hasItem = (__instance.Inventory as InventorySmelting)?.CookingSlots.Any(slot => !slot.Empty) ?? false;
				if (hasItem)
				{
					__result = false;
					return false;
				}
			}

			return true;
		}
	}
}