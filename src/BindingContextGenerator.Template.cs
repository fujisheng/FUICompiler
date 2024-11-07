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
        const string ElementUpdateValueMark = "*ElementUpdateValue*";
        const string ElementPropertyNameMark = "*ElementPropertyName*";
        const string ListBindingMark = "*ListBinding*";

        //属性绑定方法模板
        const string BindingItemFunctionTemplate = @"
void *PropertyChangedFunctionName*(object sender, *PropertyType* preValue, *PropertyType* @value)
{
    *Convert*
    if(!(this.View is FUI.IElement e))
    {
        throw new System.Exception($""{this.View.Name} not FUI.IElement""); 
    }
    var element = e.GetChild*ElementType*(""*ElementPath*"");
    if(element == null)
    {
        throw new System.Exception($""{this.View.Name} GetChild type:*ElementType* path:{@""*ElementPath*""} failed""); 
    }
    *ListBinding*
    *ElementUpdateValue*
}
";
        //ListView绑定模板
        const string ListBindingTemplate = @"
    if(element is FUI.IListView listView)
    {
        if(preValue != null)
        {
            preValue.CollectionAdd -= listView.OnAdd;
            preValue.CollectionRemove -= listView.OnRemove;
            preValue.CollectionReplace -= listView.OnReplace;
            preValue.CollectionUpdate -= listView.OnUpdate;
        }
        
        if(@value != null)
        {
            @value.CollectionAdd += listView.OnAdd;
            @value.CollectionRemove += listView.OnRemove;
            @value.CollectionReplace += listView.OnReplace;
            @value.CollectionUpdate += listView.OnUpdate;
        }
    }
";


        //Element属性更新模板
        const string ElementPropertyUpdateValue = @"
    if(element is *ElementType* typedElement)
    {
        var exception = $""Cannot convert the property *ViewModelType*.*PropertyName*(*PropertyType*) to the property *ElementType*.*ElementPropertyName*({typedElement.*ElementPropertyName*.GetType()}), please consider using Convertor for this binding..."";
        typedElement.*ElementPropertyName*.SetValue(convertedValue, exception);
    }
";
        const string ListUnbindingFunctionNameMark = @"*ListUnbindingFunctionName*";
        //ListView解绑方法模板
        const string ListUnbindingFunctionTemplate = @"
void *ListUnbindingFunctionName*(*PropertyType* list)
{
    if(list == null)
    {
        return;
    }

    if(!(this.View is FUI.IElement e))
    {
        throw new System.Exception($""{this.View.Name} not FUI.IElement""); 
    }
    var element = e.GetChild*ElementType*(""*ElementPath*"");
    if(element == null)
    {
        throw new System.Exception($""{this.View.Name} GetChild type:*ElementType* path:{@""*ElementPath*""} failed""); 
    }

    if(element is FUI.IListView listView)
    {
        list.CollectionAdd -= listView.OnAdd;
        list.CollectionRemove -= listView.OnRemove;
        list.CollectionReplace -= listView.OnReplace;
        list.CollectionUpdate -= listView.OnUpdate;
    }
}
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
    if(!(this.View is FUI.IElement e))
    {
        throw new System.Exception($""{this.View.Name} not FUI.IElement""); 
    }
    var element = e.GetChild<*ElementType*>(""*ElementPath*"");
    if(element == null)
    {
        throw new System.Exception($""{this.View.Name} GetChild type:*ElementType* path:{@""*ElementPath*""} failed""); 
    }

    if(element is *ElementType* typedElement)
    {
        var exception = $""Cannot convert the property *ViewModelType*.*PropertyName*(*PropertyType*) to the property *ElementType*.*ElementPropertyName*({typedElement.*ElementPropertyName*.GetType()}), please consider using Convertor for this binding..."";
        *V2VMOperate*
    }
}          
";
        const string V2VMBindingTemplate = @"
typedElement.*ElementPropertyName*.OnValueChanged += (oldValue, newValue)=>
{   
    typedElement.*ElementPropertyName*.MuteValueChangedEvent(true);
    *ViewModelName*.*PropertyName* = newValue;
    typedElement.*ElementPropertyName*.MuteValueChangedEvent(false);
};
*V2VMBindingInvocationName* = typedElement.*ElementPropertyName*.GetLastInvocation();
";
        const string V2VMUnbindingTemplate = @"
typedElement.*ElementPropertyName*.RemoveValueChanged(*V2VMBindingInvocationName*);
";
        #endregion
    }
}
