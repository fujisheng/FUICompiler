using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Reflection;

namespace FUICompiler
{
    /// <summary>
    /// 通过特性绑定来生成绑定上下文
    /// </summary>
    internal class AttributeBindingContextGenerator : ITypeSyntaxNodeSourcesGenerator
    {
        BindingContextGenerator bindingContextGenerator = new BindingContextGenerator(null, null);

        public Source?[] Generate(SemanticModel semanticModel, SyntaxNode root, out SyntaxNode newRoot)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToArray().Select(item=> 
            {
                return item?.Name?.ToString();
            });

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            var sources = new List<Source?>();
            foreach (var classDeclaration in classDeclarations)
            {
                if(!Utility.IsObservableObject(classDeclaration))
                {
                    continue;
                }

                var bindingConfig = new BindingInfo();

                //一个可观察对象支持绑定到多个视图
                if (Utility.TryGetClassBindingAttribute(classDeclaration, out var attributes))
                {
                    foreach (var attribute in attributes)
                    {
                        CreateContext(bindingConfig, semanticModel, classDeclaration, attribute);
                    }
                }
                else
                {
                    CreateContext(bindingConfig, semanticModel, classDeclaration, null);
                }

                var @namespace = string.Empty;
                if (Utility.TryGetNamespace(classDeclaration, out var namespaceName))
                {
                    @namespace = namespaceName;
                }
                bindingContextGenerator.Generate(bindingConfig, ref sources, usings, @namespace);
            }
            newRoot = root;
            return sources.ToArray();
        }

        /// <summary>
        /// 创建一个绑定上下文配置
        /// </summary>
        /// <param name="bindingConfig">配置</param>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="classDeclaration">类定义节点</param>
        /// <param name="attribute">特性节点</param>
        void CreateContext(BindingInfo bindingConfig, SemanticModel semanticModel, ClassDeclarationSyntax classDeclaration, AttributeSyntax attribute)
        {
            var bindingContext = new ContextBindingInfo();

            bindingContext.viewModelType = classDeclaration.Identifier.Text;

            if(attribute == null)
            {
                bindingConfig.viewName = string.Empty;
            }
            else
            {
                //从特性参数中获取视图名
                foreach (var args in attribute.ArgumentList.Arguments)
                {
                    var arg = args.Expression as LiteralExpressionSyntax;
                    if (arg == null)
                    {
                        continue;
                    }
                    bindingConfig.viewName = arg.Token.ValueText;
                }
            }

            //获取属性绑定
            foreach (var property in classDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>())
            {
                //一个属性支持绑定到多个元素
                if (!Utility.TryGetPropertyBindingAttribute(property, out var propertyAttributes))
                {
                    continue;
                }

                //为每个属性创建绑定
                foreach (var propertyAttribute in propertyAttributes)
                {
                   var propertyBindingInfo = CreatePropertyBindingInfo(semanticModel, classDeclaration, property, propertyAttribute);
                    if (propertyBindingInfo != null)
                    {
                        bindingContext.properties.Add(propertyBindingInfo);
                    }
                }
            }

            //获取命令绑定
            foreach(var member in classDeclaration.ChildNodes().OfType<MemberDeclarationSyntax>())
            {
                if(!Utility.TryGetCommandBindingAttribute(member, out var commandAttributes))
                {
                    continue;
                }

                foreach(var commandAttribute  in commandAttributes)
                {
                    var commandBindingInfo = CreateCommand(semanticModel, classDeclaration, member, commandAttribute);
                    if(commandBindingInfo != null)
                    {
                        bindingContext.commands.Add(commandBindingInfo);
                    }
                }
            }

            //如果没有绑定的属性或命令 则不生成绑定上下文
            if(bindingContext.properties.Count > 0 || bindingContext.commands.Count > 0)
            {
                bindingConfig.contexts.Add(bindingContext);
            }
        }

        /// <summary>
        /// 创建属性绑定配置文件
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="clazz">类定义节点</param>
        /// <param name="property">属性定义节点</param>
        /// <param name="propertyAttribute">属性特性节点</param>
        /// <returns></returns>
        PropertyBindingInfo CreatePropertyBindingInfo(SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax propertyAttribute)
        {
            var propertyInfo = CreatePropertyInfo(semanticModel, clazz, property, propertyAttribute);
            var converterInfo = CreateConverterInfo(semanticModel, clazz, property, propertyAttribute);
            var targetInfo = CreateTargetInfo(semanticModel, clazz, property, propertyAttribute);
            var bindingMode = CreateBindingModeInfo(semanticModel, clazz, property, propertyAttribute);

            if(propertyInfo == null || targetInfo == null)
            {
                return null;
            }

            return new PropertyBindingInfo
            {
                propertyInfo = propertyInfo,
                converterInfo = converterInfo,
                targetInfo = targetInfo,
                bindingMode = bindingMode,
            };
        }

        /// <summary>
        /// 创建属性信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="clazz">类定义节点</param>
        /// <param name="property">属性定义节点</param>
        /// <param name="attribute">属性特性节点</param>
        /// <returns></returns>
        PropertyInfo CreatePropertyInfo(SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax attribute)
        {
            var propertyName = property.Identifier.Text;
            var propertyType = property.Type.ToString();
            var isList = Utility.IsObservableList(clazz, property);
            var lineSpan = property.GetLocation().GetLineSpan();
            var location = new LocationInfo
            {
                line = lineSpan.Span.End.Line,
                path = lineSpan.Path,
            };

            return new PropertyInfo
            {
                name = propertyName,
                type = propertyType,
                isList = isList,
                location = location,
            };
        }

        /// <summary>
        /// 创建目标信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="clazz">类定义节点</param>
        /// <param name="property">属性定义节点</param>
        /// <param name="attribute">属性特性节点</param>
        /// <returns></returns>
        TargetInfo CreateTargetInfo(SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax attribute)
        {
            //解析nameof 说明是绑定到某个element的某个属性
            var targetArgs = attribute.ArgumentList.Arguments
                .FirstOrDefault((item) => item.Expression is InvocationExpressionSyntax invocation 
                && invocation.Expression.ToString() == "nameof");
            var targetInvocationArgs = targetArgs == null ? null : targetArgs.Expression as InvocationExpressionSyntax;

            //当参数是字符串字面量时 说明是元素路径
            var targetPathArgs = attribute.ArgumentList.Arguments
                .FirstOrDefault((item) => item.Expression is LiteralExpressionSyntax);
            var targetPathLiteralArgs = targetPathArgs == null ? null : targetPathArgs.Expression as LiteralExpressionSyntax;

            if(targetInvocationArgs == null || targetPathLiteralArgs == null)
            {
                return null;
            }

            string elementType = string.Empty;
            string targetPropertyName = string.Empty;
            string targetPropertyType = string.Empty;
            string targetPropertyValueType = string.Empty;

            var memberAccess = targetInvocationArgs.ArgumentList.Arguments[0].ChildNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
            if(memberAccess == null)
            {
                return null;
            }

            var sm = semanticModel.GetSymbolInfo(memberAccess.Expression);
            if(sm.Symbol is ITypeSymbol typeSymbol)
            {
                elementType = typeSymbol.ToString();
            }

            //如果这个属性是另一个类型的属性
            if(sm.Symbol is IPropertySymbol propertySymbol)
            {
                //targetPropertyName = propertySymbol.Name;
                //targetPropertyType = propertySymbol.Type.ToString();
            }
            
            var typeInfo = semanticModel.GetTypeInfo(memberAccess);
            targetPropertyName = memberAccess.Name.ToString();
            targetPropertyType = typeInfo.Type.ToString();
            foreach(var @interface in typeInfo.Type.AllInterfaces)
            {
                if(@interface.IsGenericType && @interface.ToString().StartsWith("FUI.Bindable.IBindableProperty"))
                {
                    targetPropertyValueType = @interface.TypeArguments[0].ToString();
                }
            }

            return new TargetInfo
            {
                type = elementType,
                path = targetPathLiteralArgs.Token.ValueText,
                propertyType = targetPropertyType,
                propertyName = targetPropertyName,
                propertyValueType = targetPropertyValueType,
            };
        }

        /// <summary>
        /// 创建值转换器信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="clazz">类定义节点</param>
        /// <param name="property">属性定义节点</param>
        /// <param name="attribute">属性特性节点</param>
        /// <returns></returns>
        ConverterInfo CreateConverterInfo(SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax attribute)
        {
            //当参数是类型时 说明转换器类型
            var args = attribute.ArgumentList.Arguments.FirstOrDefault((item) => item.Expression is TypeOfExpressionSyntax);

            if(args == null)
            {
                return null;
            }

            var typeArgs = args.Expression as TypeOfExpressionSyntax;
            var typeInfo = semanticModel.GetTypeInfo(typeArgs.Type);

            if (!typeInfo.Type.IsValueConverter(out var valueType, out var targetType))
            {
                Message.Message.WriteMessage(Message.MessageType.Log, new Message.LogMessage
                {
                    Level = Message.LogLevel.Error,
                    Message = $"{clazz.Identifier.Text}.{property.Identifier.Text}[{typeInfo.Type.Name}] is not {Utility.ValueConverterFullName}<,>"
                });
                return null;
            }

            return new ConverterInfo
            {
                type = typeInfo.Type.Name,
                sourceType = valueType.Name,
                targetType = targetType.Name
            };
        }

        /// <summary>
        /// 创建绑定模式信息
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="clazz">类定义节点</param>
        /// <param name="property">属性定义节点</param>
        /// <param name="attribute">属性特性节点</param>
        /// <returns></returns>
        BindingMode CreateBindingModeInfo(SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax attribute)
        {
            //当参数是成员访问表达式时 说明是绑定类型
            var args = attribute.ArgumentList.Arguments.FirstOrDefault((item) => item.Expression is MemberAccessExpressionSyntax);
            if(args == null)
            {
                return BindingMode.OneWay;
            }

            var memberAccess = args.Expression as MemberAccessExpressionSyntax;
            return Enum.Parse<BindingMode>(memberAccess.Name.ToString());
        }

        /// <summary>
        /// 创建命令绑定配置文件
        /// </summary>
        /// <param name="semanticModel">语义模型</param>
        /// <param name="clazz">类定义文件</param>
        /// <param name="member">方法定义节点或事件字段节点</param>
        /// <param name="commandAttribute">命令声明特性节点</param>
        CommandBindingInfo CreateCommand(SemanticModel semanticModel, ClassDeclarationSyntax clazz, MemberDeclarationSyntax member, AttributeSyntax commandAttribute)
        {
            var methodName = string.Empty;
            var argsType = new List<string>();
            
            //获取方法命令绑定
            if(member is MethodDeclarationSyntax methodDeclaration)
            {
                methodName = methodDeclaration.Identifier.Text;
                if(methodDeclaration.ParameterList != null)
                {
                    foreach (var parameter in methodDeclaration.ParameterList.Parameters)
                    {
                        var type = semanticModel.GetTypeInfo(parameter.Type);
                        argsType.Add(type.Type.ToString());
                    }
                }
            }

            //获取事件命令绑定
            if (member is EventFieldDeclarationSyntax eventFieldDeclaration)
            {
                methodName = Utility.GetEventMethodName(eventFieldDeclaration.Declaration.Variables.ToString());
                var type = eventFieldDeclaration.Declaration.Type;
                var typeInfo = semanticModel.GetTypeInfo(type);
                if(typeInfo.Type is INamedTypeSymbol namedTypeSymbol)
                {
                    foreach (var arg in namedTypeSymbol.TypeArguments)
                    {
                        argsType.Add(arg.ToString());
                    }
                }
            }

            if (string.IsNullOrEmpty(methodName))
            {
                return null;
            }
            
            var elementType = string.Empty;
            var elementPath = string.Empty;
            var targetPropertyName = string.Empty;

            for (int i = 0; i < commandAttribute.ArgumentList.Arguments.Count; i++)
            {
                var args = commandAttribute.ArgumentList.Arguments[i];

                //当参数是字符串字面量时 说明是元素路径
                if (args.Expression is LiteralExpressionSyntax arg)
                {
                    elementPath = arg.Token.ValueText;
                }

                //解析nameof  说明是绑定到某个element的某个属性
                if (args.Expression is InvocationExpressionSyntax invocationArgs && invocationArgs.Expression.ToString() == "nameof")
                {
                    var a = invocationArgs.ArgumentList.Arguments[0];
                    var expression = a.ToString();
                    var lastDotIndex = expression.IndexOf('.');
                    elementType = expression.Substring(0, lastDotIndex);
                    targetPropertyName = expression.Substring(lastDotIndex + 1);
                }
            }

            var lineSpan = member.GetLocation().GetLineSpan();
            var location = new LocationInfo
            {
                line = lineSpan.Span.End.Line,
                path = lineSpan.Path,
            };

            return new CommandBindingInfo
            {
                commandInfo = new CommandInfo
                {
                    isEvent = member is EventFieldDeclarationSyntax,
                    name = methodName,
                    parameters = argsType,
                    location = location,
                },

                targetInfo = new CommandTargetInfo
                {
                    path = elementPath,
                    type = elementType,
                    propertyName = targetPropertyName,
                    parameters = argsType,
                }
            };
        }
    }
}
