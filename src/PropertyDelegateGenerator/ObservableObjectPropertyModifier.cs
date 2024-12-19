using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FUICompiler
{
    /// <summary>
    /// 可绑定对象属性修改器 为属性Set添加委托调用 并将类修改为public partial
    /// </summary>
    internal class ObservableObjectPropertyModifier
    {
        internal SyntaxNode Modify(SemanticModel semanticModel, SyntaxNode root)
        {
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

            return root.ReplaceNodes(classDeclarations, (oldClass, _) =>
            {
                var type = semanticModel.GetDeclaredSymbol(oldClass);

                if (!type.IsObservableObject())
                {
                    return oldClass;
                }

                //如果是静态类或者抽象类直接不管
                if (type.IsAbstract || type.IsStatic)
                {
                    return oldClass;
                }

                //修改其修饰符为 public partial 
                var modifiers = SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Whitespace(" "))
                    );
                var newClass = oldClass.WithModifiers(modifiers);

                //遍历所有属性 并修改其Get Set 以调用其对应值更改委托
                var propertites = newClass.ChildNodes().OfType<PropertyDeclarationSyntax>().ToArray();
                newClass = newClass.ReplaceNodes(propertites, (property, _) =>
                {
                    //if (!Utility.IsObservableProperty(newClass, property))
                    //{
                    //    return property;
                    //}

                    var propertyName = property.Identifier.Text;
                    var fieldName = Utility.GetPropertyBackingFieldName(propertyName);
                    var delegateName = Utility.GetPropertyChangedDelegateName(propertyName);

                    var newProperty = property;
                    //如果这个属性包含初始化语句 则需要移除
                    if (newProperty.Initializer != null)
                    {
                        //移除属性赋值 "=xxx"
                        newProperty = newProperty.WithInitializer(null);
                        //移除属性后面的;号  这儿移除后会同时移除掉其换行符  所以再加上去
                        var newToken = SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken);
                        newProperty = newProperty.ReplaceToken(newProperty.SemicolonToken, newToken);
                        var endOfLine = SyntaxFactory.EndOfLine("\n");
                        newProperty = newProperty.WithTrailingTrivia(endOfLine);
                    }
                    return ModifyPropertyGetSet(newProperty, fieldName, delegateName);
                });
                return newClass;
            });
        }

        //为了不影响报错时的代码定位，这里都保持一行
        const string SetBody = @"if(System.Collections.Generic.EqualityComparer<{Type}>.Default.Equals(this.{FieldName}, value)) {return; }var preValue = this.{FieldName}; this.{FieldName} = value; {DelegateName}?.Invoke(this, preValue, value);";
        const string GetBody = "return this.{FieldName};";

        PropertyDeclarationSyntax ModifyPropertyGetSet(PropertyDeclarationSyntax property, string fieldName, string delegateName)
        {
            if (property == null)
            {
                return null;
            }

            if (property.AccessorList == null)
            {
                return null;
            }

            var oldGet = property.AccessorList?.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
            var oldSet = property.AccessorList?.Accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
            if (oldGet == null || oldSet == null)
            {
                return null;
            }

            oldGet = oldGet.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None));
            oldSet = oldSet.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None));

            var setBodyString = SetBody.Replace("{FieldName}", fieldName).Replace("{Type}", property.Type.ToString()).Replace("{DelegateName}", delegateName);
            var setBody = SyntaxFactory.ParseStatement(setBodyString);
            var getBodyString = GetBody.Replace("{FieldName}", fieldName);
            var getBody = SyntaxFactory.ParseStatement(getBodyString);
            var newProperty = property.WithAccessorList(
                property.AccessorList.WithAccessors(
                    SyntaxFactory.List(
                        new AccessorDeclarationSyntax[]
                        {
                            oldGet.WithBody(SyntaxFactory.Block(getBody)),
                            oldSet.WithBody(SyntaxFactory.Block(setBody))
                        })
                ));
            return newProperty;
        }
    }
}
