# CarouselView.Maui

CarouselView controls for .NET MAUI migrated from CarouselView.FormsPlugin for Xamarin


## Setup
* Available on NuGet: [CarouselView.Maui](http://www.nuget.org/packages/CarouselView.Maui) [![NuGet](https://img.shields.io/nuget/v/CarouselView.Maui.svg?label=NuGet)](https://www.nuget.org/packages/CarouselView.Maui)
* Add nuget package to your project.
* Add ```.UseMauiCarouselView()``` to your MauiApp builder.

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCarouselView()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
```


## Support platforms

- [x] Android      
- [x] iOS          
- [x] MacCatalyst  (Later)
- [x] Windows      (Later)


## Known Issues

1.[Android] Vertical CarouselView in VerticalStackLayout can not be swiped.


## Documentation

You can find all descriptions from the original repository 
[CarouselView.FormsPlugin](https://github.com/alexrainman/CarouselView)


## License
The MIT License (MIT) see [License file](LICENSE)

