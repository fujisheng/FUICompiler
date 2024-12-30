using System.Globalization;

namespace FUICompiler
{
    /// <summary>
    /// ugui数据绑定上下文生成器
    /// </summary>
    public partial class BindingContextGenerator
    {
        #region 类模板
        /// <summary>
        /// 构建绑定上下文
        /// </summary>
        public static string BuildContextCode(ContextBindingInfo contextInfo, string converters, string bindings, string unbindings, string functions)
        {
            return $$"""
{{Utility.FileHead}}
namespace {{Utility.BindingContextDefaultNamespace}}
{
    [FUI.ViewModelAttribute(typeof({{contextInfo.viewModelType}}))]
    [FUI.ViewAttribute("{{contextInfo.viewName}}")]
    public class {{contextInfo.viewModelType.ToCSharpName()}}_{{contextInfo.viewName}}_Binding_Generated : FUI.BindingContext<{{contextInfo.viewModelType}}>
    {
{{converters}}
        public {{contextInfo.viewModelType.ToCSharpName()}}_{{contextInfo.viewName}}_Binding_Generated(FUI.IView view, {{contextInfo.viewModelType}} viewModel) : base(view, viewModel) { }

        protected override void OnBinding()
        {
{{bindings}}
        }

        protected override void OnUnbinding()
        {
{{unbindings}}
        }

{{functions}}
    }
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
FUI.BindingContextUtility.BindingList<{{info.targetInfo.type}}>(element, preValue, @value);
""";
        }

        /// <summary>
        /// 构建List解绑方法
        /// </summary>
        /// <param name="functionName">方法名</param>
        /// <param name="info">属性绑定信息</param>
        /// <returns></returns>
        public static string BuildListUnbindingFunctionCode(string functionName, PropertyBindingInfo info)
        {
            return $$"""
void {{functionName}}()
{
    var element = FUI.Extensions.ViewExtensions.GetElement<{{info.targetInfo.type}}>(this.View, @"{{info.targetInfo.path}}");
    FUI.BindingContextUtility.UnbindingList<{{info.targetInfo.type}}>(element, this.ViewModel.{{info.propertyInfo.name}});
}
""";
        }

        /// <summary>
        /// 构建ListUnbinding
        /// </summary>
        public static string BuildListUnbindingCode(string functionName)
        {
            return $$"""
{{functionName}}();
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
void {{functionName}}()
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
    this.ViewModel.{{info.propertyInfo.name}} = newValue;
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
void {{functionName}}()
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
element.{{info.targetInfo.propertyName}}?.AddListener(this.ViewModel.{{info.commandInfo.name}});
""";
        }

        /// <summary>
        /// 构建命令解绑操作
        /// </summary>
        public static string BuildCommandUnbindingOperateCode(ContextBindingInfo contextInfo, CommandBindingInfo info)
        {
            return $$"""
element.{{info.targetInfo.propertyName}}?.RemoveListener(this.ViewModel.{{info.commandInfo.name}});
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
