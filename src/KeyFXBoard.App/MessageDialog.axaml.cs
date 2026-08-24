using Avalonia.Controls;

namespace KeyFXBoard.App;

public partial class MessageDialog : Window
{
    public bool Confirmed { get; private set; }

    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string body, bool cancel, string? okContent = null) : this()
    {
        Title = title;
        BodyText.Text = body;
        CancelButton.IsVisible = cancel;
        OkButton.Content = okContent ?? (cancel ? "Continue" : "OK");
    }

    public static async Task Alert(Window owner, string title, string body)
    {
        var dialog = new MessageDialog(title, body, cancel: false);
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> Confirm(Window owner, string title, string body, string? okContent = null)
    {
        var dialog = new MessageDialog(title, body, cancel: true, okContent);
        await dialog.ShowDialog(owner);
        return dialog.Confirmed;
    }

    private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
