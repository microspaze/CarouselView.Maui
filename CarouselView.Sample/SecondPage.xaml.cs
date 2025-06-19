using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarouselView.Sample
{
    [QueryProperty(nameof(ImageSrc), "imageSrc")]
    public partial class SecondPage : ContentPage, INotifyPropertyChanged
	{
        private string _imageSrc;
        public string ImageSrc
        {
            get { return _imageSrc; }
            set {  _imageSrc = value; OnPropertyChanged(); }
        }

		public SecondPage ()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}