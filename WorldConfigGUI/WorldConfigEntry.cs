using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace WorldConfigGUI
{
	public class WorldConfigEntry
	{
		[JsonProperty]
		public WorldConfigurationAttribute Attribute;

		[JsonProperty]
		public object Value { get; set; }

		public bool Dirty { get; private set; }

		public T GetValue<T>() => (T) Value;
		
		public void MarkDirty()
		{
			Dirty = true;
		}

		public void Update(object newValue)
		{
			if (Value != newValue)
			{
				Value = newValue;
				MarkDirty();
			}
		}
	}
}
