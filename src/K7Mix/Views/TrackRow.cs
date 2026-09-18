using System.ComponentModel;

namespace K7Mix.Views;

/// <summary>
/// One row in the track list. The row object is stable for the lifetime of the
/// track so the list can be refreshed by mutating <see cref="Text"/> instead of
/// replacing the ListBox items source. Replacing the items source resets the
/// selection model, and the SelectionChanged that follows feeds back into the
/// track editors, which re-raises TextChanged and never settles.
/// </summary>
public sealed class TrackRow(string text) : INotifyPropertyChanged
{
    private string _text = text;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public override string ToString() => _text;
}
