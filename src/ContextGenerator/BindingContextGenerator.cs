﻿using System.Text;
using System.Xml.Linq;

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
                var bindingBuilder = new StringBuilder();
                var unbindingBuilder = new StringBuilder();
                HashSet<string> converterTypes = new HashSet<string>();

                var functionBuilder = new StringBuilder();

                //为每个属性生成对应的委托 并添加绑定代码和解绑代码
                foreach (var property in bindingContext.properties)
                {
                    BuildNormalPropertyBinding(bindingContext, property, ref functionBuilder, ref bindingBuilder, ref unbindingBuilder);

                    //如果是列表 需要单独构建ListUnbinding代码
                    if (property.propertyInfo.isList)
                    {
                        var listUnbindingFunctionName = $"UnbindingList_{GetPropertyTargetUniqueName(bindingContext, property)}";
                        functionBuilder.AppendLine(BuildListUnbindingFunctionCode(listUnbindingFunctionName, property));
                        unbindingBuilder.AppendLine(BuildListUnbindingCode(listUnbindingFunctionName));
                    }

                    //如果是双向绑定需要构建从View到ViewModel的绑定
                    if (property.bindingMode.HasFlag(BindingMode.OneWayToSource))
                    {
                        BuildV2VMBinding(bindingContext, property, ref bindingBuilder, ref unbindingBuilder, ref functionBuilder);
                        BuildV2VMInit(bindingContext, property, ref bindingBuilder, ref functionBuilder);
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
                    BuildCommandBinding(bindingContext, command, ref bindingBuilder, ref unbindingBuilder, ref functionBuilder);
                }

                //生成所有的转换器构造代码
                var convertersBuilder = new StringBuilder();
                BuildConverterCostructor(converterTypes, ref convertersBuilder);

                //组装所有的绑定代码
                var code = BuildContextCode(bindingContext, convertersBuilder.ToString(), bindingBuilder.ToString(), unbindingBuilder.ToString(), functionBuilder.ToString());

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
            var propertyTargetUniqueName = GetPropertyTargetUniqueName(bindingContext, property);
            var propertyChangedFunctionName = $"PropertyChanged__{propertyTargetUniqueName}";

            //构建属性绑定代码
            functionBuilder.AppendLine(BuildPropertyChangedFunctionCode(propertyChangedFunctionName, convert, listBinding, property));

            //生成属性绑定代码
            bindingItemsBuilder.AppendLine($"this.ViewModel.{delegateName} += {propertyChangedFunctionName};");

            //生成属性解绑代码
            unbindingItemsBuilder.AppendLine($"this.ViewModel.{delegateName} -= {propertyChangedFunctionName};");
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

        /// <summary>
        /// 生成从View到ViewModel的绑定代码和解绑代码
        /// </summary>
        /// <param name="property">绑定配置</param>
        /// <param name="bindingItemBuilder">绑定代码构建器</param>
        /// <param name="unbindingItemBuilder">解绑代码构建器</param>
        /// <param name="functionBuilder">方法构建器</param>
        void BuildV2VMBinding(ContextBindingInfo contextInfo, PropertyBindingInfo property,
            ref StringBuilder bindingItemBuilder,
            ref StringBuilder unbindingItemBuilder,
            ref StringBuilder functionBuilder)
        {
            var propertyTargetUniqueName = GetPropertyTargetUniqueName(contextInfo, property);
            var bindingFunctionName = $"BindingV2VM__{propertyTargetUniqueName}";
            var unbindingFunctionName = $"UnbindingV2VM__{propertyTargetUniqueName}";
            bindingItemBuilder.AppendLine($"{bindingFunctionName}();");
            unbindingItemBuilder.AppendLine($"{unbindingFunctionName}();");

            var invocationName = $"V2VMInvocation__{propertyTargetUniqueName}";

            var convert = BuildConvertBack(contextInfo.viewModelType, property, "@newValue");
            var invocation = BuildV2VMInvocationFunctionCode(contextInfo, property, invocationName, convert);

            var bindingOperate = BuildV2VMBindingOperateCode(invocationName, property);
            var unbindingOperate = BuildV2VMUnbindingOperateCode(invocationName, property);

            functionBuilder.AppendLine(BuildV2VMBindingFunctionCode(contextInfo, property, invocation, bindingFunctionName, bindingOperate));
            functionBuilder.AppendLine(BuildV2VMBindingFunctionCode(contextInfo, property, string.Empty, unbindingFunctionName, unbindingOperate));
        }

        /// <summary>
        /// 构建值转换器
        /// </summary>
        string BuildConvertBack(string viewModelType, PropertyBindingInfo property, string value)
        {
            var convertBuilder = new StringBuilder();

            if (property.converterInfo != null)
            {
                convertBuilder.AppendLine($"var convertedValue = {property.converterInfo.type.ToCSharpName()}.ConvertBack({value});");
            }
            else
            {
                convertBuilder.AppendLine($"var convertedValue = {value};");
            }
            return convertBuilder.ToString();
        }

        /// <summary>
        /// 构建从View到ViewModel的值初始化代码
        /// </summary>
        /// <param name="contextInfo">上下文信息</param>
        /// <param name="property">属性绑定信息</param>
        /// <param name="bindingItemBuilder">绑定方法builder</param>
        /// <param name="functionBuilder">方法builder</param>
        void BuildV2VMInit(ContextBindingInfo contextInfo, PropertyBindingInfo property,
            ref StringBuilder bindingItemBuilder,
            ref StringBuilder functionBuilder)
        {
            var convertValue = $"element.{property.targetInfo.propertyName}.Value";
            var convert = BuildConvertBack(contextInfo.viewModelType, property, convertValue);
            var propertyTargetUniqueName = GetPropertyTargetUniqueName(contextInfo, property);
            var initFunctionName = $"InitV2VM__{propertyTargetUniqueName}";
            functionBuilder.AppendLine(BuildV2VMInitFunctionCode(contextInfo, property, initFunctionName, convert));
            bindingItemBuilder.AppendLine($"{initFunctionName}();");
        }

        /// <summary>
        /// 构建命令绑定和解绑代码
        /// </summary>
        /// <param name="command">绑定配置</param>
        /// <param name="bindingItemBuilder">绑定代码构建器</param>
        /// <param name="unbindingItemBuilder">解绑代码构建器</param>
        /// <param name="functionBuilder">方法构建器</param>
        void BuildCommandBinding(ContextBindingInfo contextInfo, CommandBindingInfo command,
            ref StringBuilder bindingItemBuilder,
            ref StringBuilder unbindingItemBuilder,
            ref StringBuilder functionBuilder)
        {
            var commandTargetUniqueName = GetCommandTargetUniqueName(contextInfo, command);
            var bindingFunctionName = $"BindingCommand__{commandTargetUniqueName}";
            var unbindingFunctionName = $"UnbindingCommand__{commandTargetUniqueName}";

            bindingItemBuilder.AppendLine($"{bindingFunctionName}();");
            unbindingItemBuilder.AppendLine($"{unbindingFunctionName}();");

            var bindingOperate = BuildCommandBindingOperateCode(contextInfo, command);
            var unbindingOperate = BuildCommandUnbindingOperateCode(contextInfo, command);

            functionBuilder.AppendLine(BuildCommandBindingFunctionCode(contextInfo, command, bindingFunctionName, bindingOperate));
            functionBuilder.AppendLine(BuildCommandBindingFunctionCode(contextInfo, command, unbindingFunctionName, unbindingOperate));
        }
    }
}
