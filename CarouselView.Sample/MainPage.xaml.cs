using CarouselView.Abstractions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace CarouselView.Sample
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        MainViewModel viewModel;

        public MainPage()
        {
            InitializeComponent();

            Title = "CarouselView";

            viewModel = new MainViewModel();
            BindingContext = viewModel;
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            var itemView = CarouselHorizontal.ItemViews[CarouselHorizontal.Position];
            if (itemView is Image)
            {
                itemView.WidthRequest = 300;
                itemView.HeightRequest = 300;
            }
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"second?imageSrc={viewModel.SelectedItem.ToString()}");
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            MyItemsSource = new ObservableCollection<string>()
            {
                "c1.jpg",
                "c2.jpg",
                "c3.jpg",
            };

            PositionSelectedCommand = new Command<PositionSelectedEventArgs>((e) =>
            {
                Debug.WriteLine("Position " + e.NewValue + " selected.");
                Debug.Write(this.SelectedItem);
            });

            ScrolledCommand = new Command<Abstractions.ScrolledEventArgs>((e) =>
            {
                Debug.WriteLine("Scrolled to " + e.NewValue + " percent.");
                Debug.WriteLine("Direction = " + e.Direction);
            });
        }

        ObservableCollection<string> _myItemsSource;
        public ObservableCollection<string> MyItemsSource
        {
            set
            {
                _myItemsSource = value;
                OnPropertyChanged("MyItemsSource");
            }
            get
            {
                return _myItemsSource;
            }
        }

        object _selectedItem;
        public object SelectedItem
        {
            set
            {
                _selectedItem = value;
                OnPropertyChanged("SelectedItem");
            }
            get
            {
                return _selectedItem;
            }
        }

        public Command<PositionSelectedEventArgs> PositionSelectedCommand { protected set; get; }

        public Command<Abstractions.ScrolledEventArgs> ScrolledCommand { protected set; get; }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}