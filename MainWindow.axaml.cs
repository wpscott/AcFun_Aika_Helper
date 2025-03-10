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
///     TextBox ��Ϊ�ľ�̬�࣬�����������Ժ��¼��������
/// </summary>
public static class TextBoxBehavior
{
    /// <summary>
    ///     ���� SelectAllOnFocus �������ԡ�
    /// </summary>
    public static readonly AttachedProperty<bool> SelectAllOnFocusProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "SelectAllOnFocus",
            typeof(TextBoxBehavior));

    /// <summary>
    ///     ��ȡ SelectAllOnFocus �������Ե�ֵ��
    /// </summary>
    /// <param name="textBox">TextBox �ؼ���</param>
    /// <returns>����ֵ��</returns>
    public static bool GetSelectAllOnFocus(TextBox textBox)
    {
        return textBox.GetValue(SelectAllOnFocusProperty);
    }

    /// <summary>
    ///     ���� SelectAllOnFocus �������Ե�ֵ��
    /// </summary>
    /// <param name="textBox">TextBox �ؼ���</param>
    /// <param name="value">����ֵ��</param>
    public static void SetSelectAllOnFocus(TextBox textBox, bool value)
    {
        textBox.SetValue(SelectAllOnFocusProperty, value);

        if (value)
        {
            // ���� GotFocus �¼�
            textBox.GotFocus += TextBox_GotFocus;
            textBox.PointerPressed += TextBox_PointerPressed;
            textBox.PointerReleased += TextBox_PointerReleased;
        }
        else
        {
            // ȡ������ GotFocus �¼�
            textBox.GotFocus -= TextBox_GotFocus;
            textBox.PointerPressed -= TextBox_PointerPressed;
            textBox.PointerReleased -= TextBox_PointerReleased;
        }
    }

    /// <summary>
    ///     GotFocus �¼��Ĵ������
    /// </summary>
    /// <param name="sender">�¼������ߡ�</param>
    /// <param name="e">�¼�������</param>
    private static void TextBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        // ��� sender ���� TextBox���򷵻�
        if (sender is not TextBox textBox) return;
        // ����¼�Ϊ�Ѵ���
        e.Handled = true;

        // ѡ�� TextBox �е������ı�
        textBox.SelectAll();

        // ��ȡ�������ڵļ�����
        var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
        // ��ѡ�е��ı����õ�������
        clipboard?.SetTextAsync(textBox.SelectedText);
    }

    /// <summary>
    ///     PointerPressed �¼��Ĵ������
    /// </summary>
    /// <param name="sender">�¼������ߡ�</param>
    /// <param name="e">�¼�������</param>
    private static void TextBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // ��� sender ���� TextBox���򷵻�
        if (sender is not TextBox textBox) return;
        // ����¼�Ϊ�Ѵ���
        e.Handled = true;

        // ѡ�� TextBox �е������ı�
        textBox.SelectAll();
    }

    /// <summary>
    ///     PointerReleased �¼��Ĵ������
    /// </summary>
    /// <param name="sender">�¼������ߡ�</param>
    /// <param name="e">�¼�������</param>
    private static void TextBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // ��� sender ���� TextBox���򷵻�
        if (sender is not TextBox textBox) return;
        // ����¼�Ϊ�Ѵ���
        e.Handled = true;

        // ѡ�� TextBox �е������ı�
        textBox.SelectAll();
    }
}