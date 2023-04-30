using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

using GuiExtensions;

namespace WorldConfigGUI
{
	using WorldConfigEntries = Dictionary<string, List<WorldConfigEntry>>;
	public partial class SystemWorldConfigGui
	{
		private double ListWidth => GuiCompositeSettingsEx.SettingsDialogWidth - 2.0 * GuiStyle.ElementToDialogPadding - 35.0;
		private const double ListHeight = 500;

		private string currentMod = "";
		private Dictionary<string, float> posY;
		private GuiComposer composer;
		private GuiCompositeSettings settings;
		private WorldConfigEntries worldConfigEntriesWork;

		public void PanelActivated(GuiCompositeSettings settings, bool on)
		{
			this.settings = settings;
			currentMod = "";
			posY = new Dictionary<string, float>();
			worldConfigEntriesWork = DeserializeJSON(lastWorldConfigJSON);

			SetupDialog();
		}

		public void SetupDialog()
		{
			composer = settings.ComposerHeaderEx("worldconfiggui", "worldconfiggui");

			var contentBounds = ElementBounds.Fixed(0, 0, ListWidth, ListHeight);
			ElementBounds clipBounds = contentBounds.ForkBoundingParent();
			ElementBounds insetBounds = contentBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, 0);
			ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + contentBounds.fixedWidth + 7).WithFixedWidth(20);
			var tabBounds = ElementBounds.Fixed(-200, 35, 200, 545);

			var modDropDownBounds = ElementBounds
				.FixedSize(200, 0)
				.FixedUnder(clipBounds, 2 * 5 + 8)
				.WithAlignment(EnumDialogArea.RightFixed);

			var applyButtonBounds = ElementBounds
				.FixedSize(0, 0)
				.FixedUnder(clipBounds, 2 * 5 + 8)
				.WithAlignment(EnumDialogArea.LeftFixed)
				.WithFixedPadding(20, 4);
			//.WithFixedAlignmentOffset(-6, 3);

			var bgBounds = ElementBounds.Fill.FixedUnder(ElementBounds.FixedSize(0, 75.0));//.WithFixedPadding(GuiStyle.ElementToDialogPadding);
			bgBounds.BothSizing = ElementSizing.FitToChildren;
			bgBounds.WithChildren(insetBounds, scrollbarBounds, modDropDownBounds, applyButtonBounds);

			composer//.AddVerticalTabs(GenerateTabs(out int currentTab), tabBounds, OnTabClicked, "verticalTabs")
				.BeginChildElements(bgBounds)
					.BeginClip(clipBounds)
						.AddInset(insetBounds, 3)
						.AddContainer(contentBounds, "worldconfig");

			GuiElementContainer guiElementContainer = composer.GetContainer("worldconfig");

			foreach (var element in AddPageElements(currentMod != "" ? worldConfigEntriesWork[currentMod] : worldConfigEntriesWork.Values.SelectMany(e => e)))
			{
				guiElementContainer.Add(element);
			}

			composer.EndClip();
			composer.AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar");
			composer.AddSmallButton(Lang.Get("general-apply"), OnButtonApply, applyButtonBounds);

			modDropDownBounds.WithFixedHeight(applyButtonBounds.fixedHeight + applyButtonBounds.fixedPaddingY * 2);
			int currentIndex = GenerateModEntries(out string[] values, out string[] names);
			//composer.AddDropDown(values, names, currentIndex, OnSelectionChanged, modDropDownBounds, "modDropDown");

			//composer.AddSmallButton(Lang.Get("general-close"), OnButtonClose, closeButtonBounds);
			composer.EndChildElements();
			composer.EndChildElements(); // title bar
			composer.Compose();
			settings.LoadComposer(composer);

			GuiElementScrollbar scrollbar = composer.GetScrollbar("scrollbar");
			scrollbar.SetHeights((float) ListHeight, (float) guiElementContainer.Bounds.fixedHeight);

			if (posY.TryGetValue(currentMod, out float pos))
			{
				scrollbar.CurrentYPosition = pos;
			}
			OnNewScrollbarValue(posY[currentMod]);
			//composer.GetVerticalTab("verticalTabs").SetValue(currentTab, false);
		}

		private void OnSelectionChanged(string code, bool selected)
		{
			currentMod = code;
			SetupDialog();
		}

		private void OnTabClicked(int tabIndex, GuiTab tab)
		{
			currentMod = tabIndex == 0 ? "" : tab.Name;
			SetupDialog();
		}

		private void OnNewScrollbarValue(float value)
		{
			GuiElementContainer container = composer.GetContainer("worldconfig");
			container.Bounds.fixedY = 3 - value;
			container.Bounds.CalcWorldBounds();
			posY[currentMod] = composer.GetScrollbar("scrollbar").CurrentYPosition;
		}

		private IEnumerable<GuiElementControl> AddPageElements(IEnumerable<WorldConfigEntry> entries)
		{
			//var textBounds = ElementBounds.FixedSize(ListWidth / 2, 20);
			//var controlBounds = ElementBounds.FixedSize(ListWidth / 2, 20).FixedRightOf(textBounds);

			var textBounds = ElementBounds.Fixed(0, 5, ListWidth / 2, 20);
			var controlBounds = textBounds.FlatCopy().FixedRightOf(textBounds);
			var textFont = CairoFont.WhiteSmallishText();//.Clone().WithWeight(Cairo.FontWeight.Bold);//CairoFont.WhiteDetailText();
			var controlFont = CairoFont.WhiteSmallText();

			foreach (WorldConfigEntry worldConfig in entries)
			{
				string key = "elem-" + worldConfig.Attribute.Code;

				GuiElementControl guiElementControl = null;

				switch (worldConfig.Attribute.DataType)
				{
				case EnumDataType.Bool:
					var guiElementSwitch = new GuiElementSwitch(capi, value => worldConfig.Update(value), controlBounds.FlatCopy());
					guiElementSwitch.SetValue(worldConfig.GetValue<bool>());

					guiElementControl = guiElementSwitch;
					break;

				case EnumDataType.IntInput:
				case EnumDataType.DoubleInput:
					var guiElementNumberInput = new GuiElementNumberInput(capi, controlBounds.FlatCopy(), value => worldConfig.Update(value), controlFont);
					guiElementNumberInput.SetValue(worldConfig.GetValue<float>());

					guiElementControl = guiElementNumberInput;
					break;

				case EnumDataType.IntRange:
					var guiElementSlider = new GuiElementSlider(capi, value => { worldConfig.Update(value); return true; }, controlBounds.FlatCopy());
					guiElementSlider.SetValues(worldConfig.GetValue<int>(), (int) worldConfig.Attribute.Min, (int) worldConfig.Attribute.Max, (int) worldConfig.Attribute.Step);

					guiElementControl = guiElementSlider;
					break;

				case EnumDataType.String:
					var guiElementTextInput = new GuiElementTextInput(capi, controlBounds.FlatCopy(), value => worldConfig.Update(value), controlFont);
					guiElementTextInput.SetValue(worldConfig.GetValue<string>());

					guiElementControl = guiElementTextInput;
					break;

				case EnumDataType.DropDown:
					List<string> values = new List<string>(worldConfig.Attribute.Values);
					List<string> names = worldConfig.Attribute.Names.Select(name => Lang.Get($"worldconfig-{worldConfig.Attribute.Code}-{name}")).ToList();

					int index = Array.IndexOf(worldConfig.Attribute.Values, worldConfig.GetValue<string>());
					if (index == -1)
					{
						index = values.Count;
						values.Add(worldConfig.GetValue<string>());
						names.Add(worldConfig.GetValue<string>());
					}

					guiElementControl = new GuiElementDropDown(capi, values.ToArray(), names.ToArray(), index, (value, _) => worldConfig.Update(value), controlBounds.FlatCopy(), controlFont, false);
					break;

				default:
					continue;
				}

				yield return new GuiElementStaticText(capi, Lang.Get("worldattribute-" + worldConfig.Attribute.Code), EnumTextOrientation.Left, textBounds.FlatCopy(), textFont);
				yield return guiElementControl;

				string description = Lang.GetIfExists("worldattribute-" + worldConfig.Attribute.Code + "-desc");
				if (description != null)
				{
					yield return new GuiElementHoverText(capi, description, CairoFont.WhiteSmallText(), 320, textBounds.FlatCopy());
				}

				const double fixedDeltaY = 15;

				textBounds = textBounds.BelowCopy(0.0, fixedDeltaY, 0.0, 0.0);
				controlBounds = controlBounds.BelowCopy(0.0, fixedDeltaY, 0.0, 0.0);
			}
		}

		private int GenerateModEntries(out string[] values, out string[] names)
		{
			values = new string[worldConfigEntriesWork.Count + 1];
			names = new string[worldConfigEntriesWork.Count + 1];

			values[0] = "";
			names[0] = Lang.Get("handbook-category-everything");

			int currentIndex = 0;
			int i = 1;

			foreach (var entry in worldConfigEntriesWork)
			{
				values[i] = entry.Key;
				names[i] = entry.Key;

				if (currentMod == entry.Key)
				{
					currentIndex = i;
				}
			}

			return currentIndex;
		}

		private GuiTab[] GenerateTabs(out int currentTab)
		{
			GuiTab[] tabs = new GuiTab[worldConfigEntriesWork.Count + 1];

			tabs[0] = new GuiTab()
			{
				DataInt = 0,
				Name = Lang.Get("handbook-category-everything")
			};

			currentTab = 0;

			int i = 1;
			foreach (var entry in worldConfigEntriesWork)
			{
				tabs[i] = new GuiTab()
				{
					DataInt = i,
					Name = entry.Key
				};

				if (currentMod == entry.Key)
				{
					currentTab = i;
				}

				++i;
			}

			return tabs;
		}

		private bool OnButtonApply()
		{
			WorldConfigUpdated(worldConfigEntriesWork);
			return true;
		}
	}
}