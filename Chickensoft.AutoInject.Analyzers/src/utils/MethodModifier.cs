namespace Chickensoft.AutoInject.Analyzers.Utils;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

public static class MethodModifier {
  /// <summary>
  /// Adds a this.method call to the end of a specified method within a type
  /// declaration.
  /// </summary>
  /// <param name="document">Document to modify.</param>
  /// <param name="typeDeclaration">Type declaration off the class.</param>
  /// <param name="originalMethodNode">The method to add the call to.</param>
  /// <param name="methodToCallName">
  /// Name of the method to call at the end of the target method.
  /// </param>
  /// <param name="cancellationToken">Cancellation Token</param>
  /// <returns>Modified document</returns>
  public static async Task<Document> AddCallToMethod(
    Document document,
    MethodDeclarationSyntax originalMethodNode,
    string methodToCallName,
    CancellationToken cancellationToken
  ) {
    // Construct the parameterless call statement
    var parameterlessCallStatement = SyntaxFactory.ExpressionStatement(
      SyntaxFactory.InvocationExpression(
        SyntaxFactory.MemberAccessExpression(
          SyntaxKind.SimpleMemberAccessExpression,
          SyntaxFactory.ThisExpression(),
          SyntaxFactory.IdentifierName(methodToCallName)))
    ).WithAdditionalAnnotations(Formatter.Annotation);

    // Delegate to the more general helper
    return await AddStatementToMethodBodyAsync(
      document,
      originalMethodNode,
      parameterlessCallStatement,
      cancellationToken
    );
  }

  /// <summary>
  /// Adds a provided statement to the end of a specified method's body within a
  /// type declaration. Handles conversion from expression body to block body.
  /// </summary>
  /// <param name="document">Document to modify.</param>
  /// <param name="typeDeclaration">Type declaration of the class.</param>
  /// <param name="originalMethodNode">
  /// The method to add the statement to.
  /// </param>
  /// <param name="statementToAdd">The pre-constructed statement to add.</param>
  /// <param name="cancellationToken">Cancellation Token.</param>
  /// <returns>Modified document.</returns>
  public static async Task<Document> AddStatementToMethodBodyAsync(
    Document document,
    MethodDeclarationSyntax originalMethodNode,
    StatementSyntax statementToAdd,
    CancellationToken cancellationToken
  ) {
    var root = await document
      .GetSyntaxRootAsync(cancellationToken)
      .ConfigureAwait(false);

    if (root is null) {
      return document;
    }

    var methodInProgress = originalMethodNode;
    BlockSyntax finalNewBody;

    if (originalMethodNode.Body is not null) {
      // Existing block body
      var existingStatements = originalMethodNode.Body.Statements;
      var updatedStatements = existingStatements.Add(statementToAdd);
      finalNewBody = originalMethodNode.Body.WithStatements(updatedStatements);
    }
    else {
      // Expression body or missing body
      var statementsForNewBlock = SyntaxFactory.List<StatementSyntax>();
      if (originalMethodNode.ExpressionBody is not null) {
        var originalExpression = originalMethodNode.ExpressionBody.Expression;
        var statementFromExpr = SyntaxFactory
          .ExpressionStatement(originalExpression);

        // Make sure to preserve the trailing trivia from the original method's semicolon token
        // If we don't do this we will lose any code comments or whitespace that was after the semicolon
        var originalMethodSemicolon = originalMethodNode.SemicolonToken;
        if (
          !originalMethodSemicolon.IsKind(SyntaxKind.None)
            && !originalMethodSemicolon.IsMissing
        ) {
          var originalSemicolonTrailingTrivia = originalMethodSemicolon
            .TrailingTrivia;
          if (originalSemicolonTrailingTrivia.Any()) {
            statementFromExpr = statementFromExpr
              .WithSemicolonToken(
                statementFromExpr
                  .SemicolonToken
                  .WithTrailingTrivia(originalSemicolonTrailingTrivia)
              );
          }
        }
        statementsForNewBlock = statementsForNewBlock.Add(statementFromExpr);

        // Remove the old expression body from the method
        methodInProgress = methodInProgress
            .WithExpressionBody(null)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None));
      }

      // Add the provided statement
      statementsForNewBlock = statementsForNewBlock.Add(statementToAdd);
      // Create a new block with the collected statements
      finalNewBody = SyntaxFactory
        .Block(statementsForNewBlock);
    }

    var fullyModifiedMethod = methodInProgress
      .WithBody(finalNewBody)
      .WithAdditionalAnnotations(Formatter.Annotation);

    // Replace the original method with the modified one in the type declaration
    var newRoot = root.ReplaceNode(originalMethodNode, fullyModifiedMethod);
    return document.WithSyntaxRoot(newRoot);
  }
}
