using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private List<PersonFolder> _allPeople = [];
    private ICollectionView? _peopleView;

    private void BindPeopleGrid()
    {
        _peopleView = CollectionViewSource.GetDefaultView(_allPeople);
        _peopleView.Filter = PeopleFilter;
        PeopleGrid.ItemsSource = _peopleView;
    }

    private bool PeopleFilter(object item)
    {
        if (item is not PersonFolder person)
            return false;
        var q = PeopleSearchBox.Text?.Trim() ?? "";
        return q.Length == 0 || person.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void PeopleSearch_OnChanged(object sender, TextChangedEventArgs e)
    {
        _peopleView?.Refresh();
    }

    private void UpdatePeopleSelectionStatus()
    {
        if (!IsLoaded)
            return;
        PeopleSelectionStatus.Text = _selectedPerson is null
            ? "No person selected"
            : $"Selected: {_selectedPerson.Name} ({_selectedPerson.PhotoCount} photos)";
    }
}
