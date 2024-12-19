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
                var type = semanticModel.GetDeclaredSymbol(classDeclaration);

                //如果不是可观察对象直接不管
                if (!type.IsObservableObject())
                {
                    continue;
                }

                //如果是静态类或者抽象类直接不管
                if (type.IsAbstract || type.IsStatic)
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
                var @namespace = type.ContainingNamespace.ToDisplayString();
                var hasNamespace = !string.IsNullOrEmpty(@namespace);
                if (hasNamespace)
                {
                    appendBuilder.AppendLine($"namespace {@namespace}");
                    appendBuilder.AppendLine("{");
                }

                //生成分布类
                appendBuilder.AppendLine($"public partial class {type.Name} : {Utility.SynchronizePropertiesFullName}");
                appendBuilder.AppendLine("{");

                var syncPropertiesBuilder = new StringBuilder();

                //遍历所有属性 生成对应委托
                foreach (var property in classDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>())
                {
                    //if (!Utility.IsObservableProperty(classDeclaration, property))
                    //{
                    //    continue;
                    //}

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
                }

                //为事件生成调用方法
                var events = classDeclaration.DescendantNodes().OfType<EventFieldDeclarationSyntax>().ToArray();
                foreach (var @event in events)
                {
                    var eventCaller = GenerateEventCaller(@event);
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
                sources.Add(new Source($"{classDeclaration.Identifier.Text}.PropertyChanged.g", code));
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
            propertyDelegateBuilder.AppendLine($"void {Utility.SynchronizePropertiesFullName}.{Utility.SynchronizePropertiesMethodName}()");
            propertyDelegateBuilder.AppendLine("{");
            propertyDelegateBuilder.AppendLine(syncPropertiesBuilder.ToString());
            propertyDelegateBuilder.AppendLine("}");
        }

        /// <summary>
        /// 生成事件触发方法
        /// </summary>
        /// <param name="syntax">事件字段</param>
        /// <returns></returns>
        string GenerateEventCaller(EventFieldDeclarationSyntax syntax)
        {
            var argsType = (syntax.Declaration.Type as GenericNameSyntax).TypeArgumentList.Arguments;
            var callerName = Utility.GetEventMethodName(syntax.Declaration.Variables.ToString());
            return $$"""
public void {{callerName}} ({{argsType}} args)
{
    this.{{syntax.Declaration.Variables}}?.Invoke(args);
}
""";
        }
    }
}
