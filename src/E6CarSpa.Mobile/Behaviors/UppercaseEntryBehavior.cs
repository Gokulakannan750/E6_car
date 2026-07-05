namespace E6CarSpa.Mobile.Behaviors;

/// <summary>
/// Forces an <see cref="Entry"/>'s text to upper case as the user types — used for car numbers,
/// matching the desktop's CharacterCasing="Upper" (MAUI's Entry has no such property).
/// </summary>
public sealed class UppercaseEntryBehavior : Behavior<Entry>
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
        var upper = e.NewTextValue?.ToUpperInvariant();
        // Setting Text re-raises TextChanged, but then upper == NewTextValue so it stops (no loop).
        if (upper != e.NewTextValue)
            entry.Text = upper;
    }
}
