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
        const string ListBindingMark = "*ListBinding*";
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

        const string ElementUpdateValue = "element.UpdateValue(convertedValue);";
        const string ElementPropertyUpdateValue = @"
    if(element is *ElementType* typedElement)
    {
        var exception = $""Cannot convert the property *ViewModelType*.*PropertyName*(*PropertyType*) to the property *ElementType*.*ElementPropertyName*({typedElement.*ElementPropertyName*.GetType()}), please consider using Convertor for this binding..."";
        typedElement.*ElementPropertyName*.SetValue(convertedValue, exception);
    }
";
        const string ListUnbindingFunctionNameTemplate = @"*ViewModelName*_UnbindingList_*PropertyName*";
        const string ListUnbindingFunctionTemplate = @"
void *ViewModelName*_UnbindingList_*PropertyName*(*PropertyType* list)
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
