// -----------------------------------------------------------------------
// <copyright file="ManifoldException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Manifold;

/// <summary>Represents a failure copied from the Manifold native boundary.</summary>
public class ManifoldException : Exception
{
    internal ManifoldException(string message, string? nativeTypeName, string? nativeStackTrace)
        : base(message)
    {
        NativeTypeName = nativeTypeName;
        NativeStackTrace = nativeStackTrace;
    }

    /// <summary>Gets the copied native exception type name.</summary>
    public string? NativeTypeName { get; }

    /// <summary>Gets the copied native stack text.</summary>
    public string? NativeStackTrace { get; }
}

/// <summary>Represents a native invalid-argument failure.</summary>
public sealed class ManifoldArgumentException : ManifoldException
{
    internal ManifoldArgumentException(string message, string? type, string? stack) : base(message, type, stack) { }
}

/// <summary>Represents a native out-of-range failure.</summary>
public sealed class ManifoldArgumentOutOfRangeException : ManifoldException
{
    internal ManifoldArgumentOutOfRangeException(string message, string? type, string? stack) : base(message, type, stack) { }
}

/// <summary>Represents a native allocation failure.</summary>
public sealed class ManifoldOutOfMemoryException : ManifoldException
{
    internal ManifoldOutOfMemoryException(string message, string? type, string? stack) : base(message, type, stack) { }
}

/// <summary>Represents a native or managed-width overflow failure.</summary>
public sealed class ManifoldOverflowException : ManifoldException
{
    /// <summary>Initializes an overflow failure from copied diagnostics.</summary>
    public ManifoldOverflowException(string message, string? nativeTypeName, string? nativeStackTrace)
        : base(message, nativeTypeName, nativeStackTrace) { }
}

/// <summary>Represents an unknown non-standard native exception.</summary>
public sealed class ManifoldUnknownException : ManifoldException
{
    internal ManifoldUnknownException(string message, string? type, string? stack) : base(message, type, stack) { }
}
