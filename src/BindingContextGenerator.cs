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

                    //如果是列表 需要单独构建ListUnbinding代码
                    if (property.type.isList)
                    {
                        unbindingItemsBuilder.AppendLine(ListUnbindingTemplate
                            .Replace(ElementTypeMark, property.elementType.ToTypeString())
                            .Replace(ViewModelNameMark, vmName)
                            .Replace(PropertyNameMark, property.name)
                            .Replace(ElementPathMark, property.elementPath));
                    }

                    //如果是双向绑定需要构建从View到ViewModel的绑定
                    if(property.bindingType == BindingType.TwoWay)
                    {
                        BuildV2VMBinding(vmName, property, ref bindingItemsBuilder, ref unbindingItemsBuilder, ref bingingFunctionsBuilder);
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
                //Console.WriteLine($"generate data binding for {vmName}:{config.viewName}");
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
            var listBinding = property.type.isList ? ListBindingTemplate.Replace(ElementTypeMark, property.elementType.ToTypeString()) : string.Empty;

            //为属性生成对应的绑定方法
            var propertyChangedFunctionName = $"{vmName}_{property.name}_PropertyChanged_{Utility.ToCSharpName(property.elementPath)}_{Utility.ToCSharpName(property.elementType.ToTypeString())}";

            //构建属性绑定代码
            bindingFunctionBuilder.AppendLine(BindingItemFunctionTemplate
                .Replace(PropertyChangedFunctionNameMark, propertyChangedFunctionName)
                .Replace(PropertyTypeMark, property.type.ToTypeString())

                .Replace(ConvertMark, convert)

                .Replace(ElementTypeMark, property.elementType.ToTypeString())
                .Replace(ElementPathMark, property.elementPath)

                .Replace(ListBindingMark, listBinding)

                .Replace(ViewModelTypeMark, vmName)
                .Replace(PropertyNameMark, property.name)
                .Replace(ElementPropertyNameMark, property.elementPropertyName));

            //生成属性绑定代码
            bindingItemsBuilder.AppendLine($"{vmName}.{delegateName} += {propertyChangedFunctionName};");

            //生成属性解绑代码
            unbindingItemsBuilder.AppendLine($"{vmName}.{delegateName} -= {propertyChangedFunctionName};");

            converterTypes.Add(property.converterType.ToTypeString());
        }

        /// <summary>
        /// 构建值转换器
        /// </summary>
        string BuildConvert(string viewModelType, BindingProperty property)
        {
            var convertBuilder = new StringBuilder();

            if (!property.converterType.IsNull())
            {
                convertBuilder.AppendLine($"var convertedValue = {GetFormattedType(property.converterType.ToTypeString())}.Convert(@value);");
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
            var bindingFunctionName = $"{vmName}_{property.name}_BindingV2VM_{property.elementPath.ToCSharpName()}_{property.elementType.ToTypeString().ToCSharpName()}";
            var unbindingFunctionName = $"{vmName}_{property.name}_UnbindingV2VM_{property.elementPath.ToCSharpName()}_{property.elementType.ToTypeString().ToCSharpName()}";
            bindingItemBuilder.AppendLine($"{bindingFunctionName}({vmName});");
            unbindingItemBuilder.AppendLine($"{unbindingFunctionName}({vmName});");

            var invocationName = $"{vmName}_{property.name}_V2VM_{property.elementPath.ToCSharpName()}_{property.elementType.ToTypeString().ToCSharpName()}_Invocation";

            var invocation = $"System.Delegate {invocationName};";

            var tempBuilder = new StringBuilder();
            
            tempBuilder.AppendLine(V2VMBindingFunctionTemplate
                .Replace(ViewModelTypeMark, vmName)
                .Replace(PropertyNameMark, property.name)
                .Replace(ViewModelNameMark, vmName)
                .Replace(PropertyTypeMark, property.type.ToTypeString())
                .Replace(ElementTypeMark, property.elementType.ToTypeString())
                .Replace(ElementPathMark, property.elementPath)
                .Replace(ElementPropertyNameMark, property.elementPropertyName));

            var temp = tempBuilder.ToString();

            var bindingOperate = V2VMBindingTemplate
                .Replace(ElementPropertyNameMark, property.elementPropertyName)
                .Replace(ViewModelNameMark, vmName)
                .Replace(PropertyNameMark, property.name)
                .Replace(V2VMBindingInvocationNameMark, invocationName);

            functionBuilder.AppendLine(temp
                .Replace(InvocationMark, invocation)
                .Replace(V2VMBindingFunctionNameMark, bindingFunctionName)
                .Replace(V2VMOperateMark, bindingOperate));

            var unbindingOperate = V2VMUnbindingTemplate
                .Replace(ElementPropertyNameMark, property.elementPropertyName)
                .Replace(V2VMBindingInvocationNameMark, invocationName);
            functionBuilder.AppendLine(temp
                .Replace(InvocationMark, "")
                .Replace(V2VMBindingFunctionNameMark, unbindingFunctionName)
                .Replace(V2VMOperateMark, unbindingOperate));

        }

        string GetFormatName(string path)
        {
            return path.Replace(".", "_").Replace("/", "_").Replace(" ", "_");
        }
    }
}
