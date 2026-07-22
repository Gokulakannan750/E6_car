namespace E6CarSpa.Mobile.Behaviors;

/// <summary>
/// Restricts an <see cref="Entry"/> to digits only (used for phone numbers); combine with
/// MaxLength="10" to cap it at a 10-digit number. Strips anything non-numeric as the user types
/// or pastes.
/// </summary>
public sealed class DigitsOnlyEntryBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        entry.TextChanged += OnTextChanged;
        base.OnAttachedTo(entry);
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.TextChanged -= OnTextChanged;
        base.OnDetachingFrom(entry);
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry) return;
        var digits = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());
        // Setting Text re-raises TextChanged, but then digits == NewTextValue so it stops.
        if (digits != e.NewTextValue)
            entry.Text = digits;
    }
}
