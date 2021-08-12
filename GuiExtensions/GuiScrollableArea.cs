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

using HarmonyLib;

using System;

using Vintagestory.API.Client;

namespace GuiExtensions
{
	public class GuiScrollableArea : GuiElement
	{
		public ElementBounds ClippingBounds { get; private init; }
		public ElementBounds ContentBounds { get; private init; }

		public GuiElementScrollbar Scrollbar { get; private init; }

		public GuiScrollableArea(GuiComposer composer, ElementBounds bounds, Vintagestory.API.Common.Action<GuiComposer, ElementBounds> addElements, double padding = 3.0, Vintagestory.API.Common.Action<float> onNewScrollbarValue = null, int insetDepth = 4, float insetBrightness = 0.85f) : base(composer.Api, bounds)
		{
			ClippingBounds = bounds.ForkContainingChild(padding, padding, padding, padding);
			ContentBounds = ClippingBounds.ForkContainingChild(0.0, 0.0, 0.0, -padding);
			ContentBounds.fixedY = 0;

			Scrollbar = new(composer.Api, value =>
			{
				ContentBounds.fixedY = -value;
				ContentBounds.CalcWorldBounds();
				onNewScrollbarValue?.Invoke(value);
			}, ElementStdBounds.VerticalScrollbar(bounds));

			composer
				.AddInset(bounds, insetDepth, insetBrightness)
				.AddInteractiveElement(Scrollbar)
				.BeginClip(ClippingBounds);

			addElements(composer, ContentBounds);

			composer.EndClip();
		}

		public void ScrollToTop()
		{
			AccessTools.DeclaredMethod(typeof(GuiElementScrollbar), "SetScrollbarPosition").Invoke(Scrollbar, new object[] { 0 });
		}

		public void ScrollToBottom()
		{
			Scrollbar.ScrollToBottom();
		}

		public void CalcTotalHeight()
		{
			Scrollbar.SetHeights((float) ClippingBounds.fixedHeight, (float) ContentBounds.fixedHeight);
		}
	}

	public static partial class GuiComposerHelpers
	{
		public static GuiComposer AddScrollableArea(this GuiComposer composer, ElementBounds bounds, Vintagestory.API.Common.Action<GuiComposer, ElementBounds> addElements, double padding = 3.0, Vintagestory.API.Common.Action<float> onNewScrollbarValue = null, int insetDepth = 4, float insetBrightness = 0.85f, string key = null)
		{
			if (!(bool) AccessTools.DeclaredField(typeof(GuiComposer), "composed").GetValue(composer))
			{
				composer.AddStaticElement(new GuiScrollableArea(composer, bounds, addElements, padding, onNewScrollbarValue, insetDepth, insetBrightness), key);
			}

			return composer;
		}

		public static GuiScrollableArea GetScrollableArea(this GuiComposer composer, string key)
		{
			return (GuiScrollableArea) composer.GetElement(key);
		}
	}
}