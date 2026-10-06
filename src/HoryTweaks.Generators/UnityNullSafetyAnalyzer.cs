using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HoryTweaks.Generators;

/// <summary>
/// Flags null operators that bypass the Unity lifetime check on <see cref="T:UnityEngine.Object"/>
/// derivatives. Unity overloads ==/!= so a "destroyed" object compares equal to null; managed
/// operators like ??= and ?. skip that check and observe a live wrapper instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnityNullSafetyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CoalesceAssignment = new(
        id: "HTU001",
        title: "Null-coalescing assignment on a Unity object",
        messageFormat: "'??=' skips the Unity lifetime check; use an explicit 'obj == null' guard instead",
        category: "HoryTweaks.Il2Cpp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConditionalComparison = new(
        id: "HTU002",
        title: "Conditional access on a Unity object used in a comparison",
        messageFormat: "'?.' skips the Unity lifetime check; compare with 'obj == null' / 'obj != null' instead",
        category: "HoryTweaks.Il2Cpp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConditionalAssignmentTarget = new(
        id: "HTU003",
        title: "Assignment target uses conditional access on a Unity object",
        messageFormat: "'?.' in an assignment target skips the Unity lifetime check; use an explicit null guard",
        category: "HoryTweaks.Il2Cpp",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(CoalesceAssignment, ConditionalComparison, ConditionalAssignmentTarget);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeCoalesceAssignment, SyntaxKind.CoalesceAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeConditionalAccess, SyntaxKind.ConditionalAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeCoalesceAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (IsUnityObject(context.SemanticModel.GetTypeInfo(assignment.Left, context.CancellationToken).Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(CoalesceAssignment, assignment.GetLocation()));
        }
    }

    private static void AnalyzeConditionalAccess(SyntaxNodeAnalysisContext context)
    {
        var access = (ConditionalAccessExpressionSyntax)context.Node;

        // Only evaluate the outermost access of a ?. chain; inner nodes are handled by it.
        if (access.Parent is ConditionalAccessExpressionSyntax)
        {
            return;
        }

        if (!IsComparisonOperand(access.Parent))
        {
            return;
        }

        if (IsUnityObject(context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConditionalComparison, access.GetLocation()));
        }
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        foreach (var access in assignment.Left.DescendantNodesAndSelf().OfType<ConditionalAccessExpressionSyntax>())
        {
            // Find the receiver of the outermost conditional access chain.
            var outermost = access;
            while (outermost.Parent is ConditionalAccessExpressionSyntax parent)
            {
                outermost = parent;
            }

            if (IsUnityObject(context.SemanticModel.GetTypeInfo(outermost.Expression, context.CancellationToken).Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(ConditionalAssignmentTarget, outermost.GetLocation()));
                return;
            }
        }
    }

    private static bool IsComparisonOperand(SyntaxNode? parent) => parent switch
    {
        BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.EqualsExpression)
            || binary.IsKind(SyntaxKind.NotEqualsExpression)
            || binary.IsKind(SyntaxKind.LessThanExpression)
            || binary.IsKind(SyntaxKind.LessThanOrEqualExpression)
            || binary.IsKind(SyntaxKind.GreaterThanExpression)
            || binary.IsKind(SyntaxKind.GreaterThanOrEqualExpression),
        IsPatternExpressionSyntax => true,
        _ => false,
    };

    private static bool IsUnityObject(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "Object" &&
                current.ContainingNamespace?.ToDisplayString() == "UnityEngine")
            {
                return true;
            }
        }

        return false;
    }
}
