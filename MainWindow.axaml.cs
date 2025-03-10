using AikaHelper.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;

namespace AikaHelper;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainWindowViewModel();
    }
}

/// <summary>
///     TextBox 行为的静态类，包含附加属性和事件处理程序。
/// </summary>
public static class TextBoxBehavior
{
    /// <summary>
    ///     定义 SelectAllOnFocus 附加属性。
    /// </summary>
    public static readonly AttachedProperty<bool> SelectAllOnFocusProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "SelectAllOnFocus",
            typeof(TextBoxBehavior));

    /// <summary>
    ///     获取 SelectAllOnFocus 附加属性的值。
    /// </summary>
    /// <param name="textBox">TextBox 控件。</param>
    /// <returns>属性值。</returns>
    public static bool GetSelectAllOnFocus(TextBox textBox)
    {
        return textBox.GetValue(SelectAllOnFocusProperty);
    }

    /// <summary>
    ///     设置 SelectAllOnFocus 附加属性的值。
    /// </summary>
    /// <param name="textBox">TextBox 控件。</param>
    /// <param name="value">属性值。</param>
    public static void SetSelectAllOnFocus(TextBox textBox, bool value)
    {
        textBox.SetValue(SelectAllOnFocusProperty, value);

        if (value)
        {
            // 订阅 GotFocus 事件
            textBox.GotFocus += TextBox_GotFocus;
            textBox.PointerPressed += TextBox_PointerPressed;
            textBox.PointerReleased += TextBox_PointerReleased;
        }
        else
        {
            // 取消订阅 GotFocus 事件
            textBox.GotFocus -= TextBox_GotFocus;
            textBox.PointerPressed -= TextBox_PointerPressed;
            textBox.PointerReleased -= TextBox_PointerReleased;
        }
    }

    /// <summary>
    ///     GotFocus 事件的处理程序。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    private static void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        // 如果 sender 不是 TextBox，则返回
        if (sender is not TextBox textBox) return;
        // 标记事件为已处理
        e.Handled = true;

        // 选择 TextBox 中的所有文本
        textBox.SelectAll();

        // 获取顶级窗口的剪贴板
        var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
        // 将选中的文本设置到剪贴板
        clipboard?.SetTextAsync(textBox.SelectedText);
    }

    /// <summary>
    ///     PointerPressed 事件的处理程序。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    private static void TextBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 如果 sender 不是 TextBox，则返回
        if (sender is not TextBox textBox) return;
        // 标记事件为已处理
        e.Handled = true;

        // 选择 TextBox 中的所有文本
        textBox.SelectAll();
    }

    /// <summary>
    ///     PointerReleased 事件的处理程序。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    private static void TextBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 如果 sender 不是 TextBox，则返回
        if (sender is not TextBox textBox) return;
        // 标记事件为已处理
        e.Handled = true;

        // 选择 TextBox 中的所有文本
        textBox.SelectAll();
    }
}