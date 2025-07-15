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
using Microsoft.CodeAnalysis.Simplification;
using Utils;

[
  ExportCodeFixProvider(
    LanguageNames.CSharp,
    Name = nameof(AutoInjectNotificationOverrideFixProvider)
  ),
  Shared
]
public class AutoInjectNotificationOverrideFixProvider : CodeFixProvider {
  private static readonly ImmutableArray<string> _fixableDiagnosticIds =
    [Diagnostics.MissingAutoInjectNotificationOverrideDescriptor.Id];

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
        title: "Add \"public override void _Notification(int what) => this.Notify(what);\" method",
        createChangedDocument: cancellationToken =>
          AddAutoInjectNotificationOverrideAsync(
            context.Document,
            typeDeclaration,
            cancellationToken
          ),
        equivalenceKey: nameof(AutoInjectNotificationOverrideFixProvider)
      ),
      diagnostic
    );
  }

  private static async Task<Document> AddAutoInjectNotificationOverrideAsync(
    Document document,
    TypeDeclarationSyntax typeDeclaration,
    CancellationToken cancellationToken
  ) {
    var root = await document
      .GetSyntaxRootAsync(cancellationToken)
      .ConfigureAwait(false);
    if (root is null) {
      return document;
    }

    var methodDeclaration = SyntaxFactory.MethodDeclaration(
        SyntaxFactory.PredefinedType(
          SyntaxFactory.Token(SyntaxKind.VoidKeyword)
        ),
        Constants.NOTIFICATION_METHOD_NAME
      )
      .WithModifiers(
        SyntaxFactory.TokenList(
          SyntaxFactory.Token(SyntaxKind.PublicKeyword),
          SyntaxFactory.Token(SyntaxKind.OverrideKeyword)
        )
      )
      .WithParameterList(
        SyntaxFactory.ParameterList(
          SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory
              .Parameter(SyntaxFactory.Identifier(Constants.WHAT_PARAMETER_NAME))
              .WithType(
                SyntaxFactory.PredefinedType(
                  SyntaxFactory.Token(SyntaxKind.IntKeyword)
                )
              )
          )
        )
      );

    var expressionBody = SyntaxFactory
      .InvocationExpression(
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
              SyntaxFactory.IdentifierName(Constants.WHAT_PARAMETER_NAME)
            )
          )
        )
      );

    var arrowExpressionClause = SyntaxFactory
      .ArrowExpressionClause(expressionBody)
      .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

    methodDeclaration = methodDeclaration
      .WithExpressionBody(arrowExpressionClause)
      .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

    // Insert the new method at the beginning of the class members
    var existingMembers = typeDeclaration.Members;
    var newMembers = existingMembers.Insert(0, methodDeclaration);

    // Update the type declaration with the new list of members
    var newTypeDeclaration = typeDeclaration.WithMembers(newMembers);

    var newRoot = root.ReplaceNode(typeDeclaration, newTypeDeclaration);

    // Return the updated document.
    return document.WithSyntaxRoot(newRoot);
  }
}
