using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FUICompiler
{
    /// <summary>
    /// 通过描述文件生成绑定上下文
    /// </summary>
    internal class DescriptorBindingContextGenerator : ITypeSyntaxNodeSourcesGenerator
    {
        BuildParam buildParam;

        public DescriptorBindingContextGenerator(BuildParam buildParam)
        {
            this.buildParam = buildParam;
        }

        public Source?[] Generate(SemanticModel semanticModel, SyntaxNode root, out SyntaxNode newRoot)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToArray().Select(item =>
            {
                return item?.Name?.ToString();
            });

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            var sources = new List<Source?>();
            foreach(var classDeclaration in classDeclarations)
            {
                if(!IsDescriptor(semanticModel, classDeclaration, out var viewModelType))
                {
                    continue;
                }

                var bindingInfo = new BindingInfo();

                var properties = classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                foreach(var property in properties)
                {
                    var propertyTypeInfo = semanticModel.GetTypeInfo(property);

                    var propertyType = propertyTypeInfo.Type;
                    CreateContext(semanticModel, bindingInfo, property, propertyType as INamedTypeSymbol);
                }
            }

            newRoot = root;
            return null;
        }

        /// <summary>
        /// 判断一个类型定义是否是描述文件
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="classSyntax">类声明语法树</param>
        /// <returns></returns>
        bool IsDescriptor(SemanticModel semanticModel, ClassDeclarationSyntax classSyntax, out INamedTypeSymbol viewModelType)
        {
            viewModelType = null;
            var symbol = semanticModel.GetDeclaredSymbol(classSyntax);
            if(symbol is not INamedTypeSymbol namedTypeSymbol)
            {
                return false;
            }

            foreach(var baseType in namedTypeSymbol.GetBaseTypesAndThis())
            {
                if(baseType is not INamedTypeSymbol baseNamed)
                {
                    continue;
                }

                if(baseNamed.IsGenericType && baseNamed.ToString().StartsWith(nameof(FUI.BindingDescriptor.ContextDescriptor)))
                {
                    viewModelType = baseNamed.TypeArguments[0] as INamedTypeSymbol;
                    return true;
                }
            }

            return false;
        }

        void CreateContext(SemanticModel semanticModel, BindingInfo bindingInfo, PropertyDeclarationSyntax property, INamedTypeSymbol propertyType)
        {
            if(propertyType.IsType(typeof(string)) && property.Identifier.Text == "ViewName")
            {
                bindingInfo.viewName = property.Initializer.Value.ToString();
            }

            if (propertyType.IsType(typeof(FUI.BindingDescriptor.PropertyBindingDescriptor[])) && property.Identifier.Text == "Propertites")
            {
                var propertites = property.Initializer.Value as ArrayCreationExpressionSyntax;
                var invocations = propertites.ChildNodes().OfType<InvocationExpressionSyntax>();
                CreatePropertites(semanticModel, bindingInfo, invocations);
            }

            if (propertyType.IsType(typeof(FUI.BindingDescriptor.CommandBindingDescriptor[])) && property.Identifier.Text == "Commands")
            {
                var commands = property.Initializer.Value as ArrayCreationExpressionSyntax;
                var invocations = commands.ChildNodes().OfType<InvocationExpressionSyntax>();
                CreatePropertites(semanticModel, bindingInfo, invocations);
            }
        }

        void CreatePropertites(SemanticModel semanticModel, BindingInfo bindingInfo, IEnumerable<InvocationExpressionSyntax> invocations)
        {
            foreach(var invocation in  invocations)
            {
                var argument = invocation.ArgumentList?.Arguments[0];
                switch (invocation.Expression.ToString())
                {
                    case "BindingProperty":
                        break;
                    case "ToTarget":
                        break;
                    case "ToElement":
                        break;
                    case "WithConverter":
                        break;
                    case "WithBindingMode":
                        break;
                }
            }
        }

        void CreateCommands(SemanticModel semanticModel, BindingInfo bindingInfo, IEnumerable<InvocationExpressionSyntax> invocations)
        {
            foreach(var invocation in invocations)
            {
                var argument = invocation.ArgumentList?.Arguments[0];
                switch(invocation.Expression.ToString())
                {
                    case "BindingCommand":
                        break;
                    case "ToTarget":
                        break;
                    case "ToCommand":
                        break;
                }
            }
        }
    }
}
