using System.Windows;

namespace PhotoHub;

public partial class PickPersonDialog : Window
{
    public string? Selected => PeopleBox.SelectedItem as string;

    public PickPersonDialog(string prompt, IReadOnlyList<string> people)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        foreach (var p in people)
            PeopleBox.Items.Add(p);
        if (PeopleBox.Items.Count > 0)
            PeopleBox.SelectedIndex = 0;
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        if (PeopleBox.SelectedItem is null)
        {
            MessageBox.Show("Pick a person.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
