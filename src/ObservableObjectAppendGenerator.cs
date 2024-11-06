using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Text;

namespace FUICompiler
{
    /// <summary>
    /// 可绑定属性委托生成器
    /// </summary>
    internal class ObservableObjectAppendGenerator : ITypeSyntaxNodeSourcesGenerator
    {
        public Source?[] Generate(SyntaxNode root, out SyntaxNode newRoot)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToArray().Select(item =>
            {
                return item?.Name?.ToString();
            });

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            var sources = new List<Source?>();
            newRoot =  root.ReplaceNodes(classDeclarations, (oldClass, _) =>
            {
                if (!Utility.IsObservableObject(oldClass))
                {
                    return oldClass;
                }

                var appendBuilder = new StringBuilder();

                //添加using
                if (usings != null)
                {
                    foreach (var @using in usings)
                    {
                        appendBuilder.AppendLine($"using {@using};");
                    }
                }

                //判断是否有命名空间
                var hasNamespace = Utility.TryGetNamespace(oldClass, out var namespaceName);
                if (hasNamespace)
                {
                    appendBuilder.AppendLine($"namespace {namespaceName}");
                    appendBuilder.AppendLine("{");
                }

                //生成分布类
                appendBuilder.AppendLine($"public partial class {oldClass.Identifier.Text} : {Utility.SynchronizePropertiesFullName}");
                appendBuilder.AppendLine("{");

                var syncPropertiesBuilder = new StringBuilder();

                //如果是静态类或者抽象类直接不管
                if (oldClass.Modifiers.Any((k) => k.IsKind(SyntaxKind.StaticKeyword) || k.IsKind(SyntaxKind.AbstractKeyword)))
                {
                    return oldClass;
                }

                var newClass = oldClass.WithModifiers(SyntaxFactory.TokenList());
                newClass = newClass.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword)).NormalizeWhitespace();
                newClass = newClass.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword)).NormalizeWhitespace();

                //遍历所有属性 生成对应委托
                var propertites = newClass.ChildNodes().OfType<PropertyDeclarationSyntax>().ToArray();
                newClass = newClass.ReplaceNodes(propertites, (property, _) =>
                {
                    if (!Utility.IsObservableProperty(newClass, property))
                    {
                        return property;
                    }

                    var propertyType = property.Type.ToString();
                    var propertyName = property.Identifier.Text;

                    //生成BackingField
                    var fieldName = Utility.GetPropertyBackingFieldName(propertyName);
                    appendBuilder.AppendLine($"public {propertyType} {fieldName} {property.Initializer};");

                    //生成对应的委托
                    string delegateType = $"FUI.Bindable.PropertyChangedHandler<{propertyType}>";
                    var delegateName = Utility.GetPropertyChangedDelegateName(propertyName);
                    
                    appendBuilder.AppendLine($"public {delegateType} {delegateName};");

                    //生成对应的委托调用
                    syncPropertiesBuilder.AppendLine($"{delegateName}?.Invoke(this, this.{propertyName}, this.{propertyName});");

                    var newProperty = property;

                    if(property.Initializer != null)
                    {
                        newProperty = property.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None));
                        newProperty = newProperty.RemoveNode(newProperty.Initializer, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                    return ModifyPropertyGetSet(newProperty, fieldName, delegateName);
                });

                GenerateSyncPropertites(appendBuilder, syncPropertiesBuilder);

                appendBuilder.AppendLine("}");
                if (hasNamespace)
                {
                    appendBuilder.AppendLine("}");
                }
                var code = Utility.NormalizeCode(appendBuilder.ToString());
                //Console.WriteLine($"generate property changed for {oldClass.Identifier.Text}");
                sources.Add(new Source($"{oldClass.Identifier.Text}.PropertyChanged", code));
                return newClass;
            });

            return sources.ToArray();
        }

        void GenerateSyncPropertites(StringBuilder propertyDelegateBuilder, StringBuilder syncPropertiesBuilder)
        {
            //生成同步所有属性的方法
            propertyDelegateBuilder.AppendLine($"void {Utility.SynchronizePropertiesFullName}.{Utility.SynchronizePropertiesMethodName}()");
            propertyDelegateBuilder.AppendLine("{");
            propertyDelegateBuilder.AppendLine(syncPropertiesBuilder.ToString());
            propertyDelegateBuilder.AppendLine("}");
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
            if(oldGet == null || oldSet == null)
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
                    SyntaxFactory.List<AccessorDeclarationSyntax>(
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
