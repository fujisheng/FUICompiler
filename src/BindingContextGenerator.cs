using Newtonsoft.Json;

using System.Text;

namespace FUICompiler
{
    public partial class BindingContextGenerator : IBeforeCompilerSourcesGenerator
    {
        string configPath;
        string configExtension;

        public BindingContextGenerator(string configPath, string configExtension)
        {
            this.configPath = configPath;
            this.configExtension = configExtension;
        }

        Source?[] IBeforeCompilerSourcesGenerator.Generate()
        {
            var result = new List<Source?>();
            foreach (var file in Directory.GetFiles(configPath, $"*{configExtension}"))
            {
                var config = JsonConvert.DeserializeObject<BindingConfig>(File.ReadAllText(file));

                Generate(config, ref result);
            }

            return result.ToArray();
        }

        //将类型名转换成合法的C#命名
        static string GetFormattedType(string type)
        {
            return type.Replace(".", "_");
        }

        internal void Generate(BindingConfig config, ref List<Source?> result, IEnumerable<string> usings = null, string @namespace = null)
        {
            foreach (var bindingContext in config.contexts)
            {
                var code = Template.Replace(ViewNameMark, config.viewName);

                var bindingBuilder = new StringBuilder();
                var unbindingBuilder = new StringBuilder();
                HashSet<string> converterTypes = new HashSet<string>();
                HashSet<string> propertyDelegates = new HashSet<string>();
                var bingingFunctionsBuilder = new StringBuilder();

                //为每个上下文生成对应的绑定代码
                var bindingItemsBuilder = new StringBuilder();
                var unbindingItemsBuilder = new StringBuilder();
                var defaultViewModelType = bindingContext.type;
                var vmName = GetFormattedType(bindingContext.type);

                //为每个属性生成对应的委托 并添加绑定代码和解绑代码
                foreach (var property in bindingContext.properties)
                {
                    BuildNormalPropertyBinding(bindingContext, vmName, property,
                            ref propertyDelegates, //属性对应的委托
                            ref converterTypes, //转换器类型
                            ref bingingFunctionsBuilder, //属性对应的绑定方法
                            ref bindingItemsBuilder, //属性对应的绑定代码
                            ref unbindingItemsBuilder);//属性对应的解绑代码

                    if (property.type.isList)
                    {
                        var unbindingFunctionName = ListUnbindingFunctionNameTemplate
                            .Replace(PropertyNameMark, property.name)
                            .Replace(ViewModelNameMark, vmName)
                            .Replace(PropertyTypeMark, property.type.ToTypeString());
                        unbindingItemsBuilder.AppendLine($"{unbindingFunctionName}({vmName}.{property.name});");
                        BuildListUnbindingFunction(vmName, property, ref bingingFunctionsBuilder);
                    }
                }

                //组装绑定代码
                bindingBuilder.AppendLine(BindingTemplate.Replace(ViewModelTypeMark, bindingContext.type)
                    .Replace(ViewModelNameMark, vmName)
                    .Replace(BindingItemsMark, bindingItemsBuilder.ToString()));

                //组装解绑代码
                unbindingBuilder.AppendLine(BindingTemplate.Replace(ViewModelTypeMark, bindingContext.type)
                    .Replace(ViewModelNameMark, vmName)
                    .Replace(BindingItemsMark, unbindingItemsBuilder.ToString()));

                //组装所有的绑定代码
                code = code.Replace(BindingMark, bindingBuilder.ToString())
                    .Replace(UnbindingMark, unbindingBuilder.ToString())
                    .Replace(PropertyChangedFunctionsMark, bingingFunctionsBuilder.ToString())
                    .Replace(DefaultViewModelTypeMark, defaultViewModelType)
                    .Replace(ViewModelNameMark, vmName);

                //生成所有的转换器构造代码
                var convertersBuilder = new StringBuilder();
                BuildConverterCostructor(converterTypes, ref convertersBuilder);
                code = code.Replace(ConvertersMark, convertersBuilder.ToString());

                //添加using
                var usingBuilder = new StringBuilder();
                BuildUsings(usings, ref usingBuilder);
                code = code.Replace(UsingMark, usingBuilder.ToString());

                //添加Namespace
                var @namespaceName = string.IsNullOrEmpty(@namespace) ? DefaultNamespace : @namespace;
                code = code.Replace(NamespaceMark, @namespaceName);

                //格式化代码
                code = Utility.NormalizeCode(code);
                //Console.WriteLine(code);
                Console.WriteLine($"generate data binding for {vmName}:{config.viewName}");
                result.Add(new Source($"{vmName}_{config.viewName}.DataBinding", code));
            }
        }

        //构建普通属性绑定
        void BuildNormalPropertyBinding(BindingContext bindingContext, string vmName, BindingProperty property, 
            ref HashSet<string> propertyDelegates,
            ref HashSet<string> converterTypes,
            ref StringBuilder bindingFunctionBuilder, 
            ref StringBuilder bindingItemsBuilder, 
            ref StringBuilder unbindingItemsBuilder)
        {
            var delegateName = Utility.GetPropertyChangedDelegateName(property.name);
            propertyDelegates.Add(delegateName);

            //构建值转换
            var convert = BuildConvert(bindingContext.type, property);

            //如果这个属性是List则生成List绑定代码
            var listBinding = property.type.isList ? ListBindingTemplate : string.Empty;

            //为属性生成对应的绑定方法
            var propertyChangedFunctionName = $"{vmName}_{property.name}_PropertyChanged";
            var elementUpdateValue = string.IsNullOrEmpty(property.elementPropertyName)
                ? ElementUpdateValue
                : ElementPropertyUpdateValue
                    .Replace(ElementTypeMark, property.elementType.ToTypeString())
                    .Replace(ElementPropertyNameMark, property.elementPropertyName)
                    .Replace(PropertyNameMark, property.name)
                    .Replace(PropertyTypeMark, property.type.ToTypeString())
                    .Replace(ViewModelTypeMark, vmName);

            var elementType = property.elementType.IsNull() ? string.Empty : $"<{property.elementType.ToTypeString()}>";
            bindingFunctionBuilder.AppendLine(BindingItemFunctionTemplate.Replace(PropertyChangedFunctionNameMark, propertyChangedFunctionName)
                .Replace(PropertyNameMark, property.name))
                .Replace(PropertyTypeMark, property.type.ToTypeString())
                .Replace(ConvertMark, convert)
                .Replace(ElementTypeMark, elementType)
                .Replace(ElementPathMark, property.elementPath)
                .Replace(ListBindingMark, listBinding)
                .Replace(ElementUpdateValueMark, elementUpdateValue);

            //生成属性绑定代码
            bindingItemsBuilder.AppendLine($"{vmName}.{delegateName} += {propertyChangedFunctionName};");

            //生成属性解绑代码
            unbindingItemsBuilder.AppendLine($"{vmName}.{delegateName} -= {propertyChangedFunctionName};");

            //Console.WriteLine(bindingFunctionBuilder.ToString());

            converterTypes.Add(property.converterType.ToTypeString());
        }

        /// <summary>
        /// 构建值转换器
        /// </summary>
        string BuildConvert(string viewModelType, BindingProperty property)
        {
            var convertBuilder = new StringBuilder();
            //convertBuilder.AppendLine($"{property.elementValueType.ToTypeString()} convertedValue;");
            if (!property.converterType.IsNull())
            {
                //convertBuilder.AppendLine($"var input = ({property.converterValueType.ToTypeString()})@value;");
                //convertBuilder.AppendLine($"var convertedTempValue = {GetFormattedType(property.converterType.ToTypeString())}.Convert(input);");
                //convertBuilder.AppendLine($"convertedValue = ({property.elementValueType.ToTypeString()})convertedTempValue;");
                convertBuilder.AppendLine($"var convertedValue = {GetFormattedType(property.converterType.ToTypeString())}.Convert(@value);");
            }
            else
            {
                convertBuilder.AppendLine($"var convertedValue = @value;");
                //convertBuilder.AppendLine($"convertedValue = ({property.elementValueType.ToTypeString()})@value;");
            }
            return convertBuilder.ToString();
        }

        /// <summary>
        /// 构建List绑定方法
        /// </summary>
        /// <param name="viewModelName">ViewModel名</param>
        /// <param name="property">属性</param>
        /// <param name="functionBuilder">方法构建器</param>
        void BuildListUnbindingFunction(string viewModelName, BindingProperty property, ref StringBuilder functionBuilder)
        {
            var elementType = property.elementType.IsNull() ? string.Empty : $"<{property.elementType.ToTypeString()}>";
            functionBuilder.AppendLine(ListUnbindingFunctionTemplate
                .Replace(ViewModelNameMark, viewModelName)
                .Replace(PropertyNameMark, property.name)
                .Replace(PropertyTypeMark, property.type.ToTypeString())
                .Replace(ElementTypeMark, elementType)
                .Replace(ElementPathMark, property.elementPath));
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
                convertersBuilder.AppendLine($"{converterType} {GetFormattedType(converterType)} = new {converterType}();");
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

        void BuildV2VMBinding(string vmName, BindingProperty property,
            ref StringBuilder bindingItemBuilder,
            ref StringBuilder unbindingItemBuilder,
            ref StringBuilder functionBuilder)
        {
            var elementType = property.elementType.IsNull() ? string.Empty : $"<{property.elementType.ToTypeString()}>";
            var elementName = GetFormatName(property.elementPath);
            bindingItemBuilder.AppendLine($"var @{elementName} = this.View.GetVisualElement{elementType}(\"{property.elementPath}\")");
            bindingItemBuilder.AppendLine($"if(@{elementName} is FUI.IObservableVisualElement)");
            bindingItemBuilder.AppendLine("{");
            bindingItemBuilder.AppendLine($"@{elementName}.OnValueChanged += OnElement_{elementName}_ValueChanged;");
            bindingItemBuilder.AppendLine("}");

            unbindingItemBuilder.AppendLine($"var @{elementName} = this.View.GetVisualElement{elementType}(\"{property.elementPath}\")");
            unbindingItemBuilder.AppendLine($"if(@{elementName} is FUI.IObservableVisualElement)");
            unbindingItemBuilder.AppendLine("{");
            unbindingItemBuilder.AppendLine($"@{elementName}.OnValueChanged -= OnElement_{elementName}_ValueChanged;");
            unbindingItemBuilder.AppendLine("}");
        }

        string GetFormatName(string path)
        {
            return path.Replace(".", "_").Replace("/", "_").Replace(" ", "_");
        }
    }
}
