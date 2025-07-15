namespace Chickensoft.AutoInject.Analyzers.Fixes;

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Utils;

[
  ExportCodeFixProvider(
    LanguageNames.CSharp,
    Name = nameof(AutoInjectNotifyMissingFixProvider)
  ),
  Shared
]
public class AutoInjectNotifyMissingFixProvider : CodeFixProvider {
  private static readonly ImmutableArray<string> _fixableDiagnosticIds =
    [Diagnostics.MissingAutoInjectNotifyDescriptor.Id];

  public sealed override ImmutableArray<string> FixableDiagnosticIds =>
    _fixableDiagnosticIds;

  public sealed override FixAllProvider GetFixAllProvider() =>
    WellKnownFixAllProviders.BatchFixer;

  public sealed override async Task RegisterCodeFixesAsync(
    CodeFixContext context
  ) {
    if (context.Diagnostics.Length == 0) {
      return;
    }

    var root = await context.Document
      .GetSyntaxRootAsync(context.CancellationToken)
      .ConfigureAwait(false);
    if (root is null) {
      return;
    }

    var diagnostic = context.Diagnostics[0];
    var diagnosticSpan = diagnostic.Location.SourceSpan;

    var parent = root.FindToken(diagnosticSpan.Start).Parent;
    if (parent is null) {
      return;
    }

    // Find the type declaration identified by the diagnostic
    TypeDeclarationSyntax? typeDeclaration = default;
    foreach (var ancestor in parent.AncestorsAndSelf()) {
      if (ancestor is TypeDeclarationSyntax td) {
        typeDeclaration = td;
        break;
      }
    }
    if (typeDeclaration is null) {
      return;
    }

    context.RegisterCodeFix(
      CodeAction.Create(
        title: "Add \"this.Notify(what);\" to existing \"_Notification\" override",
        createChangedDocument: cancellationToken =>
          AddAutoInjectNotifyCallAsync(
            context.Document,
            typeDeclaration,
            cancellationToken
          ),
        equivalenceKey: nameof(AutoInjectNotificationOverrideFixProvider)
      ),
      diagnostic
    );
  }

  private static async Task<Document> AddAutoInjectNotifyCallAsync(
      Document document,
      TypeDeclarationSyntax typeDeclaration,
      CancellationToken cancellationToken
  ) {
    var originalMethodNode = AnalyzerTools
      .GetMethodOverride(typeDeclaration, Constants.NOTIFICATION_METHOD_NAME);
    if (originalMethodNode is null) {
      return document;
    }

    ParameterSyntax? parameterSyntax = default;
    foreach (var p in originalMethodNode.ParameterList.Parameters) {
      if (
        p.Type is PredefinedTypeSyntax pts
          && pts.Keyword.IsKind(SyntaxKind.IntKeyword)
      ) {
        parameterSyntax = p;
        break;
      }
    }
    if (parameterSyntax is null) {
      return document;
    }

    // Get the actual name of the parameter from the found method
    // It really should be "what", but this makes it more robust
    // to changes in the parameter name
    var actualParameterName = parameterSyntax.Identifier.ValueText;

    // Construct the statement to add
    var statementToAdd = SyntaxFactory.ExpressionStatement(
        SyntaxFactory.InvocationExpression(
          SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ThisExpression(),
            SyntaxFactory.IdentifierName(Constants.NOTIFY_METHOD_NAME)
          )
        )
        .WithArgumentList(
          SyntaxFactory.ArgumentList(
            SyntaxFactory.SingletonSeparatedList(
              SyntaxFactory.Argument(
                SyntaxFactory.IdentifierName(actualParameterName)
              )
            )
          )
        )
      )
      .WithAdditionalAnnotations(Formatter.Annotation);

    // Add the statement to the method body
    return await MethodModifier.AddStatementToMethodBodyAsync(
        document,
        typeDeclaration,
        originalMethodNode,
        statementToAdd,
        cancellationToken
    );
  }
}
