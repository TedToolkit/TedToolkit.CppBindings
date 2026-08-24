// -----------------------------------------------------------------------
// <copyright file="CAbiHeaderGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services;

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Generates the canonical ABI-major-1 C11 declaration surface.
/// </summary>
internal static class CAbiHeaderGenerator
{
    /// <summary>
    /// Generates one order-independent header and omits every incompletely mapped operation.
    /// </summary>
    /// <param name="operations">The candidate semantic operations.</param>
    /// <returns>The canonical header and deterministic omission diagnostics.</returns>
    public static CAbiHeaderGenerationResult Generate(IEnumerable<AbiOperationModel> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var exportable = new List<(AbiOperationModel Operation, AbiOperationIdentity Identity)>();
        var diagnostics = new List<AbiProjectionDiagnostic>();
        foreach (var operation in operations)
        {
            ArgumentNullException.ThrowIfNull(operation);
            var validation = AbiContractValidator.Validate(operation);
            if (!validation.IsExportable)
            {
                diagnostics.Add(validation.Diagnostic!);
                continue;
            }

            exportable.Add((operation, AbiOperationIdentity.Create(operation)));
        }

        exportable.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Identity.SymbolName, right.Identity.SymbolName));
        diagnostics.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.ToString(), right.ToString()));
        RejectCollisions(exportable);

        var builder = new StringBuilder();
        AppendContractPreamble(builder);
        foreach (var entry in exportable)
        {
            AppendOperation(builder, entry.Operation, entry.Identity);
        }

        builder.Append("\n#ifdef __cplusplus\n}\n#endif\n\n#endif\n");
        return new() { Header = builder.ToString(), Diagnostics = diagnostics, };
    }

    private static void AppendContractPreamble(StringBuilder builder)
    {
        builder.Append(
            "#ifndef TED_TOOLKIT_OCCT_V1_H\n"
            + "#define TED_TOOLKIT_OCCT_V1_H\n\n"
            + "#include <stdint.h>\n\n"
            + "#if defined(_WIN32)\n"
            + "#if defined(TED_OCCT_V1_BUILD)\n"
            + "#define TED_OCCT_V1_API __declspec(dllexport)\n"
            + "#else\n"
            + "#define TED_OCCT_V1_API __declspec(dllimport)\n"
            + "#endif\n"
            + "#define TED_OCCT_V1_CALL __cdecl\n"
            + "#else\n"
            + "#define TED_OCCT_V1_API __attribute__((visibility(\"default\")))\n"
            + "#define TED_OCCT_V1_CALL\n"
            + "#endif\n\n"
            + "#ifdef __cplusplus\n"
            + "extern \"C\" {\n"
            + "#endif\n\n"
            + "#define TED_OCCT_V1_ABI_MAJOR UINT32_C(1)\n"
            + "#define TED_OCCT_V1_ABI_MINOR UINT32_C(0)\n"
            + "#define TED_OCCT_V1_ABI_VERSION UINT32_C(0x00010000)\n\n"
            + "typedef int32_t ted_occt_v1_error_kind;\n\n"
            + "#define TED_OCCT_V1_ERROR_NONE ((ted_occt_v1_error_kind)0)\n"
            + "#define TED_OCCT_V1_ERROR_ARGUMENT ((ted_occt_v1_error_kind)1)\n"
            + "#define TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE ((ted_occt_v1_error_kind)2)\n"
            + "#define TED_OCCT_V1_ERROR_ARITHMETIC ((ted_occt_v1_error_kind)3)\n"
            + "#define TED_OCCT_V1_ERROR_INVALID_OPERATION ((ted_occt_v1_error_kind)4)\n"
            + "#define TED_OCCT_V1_ERROR_NULL_OBJECT ((ted_occt_v1_error_kind)5)\n"
            + "#define TED_OCCT_V1_ERROR_OUT_OF_MEMORY ((ted_occt_v1_error_kind)6)\n"
            + "#define TED_OCCT_V1_ERROR_OVERFLOW ((ted_occt_v1_error_kind)7)\n"
            + "#define TED_OCCT_V1_ERROR_OCCT_FAILURE ((ted_occt_v1_error_kind)8)\n"
            + "#define TED_OCCT_V1_ERROR_STD_EXCEPTION ((ted_occt_v1_error_kind)9)\n"
            + "#define TED_OCCT_V1_ERROR_UNKNOWN ((ted_occt_v1_error_kind)255)\n\n"
            + "typedef struct ted_occt_v1_error\n"
            + "{\n"
            + "    ted_occt_v1_error_kind kind;\n"
            + "    const char* type_name;\n"
            + "    const char* message;\n"
            + "    const char* stack_trace;\n"
            + "} ted_occt_v1_error;\n\n"
            + "typedef struct ted_occt_v1_pnt2d\n"
            + "{\n"
            + "    double x;\n"
            + "    double y;\n"
            + "} ted_occt_v1_pnt2d;\n\n"
            + "typedef struct ted_occt_v1_geom2d_cartesian_point ted_occt_v1_geom2d_cartesian_point;\n\n"
            + "typedef struct ted_occt_v1_bytes_view\n"
            + "{\n"
            + "    const uint8_t* data;\n"
            + "    uint64_t length;\n"
            + "} ted_occt_v1_bytes_view;\n\n"
            + "typedef struct ted_occt_v1_owned_bytes\n"
            + "{\n"
            + "    uint8_t* data;\n"
            + "    uint64_t length;\n"
            + "} ted_occt_v1_owned_bytes;\n\n"
            + "TED_OCCT_V1_API uint32_t TED_OCCT_V1_CALL ted_occt_v1_abi_version(void);\n"
            + "TED_OCCT_V1_API void TED_OCCT_V1_CALL ted_occt_v1_error_clear(ted_occt_v1_error* error);\n"
            + "TED_OCCT_V1_API void TED_OCCT_V1_CALL ted_occt_v1_owned_bytes_clear("
            + "ted_occt_v1_owned_bytes* buffer);\n");
    }

    private static void AppendOperation(
        StringBuilder builder,
        AbiOperationModel operation,
        AbiOperationIdentity identity)
    {
        builder.Append("TED_OCCT_V1_API ted_occt_v1_error TED_OCCT_V1_CALL ")
            .Append(identity.SymbolName)
            .Append('(');

        var hasValue = false;
        foreach (var parameter in operation.Parameters)
        {
            if (hasValue)
            {
                builder.Append(", ");
            }

            builder.Append(parameter.Type.CAbiTypeName)
                .Append(' ')
                .Append(parameter.Name);
            hasValue = true;
        }

        if (operation.Result is not null)
        {
            if (hasValue)
            {
                builder.Append(", ");
            }

            builder.Append(operation.Result.Type.CAbiTypeName)
                .Append(" result");
            hasValue = true;
        }

        if (!hasValue)
        {
            builder.Append("void");
        }

        builder.Append(");\n");
    }

    private static void RejectCollisions(
        List<(AbiOperationModel Operation, AbiOperationIdentity Identity)> operations)
    {
        for (var index = 1; index < operations.Count; index++)
        {
            var previous = operations[index - 1].Identity;
            var current = operations[index].Identity;
            if (StringComparer.Ordinal.Equals(previous.SymbolName, current.SymbolName))
            {
                throw new InvalidOperationException($"Duplicate ABI operation symbol '{current.SymbolName}'.");
            }
        }
    }
}