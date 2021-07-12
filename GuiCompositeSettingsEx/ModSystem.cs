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
using Vintagestory.API.Common;

namespace GuiExtensions
{
	/// <summary>
	/// This mod system automatically patches and unpatches the settings dialog.
	/// </summary>
	public class SystemGuiCompositeSettingsEx : ModSystem
	{
		/// <inheritdoc/>
		public override double ExecuteOrder() => 0.00;

		/// <inheritdoc/>
		public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

		/// <summary>
		/// Whether the dialog has been patched. See <see cref="GuiCompositeSettingsEx.Patched"/> for details.
		/// </summary>
		public bool Patched => GuiCompositeSettingsEx.Patched;

		/// <inheritdoc/>
		public override void StartClientSide(ICoreClientAPI api)
		{
			base.StartClientSide(api);
			GuiCompositeSettingsEx.Patch();
		}

		/// <inheritdoc/>
		public override void Dispose()
		{
			GuiCompositeSettingsEx.Unpatch();
			base.Dispose();
		}
	}
}
