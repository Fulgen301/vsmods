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
using System.Linq;
using BindingFlags = System.Reflection.BindingFlags;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ModuleInitializerTask
{
	public class ModuleInitializerTask : Task
	{
		[Required]
		public string AssemblyPath { get; set; }

		private const string TypeName = "ModuleInitializer";

		public override bool Execute()
		{
			AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(AssemblyPath, new ReaderParameters
			{
				ReadWrite = true
			});

			foreach (ModuleDefinition module in assembly.Modules)
			{
				var cctor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.ImportReference(typeof(void)));

				TypeDefinition moduleInitializerStaticClass = module.Types.Single(t => t.IsClass && t.Name == TypeName);
				MethodReference method = moduleInitializerStaticClass.Methods.Single(m => m.IsStatic && m.Name == "cctor");
				var processor = cctor.Body.GetILProcessor();

				Type opCodes = typeof(TypeDefinition).Assembly.GetType("Mono.Cecil.Cil.OpCodes", true);
				processor.Append(processor.Create(OpCode(opCodes, "Call"), method));
				processor.Append(processor.Create(OpCode(opCodes, "Ret")));

				module.GetType("<Module>").Methods.Add(cctor);
			}

			assembly.Write();
			return true;
		}

		private OpCode OpCode(Type opCodes, string name)
		{
			return (OpCode) opCodes.GetField(name, BindingFlags.Static | BindingFlags.Public).GetValue(null);
		}
	}
}