using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Cairo;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

using ProtoBuf;
using HarmonyLib;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

using GuiExtensions;

namespace WorldConfigGUI
{
	using WorldConfigEntries = Dictionary<string, List<WorldConfigEntry>>;
	public delegate void WorldConfigReceived(WorldConfigEntries entries);

	public partial class SystemWorldConfigGui : ModSystem
	{
		[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
		private class WorldConfigEntriesPacket
		{
			public string JSON;
		}

		[ProtoContract]
		private class WorldConfigEntriesRequestPacket
		{
		}

		[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
		private class WorldConfigEntriesApplied
		{
			public uint? Count;
			public string FailureReason;
		}

		#region Server variables
		private IServerNetworkChannel serverNetworkChannel;
		private ICoreServerAPI sapi;
		#endregion

		#region Client variables
		private IClientNetworkChannel clientNetworkChannel;
		private ICoreClientAPI capi;
		private string lastWorldConfigJSON;
		long tickId;
		private bool wasPaused = false;

		private bool IsPaused => (bool) AccessTools.Field(typeof(ClientMain), "IsPaused").GetValue(capi.World);
		private bool OpenedToLan => (bool) AccessTools.Field(typeof(ClientMain), "OpenedToLan").GetValue(capi.World);
		#endregion

		public override double ExecuteOrder() => 0.01;

		#region Server world config handling

		public override void StartServerSide(ICoreServerAPI api)
		{
			base.StartServerSide(api);
			sapi = api;
			serverNetworkChannel = api.Network.RegisterChannel("worldconfiggui")
				.RegisterMessageType<WorldConfigEntriesPacket>()
				.RegisterMessageType<WorldConfigEntriesRequestPacket>()
				.RegisterMessageType<WorldConfigEntriesApplied>()
				.SetMessageHandler<WorldConfigEntriesPacket>(OnWorldConfigEntriesReceived)
				.SetMessageHandler<WorldConfigEntriesRequestPacket>(OnWorldConfigEntriesRequestReceived);
		}

		private void OnWorldConfigEntriesReceived(IServerPlayer player, WorldConfigEntriesPacket json)
		{
			if (!HasPermission(player))
			{
				serverNetworkChannel.SendPacket(new WorldConfigEntriesApplied
				{
					Count = null,
					FailureReason = "No permissions"
				}, player);

				return;
			}

			var worldConfigs = DeserializeJSON(json.JSON);
			if (worldConfigs == null)
			{
				sapi.Logger.Error("[worldconfiggui] Received invalid JSON!");
				
				serverNetworkChannel.SendPacket(new WorldConfigEntriesApplied
				{
					Count = null,
					FailureReason = "Invalid JSON"
				}, player);
			}

			ITreeAttribute worldConfigInSaveGame = GetWorldConfigFromSaveGame();

			uint counter = 0;
			foreach (WorldConfigEntry worldConfig in worldConfigs.Values.SelectMany(value => value))
			{
				WorldConfigurationAttribute attribute = worldConfig.Attribute;

				switch (attribute.DataType)
				{
				case EnumDataType.Bool:
					worldConfigInSaveGame.SetBool(attribute.Code, worldConfig.GetValue<bool>());
					break;

				case EnumDataType.IntInput:
				case EnumDataType.IntRange:
					worldConfigInSaveGame.SetInt(attribute.Code, worldConfig.GetValue<int>());
					break;

				case EnumDataType.DoubleInput:
					worldConfigInSaveGame.SetDouble(attribute.Code, worldConfig.GetValue<double>());
					break;

				case EnumDataType.String:
				case EnumDataType.DropDown:
					worldConfigInSaveGame.SetString(attribute.Code, worldConfig.GetValue<string>());
					break;
				}

				sapi.Logger.Debug($"[worldconfig] Set config {attribute.Code} to {worldConfig.Value}");
				++counter;
			}

			serverNetworkChannel.SendPacket(new WorldConfigEntriesApplied
			{
				Count = counter
			}, player);

			SendWorldConfigToClient(player);
		}

		private void OnWorldConfigEntriesRequestReceived(IServerPlayer player, WorldConfigEntriesRequestPacket _)
		{
			SendWorldConfigToClient(player);
		}

		private void SendWorldConfigToClient(IServerPlayer player)
		{
			if (!HasPermission(player))
			{
				return;
			}

			var worldConfigMap = new WorldConfigEntries();

			foreach (Mod mod in sapi.ModLoader.Mods)
			{
				ModWorldConfiguration modWorldConfiguration = mod.WorldConfig;
				if (modWorldConfiguration == null)
				{
					continue;
				}

				var worldConfigEntries = new List<WorldConfigEntry>();
				ITreeAttribute worldConfigInSaveGame = GetWorldConfigFromSaveGame();

				foreach (WorldConfigurationAttribute attribute in modWorldConfiguration.WorldConfigAttributes)
				{
					if (attribute.OnlyDuringWorldCreate)
					{
						continue;
					}

					var entry = new WorldConfigEntry()
					{
						Attribute = attribute
					};

					if (worldConfigInSaveGame.HasAttribute(attribute.Code))
					{
						switch (attribute.DataType)
						{
						case EnumDataType.Bool:
							entry.Value = worldConfigInSaveGame.GetBool(attribute.Code, false);
							break;

						case EnumDataType.IntInput:
						case EnumDataType.IntRange:
							entry.Value = worldConfigInSaveGame.GetInt(attribute.Code, 0);
							break;

						case EnumDataType.DoubleInput:
							entry.Value = worldConfigInSaveGame.GetDecimal(attribute.Code, 0.0);
							break;

						case EnumDataType.String:
						case EnumDataType.DropDown:
							entry.Value = worldConfigInSaveGame.GetString(attribute.Code, "");
							break;
						}
					}
					else
					{
						entry.Value = attribute.TypedDefault;
					}

					worldConfigEntries.Add(entry);
				}

				if (worldConfigEntries.Count > 0)
				{
					worldConfigMap.Add(mod.Info.Name, worldConfigEntries);
				}
			}

			serverNetworkChannel.SendPacket(new WorldConfigEntriesPacket() { JSON = JsonConvert.SerializeObject(worldConfigMap) }, player);
		}

		private ITreeAttribute GetWorldConfigFromSaveGame()
		{
			/*object saveGame = sapi.World.GetType().GetField("SaveGameData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sapi.World);
			if (worldConfigInSaveGameField == null)
			{
				IEnumerable<FieldInfo> fields = saveGame.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
				worldConfigInSaveGameField = fields.Single(fieldInfo => fieldInfo.FieldType == typeof(ITreeAttribute));
			}

			return (ITreeAttribute) worldConfigInSaveGameField.GetValue(saveGame);*/

			return sapi.WorldManager.SaveGame.WorldConfiguration;
		}

		#endregion

		#region Client world config handling

		public override void StartClientSide(ICoreClientAPI api)
		{
			base.StartClientSide(api);

			// GuiCompositeSettingsEx.Patch();

			capi = api;
			clientNetworkChannel = api.Network.RegisterChannel("worldconfiggui")
				.RegisterMessageType<WorldConfigEntriesPacket>()
				.RegisterMessageType<WorldConfigEntriesRequestPacket>()
				.RegisterMessageType<WorldConfigEntriesApplied>()
				.SetMessageHandler<WorldConfigEntriesPacket>(OnJsonReceived)
				.SetMessageHandler<WorldConfigEntriesApplied>(OnEntriesApplied);

			capi.Event.LevelFinalize += RequestWorldConfigEntries;
			tickId = capi.Event.RegisterGameTickListener(_ => RequestWorldConfigEntries(), 30000);
		}

		public override void Dispose()
		{
			capi?.Event.UnregisterGameTickListener(tickId);
			//GuiCompositeSettingsEx.Unpatch();

			base.Dispose();
		}

		private void RequestWorldConfigEntries()
		{
			clientNetworkChannel.SendPacket(new WorldConfigEntriesRequestPacket());
		}

		private void OnJsonReceived(WorldConfigEntriesPacket json)
		{
			if (DeserializeJSON(json.JSON) == null)
			{
				capi.Logger.Error("[worldconfiggui] Received invalid JSON!");
				return;
			}

			if (lastWorldConfigJSON == null)
			{
				GuiCompositeSettingsEx.AddPanel("worldconfiggui", PanelActivated);
			}

			lastWorldConfigJSON = string.Copy(json.JSON);
		}

		private void OnEntriesApplied(WorldConfigEntriesApplied packet)
		{
			var game = (ClientMain) capi.World;

			if (packet.Count.HasValue)
			{
				//((ClientEventManager) typeof(ClientMain).GetMember("eventManager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).TriggerNewClientChatLine(GlobalConstants.GeneralChatGroup, Lang.Get("worldconfiggui:values-updated", packet.Count.Value), EnumChatType.CommandSuccess, null);
				capi.ShowChatMessage(Lang.Get("worldconfiggui:values-updated", packet.Count.Value));
			}
			else
			{
				capi.ShowChatMessage(Lang.Get("worldconfiggui:value-update-failed", packet.FailureReason ?? "Unknown reason"));
			}

			if (wasPaused)
			{
				wasPaused = false;
				game.PauseGame(true);
			}
		}

		private bool HasPermission(IPlayer player)
		{
			return player.HasPrivilege(Privilege.controlserver);
		}

		private WorldConfigEntries DeserializeJSON(string json)
		{
			return JsonConvert.DeserializeObject<WorldConfigEntries>(json);
		}

		public void WorldConfigUpdated(WorldConfigEntries entries)
		{
			var dirty = entries.Select(t => new KeyValuePair<string, List<WorldConfigEntry>>(t.Key, t.Value.Where(worldConfig => worldConfig.Dirty).ToList())).Where(t => t.Value.Any());

			if (dirty.Any())
			{
				if (IsPaused && !OpenedToLan)
				{
					wasPaused = true;
					((ClientMain) capi.World).PauseGame(false);
				}

				clientNetworkChannel.SendPacket(new WorldConfigEntriesPacket() { JSON = JsonConvert.SerializeObject(dirty.ToDictionary(t => t.Key, t => t.Value)) });
			}
		}

		#endregion
	}
}
