using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using System.Reflection;

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
                var contextInfo = new ContextBindingInfo
                {
                    viewModelType = viewModelType.ToString(),
                };
                bindingInfo.contexts.Add(contextInfo);
                var properties = classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                foreach(var property in properties)
                {
                    var propertyTypeInfo = semanticModel.GetTypeInfo(property.Type);
                    CreateContext(semanticModel, bindingInfo, property, propertyTypeInfo.Type);
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

                if(baseNamed.IsGenericType && baseNamed.ToString().StartsWith("FUI.BindingDescriptor.ContextDescriptor"))
                {
                    viewModelType = baseNamed.TypeArguments[0] as INamedTypeSymbol;
                    return true;
                }
            }

            return false;
        }

        void CreateContext(SemanticModel semanticModel, BindingInfo bindingInfo, PropertyDeclarationSyntax property, ITypeSymbol propertyType)
        {
            if(propertyType.IsType(typeof(string)) && property.Identifier.Text == "ViewName")
            {
                var expression = property.ChildNodes().OfType<ArrowExpressionClauseSyntax>().First().Expression;
                bindingInfo.viewName = (expression as LiteralExpressionSyntax).Token.ValueText;
            }

            var isType = propertyType.IsType(typeof(FUI.BindingDescriptor.PropertyBindingDescriptor[]));
            if (propertyType.IsType(typeof(FUI.BindingDescriptor.PropertyBindingDescriptor[])) && property.Identifier.Text == "Properties")
            {
                var propertites = property.ChildNodes().OfType<ArrowExpressionClauseSyntax>().First()
                    .ChildNodes().OfType<ArrayCreationExpressionSyntax>().First().Initializer
                    .ChildNodes().OfType<InvocationExpressionSyntax>();
                CreatePropertites(semanticModel, bindingInfo, propertites);
            }

            if (propertyType.IsType(typeof(FUI.BindingDescriptor.CommandBindingDescriptor[])) && property.Identifier.Text == "Commands")
            {
                var commands = property.ChildNodes().OfType<ArrowExpressionClauseSyntax>().First()
                    .ChildNodes().OfType<ArrayCreationExpressionSyntax>().First().Initializer
                    .ChildNodes().OfType<InvocationExpressionSyntax>();
                CreateCommands(semanticModel, bindingInfo, commands);
            }
        }

        void CreatePropertites(SemanticModel semanticModel, BindingInfo bindingInfo, IEnumerable<InvocationExpressionSyntax> invocations)
        {
            var propertyBindingInfo = new PropertyBindingInfo();
            foreach(var invocation in invocations)
            {
                propertyBindingInfo.targetInfo = new TargetInfo();
                CreateProperty(semanticModel, propertyBindingInfo, invocation);
            }
        }

        void CreateProperty(SemanticModel semanticModel, PropertyBindingInfo propertyBinding, InvocationExpressionSyntax invocation)
        {
            var memberAccesses = invocation.ChildNodes().OfType<MemberAccessExpressionSyntax>();
            var hasMemberAccess = memberAccesses != null && memberAccesses.Count() != 0;
            var accessName = hasMemberAccess ? memberAccesses.First().Name.Identifier.Text : invocation.Expression.ToString();
            switch (accessName)
            {
                case "BindingProperty":
                    var propertyInfo = CreatePropertyInfo(semanticModel, invocation);
                    propertyBinding.propertyInfo = propertyInfo;
                    break;
                case "ToTarget":
                    CreateTargetInfo(semanticModel, propertyBinding.targetInfo, invocation);
                    break;
                case "ToElement":
                    CreateElementInfo(semanticModel, propertyBinding.targetInfo, invocation);
                    break;
                case "WithConverter":
                    var converterInfo = CreateConverterInfo(semanticModel, memberAccesses.First());
                    propertyBinding.converterInfo = converterInfo;
                    break;
                case "WithBindingMode":
                    var bindingMode = CreateBindingMode(semanticModel, invocation);
                    propertyBinding.bindingMode = bindingMode;
                    break;
            }

            if (!hasMemberAccess)
            {
                return;
            }

            var child = memberAccesses.First().ChildNodes().OfType<InvocationExpressionSyntax>().First();
            if (child != null)
            {
                CreateProperty(semanticModel, propertyBinding, child);
            }
        }

        PropertyInfo CreatePropertyInfo(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
        {
            var memberAccess = invocation.ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;
            var propertyInfo = new PropertyInfo();
            propertyInfo.name = memberAccess.Name.Identifier.Text;
            var propertyTypeInfo = semanticModel.GetTypeInfo(memberAccess);
            propertyInfo.type = propertyTypeInfo.Type.ToString();
            var propertySymbolInfo = semanticModel.GetSymbolInfo(memberAccess.Name);
            propertyInfo.location = propertySymbolInfo.Symbol.Locations.First().ToLocationInfo();

            foreach (var @interface in propertyTypeInfo.Type.AllInterfaces)
            {
                if (@interface.IsGenericType && @interface.ToString().StartsWith("FUI.Bindable.IReadOnlyObservableList"))
                {
                    propertyInfo.isList = true;
                }
            }

            return propertyInfo;
        }

        void CreateTargetInfo(SemanticModel semanticModel, TargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var targetPath = (invocation.ArgumentList.Arguments.First().Expression as LiteralExpressionSyntax).Token.ValueText;
            targetInfo.path = targetPath;
        }

        void CreateElementInfo(SemanticModel semanticModel, TargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var nameofArg = invocation.ArgumentList.Arguments.First().Expression as InvocationExpressionSyntax;
            var memberAccessExpr = nameofArg.ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;
            
            targetInfo.type = semanticModel.GetTypeInfo(memberAccessExpr.Expression).Type.ToString();
            var propertyTypeInfo = semanticModel.GetTypeInfo(memberAccessExpr);
            targetInfo.propertyType = semanticModel.GetTypeInfo(memberAccessExpr).Type.ToString();
            targetInfo.propertyName = memberAccessExpr.Name.ToString();

            foreach (var @interface in propertyTypeInfo.Type.AllInterfaces)
            {
                if (@interface.IsGenericType && @interface.ToString().StartsWith("FUI.Bindable.IBindableProperty"))
                {
                    targetInfo.propertyValueType = @interface.TypeArguments[0].ToString();
                    break;
                }
            }
        }

        ConverterInfo CreateConverterInfo(SemanticModel semanticModel, MemberAccessExpressionSyntax memberAccess)
        {
            var genericName = memberAccess.ChildNodes().OfType<GenericNameSyntax>().First();
            var converterType = semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
            var converterInfo = new ConverterInfo();
            converterInfo.type = converterType.ToString();
            if (converterType.IsValueConverter(out var sourceType, out var targetType))
            {
                converterInfo.sourceType = sourceType.ToString();
                converterInfo.targetType = targetType.ToString();
            }
            return converterInfo;
        }

        BindingMode CreateBindingMode(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
        {
            var argument = invocation.ArgumentList.Arguments.First();
            var memberAccess = argument.ChildNodes().OfType<MemberAccessExpressionSyntax>().First();
            return Enum.Parse<BindingMode>(memberAccess.Name.Identifier.Text);
        }

        void CreateCommands(SemanticModel semanticModel, BindingInfo bindingInfo, IEnumerable<InvocationExpressionSyntax> invocations)
        {
            var commandBindingInfo = new CommandBindingInfo();
            foreach (var invocation in invocations)
            {
                commandBindingInfo.targetInfo = new CommandTargetInfo();
                commandBindingInfo.commandInfo = new CommandInfo();
                CreateCommand(semanticModel, commandBindingInfo, invocation);
            }
        }

        void CreateCommand(SemanticModel semanticModel, CommandBindingInfo bindingInfo, InvocationExpressionSyntax invocation)
        {
            var memberAccesses = invocation.ChildNodes().OfType<MemberAccessExpressionSyntax>();
            var hasMemberAccess = memberAccesses != null && memberAccesses.Count() != 0;
            var accessName = hasMemberAccess ? memberAccesses.First().Name.Identifier.Text : invocation.Expression.ToString();
            switch (accessName)
            {
                case "BindingCommand":
                    CreateCommandInfo(semanticModel, bindingInfo.commandInfo, invocation);
                    break;
                case "ToTarget":
                    CreateCommandTargetPathInfo(semanticModel, bindingInfo.targetInfo, invocation);
                    break;
                case "ToCommand":
                    CreateCommandTargetInfo(semanticModel, bindingInfo.targetInfo, invocation);
                    break;
            }

            if (!hasMemberAccess)
            {
                return;
            }

            var child = memberAccesses.First().ChildNodes().OfType<InvocationExpressionSyntax>().First();
            if (child != null)
            {
                CreateCommand(semanticModel, bindingInfo, child);
            }
        }

        void CreateCommandInfo(SemanticModel semanticModel, CommandInfo commandInfo, InvocationExpressionSyntax invocation)
        {
            var argument = invocation.ArgumentList.Arguments.First().Expression;
            var memberAccess = argument is MemberAccessExpressionSyntax 
                ? argument as MemberAccessExpressionSyntax 
                : (argument as InvocationExpressionSyntax).ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;

            var typeInfo = semanticModel.GetSymbolInfo(memberAccess.Name);
            commandInfo.name = memberAccess.Name.Identifier.ValueText;
            commandInfo.parameters = new List<string>();
            if (typeInfo.Symbol is IMethodSymbol methodSymbol)
            {
                commandInfo.location = methodSymbol.Locations.First().ToLocationInfo();
                foreach (var param in methodSymbol.Parameters)
                {
                    commandInfo.parameters.Add(param.Type.ToString());
                }
            }

            if (typeInfo.Symbol is IEventSymbol eventSymbol)
            {
                commandInfo.isEvent = true;
                commandInfo.location = eventSymbol.Locations.First().ToLocationInfo();
                if(eventSymbol.Type is INamedTypeSymbol namedTypeSymbol)
                {
                    foreach (var param in namedTypeSymbol.TypeArguments)
                    {
                        commandInfo.parameters.Add(param.ToString());
                    }
                }
            }
        }

        void CreateCommandTargetPathInfo(SemanticModel semanticModel, CommandTargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var targetPath = (invocation.ArgumentList.Arguments.First().Expression as LiteralExpressionSyntax).Token.ValueText;
            targetInfo.path = targetPath;
        }

        void CreateCommandTargetInfo(SemanticModel semanticModel, CommandTargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var nameofArg = invocation.ArgumentList.Arguments.First().Expression as InvocationExpressionSyntax;
            var memberAccessExpr = nameofArg.ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;

            targetInfo.type = semanticModel.GetTypeInfo(memberAccessExpr.Expression).Type.ToString();
            var propertyTypeInfo = semanticModel.GetTypeInfo(memberAccessExpr);
            targetInfo.propertyName = memberAccessExpr.Name.ToString();

            targetInfo.parameters = new List<string>();
            foreach (var @interface in propertyTypeInfo.Type.AllInterfaces)
            {
                if (@interface.IsGenericType && @interface.ToString().StartsWith("FUI.Bindable.ICommand"))
                {
                    foreach (var type in @interface.TypeArguments)
                    {
                        targetInfo.parameters.Add(type.ToString());
                    }
                }
            }
        }
    }
}
