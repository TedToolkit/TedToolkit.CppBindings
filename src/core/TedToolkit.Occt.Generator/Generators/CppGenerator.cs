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
            foreach (var header in GetRequiredHeaders(recordDecl))
            {
                builder.Append("#include <");
                builder.Append(header);
                builder.AppendLine(">");
            }

            builder.AppendLine();

            foreach (var recordDeclMethodModel in recordDecl.MethodModels)
            {
                builder.Append(recordDeclMethodModel.NoExceptions ? "CSHARP_WRAPPER(" : "CSHARP_WRAPPER_TRY(");
                builder.Append(recordDeclMethodModel.GetMethodInteropName(recordDecl));
                builder.Append('(');

                switch (recordDeclMethodModel.Type)
                {
                    case MethodModelType.NORMAL:
                        GenerateNormalMethod(ref builder, recordDeclMethodModel, recordDecl);
                        break;

                    case MethodModelType.NEW:
                        GenerateNew(ref builder, recordDeclMethodModel, recordDecl);
                        break;

                    case MethodModelType.DELETE:
                        GenerateDelete(ref builder, recordDecl);
                        break;

                    case MethodModelType.OPERATOR:
                        GenerateNormalMethod(ref builder, recordDeclMethodModel, recordDecl);
                        break;

                    case MethodModelType.IMPLICIT:
                    case MethodModelType.EXPLICIT:
                        GenerateConversionMethod(ref builder, recordDeclMethodModel, recordDecl);
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

    private static IEnumerable<string> GetRequiredHeaders(RecordModel recordModel)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (seen.Add(recordModel.SourceHeader))
        {
            yield return recordModel.SourceHeader;
        }

        foreach (var header in EnumerateRequiredHeaders(recordModel.Type))
        {
            if (seen.Add(header))
            {
                yield return header;
            }
        }

        foreach (var fieldModel in recordModel.FieldModels)
        {
            foreach (var header in EnumerateRequiredHeaders(fieldModel.Type))
            {
                if (seen.Add(header))
                {
                    yield return header;
                }
            }
        }

        foreach (var methodModel in recordModel.MethodModels)
        {
            foreach (var header in EnumerateRequiredHeaders(methodModel.ReturnType))
            {
                if (seen.Add(header))
                {
                    yield return header;
                }
            }

            foreach (var parameterModel in methodModel.Parameters)
            {
                foreach (var header in EnumerateRequiredHeaders(parameterModel.Type))
                {
                    if (seen.Add(header))
                    {
                        yield return header;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateRequiredHeaders(TypeModel typeModel)
    {
        foreach (var header in typeModel.RequiredHeaders)
        {
            yield return header;
        }
    }

    private static void GenerateNew(ref Utf16ValueStringBuilder builder, MethodModel methodModel, RecordModel recordModel)
    {
        var alloc = recordModel.RequiresNew;
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

        if (!recordModel.IsStandardTransient)
        {
            return;
        }

        builder.AppendLine("\tinstance->IncrementRefCounter();");
    }

    private static void GenerateDelete(ref Utf16ValueStringBuilder builder, RecordModel recordModel)
    {
        var alloc = recordModel.RequiresNew;
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

    private static void GenerateConversionMethod(
        ref Utf16ValueStringBuilder builder,
        MethodModel methodModel,
        RecordModel recordModel)
    {
        if (methodModel.IsConst)
        {
            builder.Append("const ");
        }

        builder.Append(recordModel.Type.CppTypeName);
        builder.Append(" & self");

        var hasReference = false;
        if (!methodModel.IsReturnVoid)
        {
            builder.Append(", ");

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
        AppendInvocationStatement(ref builder, methodModel, recordModel, hasReference);
    }

    private void GenerateNormalMethod(
        ref Utf16ValueStringBuilder builder,
        MethodModel methodModel,
        RecordModel recordModel)
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
        if (!methodModel.IsReturnVoid && !methodModel.ReturnSelf)
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
        AppendInvocationStatement(ref builder, methodModel, recordModel, hasReference);
    }

    private static void AppendInvocationStatement(
        ref Utf16ValueStringBuilder builder,
        MethodModel methodModel,
        RecordModel recordModel,
        bool hasReference)
    {
        if (hasReference)
        {
            builder.Append("\t*result = &");
        }
        else
        {
            builder.Append(methodModel.IsReturnVoid || methodModel.ReturnSelf ? "\t" : "\t*result = ");
        }

        AppendInvocationExpression(ref builder, methodModel, recordModel);
        builder.AppendLine(";");
    }

    private static void AppendInvocationExpression(
        ref Utf16ValueStringBuilder builder,
        MethodModel methodModel,
        RecordModel recordModel)
    {
        switch (methodModel.Type)
        {
            case MethodModelType.NORMAL:
                AppendNamedMethodInvocation(ref builder, methodModel, recordModel);
                break;

            case MethodModelType.OPERATOR:
                builder.Append("self.operator");
                builder.Append(methodModel.MethodName);
                AppendInvocationArguments(ref builder, methodModel);
                break;

            case MethodModelType.IMPLICIT:
            case MethodModelType.EXPLICIT:
                builder.Append("self.operator ");
                builder.Append(methodModel.ReturnType.CppTypeName.Trim());
                builder.Append("()");
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported invocation method model type '{methodModel.Type}'.");
        }
    }

    private static void AppendNamedMethodInvocation(
        ref Utf16ValueStringBuilder builder,
        MethodModel methodModel,
        RecordModel recordModel)
    {
        if (methodModel.IsStatic)
        {
            builder.Append(recordModel.Type.CppTypeName);
            builder.Append("::");
        }
        else
        {
            builder.Append("self.");
        }

        builder.Append(methodModel.MethodName);
        AppendInvocationArguments(ref builder, methodModel);
    }

    private static void AppendInvocationArguments(ref Utf16ValueStringBuilder builder, MethodModel methodModel)
    {
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

        builder.Append(')');
    }
}
