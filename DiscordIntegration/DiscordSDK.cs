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

using Vintagestory.API.Client;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace DiscordIntegration
{
	public abstract class DiscordSDK
	{
		public static DiscordSDK Instance { get; private set; }
		protected ScreenManager Main { get; private set; }
		protected ICoreClientAPI API { get; set; }

		public static void NewInstance(ScreenManager main)
		{
			if (Instance != null)
			{
				return;
			}

			try
			{
				Instance = new DiscordSDKCSharp(main);
				Instance.UpdateActivity();
			}
			catch (InvalidOperationException)
			{
			}
		}

		public static void RunCallbacks() => Instance?.Execute();

		internal DiscordSDK(ScreenManager main)
		{
			Main = main;
		}

		public abstract void UpdateActivity();
		public abstract void ClearActivity();
		public abstract void JoinLobby();

		public void NewGame(ICoreClientAPI api)
		{
			API = api;

			/*API.Event.LevelFinalize += () =>
			{*/
			var clientServerInfo = DeclaredField<ServerInformation>(api.World, "ServerInfo");
			var connectData = DeclaredField<ServerConnectData>(clientServerInfo, "connectdata");

			// FIXME: MaxClients
			var serverName = DeclaredField<string>(clientServerInfo, "ServerName");

			NewGameStarted(API.IsSinglePlayer);

			UpdateServerInfo(serverName, connectData.Host, connectData.Port, connectData.IsServePasswordProtected, connectData.ServerPassword);
			UpdateActivity();
			//};
		}

		public void ExitGame()
		{
			API = null;
			GameExited();
			UpdateActivity();
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
