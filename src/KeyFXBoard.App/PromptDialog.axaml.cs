using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace KeyFXBoard.App;

public partial class PromptDialog : Window
{
    private Func<string, string?> _validate = _ => null;
    public string? Result { get; private set; }

    public PromptDialog()
    {
        InitializeComponent();
    }

    public PromptDialog(string title, string prompt, string? initial, Func<string, string?> validate) : this()
    {
        Title = title;
        PromptText.Text = prompt;
        NameBox.Text = initial ?? "";
        _validate = validate;
        NameBox.KeyDown += OnKeyDown;
        Opened += (_, _) => NameBox.Focus();
    }

    public static async Task<string?> Ask(
        Window owner,
        string title,
        string prompt,
        string? initial,
        Func<string, string?> validate)
    {
        var dialog = new PromptDialog(title, prompt, initial, validate);
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnOk(sender, e);
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var value = NameBox.Text?.Trim() ?? "";
        var error = _validate(value);
        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.IsVisible = true;
            return;
        }

        Result = value;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
