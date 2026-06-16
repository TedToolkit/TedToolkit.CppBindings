// -----------------------------------------------------------------------
// <copyright file="IFieldService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Provides metadata access for field declarations.
/// </summary>
public interface IFieldService : IDeclService<FieldDecl>;