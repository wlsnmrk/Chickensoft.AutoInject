namespace Chickensoft.AutoInject.Analyzers;

using System.Collections.Immutable;
using Chickensoft.AutoInject.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoInjectNotificationOverrideMissingAnalyzer : DiagnosticAnalyzer {
  private static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics =
    [Diagnostics.MissingAutoInjectNotificationOverrideDescriptor];

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    _supportedDiagnostics;

  public override void Initialize(AnalysisContext context) {
    context.EnableConcurrentExecution();

    context.ConfigureGeneratedCodeAnalysis(
      GeneratedCodeAnalysisFlags.None
    );

    context.RegisterSyntaxNodeAction(
      AnalyzeClassDeclaration,
      SyntaxKind.ClassDeclaration
    );
  }

  private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context) {
    var classDeclaration = (ClassDeclarationSyntax)context.Node;

    var attribute = AnalyzerTools
      .GetAutoInjectMetaAttribute(
        classDeclaration,
        Constants.IsAutoInjectMetaName
      );

    if (attribute is null) {
      return;
    }

    if (
      AnalyzerTools.GetMethodOverride(
        classDeclaration,
        Constants.NOTIFICATION_METHOD_NAME
      ) is null
    ) {
      // Report missing _Notification override.
      context.ReportDiagnostic(
        Diagnostics.MissingAutoInjectNotificationOverride(
          attribute.GetLocation(),
          classDeclaration.Identifier.ValueText
        )
      );
    }
  }
}
