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
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

using Vintagestory.Common;
using Vintagestory.API.Common;

using HarmonyLib;

[assembly: RoslynSourceCompiler.ModInfoForceModuleInitializer(
	"RoslynSourceCompiler",
	"roslynsourcecompiler",
	Authors = new[] { "Fulgen" },
	Description = "Replaces the builtin C# compiler with Roslyn to allow source mods with C# > 5.0.",
	RequiredOnClient = false,
	RequiredOnServer = false,
	Version = "1.0.0"
	)]

namespace ModuleInitializer
{
	public static class ModuleInitializer
	{
		private const string HarmonyID = "org.github.fulgen301.roslynsourcecompiler";
		public static void cctor()
		{
			Harmony.DEBUG = true;
			new Harmony(HarmonyID).PatchAll(Assembly.GetExecutingAssembly());
		}
	}
}

namespace RoslynSourceCompiler
{
	public class DummyModSystem : ModSystem
	{
		public override bool ShouldLoad(EnumAppSide forSide) => false;
	}

	[HarmonyPatch(typeof(ModLoader))]
	internal static class ModLoaderPatch
	{
		private static ILogger GetLogger(this ModLoader @this)
		{
			return (ILogger) AccessTools.DeclaredField(typeof(ModLoader), "logger").GetValue(@this);
		}

		[HarmonyPrefix]
		[HarmonyPatch("DisableMods")]
		private static void DisableMods(List<ModContainer> mods, ModLoader __instance)
		{
			foreach (ModContainer modContainer in mods)
			{
				if (modContainer.RequiresCompilation)
				{
					__instance.GetLogger().Debug("Recompiling {0} with Roslyn", string.Join(", ", modContainer.SourceFiles));

					Assembly assembly = Compiler.CompileWithRoslyn(modContainer, modContainer.SourceFiles);

					AccessTools.DeclaredPropertySetter(typeof(ModContainer), "Assembly").Invoke(modContainer, new[] { assembly });
					modContainer.Error = assembly is not null ? null : ModError.Loading;
					modContainer.LoadModInfo(null, null, (EnumAppSide) AccessTools.DeclaredField(typeof(ModLoader), "side").GetValue(__instance));
				}
			}
		}
	}

	[HarmonyPatch(typeof(ModCompilationContext))]
	internal static class ModCompilationContextPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch("CompileFromFiles")]
		public static bool CompileFromFiles(ModContainer mod, IEnumerable<string> paths, ref Assembly __result, ModCompilationContext __instance)
		{
			mod.Logger.Debug("CompileFromFiles({0})", mod.SourceFiles.FirstOrDefault() ?? "(null)");
			__result = Compiler.CompileWithRoslyn(mod, paths);
			return false;
		}
	}

	public static class Compiler
	{
		public static IEnumerable<string> DefaultReferences => ((string[]) AccessTools.DeclaredField(typeof(ModCompilationContext), "references").GetValue(new ModCompilationContext())).Where(r => !r.StartsWith("System")).Append(typeof(void).Assembly.Location);

		public static Assembly CompileWithRoslyn(ModContainer modContainer, IEnumerable<string> paths, IEnumerable<string> extraReferences = null)
		{
			IEnumerable<SyntaxTree> syntaxTrees = paths.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)));

			var compilation = CSharpCompilation.Create(
				"roslyn-" + Guid.NewGuid().ToString(),
				syntaxTrees,
				DefaultReferences.Concat(extraReferences ?? Enumerable.Empty<string>()).Select(r => MetadataReference.CreateFromFile(r)),
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
				);

			using MemoryStream memoryStream = new();
			EmitResult result = compilation.Emit(memoryStream);
			if (!result.Success)
			{
				foreach (Diagnostic error in result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error))
				{
					modContainer.Logger.Error("{0}: {1}", error.Id, error.GetMessage());
				}

				return null;
			}

			memoryStream.Seek(0, SeekOrigin.Begin);
			modContainer.Logger.Debug("Successfully compiled mod with Roslyn");
			return Assembly.Load(memoryStream.ToArray());
		}
	}
}
