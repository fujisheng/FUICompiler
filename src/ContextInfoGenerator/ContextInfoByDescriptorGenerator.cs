using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FUICompiler
{
    /// <summary>
    /// 通过描述文件生成上下文信息
    /// </summary>
    internal class ContextInfoByDescriptorGenerator : IContextInfoGenerator
    {
        public List<ContextBindingInfo> Generate(SemanticModel semanticModel, SyntaxNode root)
        {
            var result = new List<ContextBindingInfo>();
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            
            foreach (var classDeclaration in classDeclarations)
            {
                var classType = semanticModel.GetDeclaredSymbol(classDeclaration);
                if(!classType.IsContextDescriptor(out var viewModelType))
                {
                    continue;
                }

                var contextInfo = new ContextBindingInfo
                {
                    viewModelType = viewModelType.ToString(),
                };

                var properties = classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                foreach (var property in properties)
                {
                    var propertyTypeInfo = semanticModel.GetTypeInfo(property.Type);
                    CreateContext(semanticModel, contextInfo, property, propertyTypeInfo.Type);
                }

                result.Add(contextInfo);
            }

            return result;
        }

        /// <summary>
        /// 生成一个上下文信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="contextInfo">上下文信息</param>
        /// <param name="property">描述文件中的属性定义</param>
        /// <param name="propertyType">描述文件中的属性类型</param>
        void CreateContext(SemanticModel semanticModel, ContextBindingInfo contextInfo, PropertyDeclarationSyntax property, ITypeSymbol propertyType)
        {
            //如果这个属性是ViewName属性 获取ViewName
            if (propertyType.IsType(typeof(string)) && property.Identifier.Text == "ViewName")
            {
                var expression = property.ChildNodes().OfType<ArrowExpressionClauseSyntax>().First().Expression;
                contextInfo.viewName = (expression as LiteralExpressionSyntax).Token.ValueText;
            }

            //如果这个属性是Properties属性 获取属性绑定信息
            if (propertyType.IsType(typeof(FUI.BindingDescriptor.PropertyBindingDescriptor[])) && property.Identifier.Text == "Properties")
            {
                var propertites = property.ChildNodes().OfType<ArrowExpressionClauseSyntax>().First()
                    .ChildNodes().OfType<ArrayCreationExpressionSyntax>().First().Initializer
                    .ChildNodes().OfType<InvocationExpressionSyntax>();
                contextInfo.properties = CreatePropertites(semanticModel, propertites);
            }

            //如果这个属性是Commands属性 获取命令绑定信息
            if (propertyType.IsType(typeof(FUI.BindingDescriptor.CommandBindingDescriptor[])) && property.Identifier.Text == "Commands")
            {
                var commands = property.ChildNodes().OfType<ArrowExpressionClauseSyntax>().First()
                    .ChildNodes().OfType<ArrayCreationExpressionSyntax>().First().Initializer
                    .ChildNodes().OfType<InvocationExpressionSyntax>();
                contextInfo.commands = CreateCommands(semanticModel, commands);
            }
        }

        /// <summary>
        /// 创建所有属性绑定信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="invocations">所有绑定方法调用</param>
        /// <returns></returns>
        List<PropertyBindingInfo> CreatePropertites(SemanticModel semanticModel, IEnumerable<InvocationExpressionSyntax> invocations)
        {
            var result = new List<PropertyBindingInfo>();
            foreach (var invocation in invocations)
            {
                var propertyBindingInfo = new PropertyBindingInfo
                {
                    targetInfo = new TargetInfo()
                };
                CreateProperty(semanticModel, propertyBindingInfo, invocation);
                result.Add(propertyBindingInfo);
            }
            return result;
        }

        /// <summary>
        /// 创建一个属性绑定信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="propertyBinding">属性绑定信息</param>
        /// <param name="invocation">绑定方法调用</param>
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

        /// <summary>
        /// 创建属性源信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="invocation">BindingProperty调用</param>
        /// <returns></returns>
        PropertyInfo CreatePropertyInfo(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
        {
            var memberAccess = invocation.ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;
            var propertyInfo = new PropertyInfo();
            propertyInfo.name = memberAccess.Name.Identifier.Text;
            var propertyTypeInfo = semanticModel.GetTypeInfo(memberAccess);
            propertyInfo.type = propertyTypeInfo.Type.ToString();
            var propertySymbolInfo = semanticModel.GetSymbolInfo(memberAccess.Name);
            propertyInfo.location = propertySymbolInfo.Symbol.Locations.First().ToLocationInfo();
            propertyInfo.isList = propertyTypeInfo.Type.IsObservableList();

            return propertyInfo;
        }

        /// <summary>
        /// 创建目标路径信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="targetInfo">目标信息</param>
        /// <param name="invocation">ToTarget调用</param>
        void CreateTargetInfo(SemanticModel semanticModel, TargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var targetPath = (invocation.ArgumentList.Arguments.First().Expression as LiteralExpressionSyntax).Token.ValueText;
            targetInfo.path = targetPath;
        }

        /// <summary>
        /// 创建目标元素信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="targetInfo">目标信息</param>
        /// <param name="invocation">ToElement调用</param>
        void CreateElementInfo(SemanticModel semanticModel, TargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var nameofArg = invocation.ArgumentList.Arguments.First().Expression as InvocationExpressionSyntax;
            var memberAccessExpr = nameofArg.ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;

            targetInfo.type = semanticModel.GetTypeInfo(memberAccessExpr.Expression).Type.ToString();
            var propertyTypeInfo = semanticModel.GetTypeInfo(memberAccessExpr);
            targetInfo.propertyType = semanticModel.GetTypeInfo(memberAccessExpr).Type.ToString();
            targetInfo.propertyName = memberAccessExpr.Name.ToString();

            if(propertyTypeInfo.Type.IsBindableProperty(out var propertyValueType))
            {
                targetInfo.propertyValueType = propertyValueType.ToString();
            }
        }

        /// <summary>
        /// 创建转换器信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="memberAccess">.ToConverter调用</param>
        /// <returns></returns>
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

        /// <summary>
        /// 创建绑定模式
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="invocation">WithBindingMode调用</param>
        /// <returns></returns>
        BindingMode CreateBindingMode(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
        {
            var argument = invocation.ArgumentList.Arguments.First();
            var memberAccess = argument.ChildNodes().OfType<MemberAccessExpressionSyntax>().First();
            return Enum.Parse<BindingMode>(memberAccess.Name.Identifier.Text);
        }

        /// <summary>
        /// 创建所有命令绑定信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="invocations">所有BingingCommand调用</param>
        /// <returns></returns>
        List<CommandBindingInfo> CreateCommands(SemanticModel semanticModel, IEnumerable<InvocationExpressionSyntax> invocations)
        {
            var result = new List<CommandBindingInfo>();
            foreach (var invocation in invocations)
            {
                var commandBindingInfo = new CommandBindingInfo
                {
                    targetInfo = new CommandTargetInfo(),
                    commandInfo = new CommandInfo()
                };
                CreateCommand(semanticModel, commandBindingInfo, invocation);
                result.Add(commandBindingInfo);
            }
            return result;
        }

        /// <summary>
        /// 创建一个命令绑定信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="bindingInfo">命令绑定信息</param>
        /// <param name="invocation">BindingCommand调用</param>
        void CreateCommand(SemanticModel semanticModel, CommandBindingInfo bindingInfo, InvocationExpressionSyntax invocation)
        {
            var memberAccesses = invocation.ChildNodes().OfType<MemberAccessExpressionSyntax>();
            var hasMemberAccess = memberAccesses != null && memberAccesses.Count() != 0;
            var accessName = string.Empty;
            if (hasMemberAccess)
            {
                accessName = memberAccesses.First().Name.Identifier.Text;
            }
            else
            {
                if(invocation.Expression is GenericNameSyntax generic)
                {
                    accessName = generic.Identifier.ValueText;
                }
                else
                {
                    accessName = invocation.Expression.ToString();
                }
            }

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

        /// <summary>
        /// 创建命令源信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="commandInfo">命令绑定信息</param>
        /// <param name="invocation">BindingCommand调用</param>
        void CreateCommandInfo(SemanticModel semanticModel, CommandInfo commandInfo, InvocationExpressionSyntax invocation)
        {
            var argument = invocation.ArgumentList.Arguments.First().Expression;
            var memberAccess = argument is MemberAccessExpressionSyntax
                ? argument as MemberAccessExpressionSyntax
                : (argument as InvocationExpressionSyntax).ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;

            var typeInfo = semanticModel.GetSymbolInfo(memberAccess.Name);
            commandInfo.parameters = new List<string>();

            //如果命令源是一个方法
            if (typeInfo.Symbol is IMethodSymbol methodSymbol)
            {
                commandInfo.name = memberAccess.Name.Identifier.ValueText;
                commandInfo.location = methodSymbol.Locations.First().ToLocationInfo();
                foreach (var param in methodSymbol.Parameters)
                {
                    commandInfo.parameters.Add(param.Type.ToString());
                }
            }

            //如果命令源是一个事件
            if (typeInfo.Symbol is IEventSymbol eventSymbol)
            {
                //命令源是一个事件的时候 会生成一个事件调用方法 这儿存这个方法的名字
                commandInfo.name = Utility.GetEventMethodName(memberAccess.Name.Identifier.ValueText);
                commandInfo.isEvent = true;
                commandInfo.location = eventSymbol.Locations.First().ToLocationInfo();
                if (eventSymbol.Type is INamedTypeSymbol namedTypeSymbol)
                {
                    foreach (var param in namedTypeSymbol.TypeArguments)
                    {
                        commandInfo.parameters.Add(param.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// 创建命令目标路径信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="targetInfo">目标信息</param>
        /// <param name="invocation">ToTarget调用</param>
        void CreateCommandTargetPathInfo(SemanticModel semanticModel, CommandTargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var targetPath = (invocation.ArgumentList.Arguments.First().Expression as LiteralExpressionSyntax).Token.ValueText;
            targetInfo.path = targetPath;
        }

        /// <summary>
        /// 创建命令目标信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="targetInfo">目标信息</param>
        /// <param name="invocation">ToCommand调用</param>
        void CreateCommandTargetInfo(SemanticModel semanticModel, CommandTargetInfo targetInfo, InvocationExpressionSyntax invocation)
        {
            var nameofArg = invocation.ArgumentList.Arguments.First().Expression as InvocationExpressionSyntax;
            var memberAccessExpr = nameofArg.ArgumentList.Arguments.First().Expression as MemberAccessExpressionSyntax;

            targetInfo.type = semanticModel.GetTypeInfo(memberAccessExpr.Expression).Type.ToString();
            var propertyTypeInfo = semanticModel.GetTypeInfo(memberAccessExpr);
            targetInfo.propertyName = memberAccessExpr.Name.ToString();
            targetInfo.parameters = new List<string>();

            if(propertyTypeInfo.Type.IsCommand(out var arguments))
            {
                foreach(var arg in arguments)
                {
                    targetInfo.parameters.Add(arg.ToString());
                }
            }
        }
    }
}
