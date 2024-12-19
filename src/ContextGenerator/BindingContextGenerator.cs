using System.Text;

namespace FUICompiler
{
    /// <summary>
    /// 绑定上下文生成器
    /// </summary>
    public partial class BindingContextGenerator
    {
        internal IReadOnlyList<Source> Generate(IReadOnlyList<ContextBindingInfo> contexts)
        {
            var result = new List<Source>();
            foreach (var bindingContext in contexts)
            {
                var usings = new List<string>(0);
                var @namespace = string.Empty;

                var bindingBuilder = new StringBuilder();
                var unbindingBuilder = new StringBuilder();
                HashSet<string> converterTypes = new HashSet<string>();

                var functionBuilder = new StringBuilder();

                //为每个上下文生成对应的绑定代码
                var bindingItemsBuilder = new StringBuilder();
                var unbindingItemsBuilder = new StringBuilder();

                //为每个属性生成对应的委托 并添加绑定代码和解绑代码
                foreach (var property in bindingContext.properties)
                {
                    BuildNormalPropertyBinding(bindingContext, property, ref functionBuilder, ref bindingItemsBuilder, ref unbindingItemsBuilder);

                    //如果是列表 需要单独构建ListUnbinding代码
                    if (property.propertyInfo.isList)
                    {
                        unbindingItemsBuilder.AppendLine(BuildListUnbindingCode(bindingContext, property));
                    }

                    //如果是双向绑定需要构建从View到ViewModel的绑定
                    if (property.bindingMode.HasFlag(BindingMode.OneWayToSource))
                    {
                        BuildV2VMBinding(bindingContext, property, ref bindingItemsBuilder, ref unbindingItemsBuilder, ref functionBuilder);
                    }

                    //如果有转换器则需要记录以便生成转换器构造代码
                    if (property.converterInfo != null)
                    {
                        converterTypes.Add(property.converterInfo.type);
                    }
                }

                //为命令生成绑定和解绑代码
                foreach (var command in bindingContext.commands)
                {
                    BuildCommandBinding(bindingContext, command, ref bindingItemsBuilder, ref unbindingItemsBuilder, ref functionBuilder);
                }

                //组装绑定代码
                bindingBuilder.AppendLine(BuildBindingsCode(bindingContext, bindingItemsBuilder.ToString()));

                //组装解绑代码
                unbindingBuilder.AppendLine(BuildBindingsCode(bindingContext, unbindingItemsBuilder.ToString()));

                //生成所有的转换器构造代码
                var convertersBuilder = new StringBuilder();
                BuildConverterCostructor(converterTypes, ref convertersBuilder);

                //添加using
                var usingBuilder = new StringBuilder();
                BuildUsings(usings, ref usingBuilder);

                //添加Namespace
                var @namespaceName = string.IsNullOrEmpty(@namespace) ? DefaultNamespace : @namespace;

                //组装所有的绑定代码
                var code = BuildContextCode(bindingContext, usingBuilder.ToString(), @namespaceName, convertersBuilder.ToString(), bindingBuilder.ToString(), unbindingBuilder.ToString(), functionBuilder.ToString());

                //格式化代码
                code = Utility.NormalizeCode(code);

                result.Add(new Source($"{bindingContext.viewModelType.ToCSharpName()}_{bindingContext.viewName}.DataBinding.g", code));
            }

            return result;
        }

        //构建普通属性绑定
        void BuildNormalPropertyBinding(ContextBindingInfo bindingContext, PropertyBindingInfo property,
            ref StringBuilder functionBuilder,
            ref StringBuilder bindingItemsBuilder,
            ref StringBuilder unbindingItemsBuilder)
        {
            var delegateName = Utility.GetPropertyChangedDelegateName(property.propertyInfo.name);

            //构建值转换
            var convert = BuildConvert(bindingContext.viewModelType, property);

            //如果这个属性是List则生成List绑定代码
            var listBinding = string.Empty;
            if (property.propertyInfo.isList)
            {
                listBinding = BuildListBindingCode(property);
            }

            //为属性生成对应的绑定方法
            var vmName = bindingContext.viewModelType.ToCSharpName();
            var propertyTargetUniqueName = GetPropertyTargetUniqueName(bindingContext, property);
            var propertyChangedFunctionName = $"PropertyChanged__{propertyTargetUniqueName}";

            //构建属性绑定代码
            functionBuilder.AppendLine(BuildPropertyChangedFunctionCode(propertyChangedFunctionName, convert, listBinding, property));

            //生成属性绑定代码
            bindingItemsBuilder.AppendLine($"{vmName}.{delegateName} += {propertyChangedFunctionName};");

            //生成属性解绑代码
            unbindingItemsBuilder.AppendLine($"{vmName}.{delegateName} -= {propertyChangedFunctionName};");
        }

        /// <summary>
        /// 构建值转换器
        /// </summary>
        string BuildConvert(string viewModelType, PropertyBindingInfo property)
        {
            var convertBuilder = new StringBuilder();

            if (property.converterInfo != null)
            {
                convertBuilder.AppendLine($"var convertedValue = {property.converterInfo.type.ToCSharpName()}.Convert(@value);");
            }
            else
            {
                convertBuilder.AppendLine($"var convertedValue = @value;");
            }
            return convertBuilder.ToString();
        }

        //构造所有的转换器构造代码
        void BuildConverterCostructor(HashSet<string> converterTypes, ref StringBuilder convertersBuilder)
        {
            foreach (var converterType in converterTypes)
            {
                if (string.IsNullOrEmpty(converterType))
                {
                    continue;
                }
                convertersBuilder.AppendLine($"{converterType} {converterType.ToCSharpName()} = new {converterType}();");
            }
        }

        //构造所有的using
        void BuildUsings(IEnumerable<string> usings, ref StringBuilder usingBuilder)
        {
            if (usings == null)
            {
                usingBuilder.AppendLine("");
            }
            else
            {
                foreach (var @using in usings)
                {
                    if (string.IsNullOrEmpty(@using))
                    {
                        continue;
                    }

                    usingBuilder.AppendLine($"using {@using};");
                }
            }
        }

        /// <summary>
        /// 生成从View到ViewModel的绑定代码和解绑代码
        /// </summary>
        /// <param name="vmName">ViewModel名字</param>
        /// <param name="property">绑定配置</param>
        /// <param name="bindingItemBuilder">绑定代码构建器</param>
        /// <param name="unbindingItemBuilder">解绑代码构建器</param>
        /// <param name="functionBuilder">方法构建器</param>
        void BuildV2VMBinding(ContextBindingInfo contextInfo, PropertyBindingInfo property,
            ref StringBuilder bindingItemBuilder,
            ref StringBuilder unbindingItemBuilder,
            ref StringBuilder functionBuilder)
        {
            var vmName = contextInfo.viewModelType.ToCSharpName();
            var propertyTargetUniqueName = GetPropertyTargetUniqueName(contextInfo, property);
            var bindingFunctionName = $"BindingV2VM__{propertyTargetUniqueName}";
            var unbindingFunctionName = $"UnbindingV2VM__{propertyTargetUniqueName}";
            bindingItemBuilder.AppendLine($"{bindingFunctionName}({vmName});");
            unbindingItemBuilder.AppendLine($"{unbindingFunctionName}({vmName});");

            var invocationName = $"V2VMInvocation__{propertyTargetUniqueName}";

            var invocation = BuildV2VMInvocationFunctionCode(contextInfo, property, invocationName);

            var bindingOperate = BuildV2VMBindingOperateCode(invocationName, property);
            var unbindingOperate = BuildV2VMUnbindingOperateCode(invocationName, property);

            functionBuilder.AppendLine(BuildV2VMBindingFunctionCode(contextInfo, property, invocation, bindingFunctionName, bindingOperate));
            functionBuilder.AppendLine(BuildV2VMBindingFunctionCode(contextInfo, property, string.Empty, unbindingFunctionName, unbindingOperate));
        }

        /// <summary>
        /// 构建命令绑定和解绑代码
        /// </summary>
        /// <param name="vmName">ViewModel名字</param>
        /// <param name="command">绑定配置</param>
        /// <param name="bindingItemBuilder">绑定代码构建器</param>
        /// <param name="unbindingItemBuilder">解绑代码构建器</param>
        /// <param name="functionBuilder">方法构建器</param>
        void BuildCommandBinding(ContextBindingInfo contextInfo, CommandBindingInfo command,
            ref StringBuilder bindingItemBuilder,
            ref StringBuilder unbindingItemBuilder,
            ref StringBuilder functionBuilder)
        {
            var vmName = contextInfo.viewModelType.ToCSharpName();
            var commandTargetUniqueName = GetCommandTargetUniqueName(contextInfo, command);
            var bindingFunctionName = $"BindingCommand__{commandTargetUniqueName}";
            var unbindingFunctionName = $"UnbindingCommand__{commandTargetUniqueName}";

            bindingItemBuilder.AppendLine($"{bindingFunctionName}({vmName});");
            unbindingItemBuilder.AppendLine($"{unbindingFunctionName}({vmName});");

            var bindingOperate = BuildCommandBindingOperateCode(contextInfo, command);
            var unbindingOperate = BuildCommandUnbindingOperateCode(contextInfo, command);

            functionBuilder.AppendLine(BuildCommandBindingFunctionCode(contextInfo, command, bindingFunctionName, bindingOperate));
            functionBuilder.AppendLine(BuildCommandBindingFunctionCode(contextInfo, command, unbindingFunctionName, unbindingOperate));
        }
    }
}
