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
The MIT License (MIT) see [License file](LICENSE.md)


## Thanks
Thank JetBrains for providing DEV tools in developing. (Especially on MacOS)

![avatar](CarouselView.Sample/Resources/Images/jetbrains_logo.png)


## Release Notes

## 1.0.8

1.Fix Android Cannot Access A Disposed Object when use view list as ItemsSource.
2.Fix iOS NullPointerException when use view list as ItemSource.

## 1.0.7

1.Fix Android Java.Lang.NullPointerException convert crash when targeting to some old version android sdk. (Thanks PR by https://github.com/vitalii-smal)

## 1.0.6

1.Add .NET 8 support.

## 1.0.5

1.Fix wrong animation direction when position changed from code behind.

## 1.0.4

1.Add ItemViews to CarouselViewControl, you can fetch children views of CarouselViewControl now.

## 1.0.2

1.Fix iOS CarouselView's content can not be fullscreen issue.

## 1.0.1

1.Fix Android CarouselView's content not showing when app's first startup bug.

## 1.0.0

1.Init package for Android and iOS platforms.
