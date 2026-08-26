// -----------------------------------------------------------------------
// <copyright file="NativeError.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.Occt;

/// <summary>
/// Represents the private sequential carrier returned by the generated native boundary.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeError
{
    private int kind;

    private nint typeName;

    private nint message;

    private nint stackTrace;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeError"/> struct.
    /// </summary>
    /// <param name="kind">The native error category.</param>
    /// <param name="typeName">The borrowed null-terminated UTF-8 native type name.</param>
    /// <param name="message">The borrowed null-terminated UTF-8 native message.</param>
    /// <param name="stackTrace">The borrowed null-terminated UTF-8 native stack text.</param>
    internal NativeError(
        int kind,
#if NET6_0_OR_GREATER || NETSTANDARD2_1
        in nint typeName,
        in nint message,
        in nint stackTrace)
#else
        nint typeName,
        nint message,
        nint stackTrace)
#endif
    {
        this.kind = kind;
        this.typeName = typeName;
        this.message = message;
        this.stackTrace = stackTrace;
    }

    /// <summary>
    /// Gets the native error category.
    /// </summary>
    internal readonly int Kind
    {
        get
        {
            return kind;
        }
    }

    /// <summary>
    /// Gets the borrowed null-terminated UTF-8 native type-name pointer.
    /// </summary>
    internal readonly nint TypeName
    {
        get
        {
            return typeName;
        }
    }

    /// <summary>
    /// Gets the borrowed null-terminated UTF-8 native message pointer.
    /// </summary>
    internal readonly nint Message
    {
        get
        {
            return message;
        }
    }

    /// <summary>
    /// Gets the borrowed null-terminated UTF-8 native stack-text pointer.
    /// </summary>
    internal readonly nint StackTrace
    {
        get
        {
            return stackTrace;
        }
    }

    /// <summary>
    /// Resets every field after the owning native storage has been consumed.
    /// </summary>
    internal void Clear()
    {
        kind = 0;
        typeName = 0;
        message = 0;
        stackTrace = 0;
    }
}