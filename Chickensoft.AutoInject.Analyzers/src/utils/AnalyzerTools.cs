namespace Chickensoft.AutoInject.Analyzers.Utils;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class AnalyzerTools {
  public static AttributeSyntax? GetAutoInjectMetaAttribute(
    ClassDeclarationSyntax classDeclaration
  ) {
    foreach (var attributeList in classDeclaration.AttributeLists) {
      foreach (var attr in attributeList.Attributes) {
        if (
          attr.ArgumentList is not null
            && attr.Name.ToString() == Constants.META_ATTRIBUTE_NAME
        ) {
          foreach (var arg in attr.ArgumentList.Arguments) {
            if (
              arg.Expression is TypeOfExpressionSyntax {
                Type: IdentifierNameSyntax identifierName
              }
                && Constants
                  .AutoInjectTypeNames
                  .Contains(identifierName.Identifier.ValueText)
            ) {
              return attr;
            }
          }
        }
      }
    }
    return null;
  }

  public static MethodDeclarationSyntax? GetMethodOverride(
    TypeDeclarationSyntax typeDeclaration,
    string methodName
  ) {
    foreach (var member in typeDeclaration.Members) {
      if (
        member is MethodDeclarationSyntax method
          && method.ParameterList.Parameters.Count == 1
          && method.Identifier.ValueText == methodName
      ) {
        foreach (var modifier in method.Modifiers) {
          if (modifier.IsKind(SyntaxKind.OverrideKeyword)) {
            return method;
          }
        }
      }
    }
    return null;
  }

  public static bool HasThisCall(
    MemberDeclarationSyntax node,
    string methodName
  ) {
    foreach (var descendant in node.DescendantNodes()) {
      if (
        descendant is InvocationExpressionSyntax invocation
          && invocation.Expression is MemberAccessExpressionSyntax {
            Expression: ThisExpressionSyntax
          } memberInvocation
          && memberInvocation.Name.Identifier.ValueText == methodName
      ) {
        return true;
      }
    }
    return false;
  }
}
