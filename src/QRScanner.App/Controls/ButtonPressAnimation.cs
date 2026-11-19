namespace QRScanner.App.Controls;

/// <summary>
/// Плавно уменьшает кнопку при нажатии и возвращает размер при отпускании.
/// </summary>
public static class ButtonPressAnimation
{
    public static readonly BindableProperty IsEnabledProperty =
        BindableProperty.CreateAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ButtonPressAnimation),
            false,
            propertyChanged: OnIsEnabledChanged);

    public static bool GetIsEnabled(BindableObject view)
    {
        return (bool)view.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(BindableObject view, bool value)
    {
        view.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        if (bindable is not Button button)
        {
            return;
        }

        if ((bool)newValue)
        {
            button.Pressed += OnPressed;
            button.Released += OnReleased;
            button.PropertyChanged += OnButtonPropertyChanged;
        }
        else
        {
            button.Pressed -= OnPressed;
            button.Released -= OnReleased;
            button.PropertyChanged -= OnButtonPropertyChanged;
            button.Scale = 1;
        }
    }

    private static async void OnPressed(object? sender, EventArgs e)
    {
        if (sender is Button button && button.IsEnabled)
        {
            await button.ScaleTo(0.96, 70, Easing.CubicOut);
        }
    }

    private static async void OnReleased(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await button.ScaleTo(1, 90, Easing.CubicIn);
        }
    }

    private static void OnButtonPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is Button button &&
            e.PropertyName == nameof(Button.IsEnabled) &&
            !button.IsEnabled)
        {
            button.Scale = 1;
        }
    }
}
