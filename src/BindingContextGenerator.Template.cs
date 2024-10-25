namespace FUICompiler
{
    /// <summary>
    /// ugui数据绑定上下文生成器
    /// </summary>
    public partial class BindingContextGenerator
    {
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

        const string BindingItemsMark = "*BindingItems*";
        const string BindingTemplate = @"
            if(this.ViewModel is *ViewModelType* *ViewModelName*)
            {
*BindingItems*
                return;
            }
";

 
        const string PropertyNameMark = "*PropertyName*";
        const string PropertyTypeMark = "*PropertyType*";
        const string PropertyChangedFunctionNameMark = "*PropertyChangedFunctionName*";
        const string ConvertMark = "*Convert*";
        const string ElementTypeMark = "*ElementType*";
        const string ElementPathMark = "*ElementPath*";
        const string ElementUpdateValueMark = "*ElementUpdateValue*";
        const string ElementPropertyNameMark = "*ElementPropertyName*";
        const string BindingItemFunctionTemplate = @"
void *PropertyChangedFunctionName*(object sender, *PropertyType* preValue, *PropertyType* @value)
{
    *Convert*
    var element = this.View.GetVisualElement*ElementType*(""*ElementPath*"");
    if(element == null)
    {
        throw new System.Exception($""{this.View.Name} GetVisualElement type:*ElementType* path:{@""*ElementPath*""} failed""); 
    }
    *ElementUpdateValue*
}
";
        const string ElementUpdateValue = "element.UpdateValue(convertedValue);";
        const string ElementPropertyUpdateValue = @"
if(element is *ElementType* *ElementType*)
{
    *ElementType*.*ElementPropertyName*.Value = convertedValue;
}
";


        const string ListAddParams = "(object sender, int? index, object item)";
        const string ListRemoveParams = "(object sender, int? index, object item)";
        const string ListReplaceParams = "(object sender, int? index, object oldItem, object newItem)";
        const string ListUpdateParams = "(object sender)";
        const string OnListAdd = "OnAdd(sender, index, item)";
        const string OnListRemove = "OnRemove(sender, index, item)";
        const string OnListReplace = "OnReplace(sender, index, oldItem, newItem)";
        const string OnListUpdate = "OnUpdate(sender)";

        const string OperatorMark = "*Operator*";
        const string ListParamsMark = "*ListParams*";
        const string OnOperateMark = "*OnOperate*";
        const string BindingListTemplate = @"
void OnList_*PropertyName*_*Operator**ListParams*
{
    var element = this.View.GetVisualElement*ElementType*(""*ElementPath*"");
    if(element == null)
    {
        throw new System.Exception($""{this.View.Name} GetVisualElement type:*ElementType* path:{@""*ElementPath*""} failed""); 
    }

    if(element is FUI.IListViewElement listViewElement)
    {
        listViewElement.*OnOperate*;
    }
}
";

        const string ElementNameMark = "*ElementName*";
        const string BindingV2VMTemplate = @"
void *Operator*Element_*ElementName*()
{
    var element = this.View.GetVisualElement*ElementType*(""*ElementPath*"");
    if(element == null)
    {
        throw new System.Exception($""{this.View.Name} GetVisualElement type:*ElementType* path:{@""*ElementPath*""} failed""); 
    }

    if(element is FUI.IObservableVisualElement)
    {
        element.OnValueChanged += ;
    }
}          
";
    }
}
