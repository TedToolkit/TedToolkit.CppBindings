// -----------------------------------------------------------------------
// <copyright file="BindingComputedPropertyDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes an expression-bodied property on a composite result.
/// </summary>
/// <param name="Type">The managed property type.</param>
/// <param name="Name">The managed property name.</param>
/// <param name="Expression">The managed expression.</param>
public sealed record BindingComputedPropertyDefinition(string Type, string Name, string Expression);