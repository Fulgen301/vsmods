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
#if DEBUG
using System.Diagnostics;
using System.Reflection;
#endif
using System.Reflection.Emit;
using System.Text;

using Discord;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using System.Threading;
using HarmonyLib;

#if !NET5
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;

namespace System.Runtime.CompilerServices
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal class IsExternalInit { }
}
#endif

namespace DiscordIntegration
{
	public class DiscordSDKCSharp : DiscordSDK
	{
		private enum AppState
		{
			MainMenu,
			Ingame
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string lpLibFileName);

		[DllImport("libc.so")]
		private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string filename, int flag);
		private const int RTLD_NOW = 0x00002;
		private const int RTLD_GLOBAL = 0x01000;

		private record ServerInfo(string ServerName, string ExternalIP, int Port, bool HasPassword, string Password);

		private const long CLIENT_ID = 722219281370054657L;
		private readonly Discord.Discord discord;
		private ServerInfo serverInfo;
		private AppState appState = AppState.MainMenu;
		private bool isSingleplayer;
		private readonly uint startTime;

		private static Dictionary<Type, object> callbacks = new();

		public DiscordSDKCSharp(object main) : base(main)
		{
			callbacks = new();

			string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			IntPtr result = Environment.OSVersion.Platform switch
			{
				PlatformID.Win32NT => LoadLibraryW(Path.Combine(directory, "discord_game_sdk.dll")),
				PlatformID.Unix => dlopen(Path.Combine(directory, "libdiscord_game_sdk.so"), RTLD_NOW | RTLD_GLOBAL),
				_ => throw new NotImplementedException()
			};

			if (result == IntPtr.Zero)
			{
				throw new DllNotFoundException("Failed to load Discord Game SDK");
			}


			discord = new Discord.Discord(CLIENT_ID, (long) CreateFlags.NoRequireDiscord);
			LogLevel logLevel =
#if DEBUG
			LogLevel.Debug;
#else
			LogLevel.Info;
#endif
			discord.SetLogHook(logLevel, (level, message) =>
			{
				EnumLogType logType = level switch
				{
					LogLevel.Debug => EnumLogType.Debug,
					LogLevel.Error => EnumLogType.Error,
					LogLevel.Warn => EnumLogType.Warning,
					LogLevel.Info => EnumLogType.Notification,
					_ => throw new ArgumentException("level")
				};

				API?.Logger.Log(logType, message);

#if DEBUG
				Debug.WriteLine($"[{level.ToString().ToUpperInvariant()}] {message}");
#endif
			});

			discord.GetActivityManager().RegisterCommand(Assembly.GetEntryAssembly().Location);

			startTime = (uint) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
			UpdateActivity();
		}

		~DiscordSDKCSharp()
		{
			callbacks = null;
		}

		public override void ClearActivity()
		{
			discord.GetActivityManager().ClearActivity(OnResult);
		}

		public override void JoinLobby()
		{
			throw new NotImplementedException();
		}

		public override void UpdateActivity()
		{
			Activity activity = new()
			{
				ApplicationId = CLIENT_ID,
				Instance = true,
				Name = "Vintage Story"
			};

			activity.Timestamps = new()
			{
				Start = startTime,
				End = 0
			};

			switch (appState)
			{
			case AppState.MainMenu:
				activity.Details = Lang.Get("Main Menu");
				break;

			case AppState.Ingame:
				activity.Details = Lang.Get("Ingame");

				if (isSingleplayer)
				{
					activity.State = Lang.Get("Playing singleplayer");
				}
				else
				{
					activity.Party = new()
					{
						Size = new()
						{
							CurrentSize = 1,
							MaxSize = 10000
						},

						Id = serverInfo.ExternalIP
					};

					activity.Secrets = new()
					{
						Match = API?.World.Seed.ToString() ?? ""
					};

					activity.Secrets.Join = activity.Secrets.Match;

					activity.State = Lang.Get("Playing on {0}", serverInfo.ServerName);
				}
				break;
			}

			discord.GetActivityManager().UpdateActivity(activity, OnResult);
		}

		protected override void Execute()
		{
			discord.RunCallbacks();
		}

		protected override void GameExited()
		{
			isSingleplayer = false;
			appState = AppState.MainMenu;
			serverInfo = null;
		}

		protected override void NewGameStarted(bool isSingleplayer)
		{
			this.isSingleplayer = isSingleplayer;
			appState = AppState.Ingame;
		}

		protected override void UpdateServerInfo(string serverName, string externalIP, int port, bool hasPassword, string password)
		{
			serverInfo = new(serverName, externalIP, port, hasPassword, password);
			UpdateActivity();
		}

		private void OnResult(Result result)
		{
			Debug.WriteLine(nameof(OnResult));
			if (result != Result.Ok)
			{
				throw new ResultException(result);
			}
		}

		public static void Store(object callback)
		{
			callbacks[callback.GetType()] = callback;
		}
	}

	[HarmonyPatch]
	public class DiscordSDKDelegatePatches
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			return new List<MethodBase>()
			{
				AccessTools.DeclaredMethod(typeof(ActivityManager), nameof(ActivityManager.UpdateActivity)),
				AccessTools.DeclaredMethod(typeof(ActivityManager), nameof(ActivityManager.ClearActivity))
			};
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
		{
			foreach (CodeInstruction instruction in codeInstructions)
			{
				if (instruction.opcode == OpCodes.Callvirt)
				{
					yield return new CodeInstruction(OpCodes.Dup);
					yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(DiscordSDKCSharp), nameof(DiscordSDKCSharp.Store)));
				}

				yield return instruction;
			}
		}
	}
}
