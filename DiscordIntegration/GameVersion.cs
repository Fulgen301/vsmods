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
using System.IO;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Vintagestory.Client.NoObf;

namespace DiscordIntegration
{
	internal class GameVersion
	{
		internal class Symbols
		{
			public MethodInfo StartMainDialog;
			public string JoinGame;
			public string ServerName;
			public string ServerAddress;
			public string HostName;
			public string Port;
			public string Password;
			public string HasPassword;
		}

		internal static Symbols SymbolsForCurrentVersion { get; set; }

		internal static void DetermineSymbolsForCurrentVersion()
		{
			string vintagestoryLibLocation = AppDomain.CurrentDomain.GetAssemblies().First(t => t.GetName().Name == "VintagestoryLib").Location;
			string directory = Path.GetDirectoryName(vintagestoryLibLocation);

			OpCodesWrapper opCodes = new();
			Symbols symbols = new();
			TypeDefinition mainDialog;

			using AssemblyDefinition vintagestoryLib = AssemblyDefinition.ReadAssembly(vintagestoryLibLocation);

			foreach (MethodDefinition method in vintagestoryLib.MainModule.Types.Where(t => t.IsClass).SelectMany(t => t.Methods))
			{
				if (!method.HasBody)
				{
					continue;
				}

				foreach (Instruction instruction in method.Body.Instructions)
				{
					if ((instruction.Operand as string) == "mainmenu-sp")
					{
						mainDialog = method.DeclaringType;
						goto foundType;
					}
				}
			}

			throw new Exception("Main dialog type not found");

		foundType:
			Type screenManagerType = typeof(GuiScreenPublicServers).GetConstructors().Single().GetParameters().First().ParameterType;

			foreach (MethodDefinition method in vintagestoryLib.MainModule.GetType(screenManagerType.FullName).Methods.Where(m => m.HasBody))
			{
				foreach (Instruction instruction in method.Body.Instructions)
				{
					if (instruction.OpCode == opCodes["newobj"] && (instruction.Operand as MethodReference)?.Resolve().DeclaringType == mainDialog)
					{
						symbols.StartMainDialog = AccessTools.DeclaredMethod(screenManagerType, method.Name);
						goto foundMethod;
					}
				}
			}

			throw new Exception("Method not found");

		foundMethod:
			symbols.JoinGame = ((MethodReference) vintagestoryLib.MainModule.Types.Single(t => t.IsClass && t.Name == "GuiScreenPublicServerView")
								.Methods.Single(m => m.HasBody && m.Name == "OnJoin")
								.Body.Instructions.First(i => i.OpCode == opCodes["callvirt"]).Operand).Name;

			var serverInfoFields = vintagestoryLib.MainModule.Types.Single(t => t.IsClass && t.Name == "ClientMain")
								.Fields.Single(f => f.Name == "ServerInfo").FieldType.Resolve().Fields;

			symbols.ServerName = serverInfoFields.First().Name;
			symbols.ServerAddress = serverInfoFields.ElementAt(1).Name;

			TypeDefinition ServerAddress = serverInfoFields.ElementAt(1).FieldType.Resolve();
			symbols.HostName = ServerAddress.Fields.First().Name;
			symbols.Port = ServerAddress.Fields.ElementAt(1).Name;
			symbols.Password = ServerAddress.Fields.ElementAt(2).Name;
			symbols.HasPassword = ServerAddress.Fields.ElementAt(3).Name;

			SymbolsForCurrentVersion = symbols;
		}
	}

	internal class OpCodesWrapper
	{
		private static Type opCodesType;
		public OpCode this[string name]
		{
			get
			{
				if (opCodesType is null)
				{
					opCodesType = typeof(TypeDefinition).Assembly.GetType("Mono.Cecil.Cil.OpCodes");
				}

				FieldInfo field = opCodesType.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
				if (field is null)
				{
					throw new KeyNotFoundException("Invalid opcode: " + name);
				}

				return (OpCode) field.GetValue(null);
			}
		}
	}
}
