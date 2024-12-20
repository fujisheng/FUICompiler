using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Text;

namespace FUICompiler
{
    /// <summary>
    /// 可绑定属性委托生成器
    /// </summary>
    internal class ObservableObjectDelegateGenerator
    {
        internal List<Source> Generate(SemanticModel semanticModel, SyntaxNode root)
        {
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToArray().Select(item =>
            {
                return item?.Name?.ToString();
            });

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            var sources = new List<Source>();
            foreach(var classDeclaration in classDeclarations)
            {
                var classType = semanticModel.GetDeclaredSymbol(classDeclaration);

                //如果不是可观察对象直接不管
                if (!classType.IsObservableObject())
                {
                    continue;
                }

                //如果是静态类或者抽象类直接不管
                if (classType.IsAbstract || classType.IsStatic)
                {
                    continue;
                }

                var appendBuilder = new StringBuilder();

                //文件头
                appendBuilder.AppendLine(Utility.FileHead);

                //添加using
                if (usings != null)
                {
                    foreach (var @using in usings)
                    {
                        appendBuilder.AppendLine($"using {@using};");
                    }
                }

                //判断是否有命名空间
                var @namespace = classType.ContainingNamespace.ToDisplayString();
                var hasNamespace = !string.IsNullOrEmpty(@namespace);
                if (hasNamespace)
                {
                    appendBuilder.AppendLine($"namespace {@namespace}");
                    appendBuilder.AppendLine("{");
                }

                //生成分布类
                appendBuilder.AppendLine($"public partial class {classType.Name} : {typeof(FUI.ISynchronizeProperties).FullName}");
                appendBuilder.AppendLine("{");

                var syncPropertiesBuilder = new StringBuilder();

                //遍历所有属性 生成对应委托
                foreach (var property in classDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>())
                {
                    var propertyTypeInfo = semanticModel.GetDeclaredSymbol(property);

                    var propertyType = propertyTypeInfo.Type.ToString();
                    var propertyName = propertyTypeInfo.Name;

                    //生成BackingField
                    var fieldName = Utility.GetPropertyBackingFieldName(propertyName);
                    appendBuilder.AppendLine($"public {propertyType} {fieldName} {property.Initializer};");

                    //生成对应的委托
                    string delegateType = $"FUI.Bindable.PropertyChangedHandler<{propertyType}>";
                    var delegateName = Utility.GetPropertyChangedDelegateName(propertyName);

                    appendBuilder.AppendLine($"public {delegateType} {delegateName};");

                    //生成对应的委托调用
                    syncPropertiesBuilder.AppendLine($"{delegateName}?.Invoke(this, this.{propertyName}, this.{propertyName});");
                }

                //为事件生成调用方法
                var events = classDeclaration.DescendantNodes().OfType<EventFieldDeclarationSyntax>().ToArray();
                foreach (var @event in events)
                {
                    var eventCaller = GenerateEventCaller(semanticModel, @event);
                    appendBuilder.AppendLine(eventCaller);
                }

                //生成同步所有属性的方法
                GenerateSyncPropertites(appendBuilder, syncPropertiesBuilder);

                appendBuilder.AppendLine("}");
                if (hasNamespace)
                {
                    appendBuilder.AppendLine("}");
                }
                var code = Utility.NormalizeCode(appendBuilder.ToString());
                sources.Add(new Source($"{classType.ToString()}.PropertyChanged.g", code));
            }

            return sources;
        }

        /// <summary>
        /// 生成同步所有属性的方法
        /// </summary>
        /// <param name="propertyDelegateBuilder"></param>
        /// <param name="syncPropertiesBuilder"></param>
        void GenerateSyncPropertites(StringBuilder propertyDelegateBuilder, StringBuilder syncPropertiesBuilder)
        {
            //生成同步所有属性的方法
            propertyDelegateBuilder.AppendLine($"void {typeof(FUI.ISynchronizeProperties).FullName}.{nameof(FUI.ISynchronizeProperties.Synchronize)}()");
            propertyDelegateBuilder.AppendLine("{");
            propertyDelegateBuilder.AppendLine(syncPropertiesBuilder.ToString());
            propertyDelegateBuilder.AppendLine("}");
        }

        /// <summary>
        /// 生成事件触发方法
        /// </summary>
        /// <param name="syntax">事件字段</param>
        /// <returns></returns>
        string GenerateEventCaller(SemanticModel semanticModel, EventFieldDeclarationSyntax syntax)
        {
            var eventTypeInfo = semanticModel.GetTypeInfo(syntax.Declaration.Type);
            var namedEventType = eventTypeInfo.Type as INamedTypeSymbol;
            var argsTypesString = string.Empty;
            var invokeParams = string.Empty;
            if (namedEventType.IsGenericType)
            {
                var argsTypes = namedEventType.TypeArguments;
                for(int i = 0; i< argsTypes.Length; i++)
                {
                    var end = i == argsTypes.Length - 1 ? string.Empty : ",";
                    argsTypesString += $"{argsTypes[i]} arg{i}{end}";
                    invokeParams += $"arg{i}{end}";
                }
            }

            var callerName = Utility.GetEventMethodName(syntax.Declaration.Variables.ToString());
            return $$"""
public void {{callerName}} ({{argsTypesString}})
{
    this.{{syntax.Declaration.Variables}}?.Invoke({{invokeParams}});
}
""";
        }
    }
}
