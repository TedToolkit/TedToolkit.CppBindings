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
/// <param name="recordDecl">The record declaration to generate.</param>
internal sealed class CppGenerator(RecordModel recordDecl) : IGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var builder = ZString.CreateStringBuilder();
        try
        {
            builder.AppendLine("#include \"csharp_interop.h\"");
            builder.Append("#include <");
            builder.Append(recordDecl.SourceHeader);
            builder.AppendLine(">");
            builder.AppendLine();

            foreach (var recordDeclMethodModel in recordDecl.MethodModels)
            {
                builder.Append(recordDeclMethodModel.NoExceptions ? "CSHARP_WRAPPER(" : "CSHARP_WRAPPER_TRY(");
                builder.Append(recordDeclMethodModel.GetMethodInteropName(recordDecl));
                builder.Append('(');

                switch (recordDeclMethodModel.Type)
                {
                    case MethodModelType.NORMAL:
                        GenerateNormalMethod(ref builder, recordDeclMethodModel, recordDecl, true);
                        break;

                    case MethodModelType.NEW:
                        GenerateNew(ref builder, recordDeclMethodModel, recordDecl);
                        break;

                    case MethodModelType.DELETE:
                        GenerateDelete(ref builder, recordDecl);
                        break;

                    case MethodModelType.OPERATOR:
                        GenerateNormalMethod(ref builder, recordDeclMethodModel, recordDecl, false);
                        break;

                    case MethodModelType.IMPLICIT:

                    case MethodModelType.EXPLICIT:
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported method model type '{recordDeclMethodModel.Type}'.");
                }

                builder.AppendLine("})");
            }

            return Task.FromResult(builder.ToString());
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void GenerateNew(ref Utf16ValueStringBuilder builder, MethodModel methodModel, RecordModel recordModel)
    {
        var alloc = recordModel.Base is not null;
        builder.Append(recordModel.Type.CppTypeName);
        builder.Append(alloc ? "*& " : "& ");
        builder.Append("instance");
        foreach (var parameterModel in methodModel.Parameters)
        {
            builder.Append(", ");
            builder.Append(parameterModel.Type.CppTypeName);
            builder.Append(' ');
            builder.Append(parameterModel.Name);
        }

        builder.AppendLine("), {");
        builder.Append("\tinstance = ");
        if (alloc)
        {
            builder.Append("new ");
        }

        builder.Append(recordModel.Type.CppTypeName);
        builder.Append('(');
        var started = false;
        foreach (var parameterModel in methodModel.Parameters)
        {
            if (started)
            {
                builder.Append(", ");
            }

            started = true;
            builder.Append(parameterModel.Name);
        }

        builder.AppendLine(");");

        if (!alloc)
        {
            return;
        }

        builder.AppendLine("\tinstance->IncrementRefCounter();");
    }

    private static void GenerateDelete(ref Utf16ValueStringBuilder builder, RecordModel recordModel)
    {
        var alloc = recordModel.Base is not null;
        builder.Append(recordModel.Type.CppTypeName);
        builder.Append(alloc ? "* " : "& ");
        builder.Append("instance");

        builder.AppendLine("), {");
        if (alloc)
        {
            builder.AppendLine("\tdelete instance;");
            return;
        }

        builder.Append("\tinstance.~");
        var name = recordModel.Type.CppTypeName.Trim();
        var index = name.LastIndexOf(':');
        if (index >= 0)
        {
            name = name[(index + 1)..];
        }

        builder.Append(name);
        builder.AppendLine("();");
    }

    private void GenerateNormalMethod(ref Utf16ValueStringBuilder builder, MethodModel methodModel,
        RecordModel recordModel, bool parentheses)
    {
        if (!methodModel.IsStatic)
        {
            if (methodModel.IsConst)
            {
                builder.Append("const ");
            }

            builder.Append(recordDecl.Type.CppTypeName);
            builder.Append(" & self");
        }

        var started = !methodModel.IsStatic;
        foreach (var parameterModel in methodModel.Parameters)
        {
            if (started)
            {
                builder.Append(", ");
            }

            started = true;
            builder.Append(parameterModel.Type.CppTypeName);
            builder.Append(' ');
            builder.Append(parameterModel.Name);
        }

        var hasReference = false;
        if (!methodModel.IsReturnVoid)
        {
            if (started)
            {
                builder.Append(", ");
            }

            var name = methodModel.ReturnType.CppTypeName.Trim();
            hasReference = name.EndsWith('&');
            if (hasReference)
            {
                name = name[..^2] + '*';
            }

            builder.Append(name);
            builder.Append(" * const result");
        }

        builder.AppendLine("), {");

        if (methodModel.IsStatic)
        {
            builder.Append(methodModel.IsReturnVoid ? "\t" : "\t*result = ");
            if (hasReference)
            {
                builder.Append("&");
            }

            builder.Append(recordModel.Type.CppTypeName);
            builder.Append("::");
            builder.Append(methodModel.MethodName);
        }
        else
        {
            if (hasReference)
            {
                builder.Append("\t*result = &");
            }
            else
            {
                builder.Append(methodModel.IsReturnVoid ? "\t" : "\t*result = ");
            }

            if (!parentheses && methodModel.Parameters.Count is 0)
            {
                builder.Append(methodModel.MethodName);
                builder.Append("self");
            }
            else
            {
                builder.Append(parentheses ? "self." : "self");
                builder.Append(methodModel.MethodName);
            }
        }

        if (parentheses)
        {
            builder.Append('(');
        }

        started = false;
        foreach (var parameterModel in methodModel.Parameters)
        {
            if (started)
            {
                builder.Append(", ");
            }

            started = true;
            builder.Append(parameterModel.Name);
        }

        builder.AppendLine(parentheses ? ");" : ";");
    }
}