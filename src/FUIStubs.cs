namespace FUI
{
    public class BindingAttribute : Attribute { }

    public class CommandAttribute : Attribute { }

    public interface IValueConverter { }

    public interface IValueConverter<T1, T2> { }
}

namespace FUI.BindingDescriptor
{
    public class ContextDescriptor { }

    public class ContextDescriptor<TViewModel> : ContextDescriptor where TViewModel : FUI.Bindable.ObservableObject { }

    public class CommandBindingDescriptor { }

    public class PropertyBindingDescriptor { }
}

namespace FUI.Bindable
{
    public class ObservableObject { }
    public class BindableProperty<T> { }
}