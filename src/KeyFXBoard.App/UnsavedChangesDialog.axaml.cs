using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyFXBoard.App;

public enum UnsavedChoice
{
    Cancel,
    Discard,
    Save,
    SaveAs
}

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChoice Choice { get; private set; } = UnsavedChoice.Cancel;

    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialog(bool systemProfile) : this()
    {
        BodyText.Text = systemProfile
            ? "This system profile cannot be saved. Save as a new profile, discard the tweaks, or cancel."
            : "This profile has unsaved changes. Save, discard, or cancel.";
        SaveButton.IsVisible = !systemProfile;
    }

    public static async Task<UnsavedChoice> Ask(Window owner, bool systemProfile)
    {
        var dialog = new UnsavedChangesDialog(systemProfile);
        await dialog.ShowDialog(owner);
        return dialog.Choice;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.Cancel;
        Close();
    }

    private void OnDiscard(object? sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.Discard;
        Close();
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.Save;
        Close();
    }

    private void OnSaveAs(object? sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.SaveAs;
        Close();
    }
}
