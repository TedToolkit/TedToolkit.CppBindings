// -----------------------------------------------------------------------
// <copyright file="FclException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Fcl;

/// <summary>Represents a failure copied from the FCL native boundary.</summary>
public class FclException : Exception
{
    internal FclException(string message, string? nativeTypeName, string? nativeStackTrace) : base(message)
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
public sealed class FclArgumentException : FclException
{
    internal FclArgumentException(string message, string? type, string? stack) : base(message, type, stack) { }
}

/// <summary>Represents a native out-of-range failure.</summary>
public sealed class FclArgumentOutOfRangeException : FclException
{
    internal FclArgumentOutOfRangeException(string message, string? type, string? stack) : base(message, type, stack) { }
}

/// <summary>Represents a native allocation failure.</summary>
public sealed class FclOutOfMemoryException : FclException
{
    internal FclOutOfMemoryException(string message, string? type, string? stack) : base(message, type, stack) { }
}

/// <summary>Represents an unknown non-standard native failure.</summary>
public sealed class FclUnknownException : FclException
{
    internal FclUnknownException(string message, string? type, string? stack) : base(message, type, stack) { }
}
