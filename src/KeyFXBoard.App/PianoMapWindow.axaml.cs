using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KeyFXBoard.App.Services;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.App;

public partial class PianoMapWindow : Window
{
    private const double WhiteWidth = 32;
    private const double WhiteHeight = 168;
    private const double BlackWidth = 20;
    private const double BlackHeight = 104;

    private readonly AppRuntime? _runtime;
    private readonly Dictionary<string, KeyVisual> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _held = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IBrush WhiteFill = new SolidColorBrush(Color.FromRgb(243, 238, 228));
    private static readonly IBrush WhiteDownFill = new SolidColorBrush(Color.FromRgb(214, 226, 240));
    private static readonly IBrush BlackFill = new SolidColorBrush(Color.FromRgb(28, 28, 28));
    private static readonly IBrush BlackDownFill = new SolidColorBrush(Color.FromRgb(36, 52, 78));
    private static readonly IBrush WhiteInk = new SolidColorBrush(Color.FromRgb(28, 28, 28));
    private static readonly IBrush BlackInk = Brushes.White;
    private static readonly IBrush Edge = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));

    private sealed record KeyVisual(Border Border, bool Black, bool Mapped);

    public PianoMapWindow()
    {
        InitializeComponent();
        Rebuild(0);
    }

    public PianoMapWindow(AppRuntime runtime) : this()
    {
        _runtime = runtime;
        StayOnTopBox.IsCheckedChanged += OnStayOnTopChanged;
        Topmost = StayOnTopBox.IsChecked == true;
        runtime.Engine.PianoNoteChanged += OnPianoNoteChanged;
        Closed += (_, _) => runtime.Engine.PianoNoteChanged -= OnPianoNoteChanged;
        Rebuild(runtime.Engine.OctaveShift);
    }

    public void Rebuild(int octaveShift)
    {
        KeyboardCanvas.Children.Clear();
        _keys.Clear();
        BuildKeyboard(octaveShift);
        HintText.Text = "Each piano key shows the computer key that plays it at the current octave. Page Down / Page Up shift the whole map. Home / End return to A-row = C4. Click a key to hear it. Closing this window does not stop piano.";
        OctaveHint.Text = PianoLayout.OctaveLabel(octaveShift);
        foreach (var note in _held)
        {
            Paint(note);
        }
    }

    private void OnStayOnTopChanged(object? sender, RoutedEventArgs e) =>
        Topmost = StayOnTopBox.IsChecked == true;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnPianoNoteChanged(string note, bool down)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (down)
            {
                _held.Add(note);
            }
            else
            {
                _held.Remove(note);
            }

            Paint(note);
        });
    }

    private void Paint(string note)
    {
        if (!_keys.TryGetValue(note, out var visual))
        {
            return;
        }

        visual.Border.Background = FillFor(visual.Black, visual.Mapped, _held.Contains(note));
    }

    private void BuildKeyboard(int octaveShift)
    {
        var bindings = PianoLayout.MapBindings(octaveShift).ToDictionary(b => b.Midi);
        var whiteCount = 0;
        for (var midi = PianoLayout.MinMidi; midi <= PianoLayout.MaxMidi; midi++)
        {
            if (!PianoLayout.IsBlack(midi))
            {
                whiteCount++;
            }
        }

        KeyboardCanvas.Width = whiteCount * WhiteWidth;
        KeyboardCanvas.Height = WhiteHeight;

        var whiteIndex = 0;
        for (var midi = PianoLayout.MinMidi; midi <= PianoLayout.MaxMidi; midi++)
        {
            if (PianoLayout.IsBlack(midi))
            {
                continue;
            }

            bindings.TryGetValue(midi, out var binding);
            AddKey(
                midi,
                binding,
                left: whiteIndex * WhiteWidth,
                top: 0,
                width: WhiteWidth,
                height: WhiteHeight,
                black: false);
            whiteIndex++;
        }

        whiteIndex = 0;
        for (var midi = PianoLayout.MinMidi; midi <= PianoLayout.MaxMidi; midi++)
        {
            if (!PianoLayout.IsBlack(midi))
            {
                whiteIndex++;
                continue;
            }

            bindings.TryGetValue(midi, out var binding);
            AddKey(
                midi,
                binding,
                left: whiteIndex * WhiteWidth - BlackWidth / 2,
                top: 0,
                width: BlackWidth,
                height: BlackHeight,
                black: true);
        }
    }

    private void AddKey(
        int midi,
        PianoKeyBinding binding,
        double left,
        double top,
        double width,
        double height,
        bool black)
    {
        var note = PianoLayout.NameOfMidi(midi);
        var mapped = binding != default && binding.Midi == midi;
        var label = mapped ? KeyCaption(binding) : "";
        var fill = FillFor(black, mapped, false);
        var ink = black ? BlackInk : WhiteInk;

        var caption = new TextBlock
        {
            Text = label,
            FontSize = black ? 11 : 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        DockPanel.SetDock(caption, Dock.Bottom);
        caption.Margin = new Thickness(0, 0, 0, black ? 6 : 10);
        var body = new DockPanel();
        body.Children.Add(caption);
        body.Children.Add(new Panel());

        var key = new Border
        {
            Width = width,
            Height = height,
            Background = fill,
            BorderBrush = Edge,
            BorderThickness = new Thickness(black ? 0 : 0.6, 0, 0.6, 1),
            CornerRadius = new CornerRadius(0, 0, 3, 3),
            Padding = new Thickness(1, 0, 1, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = body,
            Tag = note
        };
        ToolTip.SetTip(key, Tip(note, binding, mapped));
        key.PointerPressed += OnKeyPressed;
        Canvas.SetLeft(key, left);
        Canvas.SetTop(key, top);
        KeyboardCanvas.Children.Add(key);
        _keys[note] = new KeyVisual(key, black, mapped);
    }

    private static IBrush FillFor(bool black, bool mapped, bool down)
    {
        if (down && mapped)
        {
            return black ? BlackDownFill : WhiteDownFill;
        }

        if (!mapped)
        {
            return black
                ? new SolidColorBrush(Color.FromRgb(18, 18, 18))
                : new SolidColorBrush(Color.FromRgb(200, 196, 188));
        }

        return black ? BlackFill : WhiteFill;
    }

    private static string KeyCaption(PianoKeyBinding binding) => binding.KeyLabel;

    private static string Tip(string note, PianoKeyBinding binding, bool mapped)
    {
        if (!mapped)
        {
            return $"{note} — not on the keyboard at this octave. Page Up/Down to reach it.";
        }

        return $"{note} — press {binding.KeyLabel}";
    }

    private void OnKeyPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: string note } || _runtime is null)
        {
            return;
        }

        try
        {
            _runtime.PlayPianoNote(note);
        }
        catch (Exception ex)
        {
            _ = MessageDialog.Alert(this, "Could not play note", ex.Message);
        }
    }
}
