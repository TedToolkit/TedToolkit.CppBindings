// -----------------------------------------------------------------------
// <copyright file="ValueLifetimeAnalyzer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TedToolkit.CppBindings.Analyzers;

/// <summary>
/// Reports locally demonstrable lifetime hazards involving a non-owning owner Value reference.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueLifetimeAnalyzer : DiagnosticAnalyzer
{
    private const string OwnerMetadataName = "TedToolkit.CppBindings.ICppOwner`1";

    /// <summary>
    /// Identifies a supported unsafe use of a non-owning owner Value reference.
    /// </summary>
    public const string DiagnosticId = "TTCB002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Owner Value reference has an unsafe lifetime pattern",
        "Non-owning owner Value is used in an unsafe lifetime pattern: {0}",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Handwritten code must keep an owner alive and undisposed while using its non-owning Value reference.");

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
            var owner = compilationContext.Compilation.GetTypeByMetadataName(OwnerMetadataName);
            if (owner is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeValueAccess(in syntaxContext, owner),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    private static void AnalyzeValueAccess(
        in SyntaxNodeAnalysisContext context,
        INamedTypeSymbol owner)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var property = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol
            as IPropertySymbol;
        if (!IsOwnerValue(property, owner))
        {
            return;
        }

        var ownerExpression = UnwrapParentheses(memberAccess.Expression);
        var reason = GetHazardReason(context, memberAccess, ownerExpression);
        if (reason is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), reason));
    }

    private static string? GetHazardReason(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess,
        ExpressionSyntax ownerExpression)
    {
        if (IsReferenceEscape(context, valueAccess))
        {
            return "the reference escapes its owner scope";
        }

        if (!TryGetStableOwnerSymbol(context, ownerExpression, out var owner))
        {
            return "Value is accessed through a temporary owner";
        }

        if (IsRoutineOperationReceiver(valueAccess))
        {
            return "Value is used as a routine operation receiver";
        }

        if (IsUsedAfterKnownDispose(context, valueAccess, owner))
        {
            return "the owner is already known to be disposed";
        }

        var hasDerivedReference = TryGetDerivedReferenceLocal(context, valueAccess, out var referenceLocal);
        if (hasDerivedReference
            && IsUsedAcrossSuspension(context, valueAccess, referenceLocal))
        {
            return "the derived reference is used across await or yield";
        }

        if (hasDerivedReference
            && IsUsedAfterAliasDispose(context, valueAccess, owner, referenceLocal))
        {
            return "a known owner alias is disposed while the derived reference remains live";
        }

        return IsFixedWithoutKeepAlive(context, valueAccess, owner)
            ? "fixed pointer use is not followed by GC.KeepAlive(owner)"
            : null;
    }

    private static bool IsOwnerValue(
        IPropertySymbol? property,
        INamedTypeSymbol owner)
    {
        if (property is not { Name: "Value", RefKind: not RefKind.None, })
        {
            return false;
        }

        var containingType = property.ContainingType;
        if (SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, owner))
        {
            return true;
        }

        foreach (var contract in containingType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, owner))
            {
                continue;
            }

            foreach (var member in contract.GetMembers("Value"))
            {
                var implementation = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(implementation?.OriginalDefinition, property.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsReferenceEscape(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess)
    {
        if (valueAccess.Parent is ArgumentSyntax { RefKindKeyword.RawKind: not 0, })
        {
            return true;
        }

        if (valueAccess.Parent is not RefExpressionSyntax reference)
        {
            return false;
        }

        if (reference.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax)
        {
            return true;
        }

        if (reference.Parent is not AssignmentExpressionSyntax assignment)
        {
            return false;
        }

        var target = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
        return target is IFieldSymbol or IPropertySymbol;
    }

    private static bool TryGetStableOwnerSymbol(
        in SyntaxNodeAnalysisContext context,
        ExpressionSyntax ownerExpression,
        out ISymbol owner)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(ownerExpression, context.CancellationToken).Symbol;
        if (symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol)
        {
            owner = symbol;
            return true;
        }

        owner = null!;
        return false;
    }

    private static bool IsRoutineOperationReceiver(MemberAccessExpressionSyntax valueAccess)
    {
        return valueAccess.Parent is MemberAccessExpressionSyntax outer
               && ReferenceEquals(outer.Expression, valueAccess)
               && outer.Parent is InvocationExpressionSyntax invocation
               && ReferenceEquals(invocation.Expression, outer);
    }

    private static bool IsUsedAfterKnownDispose(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess,
        ISymbol owner)
    {
        if (!TryGetStatementPosition(valueAccess, out var block, out var statementIndex))
        {
            return false;
        }

        var aliases = CollectAliases(context, block, statementIndex, owner);
        for (var index = 0; index < statementIndex; index++)
        {
            if (IsDisposeOfKnownAlias(context, block.Statements[index], aliases))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDerivedReferenceLocal(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess,
        out ILocalSymbol referenceLocal)
    {
        if (valueAccess.Parent is not RefExpressionSyntax reference
            || reference.Parent is not EqualsValueClauseSyntax initializer
            || initializer.Parent is not VariableDeclaratorSyntax declarator
            || context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not ILocalSymbol local)
        {
            referenceLocal = null!;
            return false;
        }

        referenceLocal = local;
        return true;
    }

    private static bool IsUsedAcrossSuspension(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess,
        ILocalSymbol referenceLocal)
    {
        if (!TryGetStatementPosition(valueAccess, out var block, out var statementIndex))
        {
            return false;
        }

        var suspensionSeen = false;
        for (var index = statementIndex + 1; index < block.Statements.Count; index++)
        {
            var statement = block.Statements[index];
            if (ContainsSuspension(statement))
            {
                suspensionSeen = true;
            }

            if (suspensionSeen && ContainsSymbol(context, statement, referenceLocal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsedAfterAliasDispose(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess,
        ISymbol owner,
        ILocalSymbol referenceLocal)
    {
        if (!TryGetStatementPosition(valueAccess, out var block, out var statementIndex))
        {
            return false;
        }

        var aliases = CollectAliases(context, block, block.Statements.Count, owner);
        var disposeSeen = false;
        for (var index = statementIndex + 1; index < block.Statements.Count; index++)
        {
            var statement = block.Statements[index];
            if (IsDisposeOfKnownAlias(context, statement, aliases))
            {
                disposeSeen = true;
                continue;
            }

            if (disposeSeen && ContainsSymbol(context, statement, referenceLocal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFixedWithoutKeepAlive(
        in SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax valueAccess,
        ISymbol owner)
    {
        var fixedStatement = valueAccess.Ancestors().OfType<FixedStatementSyntax>().FirstOrDefault();
        if (fixedStatement?.Parent is not BlockSyntax block)
        {
            return false;
        }

        var statementIndex = block.Statements.IndexOf(fixedStatement);
        if (statementIndex < 0)
        {
            return false;
        }

        var aliases = CollectAliases(context, block, block.Statements.Count, owner);
        for (var index = statementIndex + 1; index < block.Statements.Count; index++)
        {
            if (ContainsKeepAliveOfKnownAlias(context, block.Statements[index], aliases))
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<ISymbol> CollectAliases(
        in SyntaxNodeAnalysisContext context,
        BlockSyntax block,
        int statementLimit,
        ISymbol owner)
    {
        var aliases = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { owner, };

        var changed = true;
        while (changed)
        {
            changed = false;
            for (var index = 0; index < statementLimit; index++)
            {
                if (block.Statements[index] is not LocalDeclarationStatementSyntax declaration)
                {
                    continue;
                }

                foreach (var declarator in declaration.Declaration.Variables)
                {
                    if (declarator.Initializer?.Value is not { } initializer
                        || !TryGetReferencedSymbol(context, initializer, out var source)
                        || !aliases.Contains(source)
                        || context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken)
                            is not ILocalSymbol alias)
                    {
                        continue;
                    }

                    changed |= aliases.Add(alias);
                }
            }
        }

        return aliases;
    }

    private static bool IsDisposeOfKnownAlias(
        in SyntaxNodeAnalysisContext context,
        StatementSyntax statement,
        HashSet<ISymbol> aliases)
    {
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation, })
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.ValueText != "Dispose"
            || !TryGetReferencedSymbol(context, memberAccess.Expression, out var receiverSymbol)
            || !aliases.Contains(receiverSymbol))
        {
            return false;
        }

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            as IMethodSymbol;
        return method is { Parameters.Length: 0, };
    }

    private static bool ContainsKeepAliveOfKnownAlias(
        in SyntaxNodeAnalysisContext context,
        StatementSyntax statement,
        HashSet<ISymbol> aliases)
    {
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation, })
        {
            return false;
        }

        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            as IMethodSymbol;
        return method is { Name: "KeepAlive", Parameters.Length: 1, }
               && method.ContainingType.ToDisplayString() == "System.GC"
               && invocation.ArgumentList.Arguments.Count == 1
               && TryGetReferencedSymbol(
                   context,
                   invocation.ArgumentList.Arguments[0].Expression,
                   out var keptAlive)
               && aliases.Contains(keptAlive);
    }

    private static bool ContainsSuspension(StatementSyntax statement)
    {
        return statement.DescendantNodesAndSelf().Any(static node =>
            node is AwaitExpressionSyntax or YieldStatementSyntax);
    }

    private static bool ContainsSymbol(
        in SyntaxNodeAnalysisContext context,
        StatementSyntax statement,
        ISymbol symbol)
    {
        foreach (var identifier in statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var candidate = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
            if (SymbolEqualityComparer.Default.Equals(candidate, symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetReferencedSymbol(
        in SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        out ISymbol symbol)
    {
        var candidate = context.SemanticModel.GetSymbolInfo(UnwrapParentheses(expression), context.CancellationToken).Symbol;
        if (candidate is ILocalSymbol or IParameterSymbol or IFieldSymbol)
        {
            symbol = candidate;
            return true;
        }

        symbol = null!;
        return false;
    }

    private static bool TryGetStatementPosition(
        SyntaxNode node,
        out BlockSyntax block,
        out int statementIndex)
    {
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement?.Parent is BlockSyntax containingBlock)
        {
            block = containingBlock;
            statementIndex = block.Statements.IndexOf(statement);
            return statementIndex >= 0;
        }

        block = null!;
        statementIndex = -1;
        return false;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}