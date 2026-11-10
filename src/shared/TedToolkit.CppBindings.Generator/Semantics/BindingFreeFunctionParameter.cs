// -----------------------------------------------------------------------
// <copyright file="BindingFreeFunctionParameter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one free-function parameter.
/// </summary>
/// <param name="Name">The managed parameter name.</param>
/// <param name="Type">The parameter's type projection.</param>
internal sealed record BindingFreeFunctionParameter(string Name, BindingFreeFunctionType Type);