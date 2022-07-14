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
using System.Runtime.InteropServices;

using Vintagestory.API.Client;
using Vintagestory.Client;

namespace DiscordIntegration
{
	class DiscordSDKPInvoke : DiscordSDK
	{
		private delegate void Logger(string message);
		private readonly Logger logger;

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern bool Init(Logger logger);

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void Free();

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern new void RunCallbacks();

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "UpdateActivity")]
		private static extern void UpdateActivityImpl();

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "UpdateServerInfo")]
		private static extern void UpdateServerInfoImpl(string serverName, string externalIP, ushort port, bool hasPassword, string password);

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "NewGame")]
		private static extern void NewGameImpl(bool isSingleplayer, int seed);

		[DllImport("DiscordSDKPInvoke.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ExitGame")]
		private static extern void ExitGameImpl();

		internal DiscordSDKPInvoke(ScreenManager main) : base(main)
		{
			logger = new Logger(Log);

			if (!Init(logger))
			{
				throw new Exception("Init failed");
			}
		}

		public override void ClearActivity()
		{
			throw new NotImplementedException();
		}

		public override void JoinLobby()
		{
			throw new NotImplementedException();
		}

		public override void UpdateActivity()
		{
			UpdateActivityImpl();
		}

		protected override void Execute()
		{
			RunCallbacks();
		}

		protected override void UpdateServerInfo(string serverName, string externalIP, int port, bool hasPassword, string password)
		{
			ushort shortPort = (ushort) port;
			UpdateServerInfoImpl(serverName, externalIP, shortPort, hasPassword, password);
		}

		protected override void NewGameStarted(bool isSingleplayer)
		{
			NewGameImpl(isSingleplayer, API.World.Seed);
		}

		protected override void GameExited()
		{
			ExitGameImpl();
		}

		private void Log(string message)
		{
			API?.Logger.Notification("{}", message);
		}
	}
}
