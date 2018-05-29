using System;
using System.ComponentModel;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace FileSeeker
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            ViewModel = new ViewModel(Dispatcher);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            ListViewResults.DoubleTapped += ListViewResults_DoubleTapped;
            ListViewResults.KeyDown += ListViewResults_KeyDown;
        }

        public ViewModel ViewModel { get; private set; }

        void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsBusy")
            {
                if (ViewModel.IsBusy)
                {
                    VisualStateManager.GoToState(this, "Active", true);
                }
                else
                {
                    VisualStateManager.GoToState(this, "Inactive", true);
                }
            }
        }

        private async void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ViewModel.Path = folder.Path;
            }
        }

        private void ListViewResults_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListViewResults.SelectedItem != null)
            {
                ViewModel.OpenCommand.Execute(ListViewResults.SelectedItem);
            }
        }

        private void ListViewResults_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                if (ListViewResults.SelectedItem != null)
                {
                    ViewModel.OpenCommand.Execute(ListViewResults.SelectedItem);
                }
            }
        }
    }
}
