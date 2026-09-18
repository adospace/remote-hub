namespace RemoteHub.ViewModels;

/// <summary>
/// A selectable value paired with the text a picker shows for it, so enums and model objects can be
/// offered in a ComboBox with readable labels.
/// </summary>
public sealed record Choice<T>(T Value, string Label);
