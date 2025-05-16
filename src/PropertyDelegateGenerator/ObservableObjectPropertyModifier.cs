using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FUICompiler
{
    /// <summary>
    /// 修改可观察对象属性的set方法添加委托调用 并添加partial和public修饰符
    /// </summary>
    internal class ObservableObjectPropertyModifier
    {
        internal SyntaxNode Modify(SemanticModel semanticModel, SyntaxNode root)
        {
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

            return root.ReplaceNodes(classDeclarations, (oldClass, _) =>
            {
                var type = semanticModel.GetDeclaredSymbol(oldClass);

                if (!type.IsObservableObject() || type.IsAbstract || type.IsStatic)
                {
                    return oldClass;
                }

                var newModifiers = oldClass.Modifiers;
                bool hasPublic = newModifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                var internalToken = newModifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.InternalKeyword));

                //处理public修饰符（避免重复）
                if (internalToken != default(SyntaxToken))
                {
                    //替换internal为public（原类无public时）
                    newModifiers = newModifiers.Replace(internalToken, SyntaxFactory.Token(SyntaxKind.PublicKeyword));
                    hasPublic = true;
                }
                else if (!hasPublic)
                {
                    //原类无internal且无public时，添加public（带前导空格）
                    newModifiers = newModifiers.Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                        .WithLeadingTrivia(SyntaxFactory.Whitespace(" ")));
                }

                //添加partial修饰符（确保与前一个修饰符、class关键字有空格）
                if (!newModifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    //为partial设置前导空格（与前一个修饰符）和尾随空格（与class关键字）
                    var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                        .WithLeadingTrivia(SyntaxFactory.Whitespace(" "))  // 与前一个修饰符的空格
                        .WithTrailingTrivia(SyntaxFactory.Whitespace(" ")); // 与class关键字的空格
                    newModifiers = newModifiers.Add(partialToken);
                }

                var newClass = oldClass.WithModifiers(newModifiers);

                var properties = newClass.ChildNodes().OfType<PropertyDeclarationSyntax>().ToArray();
                newClass = newClass.ReplaceNodes(properties, (property, _) =>
                {
                    var propertyName = property.Identifier.Text;
                    var fieldName = Utility.GetPropertyBackingFieldName(propertyName);
                    var delegateName = Utility.GetPropertyChangedDelegateName(propertyName);

                    var newProperty = property;
                    if (newProperty.Initializer != null)
                    {
                        // 保留原属性的尾随Trivia（如换行、空行）
                        var originalTrailingTrivia = property.GetTrailingTrivia();
                        newProperty = newProperty.WithInitializer(null)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
                            .WithTrailingTrivia(originalTrailingTrivia); // 恢复原尾随Trivia
                    }

                    return ModifyPropertyGetSet(newProperty, fieldName, delegateName);
                });

                return newClass;
            });
        }

        const string SetBody = @"if(System.Collections.Generic.EqualityComparer<{Type}>.Default.Equals(this.{FieldName}, value)) {return; }var preValue = this.{FieldName}; this.{FieldName} = value; {DelegateName}?.Invoke(this, preValue, value);";
        const string GetBody = "return this.{FieldName};";

        PropertyDeclarationSyntax ModifyPropertyGetSet(PropertyDeclarationSyntax property, string fieldName, string delegateName)
        {
            if (property?.AccessorList == null)
            {
                return null;
            }

            var oldGet = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
            var oldSet = property.AccessorList.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
            if (oldGet == null || oldSet == null)
            {
                return null;
            }

            // 处理自动属性（Body为null的情况）
            var getTrivia = oldGet.Body != null 
                ? oldGet.Body.GetLeadingTrivia().Concat(oldGet.Body.GetTrailingTrivia()) 
                : oldGet.GetLeadingTrivia().Concat(oldGet.GetTrailingTrivia());
            var getBody = SyntaxFactory.ParseStatement(GetBody.Replace("{FieldName}", fieldName))
                .WithLeadingTrivia(getTrivia);

            var setTrivia = oldSet.Body != null 
                ? oldSet.Body.GetLeadingTrivia().Concat(oldSet.Body.GetTrailingTrivia()) 
                : oldSet.GetLeadingTrivia().Concat(oldSet.GetTrailingTrivia());
            var setBody = SyntaxFactory.ParseStatement(SetBody
                    .Replace("{FieldName}", fieldName)
                    .Replace("{Type}", property.Type.ToString())
                    .Replace("{DelegateName}", delegateName))
                .WithLeadingTrivia(setTrivia);

            // 显式块访问器不需要分号，移除分号Token
            return property.WithAccessorList(
                property.AccessorList.WithAccessors(SyntaxFactory.List(new[]
                {
                    oldGet.WithBody(SyntaxFactory.Block(getBody))
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None)), // 移除get访问器的分号
                    oldSet.WithBody(SyntaxFactory.Block(setBody))
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))  // 移除set访问器的分号
                }))
            );
        }
    }
}
