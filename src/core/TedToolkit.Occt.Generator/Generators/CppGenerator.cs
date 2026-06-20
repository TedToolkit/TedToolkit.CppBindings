// -----------------------------------------------------------------------
// <copyright file="CppGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Produces the C++ translation unit payload for a parsed record.
/// </summary>
public sealed class CppGenerator(RecordModel recordDecl) : IGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        using var builder = ZString.CreateStringBuilder();

        builder.Append("#include <");
        builder.Append(recordDecl.Type.CppTypeName);
        builder.AppendLine(".hxx>");
        builder.AppendLine("#include \"csharp_interop.h\"");
        builder.AppendLine();

        foreach (var recordDeclMethodModel in recordDecl.MethodModels)
        {
            if (recordDeclMethodModel.Type is MethodModelType.New or MethodModelType.Delete)
            {
                continue;
            }

            builder.Append(recordDeclMethodModel.NoExceptions ? "CSHARP_WRAPPER(" : "CSHARP_WRAPPER_TRY(");
            builder.Append(recordDeclMethodModel.GetMethodInteropName(recordDecl));
            builder.Append('(');
            builder.Append(recordDecl.Type.CppTypeName);
            builder.Append(" & self");
            foreach (var parameterModel in recordDeclMethodModel.Parameters)
            {
                builder.Append(", ");
                builder.Append(parameterModel.Type.CppTypeName);
                builder.Append(' ');
                builder.Append(parameterModel.Name);
            }

            builder.AppendLine("), {");

            builder.Append("\tself.");
            builder.Append(recordDeclMethodModel.MethodName);
            builder.Append('(');
            var started = false;
            foreach (var parameterModel in recordDeclMethodModel.Parameters)
            {
                if (started)
                {
                    builder.Append(", ");
                }

                started = true;
                builder.Append(parameterModel.Name);
            }

            builder.AppendLine(");");
            builder.AppendLine("})");
        }

        return Task.FromResult(builder.ToString());
    }
}
