using CarouselView.Abstractions;
using Microsoft.Maui.Controls.Compatibility.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace CarouselView;

public static class AppBuilderExtensions
{
    public static MauiAppBuilder UseMauiCarouselView(this MauiAppBuilder builder)
    {

        builder
            .UseMauiCompatibility()
            .ConfigureLifecycleEvents(lifecycle =>
            {
#if ANDROID
                lifecycle.AddAndroid(b =>
                {
                    b.OnCreate((activity, state) => Droid.CarouselViewRenderer.Init());
                });
#elif IOS
                lifecycle.AddiOS(b =>
                {
                    b.FinishedLaunching((application, launchOptions) => iOS.CarouselViewRenderer.Init());
                });
#elif MACCATALYST
                lifecycle.AddiOS(b =>
                {
                    
                });
#elif WINDOWS
                lifecycle.AddWindows(b =>
                {
                    
                });
#endif
            }).ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler(typeof(CarouselViewControl), typeof(CarouselView.Droid.CarouselViewRenderer));
#elif IOS
                handlers.AddHandler(typeof(CarouselViewControl), typeof(CarouselView.iOS.CarouselViewRenderer));
#elif MACCATALYST
                
#elif WINDOWS
                
#endif
            });

        return builder;
    }
}