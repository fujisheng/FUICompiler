namespace FUICompiler
{
    /// <summary>
    /// ugui数据绑定上下文生成器
    /// </summary>
    public partial class BindingContextGenerator
    {
        #region 类模板
        const string ViewNameMark = "*ViewName*";
        const string BindingMark = "*Binding*";
        const string UnbindingMark = "*Unbinding*";
        const string ViewModelTypeMark = "*ViewModelType*";
        const string ViewModelNameMark = "*ViewModelName*";
        const string ConvertersMark = "*Converters*";
        const string DefaultViewModelTypeMark = "*DefaultViewModelType*";
        const string PropertyChangedFunctionsMark = "*PropertyChangedFunctions*";
        const string UsingMark = "*Using*";
        const string NamespaceMark = "*Namespace*";
        const string DefaultNamespace = "__DataBindingGenerated";


        const string Template = @"
*Using*
namespace *Namespace*
{
    [FUI.ViewModelAttribute(typeof(*DefaultViewModelType*))]
    [FUI.ViewAttribute(""*ViewName*"")]
    public class __*ViewModelName*_*ViewName*_Binding_Generated : FUI.BindingContext
    {
*Converters*
        public __*ViewModelName*_*ViewName*_Binding_Generated(FUI.IView view, FUI.Bindable.ObservableObject viewModel) : base(view, viewModel) { }

        protected override void Binding()
        {
*Binding*
        }

        protected override void Unbinding()
        {
*Unbinding*
        }

*PropertyChangedFunctions*
    }
}
";
        #endregion

        #region 绑定方法模板
        const string BindingItemsMark = "*BindingItems*";
        const string BindingTemplate = @"
            if(this.ViewModel is *ViewModelType* *ViewModelName*)
            {
*BindingItems*
                return;
            }
";
        #endregion


        #region 属性绑定模板
        const string PropertyNameMark = "*PropertyName*";
        const string PropertyTypeMark = "*PropertyType*";
        const string PropertyChangedFunctionNameMark = "*PropertyChangedFunctionName*";
        const string ConvertMark = "*Convert*";
        const string ElementTypeMark = "*ElementType*";
        const string ElementPathMark = "*ElementPath*";
        const string ElementPropertyNameMark = "*ElementPropertyName*";
        const string ListBindingMark = "*ListBinding*";

        //属性绑定方法模板
        const string BindingItemFunctionTemplate = @"
void *PropertyChangedFunctionName*(object sender, *PropertyType* preValue, *PropertyType* @value)
{
    *Convert*
    var element = FUI.Extensions.ViewExtensions.GetElement<*ElementType*>(this.View, ""*ElementPath*"");
    *ListBinding*

    var exception = $""Cannot convert the property *ViewModelType*.*PropertyName*(*PropertyType*) to the property *ElementType*.*ElementPropertyName*({element.*ElementPropertyName*.GetType()}), please consider using Convertor for this binding..."";
    element.*ElementPropertyName*.SetValue(convertedValue, exception);
}
";
        //ListView绑定模板
        const string ListBindingTemplate = @"
FUI.Extensions.BindingContextExtensions.BindingList<*ElementType*>(element, preValue, @value);
";

        //ListView解绑模板
        const string ListUnbindingTemplate = @"
FUI.Extensions.BindingContextExtensions.UnbindingList<*ElementType*>(this, *ViewModelName*.*PropertyName*, ""*ElementPath*"");
";
        #endregion

        #region View到ViewModel绑定模板
        const string V2VMOperateMark = "*V2VMOperate*";
        const string InvocationMark = "*Invocation*";
        const string V2VMBindingFunctionNameMark = "*V2VMBindingFunctionName*";
        const string V2VMBindingInvocationNameMark = "*V2VMBindingInvocationName*";
        const string V2VMBindingFunctionTemplate = @"
*Invocation*
void *V2VMBindingFunctionName*(*ViewModelType* *ViewModelName*)
{
    var element = FUI.Extensions.ViewExtensions.GetElement<*ElementType*>(this.View, ""*ElementPath*"");

    var exception = $""Cannot convert the property *ViewModelType*.*PropertyName*(*PropertyType*) to the property *ElementType*.*ElementPropertyName*({element.*ElementPropertyName*.GetType()}), please consider using Convertor for this binding..."";
    *V2VMOperate*
}          
";
        const string V2VMBindingTemplate = @"
element.*ElementPropertyName*.OnValueChanged += (oldValue, newValue)=>
{   
    element.*ElementPropertyName*.MuteValueChangedEvent(true);
    *ViewModelName*.*PropertyName* = newValue;
    element.*ElementPropertyName*.MuteValueChangedEvent(false);
};
*V2VMBindingInvocationName* = element.*ElementPropertyName*.GetLastInvocation();
";
        const string V2VMUnbindingTemplate = @"
element.*ElementPropertyName*.RemoveValueChanged(*V2VMBindingInvocationName*);
";
        #endregion
    }
}
