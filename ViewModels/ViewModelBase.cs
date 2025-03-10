using ReactiveUI;

namespace AikaHelper.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
}