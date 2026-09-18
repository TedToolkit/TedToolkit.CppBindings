// -----------------------------------------------------------------------
// <copyright file="NativeApiGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;

namespace TedToolkit.CppBindings.Generator.Generators;

/// <summary>
/// Emits the existing process-lifetime native module loader without provider declaration knowledge.
/// </summary>
internal static class NativeApiGenerator
{
    /// <summary>
    /// The reserved managed loader filename.
    /// </summary>
    internal const string FileName = "NativeApi.g.cs";

    /// <summary>
    /// Generates the process-lifetime native table loader.
    /// </summary>
    /// <param name="options">Validated namespace and artifact settings.</param>
    /// <returns>The generated managed source.</returns>
    internal static string Generate(GenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var nativeApi = new TypeDeclaration("NativeApi", TypeDeclarationType.CLASS).Internal.Static.Unsafe
            .AddMember(new Field(new DataType("nint"), "Module").Private.Static.Readonly
                .AddDefault(new CustomExpression(
                    "global::System.Runtime.InteropServices.NativeLibrary.Load("
                    + options.NativeLibraryBaseName.ToLiteral().ToCode()
                    + ", typeof(NativeApi).Assembly, null)")))
            .AddMember(new Field(new DataType("nint*"), "Functions").Private.Static.Readonly
                .AddDefault(new CustomExpression(
                    "((delegate* unmanaged[Cdecl]<nint*>)"
                    + "global::System.Runtime.InteropServices.NativeLibrary.GetExport("
                    + "Module, \"NativeApi_GetFunctionTable\"))()")))
            .AddMember(new Method("GetFunction", ReturnType(new DataType("nint"))).Internal.Static
                .AddParameter(Parameter<int>("index"))
                .AddStatement(new CustomExpression("Functions[index]").Return));

        return File()
            .AddNameSpace(NameSpace(options.CSharpNamespace).AddMember(nativeApi))
            .ToCode();
    }
}