// -----------------------------------------------------------------------
// <copyright file="GeneratedCodeOnlyUsageAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TedToolkit.Occt.Runtime.Analyzers;

/// <summary>
/// Reports handwritten operational references to Runtime APIs reserved for generated bindings.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedCodeOnlyUsageAnalyzer : DiagnosticAnalyzer
{
    private const string MarkerMetadataName = "TedToolkit.Occt.Runtime.GeneratedCodeOnlyAttribute";

    /// <summary>
    /// Identifies use of an API reserved for generated OCCT binding code.
    /// </summary>
    public const string DiagnosticId = "TTOCCT001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Runtime API is reserved for generated binding code",
        "API '{0}' is reserved for generated OCCT binding code; use a generated factory or operation",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Handwritten code must not call public Runtime implementation hooks reserved for generated OCCT bindings.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            return [Rule,];
        }
    }

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var marker = compilationContext.Compilation.GetTypeByMetadataName(MarkerMetadataName);
            if (marker is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeOperation(operationContext, marker),
                OperationKind.ObjectCreation,
                OperationKind.Invocation,
                OperationKind.PropertyReference,
                OperationKind.EventReference,
                OperationKind.MethodReference);
        });
    }

    private static void AnalyzeOperation(in OperationAnalysisContext context, INamedTypeSymbol marker)
    {
        if (IsWithinNameOf(context.Operation) || IsWithinMarkedImplementation(context.ContainingSymbol, marker))
        {
            return;
        }

        ISymbol? referencedSymbol = context.Operation switch
        {
            IObjectCreationOperation objectCreation => objectCreation.Constructor,
            IInvocationOperation invocation => invocation.TargetMethod,
            IPropertyReferenceOperation propertyReference => propertyReference.Property,
            IEventReferenceOperation eventReference => eventReference.Event,
            IMethodReferenceOperation methodReference => methodReference.Method,
            _ => null,
        };

        if (referencedSymbol is null || !IsMarked(referencedSymbol, marker))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            context.Operation.Syntax.GetLocation(),
            referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static bool IsMarked(ISymbol symbol, INamedTypeSymbol marker)
    {
        if (HasMarker(symbol, marker)
            || (HasMarker(symbol.ContainingType, marker) && IsExternallyCallable(symbol)))
        {
            return true;
        }

        return symbol switch
        {
            IPropertySymbol property => HasMarker(property.GetMethod, marker) || HasMarker(property.SetMethod, marker),
            IEventSymbol @event => HasMarker(@event.AddMethod, marker)
                                   || HasMarker(@event.RemoveMethod, marker)
                                   || HasMarker(@event.RaiseMethod, marker),
            IMethodSymbol { AssociatedSymbol: { } associatedSymbol, } => HasMarker(associatedSymbol, marker),
            _ => false,
        };
    }

    private static bool HasMarker(ISymbol? symbol, INamedTypeSymbol marker)
    {
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker))
            {
                return true;
            }
        }

        if (SymbolEqualityComparer.Default.Equals(symbol, symbol.OriginalDefinition))
        {
            return false;
        }

        foreach (var attribute in symbol.OriginalDefinition.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExternallyCallable(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithinMarkedImplementation(ISymbol containingSymbol, INamedTypeSymbol marker)
    {
        for (ISymbol? symbol = containingSymbol; symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (HasMarker(symbol, marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinNameOf(IOperation operation)
    {
        for (var ancestor = operation.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is INameOfOperation)
            {
                return true;
            }
        }

        return false;
    }
}