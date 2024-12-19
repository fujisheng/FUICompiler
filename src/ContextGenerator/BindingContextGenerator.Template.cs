namespace FUICompiler
{
    /// <summary>
    /// ugui数据绑定上下文生成器
    /// </summary>
    public partial class BindingContextGenerator
    {
        #region 类模板
        const string DefaultNamespace = "__DataBindingGenerated";

        /// <summary>
        /// 构建绑定上下文
        /// </summary>
        public static string BuildContextCode(ContextBindingInfo contextInfo, string usings, string @namespace, string converters, string bindings, string unbindings, string functions)
        {
            return $$"""
{{Utility.FileHead}}
{{usings}}
namespace {{@namespace}}
{
    [FUI.ViewModelAttribute(typeof({{contextInfo.viewModelType}}))]
    [FUI.ViewAttribute("{{contextInfo.viewName}}")]
    public class __{{contextInfo.viewModelType.ToCSharpName()}}_{{contextInfo.viewName}}_Binding_Generated : FUI.BindingContext
    {
{{converters}}
        public __{{contextInfo.viewModelType.ToCSharpName()}}_{{contextInfo.viewName}}_Binding_Generated(FUI.IView view, FUI.Bindable.ObservableObject viewModel) : base(view, viewModel) { }

        protected override void Binding()
        {
{{bindings}}
        }

        protected override void Unbinding()
        {
{{unbindings}}
        }

{{functions}}
    }
}
""";
        }
        #endregion

        #region 绑定方法模板
        /// <summary>
        /// 构建绑定方法
        /// </summary>
        public static string BuildBindingsCode(ContextBindingInfo contextInfo, string bindingItems)
        {
            return $$"""
if(this.ViewModel is {{contextInfo.viewModelType}} {{contextInfo.viewModelType.ToCSharpName()}})
{
     {{bindingItems}}
     return;
}
""";
        }
        #endregion


        #region 属性绑定模板
        /// <summary>
        /// 构建属性绑定
        /// </summary>
        public static string BuildPropertyChangedFunctionCode(string functionName, string convert, string listBinding, PropertyBindingInfo info)
        {
            return $$"""
void {{functionName}}(object sender, {{info.propertyInfo.type}} preValue, {{info.propertyInfo.type}} @value)
{
    {{convert}}
    var element = FUI.Extensions.ViewExtensions.GetElement<{{info.targetInfo.type}}>(this.View, @"{{info.targetInfo.path}}");
    {{listBinding}}
    element.{{info.targetInfo.propertyName}}?.SetValue(convertedValue);
}
""";
        }

        /// <summary>
        /// 构建List绑定
        /// </summary>
        public static string BuildListBindingCode(PropertyBindingInfo info)
        {
            return $$"""
FUI.Extensions.BindingContextExtensions.BindingList<{{info.targetInfo.type}}>(element, preValue, @value);
""";
        }

        /// <summary>
        /// 构建ListUnbinding
        /// </summary>
        public static string BuildListUnbindingCode(ContextBindingInfo contextInfo, PropertyBindingInfo info)
        {
            return $$"""
FUI.Extensions.BindingContextExtensions.UnbindingList<{{info.targetInfo.type}}>(this, {{contextInfo.viewModelType.ToCSharpName()}}.{{info.propertyInfo.name}}, @"{{info.targetInfo.path}}");
""";
        }

        #endregion

        #region View到ViewModel绑定模板
        /// <summary>
        /// 构建View到ViewModel绑定方法
        /// </summary>
        public static string BuildV2VMBindingFunctionCode(ContextBindingInfo contextInfo, PropertyBindingInfo info, string invocation, string functionName, string operate)
        {
            return $$"""
{{invocation}}
void {{functionName}}({{contextInfo.viewModelType}} {{contextInfo.viewModelType.ToCSharpName()}})
{
    var element = FUI.Extensions.ViewExtensions.GetElement<{{info.targetInfo.type}}>(this.View, @"{{info.targetInfo.path}}");
    {{operate}}
}   
""";
        }

        /// <summary>
        /// 构建View到ViewModel绑定执行的方法
        /// </summary>
        public static string BuildV2VMInvocationFunctionCode(ContextBindingInfo contextInfo, PropertyBindingInfo info, string functionName)
        {
            return $$"""
void {{functionName}} ({{info.targetInfo.propertyValueType}} oldValue, {{info.targetInfo.propertyValueType}} newValue)
{
    if(this.ViewModel is {{contextInfo.viewModelType}} {{contextInfo.viewModelType.ToCSharpName()}})
    {
        {{contextInfo.viewModelType.ToCSharpName()}}.{{info.propertyInfo.name}} = newValue;
    }
}
""";
        }

        /// <summary>
        /// 构建View到ViewModel绑定操作
        /// </summary>
        public static string BuildV2VMBindingOperateCode(string invocationName, PropertyBindingInfo info)
        {
            return $$"""
element.{{info.targetInfo.propertyName}}.OnValueChanged += {{invocationName}};
""";
        }

        /// <summary>
        /// 构建View到ViewModel解绑操作
        /// </summary>
        public static string BuildV2VMUnbindingOperateCode(string invocationName, PropertyBindingInfo info)
        {
            return $$"""
element.{{info.targetInfo.propertyName}}.OnValueChanged -= {{invocationName}};
""";
        }
        #endregion

        #region 命令绑定模板

        /// <summary>
        /// 构建命令绑定方法
        /// </summary>
        public static string BuildCommandBindingFunctionCode(ContextBindingInfo contextInfo, CommandBindingInfo info, string functionName, string operate)
        {
            return $$"""
void {{functionName}}({{contextInfo.viewModelType}} {{contextInfo.viewModelType.ToCSharpName()}})
{
    var element = FUI.Extensions.ViewExtensions.GetElement<{{info.targetInfo.type}}>(this.View, "{{info.targetInfo.path}}");
    {{operate}}
} 
""";
        }

        /// <summary>
        /// 构建命令绑定操作
        /// </summary>
        public static string BuildCommandBindingOperateCode(ContextBindingInfo contextInfo, CommandBindingInfo info)
        {
            return $$"""
element.{{info.targetInfo.propertyName}}?.AddListener({{contextInfo.viewModelType.ToCSharpName()}}.{{info.commandInfo.name}});
""";
        }

        /// <summary>
        /// 构建命令解绑操作
        /// </summary>
        public static string BuildCommandUnbindingOperateCode(ContextBindingInfo contextInfo, CommandBindingInfo info)
        {
            return $$"""
element.{{info.targetInfo.propertyName}}?.RemoveListener({{contextInfo.viewModelType.ToCSharpName()}}.{{info.commandInfo.name}});
""";
        }
        #endregion

        public static string GetPropertyTargetUniqueName(ContextBindingInfo contextInfo, PropertyBindingInfo info)
        {
            return $"{contextInfo.viewModelType.ToCSharpName()}_{info.propertyInfo.name}__{info.targetInfo.path.ToCSharpName()}_{info.targetInfo.type.ToCSharpName()}_{info.targetInfo.propertyName}";
        }

        public static string GetCommandTargetUniqueName(ContextBindingInfo contextInfo, CommandBindingInfo info)
        {
            return $"{contextInfo.viewModelType.ToCSharpName()}_{info.commandInfo.name}__{info.targetInfo.path.ToCSharpName()}_{info.targetInfo.type.ToCSharpName()}_{info.targetInfo.propertyName}";
        }
    }
}
