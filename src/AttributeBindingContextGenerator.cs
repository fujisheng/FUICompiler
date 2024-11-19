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

                var bindingConfig = new BindingConfig();

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
        /// 创建一个绑定上下文文配置
        /// </summary>
        /// <param name="bindingConfig">绑定配置</param>
        /// <param name="classDeclaration">类定义</param>
        /// <param name="attribute">绑定特性</param>
        void CreateContext(BindingConfig bindingConfig, SemanticModel semanticModel, ClassDeclarationSyntax classDeclaration, AttributeSyntax attribute)
        {
            var bindingContext = new BindingContext();

            bindingContext.type = classDeclaration.Identifier.Text;

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
                    CreateProperty(semanticModel, classDeclaration, property, propertyAttribute, bindingContext);
                }
            }

            //获取方法命令绑定
            foreach(var method in classDeclaration.ChildNodes().OfType<MethodDeclarationSyntax>())
            {
                if(!Utility.TryGetCommandBindingAttribute(method, out var commandAttributes))
                {
                    continue;
                }

                foreach(var commandAttribute in commandAttributes)
                {
                    CreateCommand(classDeclaration, method.Identifier.Text, commandAttribute, bindingContext);
                }
            }

            //获取事件命令绑定
            foreach (var @event in classDeclaration.ChildNodes().OfType<EventFieldDeclarationSyntax>())
            {
                if (!Utility.TryGetCommandBindingAttribute(@event, out var commandAttributes))
                {
                    continue;
                }

                foreach (var commandAttribute in commandAttributes)
                {
                    var eventMethodName = Utility.GetEventMethodName(@event.Declaration.Variables.ToString());
                    CreateCommand(classDeclaration, eventMethodName, commandAttribute, bindingContext);
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
        /// <param name="property">属性定义文件</param>
        /// <param name="propertyAttribute">属性特性</param>
        /// <param name="bindingContext">当前绑定上下文配置</param>
        void CreateProperty(SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax propertyAttribute, BindingContext bindingContext)
        {
            var elementType = string.Empty;
            var elementPath = string.Empty;
            var converterType = string.Empty;
            var converterValueType = string.Empty;
            var converterTargetType = string.Empty;
            var targetPropertyName = string.Empty;
            var targetPropertyType = string.Empty;
            var targetPropertyValueType = string.Empty;
            var bindingMode = BindingMode.OneWay;

            for (int i = 0; i < propertyAttribute.ArgumentList.Arguments.Count; i++)
            {
                var args = propertyAttribute.ArgumentList.Arguments[i];

                //当参数是字符串字面量时 说明是元素路径
                if (args.Expression is LiteralExpressionSyntax arg)
                {
                    elementPath = arg.Token.ValueText;
                }


                (elementType, targetPropertyName, targetPropertyType, targetPropertyValueType) = CreateTarget(args, i, semanticModel, clazz, property);
                (converterType, converterValueType, converterTargetType) = CreateConverter(args, i, semanticModel, clazz, property);

                //当参数是成员访问表达式时 说明是绑定类型
                if (args.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    bindingMode = Enum.Parse<BindingMode>(memberAccess.Name.ToString());
                }
            }

            var propertyName = property.Identifier.Text;
            var propertyType = property.Type.ToString();
            var isList = Utility.IsObservableList(clazz, property);

            bindingContext.properties.Add(new BindingProperty
            {
                name = propertyName,
                type = new TypeInfo { fullName = propertyType, name = propertyType, isList = isList},
                elementType = new TypeInfo { fullName = elementType, name = elementType},
                converterType = new TypeInfo { fullName = converterType , name = converterType},
                elementPath = elementPath,
                bindingMode = bindingMode, 
                elementPropertyName = targetPropertyName,
            });

            //Console.WriteLine($"property:{propertyName}  type:{propertyType} elementName:{elementPath}  elementType:{elementType} converterType:{converterType}");
        }

        (string elementType, string targetPropertyName, string targetPropertyType, string targetPropertyValueType) CreateTarget(AttributeArgumentSyntax args, int argsIndex, SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property)
        {
            //解析nameof  说明是绑定到某个element的某个属性
            if (args.Expression is not InvocationExpressionSyntax invocationArgs || invocationArgs.Expression.ToString() != "nameof")
            {
                return default;
            }

            var a = invocationArgs.ArgumentList.Arguments[0];
            var acc = a.ChildNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
            if(acc == null)
            {
                return default;
            }

            string elementType = string.Empty;
            string targetPropertyName = string.Empty;
            string targetPropertyType = string.Empty;
            string targetPropertyValueType = string.Empty;

            var sm = semanticModel.GetSymbolInfo(acc.Expression);
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
            
            var typeInfo = semanticModel.GetTypeInfo(acc);
            targetPropertyName = acc.Name.ToString();
            targetPropertyType = typeInfo.Type.ToString();
            foreach(var @interface in typeInfo.Type.AllInterfaces)
            {
                if(@interface.IsGenericType && @interface.ToString().StartsWith("FUI.Bindable.IBindableProperty"))
                {
                    targetPropertyValueType = @interface.TypeArguments[0].ToString();
                }
            }

            Console.WriteLine((elementType, targetPropertyName, targetPropertyType, targetPropertyValueType));

            return (elementType, targetPropertyName, targetPropertyType, targetPropertyValueType);
        }

        (string type, string valueType, string targetType) CreateConverter(AttributeArgumentSyntax args, int argsIndex, SemanticModel semanticModel, ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property)
        {
            //当参数是类型时 说明转换器类型
            if (args.Expression is not TypeOfExpressionSyntax typeArg)
            {
                return default;
            }

            //当有可选参数 且参数名为converterType时 说明是转换器类型
            if (args.NameColon != null && args.NameColon.Name.ToString() == "converterMode")
            {
                var typeInfo = semanticModel.GetTypeInfo(typeArg.Type);
                if (!typeInfo.Type.IsValueConverter(out var valueType, out var targetType))
                {
                    PrintNotValueConverter(clazz.Identifier.Text, property.Identifier.Text, typeInfo.Type.Name);
                    return default;
                }

                return (typeInfo.Type.Name, valueType.Name, targetType.Name);
            }

            //当有多个参数且都不是可选参数时 按照顺序分别为元素类型和转换器类型
            if (argsIndex == 2)
            {
                var typeInfo = semanticModel.GetTypeInfo(typeArg.Type);
                if (!typeInfo.Type.IsValueConverter(out var valueType, out var targetType))
                {
                    PrintNotValueConverter(clazz.Identifier.Text, property.Identifier.Text, typeInfo.Type.Name);
                    return default;
                }

                return (typeInfo.Type.Name, valueType.Name, targetType.Name);
            }

            return default;
        }

        void PrintNotValueConverter(string clazz, string property, string converter)
        {
            Message.Message.WriteMessage(Message.MessageType.Log, new Message.LogMessage
            {
                Level = Message.LogLevel.Error,
                Message = $"{clazz}.{property}[{converter}] is not {Utility.ValueConverterFullName}<,>"
            });
        }

        /// <summary>
        /// 创建命令绑定配置文件
        /// </summary>
        /// <param name="property">属性定义文件</param>
        /// <param name="commandAttribute">属性特性</param>
        /// <param name="bindingContext">当前绑定上下文配置</param>
        void CreateCommand(ClassDeclarationSyntax clazz, string methodName, AttributeSyntax commandAttribute, BindingContext bindingContext)
        {
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

            bindingContext.commands.Add(new BindingCommand
            {
                name = methodName,
                elementType = new TypeInfo { fullName = elementType, name = elementType },
                elementPath = elementPath,
                elementPropertyName = targetPropertyName,
            });
        }
    }
}
