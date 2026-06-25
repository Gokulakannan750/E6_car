namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Implemented by page view-models that need to load data when navigated to.</summary>
public interface IAsyncInitialize
{
    Task InitializeAsync();
}
