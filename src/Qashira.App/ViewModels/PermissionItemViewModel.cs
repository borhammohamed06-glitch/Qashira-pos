using Qashira.Application.DTOs;

namespace Qashira.App.ViewModels;

public sealed class PermissionItemViewModel : ViewModelBase
{
    private bool _isGranted;

    public PermissionItemViewModel(PermissionOptionDto permission)
    {
        Id = permission.Id;
        Code = permission.Code;
        Name = permission.Name;
        _isGranted = permission.IsGranted;
    }

    public int Id { get; }
    public string Code { get; }
    public string Name { get; }

    public bool IsGranted
    {
        get => _isGranted;
        set => SetProperty(ref _isGranted, value);
    }
}
