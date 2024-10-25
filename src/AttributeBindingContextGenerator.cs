﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FUICompiler
{
    /// <summary>
    /// 通过特性绑定来生成绑定上下文
    /// </summary>
    internal class AttributeBindingContextGenerator : ITypeSyntaxNodeSourcesGenerator
    {
        BindingContextGenerator bindingContextGenerator = new BindingContextGenerator(null, null);

        public Source?[] Generate(SyntaxNode root, out SyntaxNode newRoot)
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
                        CreateContext(bindingConfig, classDeclaration, attribute);
                    }
                }
                else
                {
                    CreateContext(bindingConfig, classDeclaration, null);
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
        void CreateContext(BindingConfig bindingConfig, ClassDeclarationSyntax classDeclaration, AttributeSyntax attribute)
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
                    CreateProperty(classDeclaration, property, propertyAttribute, bindingContext);
                }
            }

            //如果没有通过特性绑定属性 则不生成绑定上下文
            if(bindingContext.properties.Count > 0)
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
        void CreateProperty(ClassDeclarationSyntax clazz, PropertyDeclarationSyntax property, AttributeSyntax propertyAttribute, BindingContext bindingContext)
        {
            var elementType = string.Empty;
            var elementPath = string.Empty;
            var converterType = string.Empty;
            var targetPropertyName = string.Empty;
            var bindingType = BindingType.OneWay;

            for (int i = 0; i < propertyAttribute.ArgumentList.Arguments.Count; i++)
            {
                var args = propertyAttribute.ArgumentList.Arguments[i];

                //当参数是字符串字面量时 说明是元素路径
                if (args.Expression is LiteralExpressionSyntax arg)
                {
                    elementPath = arg.Token.ValueText;
                }

                //解析nameof  说明是绑定到某个element的某个属性
                if(args.Expression is InvocationExpressionSyntax invocationArgs && invocationArgs.Expression.ToString() == "nameof")
                {
                    var a = invocationArgs.ArgumentList.Arguments[0];
                    var expression = a.ToString();
                    var lastDotIndex = expression.IndexOf('.');
                    elementType = expression.Substring(0, lastDotIndex);
                    targetPropertyName = expression.Substring(lastDotIndex + 1);
                }

                //当参数是类型时 说明是元素类型或转换器类型
                if (args.Expression is TypeOfExpressionSyntax typeArg)
                {
                    //当有可选参数 且参数名为elementType时 说明是元素类型
                    if (args.NameColon != null && args.NameColon.Name.ToString() == "elementType")
                    {
                        elementType = typeArg.Type.ToString();
                        continue;
                    }

                    //当有可选参数 且参数名为converterType时 说明是转换器类型
                    if (args.NameColon != null && args.NameColon.Name.ToString() == "converterType")
                    {
                        converterType = typeArg.Type.ToString();
                        continue;
                    }

                    //当有多个参数且都不是可选参数时 按照顺序分别为元素类型和转换器类型
                    if (i == 1)
                    {
                        converterType = typeArg.Type.ToString();
                    }

                    if (i == 2)
                    {
                        elementType = typeArg.Type.ToString();
                    }
                }

                //当参数是成员访问表达式时 说明是绑定类型
                if(args.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    bindingType = Enum.Parse<BindingType>(memberAccess.Name.ToString());
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
                bindingType = bindingType, 
                elementPropertyName = targetPropertyName,
            });

            //Console.WriteLine($"property:{propertyName}  type:{propertyType} elementName:{elementPath}  elementType:{elementType} converterType:{converterType}");
        }
    }
}
