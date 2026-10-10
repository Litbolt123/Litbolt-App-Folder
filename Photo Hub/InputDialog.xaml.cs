using System.Windows;

namespace PhotoHub;

public partial class InputDialog : Window
{
    public string Value => InputBox.Text;

    public InputDialog(string title, string prompt, string? initial = null)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initial ?? "";
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
