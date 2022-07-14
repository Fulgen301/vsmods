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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

using HarmonyLib;
using Vintagestory.API.Config;

namespace GuiExtensions
{
	internal class GuiElementMenuDropDown : GuiElementDropDown
	{
		public GuiElementMenuDropDown(ICoreClientAPI capi, string[] values, string[] names, int selectedIndex, SelectionChangedDelegate onSelectionChanged, ElementBounds bounds, CairoFont font, bool multiSelect) : base(capi, values, names, selectedIndex, onSelectionChanged, bounds, font, multiSelect)
		{
		}

		public override double DrawOrder => 1.0;
	}

	internal class GuiElementMenuToggleButton : GuiElementToggleButton
	{
		public GuiElementMenuToggleButton(ICoreClientAPI capi, string icon, string text, CairoFont font, Action<bool> OnToggled, ElementBounds bounds, bool toggleable = false) : base(capi, icon, text, font, OnToggled, bounds, toggleable)
		{
		}

		public override double DrawOrder => 1.0;
	}

	internal static partial class GuiComposeHelpers
	{
		/// <summary>
		/// Adds a menu dropdown to the current GUI instance.
		/// </summary>
		/// <param name="composer">The composer instance.</param>
		/// <param name="values">The values of the current dropdown.</param>
		/// <param name="names">The names of those values.</param>
		/// <param name="selectedIndex">The default selected index.</param>
		/// <param name="onSelectionChanged">The event fired when the index is changed.</param>
		/// <param name="bounds">The bounds of the index.</param>
		/// <param name="key">The name of this dropdown.</param>
		public static GuiComposer AddMenuDropDown(this GuiComposer composer, string[] values, string[] names, int selectedIndex, SelectionChangedDelegate onSelectionChanged, ElementBounds bounds, string key = null)
		{
			if (!composer.Composed)
			{
				composer.AddInteractiveElement(new GuiElementMenuDropDown(composer.Api, values, names, selectedIndex, onSelectionChanged, bounds, CairoFont.WhiteSmallText(), false), key);
			}
			return composer;
		}

		/// <summary>
		/// Creates a menu toggle button with the given parameters.
		/// </summary>
		/// /// <param name="composer">The composer instance.</param>
		/// <param name="text">The text of the button.</param>
		/// <param name="font">The font of the text.</param>
		/// <param name="onToggle">The event that happens once the button is toggled.</param>
		/// <param name="bounds">The bounding box of the button.</param>
		/// <param name="key">The name of the button for easy access.</param>
		public static GuiComposer AddMenuToggleButton(this GuiComposer composer, string text, CairoFont font, Action<bool> onToggle, ElementBounds bounds, string key = null)
		{
			if (!composer.Composed)
			{
				composer.AddInteractiveElement(new GuiElementMenuToggleButton(composer.Api, "", text, font, onToggle, bounds, true), key);
			}
			return composer;
		}
	}

	/// <summary>
	/// Allows the developer to add custom settings dialog panels.
	/// Panels consist of a key, which is also used for translation, and a delegate that is called to create the UI.
	/// 
	/// If you are not using <see cref="SystemGuiCompositeSettingsEx"/>, prior to any API calls, <see cref="Patch"/> must be called to patch the <see cref="GuiCompositeSettings"/> class.
	/// Then, panels can be added either by using <see cref="AddPanel(string, GuiCompositeSettingsEx.PanelActivatedDelegate)"/> for panels that should be used for all settings dialogs or 
	/// <see cref="AddPanel(GuiCompositeSettings, string, GuiCompositeSettingsEx.PanelActivatedDelegate)"/> for panels that should only be used by said settings dialog.
	/// 
	/// Do not forget to call <see cref="Unpatch"/> on cleanup if you are not using <see cref="SystemGuiCompositeSettingsEx"/>!
	/// 
	/// <example>
	/// <code>
	/// public override void StartClientSide(ICoreClientAPI capi)
	/// {
	///		// GuiCompositeSettingsEx.Patch(); -- if not using see cref="SystemGuiCompositeSettingsEx"/>
	///		GuiCompositeSettingsEx.AddPanel("testpanel", (settings, on) =>
	///		{
	///			GuiComposer composer = settings.LoadComposerEx("testpanel", "testpanel");
	///			// Add your GUI elements here using standard UI elements.
	///			// Be aware that the header is 75.0 pixels high.
	///			
	///			composer.EndChildElements(); // title bar
	///			composer.Compose();
	///			settings.LoadComposer(composer);
	///		};
	/// }
	/// </code>
	/// </example>
	/// </summary>

	public static partial class GuiCompositeSettingsEx
	{
		private class Members
		{
			public class Panel
			{
				public ElementBounds Bounds;
				public PanelActivatedDelegate Delegate;
			}

			public Dictionary<string, Panel> Panels = new();
			public bool ShowButtons = false;
		}

		private static readonly Dictionary<GuiCompositeSettings, Members> ExtraMembers = new();
		private static readonly Dictionary<string, PanelActivatedDelegate> ExtraPanels = new();
		private static readonly ElementBounds oButtonBounds = DefaultButtonBounds();

		private static readonly string ID = "com.github.fulgen301.guicompositesettingsex";

		#region Helpers

		private static void EnsurePatched()
		{
			if (!Patched)
			{
				throw new Exception("Not patched");
			}
		}

		private static void EnsureRegistered(this GuiCompositeSettings @this)
		{
			EnsurePatched();

			try
			{
				ExtraMembers.Add(@this, new Members()
				{
					Panels = ExtraPanels.ToDictionary(p => p.Key, p => new Members.Panel()
					{
						Bounds = DefaultButtonBounds(),
						Delegate = p.Value
					})
				});
			}
			catch (ArgumentException)
			{
			}
		}

		private static double GetWidthForKey(string key, CairoFont font)
		{
			return font.GetTextExtents(Lang.Get(key)).Width / ClientSettings.GUIScale + 15.0;
		}

		#endregion

		#region Patches

#pragma warning disable IDE0051 // Remove unused private members

		private static void OpenSettingsMenu(GuiCompositeSettings __instance)
		{
			__instance.EnsureRegistered();
		}

		private static void Destructor(GuiCompositeSettings __instance)
		{
			ExtraMembers.Remove(__instance);
		}

		private static void ComposerHeaderPrefix(GuiCompositeSettings __instance, bool ___onMainscreen)
		{
			if (!___onMainscreen)
			{
				__instance.EnsureRegistered();
				ExtraMembers[__instance].ShowButtons = false;
			}
		}

		private static void ComposerHeaderPostfix(string currentTab, GuiCompositeSettings __instance, ref GuiComposer __result, bool ___onMainscreen)
		{
			if (___onMainscreen)
			{
				return;
			}

			__instance.EnsureRegistered();

			__result.AddMenuToggleButton(Lang.Get("Other"), CairoFont.ButtonText(), on => ShowButtons(__instance, on), oButtonBounds, "other");

			foreach (var pair in ExtraMembers[__instance].Panels)
			{
				__result.AddMenuToggleButton(Lang.Get($"settings-{pair.Key}-header"), CairoFont.ButtonText(), on => pair.Value.Delegate(__instance, on), pair.Value.Bounds, pair.Key);
				__result.GetToggleButton(pair.Key).SetValue(pair.Key == currentTab);
			}

			//__result.BeginChildElements(ElementBounds.Fixed(0, 75));

			/*

			string[] values = ExtraMembers[__instance].Panels.Keys.Select(k => k).ToArray();
			string[] names = ExtraMembers[__instance].Panels.Keys.Select(k => Lang.Get($"settings-{k}-header")).ToArray();

			__result.AddMenuDropDown(new[] { "other" }, new[] { Lang.Get("Other") }, 0, (code, on) => OnMenuDropDownSelectionChanged(__instance, code, on), oButtonBounds, "other");
			
			var dropDown = __result.GetDropDown("other");
			dropDown.SetList(values, names);
			*/
		}

		private static void OnMenuDropDownSelectionChanged(GuiCompositeSettings settings, string code, bool on)
		{
			if (code == "other")
			{
				return;
			}

			ExtraMembers[settings].Panels[code].Delegate(settings, on);
		}

		private static void ShowButtons(GuiCompositeSettings settings, bool on)
		{
			ExtraMembers[settings].ShowButtons = on;
			var composer = (GuiComposer) AccessTools.DeclaredField(typeof(GuiCompositeSettings), "composer").GetValue(settings);
			var dButtonBounds = (ElementBounds) AccessTools.DeclaredField(typeof(GuiCompositeSettings), "dButtonBounds").GetValue(settings);

			// yes, only calling the patch, _not_ the original method
			updateButtonBounds(settings, dButtonBounds);
			composer.ReCompose();
		}


#pragma warning disable IDE1006
		private static void updateButtonBounds(GuiCompositeSettings __instance, ElementBounds ___dButtonBounds)
#pragma warning restore IDE1006
		{
			__instance.EnsureRegistered();

			CairoFont cairoFont = CairoFont.ButtonText();

			oButtonBounds.WithFixedWidth(GetWidthForKey("other", cairoFont)).FixedRightOf(___dButtonBounds, 15.0);

			ElementBounds lastBounds = oButtonBounds.FlatCopy().WithFixedOffset(0.0, 15.0);
			foreach (var pair in ExtraMembers[__instance].Panels)
			{
				ElementBounds bounds = pair.Value.Bounds;
				bounds.ParentBounds = ___dButtonBounds.ParentBounds;

				if (ExtraMembers[__instance].ShowButtons)
				{
					double width = GetWidthForKey(Lang.Get($"settings-{pair.Key}-header"), cairoFont);
					bounds.WithFixedWidth(width);

					// FixedUnder always adds to fixedY, don't use that
					bounds.fixedY = lastBounds.fixedY + lastBounds.fixedHeight + 5.0;
				}
				else
				{
					bounds.WithFixedWidth(0.0);
				}

				bounds.FixedRightOf(___dButtonBounds, 15.0);
				bounds.fixedX += oButtonBounds.fixedWidth - bounds.fixedWidth;

				lastBounds = pair.Value.Bounds;
			}
		}

#pragma warning restore IDE0051

		#endregion
	}
}
