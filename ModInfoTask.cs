using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Vintagestory.API.Common;

namespace ModInfoTask
{
	public class ModInfoTask : Task
	{
		public string AttributeType { get; set; }

		[Required]
		public string Name { get; set; }
		public string ModID { get; set; }

		[Required]
		public string Version { get; set; }

		public string NetworkVersion { get; set; }

		[Required]
		public string Description { get; set; }

		public string Website { get; set; }
		public string Authors { get; set; }
		public string Contributors { get; set; }
		public string Side { get; set; }
		public string RequiredOnClient { get; set; }
		public string RequiredOnServer { get; set; }
		public string WorldConfig { get; set; }

		[Output]
		public ITaskItem[] AssemblyAttribute { get; set; }

		public override bool Execute()
		{
			if (ModID != null)
			{
				if (!ModInfo.IsValidModID(ModID))
				{
					Log.LogError("Invalid value for ModID ({0}).", ModID);
					return false;
				}
			}
			else
			{
				ModID = ModInfo.ToModID(Name);
			}

			if (Side != null)
			{
				if (!Enum.IsDefined(typeof(EnumAppSide), Side))
				{
					Log.LogError("Invalid value for Side ({0}). Possible values: {1}", Utilities.Escape(Side), string.Join(", ", Enum.GetNames(typeof(EnumAppSide))));
					return false;
				}
			}
			else
			{
				Side = "Universal";
			}

			TaskItem modInfoAttribute = new TaskItem(AttributeType ?? "Vintagestory.API.Common.ModInfoAttribute");
			modInfoAttribute.SetMetadata("_Parameter1", Name);
			modInfoAttribute.SetMetadata("_Parameter2", ModID);

			modInfoAttribute.SetMetadata("Authors", ItemGroupToCSharpArray(Authors));
			modInfoAttribute.SetMetadata("Authors_IsLiteral", "true");

			modInfoAttribute.SetMetadata("Description", Description);
			modInfoAttribute.SetMetadata("Version", Version);

			if (NetworkVersion != null) modInfoAttribute.SetMetadata("NetworkVersion", NetworkVersion);
			if (Website != null) modInfoAttribute.SetMetadata("Website", Website);
			if (Contributors != null)
			{
				modInfoAttribute.SetMetadata("Contributors", ItemGroupToCSharpArray(Contributors));
				modInfoAttribute.SetMetadata("Contributors_IsLiteral", "true");
			}

			if (Side != null) modInfoAttribute.SetMetadata("Side", Side);
			if (RequiredOnClient != null)
			{
				modInfoAttribute.SetMetadata("RequiredOnClient", RequiredOnClient);
				modInfoAttribute.SetMetadata("RequiredOnClient_IsLiteral", "true");
			}
			if (RequiredOnServer != null)
			{
				modInfoAttribute.SetMetadata("RequiredOnServer", RequiredOnClient);
				modInfoAttribute.SetMetadata("RequiredOnServer_IsLiteral", "true");
			}

			if (WorldConfig != null) modInfoAttribute.SetMetadata("WorldConfig", WorldConfig);

			AssemblyAttribute = new ITaskItem[] { modInfoAttribute };

			return !Log.HasLoggedErrors;
		}

		private string ItemGroupToCSharpArray(string itemGroup)
		{
			return string.Format("new[] {{{0}}}", string.Join(", ", itemGroup.Split(';').Select(a => "\"" + Utilities.Escape(a) + "\"")));
		}
	}
}
