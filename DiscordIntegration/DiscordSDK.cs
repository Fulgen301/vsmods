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
using System.Diagnostics;

using Vintagestory.API.Client;

namespace DiscordIntegration
{
	public abstract class DiscordSDK
	{
		public static DiscordSDK Instance { get; private set; }
		protected object Main { get; private set; }
		protected ICoreClientAPI API { get; set; }

		public static void NewInstance(object main)
		{
			if (Instance != null)
			{
				return;
			}

			Debug.WriteLine("New instance");
			Instance = new DiscordSDKPInvoke(main);
		}

		public static void RunCallbacks() => Instance?.Execute();

		internal DiscordSDK(object main)
		{
			Main = main;
			GameVersion.DetermineSymbolsForCurrentVersion();
		}

		public abstract void UpdateActivity();
		public abstract void ClearActivity();
		public abstract void JoinLobby();

		public void NewGame(ICoreClientAPI api)
		{
			API = api;

			/*API.Event.LevelFinalize += () =>
			{*/
			var clientServerInfo = DeclaredField<object>(api.World, "ServerInfo");
			var symbols = GameVersion.SymbolsForCurrentVersion;

			var serverAddress = DeclaredField<object>(clientServerInfo, symbols.ServerAddress);

			// FIXME: MaxClients
			var serverName = DeclaredField<string>(clientServerInfo, symbols.ServerName);
			var externalIP = DeclaredField<string>(serverAddress, symbols.HostName);
			var port = DeclaredField<int>(serverAddress, symbols.Port);

			var hasPassword = DeclaredField<bool>(serverAddress, symbols.HasPassword);
			var password = hasPassword ? DeclaredField<string>(serverAddress, symbols.Password) : "";

			NewGameStarted(API.IsSinglePlayer);

			UpdateServerInfo(serverName, externalIP, port, hasPassword, password);
			//};
		}

		public void ExitGame()
		{
			API = null;
			GameExited();
		}

		protected abstract void Execute();

		protected abstract void UpdateServerInfo(string serverName, string externalIP, int port, bool hasPassword, string password);

		protected abstract void NewGameStarted(bool isSingleplayer);
		protected abstract void GameExited();

		private T DeclaredField<T>(object obj, string field)
		{
			return (T) obj.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(obj);
		}
	}
}
