using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Thermalith.App.Views;

public enum UnsavedChangesChoice { Cancel, Discard, Save }

/// <summary>Ask whether to save, discard, or keep editing unsaved work.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);
    private void OnDiscard(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
