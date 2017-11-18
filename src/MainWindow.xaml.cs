using System.Windows;
using System.Windows.Input;

namespace FileSeeker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModel vm;

        public MainWindow()
        {
            InitializeComponent();
            vm = new ViewModel() { Dispatcher = Dispatcher };
            vm.PropertyChanged += ViewModel_PropertyChanged;
            this.DataContext = vm;

            listViewResults.MouseDoubleClick += listViewResults_MouseDoubleClick;
            listViewResults.KeyDown += listViewResults_KeyDown;
        }

        void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsBusy")
            {
                if (vm.IsBusy)
                {
                    VisualStateManager.GoToElementState(this, "Active", true);
                }
                else
                {
                    VisualStateManager.GoToElementState(this, "Inactive", true);
                }
            }
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                textFolder.Text = dialog.SelectedPath;
            }
        }

        void listViewResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listViewResults.SelectedItem != null)
            {
                vm.OpenCommand.Execute(listViewResults.SelectedItem);
            }
        }

        void listViewResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (listViewResults.SelectedItem != null)
                {
                    vm.OpenCommand.Execute(listViewResults.SelectedItem);
                }
            }
        }
    }
}
