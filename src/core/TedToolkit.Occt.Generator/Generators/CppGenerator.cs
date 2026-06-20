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
        var builder = ZString.CreateStringBuilder();
        try
        {
            builder.Append("#include <");
            builder.Append(recordDecl.Type.CppTypeName);
            builder.AppendLine(".hxx>");
            builder.AppendLine("#include \"csharp_interop.h\"");
            builder.AppendLine();

            foreach (var recordDeclMethodModel in recordDecl.MethodModels)
            {
                builder.Append(recordDeclMethodModel.NoExceptions ? "CSHARP_WRAPPER(" : "CSHARP_WRAPPER_TRY(");
                builder.Append(recordDeclMethodModel.GetMethodInteropName(recordDecl));
                builder.Append('(');

                switch (recordDeclMethodModel.Type)
                {
                    case MethodModelType.Normal:
                        GenerateNormalMethod(ref builder, recordDeclMethodModel);
                        break;
                    case MethodModelType.New:
                        GenerateNew(ref builder, recordDeclMethodModel, recordDecl);
                        break;
                    case MethodModelType.Delete:
                        GenerateDelete(ref builder, recordDecl);
                        break;
                    case MethodModelType.Operator:
                        break;
                    case MethodModelType.Implicit:
                        break;
                    case MethodModelType.Explicit:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
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

    private void GenerateNew(ref Utf16ValueStringBuilder builder, MethodModel methodModel, RecordModel recordModel)
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

        if (alloc)
        {
            builder.AppendLine("\tinstance->IncrementRefCounter();");
        }
    }

    private void GenerateDelete(ref Utf16ValueStringBuilder builder, RecordModel recordModel)
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
        builder.Append(recordModel.Type.CppTypeName);
        builder.AppendLine("();");
    }

    private void GenerateNormalMethod(ref Utf16ValueStringBuilder builder, MethodModel methodModel)
    {
        builder.Append(recordDecl.Type.CppTypeName);
        builder.Append("& self");
        foreach (var parameterModel in methodModel.Parameters)
        {
            builder.Append(", ");
            builder.Append(parameterModel.Type.CppTypeName);
            builder.Append(' ');
            builder.Append(parameterModel.Name);
        }

        if (!methodModel.IsReturnVoid)
        {
            builder.Append(", ");
            builder.Append(methodModel.ReturnType.CppTypeName);
            builder.Append("& result");
        }

        builder.AppendLine("), {");

        builder.Append(methodModel.IsReturnVoid ? "\tself." : "\tresult = self.");
        builder.Append(methodModel.MethodName);
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
    }
}