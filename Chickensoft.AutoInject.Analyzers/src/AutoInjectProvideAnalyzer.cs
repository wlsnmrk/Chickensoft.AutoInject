namespace Chickensoft.AutoInject.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Utils;

/// <summary>
/// When inheriting IProvide, the class must call this.Provide() somewhere in the setup.
/// This analyzer checks that the class does not forget to call this.Provide().
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoInjectProvideAnalyzer : DiagnosticAnalyzer {
  private static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics =
    [Diagnostics.MissingAutoInjectProvideDescriptor];

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

  private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context) {
    var classDeclaration = (ClassDeclarationSyntax)context.Node;

    // Check that IProvide is implemented by the class, as these are the only classes that need to call Provide().
    var classSymbol = context
      .SemanticModel
      .GetDeclaredSymbol(classDeclaration, context.CancellationToken);

    if (classSymbol is null) {
      return;
    }

    var implementsIProvide = false;
    foreach (var @interface in classSymbol.AllInterfaces) {
      if (@interface.IsGenericType
        && @interface.Name == Constants.PROVIDER_INTERFACE_NAME
      ) {
        implementsIProvide = true;
        break;
      }
    }

    if (!implementsIProvide) {
      return;
    }

    // Check that Meta attribute has an AutoInject Provider type (ex: [Meta(typeof(IAutoNode))])
    var attribute = AnalyzerTools.GetAutoInjectMetaAttribute(classDeclaration);

    if (attribute is null) {
      return;
    }

    // Check if the class calls "this.Provide()" anywhere
    if (
      AnalyzerTools
        .HasThisCall(classDeclaration, Constants.PROVIDE_METHOD_NAME)
    ) {
      return;
    }

    // No provide call found, report the diagnostic
    context.ReportDiagnostic(
      Diagnostics.MissingAutoInjectProvide(
        attribute.GetLocation(),
        classDeclaration.Identifier.ValueText
      )
    );
  }
}
