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

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace GuiExtensions
{
	public static partial class GuiCompositeSettingsEx
	{
		#region API

		/// <summary>
		/// Whether the dialog has been patched.
		/// <seealso cref="Patch"/>
		/// <seealso cref="Unpatch"/>
		/// <seealso cref="SystemGuiCompositeSettingsEx"/>
		/// </summary>
		public static bool Patched { get; private set; } = false;

		/// <summary>
		/// Callback for panel activation. Build your GUI here.
		/// Obtain a <see cref="GuiComposer"/> instance via <see cref="ComposerHeaderEx(GuiCompositeSettings, string, string)"/>, then load it with <see cref="LoadComposer(GuiCompositeSettings, GuiComposer)"/>.
		/// <seealso cref="GuiCompositeSettingsEx"/>
		/// </summary>
		/// <param name="settings">The settings dialog instance.</param>
		/// <param name="on">Whether the button is pressed.</param>
		public delegate void PanelActivatedDelegate(GuiCompositeSettings settings, bool on);

		/// <summary>
		/// Patches the dialog.
		/// </summary>

		public static void Patch()
		{
			if (Patched)
			{
				return;
			}

			var harmony = new Harmony(ID);
			harmony.Patch(AccessTools.DeclaredMethod(typeof(GuiCompositeSettings), "OpenSettingsMenu"), prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(GuiCompositeSettingsEx), "OpenSettingsMenu")));
			harmony.Patch(AccessTools.Method(typeof(object), "Finalize"), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(GuiCompositeSettingsEx), "Destructor")));
			harmony.Patch(AccessTools.DeclaredMethod(typeof(GuiCompositeSettings), "ComposerHeader"), prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(GuiCompositeSettingsEx), "ComposerHeaderPrefix")), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(GuiCompositeSettingsEx), "ComposerHeaderPostfix")));
			harmony.Patch(AccessTools.DeclaredMethod(typeof(GuiCompositeSettings), "updateButtonBounds"), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(GuiCompositeSettingsEx), "updateButtonBounds")));

			Patched = true;
		}

		/// <summary>
		/// Unpatches the dialog.
		/// </summary>

		public static void Unpatch()
		{
			if (!Patched)
			{
				return;
			}

			new Harmony(ID).UnpatchAll(ID);
			Patched = false;
		}

		/// <summary>
		/// The default bounds for a menu button.
		/// </summary>
		/// <returns>The default bounds for a menu button.</returns>

		public static ElementBounds DefaultButtonBounds()
		{
			return ElementBounds.Fixed(0.0, 0.0, 0.0, 40.0).WithFixedPadding(0.0, 3.0);
		}

		/// <summary>
		/// The default bounds for a menu button.
		/// </summary>
		/// <returns>The default bounds for a menu button.</returns>

		public static ElementBounds DefaultButtonBounds(this GuiCompositeSettings _) => DefaultButtonBounds();

		/// <summary>
		/// Adds a settings panel for all settings dialogs.
		/// </summary>
		/// <param name="key">The key to use for the panel. Will be used as the key for the button and as translation string for the button name ("settings-{key}-header").</param>
		/// <param name="delegate">The delegate to call when the panel has been selected. Should build the GUI.</param>
		/// <seealso cref="AddPanel(GuiCompositeSettings, string, PanelActivatedDelegate)"/>
		public static void AddPanel(string key, PanelActivatedDelegate @delegate)
		{
			EnsurePatched();
			ExtraPanels[key] = @delegate;
		}

		/// <summary>
		/// Adds a settings panel for this settings dialog only.
		/// </summary>
		/// <param name="this">The settings dialog instance.</param>
		/// <param name="key">The key to use for the panel. Will be used as the key for the button and as translation string for the button name ("settings-{key}-header").</param>
		/// <param name="delegate">The delegate to call when the panel has been selected. Should build the GUI.</param>
		/// <seealso cref="AddPanel(string, PanelActivatedDelegate)"/>

		public static void AddPanel(this GuiCompositeSettings @this, string key, PanelActivatedDelegate @delegate)
		{
			@this.EnsureRegistered();
			ExtraMembers[@this].Panels[key] = new Members.Panel()
			{
				Bounds = DefaultButtonBounds(),
				Delegate = @delegate
			};
		}

		/// <summary>
		/// Returns a GuiComposer instance with the dialog header already created. Must be used for dialog creation.
		/// </summary>
		/// <remarks>
		/// This function calls <see cref="GuiComposer.BeginChildElements(ElementBounds)"/> internally, which needs to be matched.
		/// </remarks>
		/// <param name="this">The settings dialog instance.</param>
		/// <param name="dialogName">The dialog name.</param>
		/// <param name="currentTab">The currently selected tab. See the parameter <c>key</c> of <see cref="AddPanel(string, PanelActivatedDelegate)" />. </param>
		/// <returns>A GuiComposer instance with the dialog header already created.</returns>
		/// <seealso cref="LoadComposer(GuiCompositeSettings, GuiComposer)"/>

		public static GuiComposer ComposerHeaderEx(this GuiCompositeSettings @this, string dialogName, string currentTab)
		{
			@this.EnsureRegistered();
			return (GuiComposer) AccessTools.DeclaredMethod(typeof(GuiCompositeSettings), "ComposerHeader").Invoke(@this, new string[] { dialogName, currentTab });
		}

		/// <summary>
		/// Sets the composer as the currently used composer for the dialog panel. Must be used after a call to <see cref="GuiComposer.Compose(bool)"/>.
		/// </summary>
		/// <param name="this">The settings dialog instance.</param>
		/// <param name="composer">The composer to use. Must be obtained by a call from <see cref="ComposerHeaderEx(GuiCompositeSettings, string, string)"/>.</param>
		/// <seealso cref="ComposerHeaderEx(GuiCompositeSettings, string, string)"/>

		public static void LoadComposer(this GuiCompositeSettings @this, GuiComposer composer)
		{
			AccessTools.DeclaredField(typeof(GuiCompositeSettings), "composer").SetValue(@this, composer);
			((IGameSettingsHandler) AccessTools.DeclaredField(typeof(GuiCompositeSettings), "handler").GetValue(@this)).LoadComposer(composer);
		}

		#endregion
	}
}
