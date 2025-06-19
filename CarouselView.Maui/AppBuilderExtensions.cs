using CarouselView.Abstractions;
using Microsoft.Maui.LifecycleEvents;

namespace CarouselView;

public static class AppBuilderExtensions
{
    public static MauiAppBuilder UseMauiCarouselView(this MauiAppBuilder builder)
    {

        builder
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
                handlers.AddHandler(typeof(CarouselViewControl), typeof(CarouselView.Droid.CarouselViewHandler));
#elif IOS
                handlers.AddHandler(typeof(CarouselViewControl), typeof(CarouselView.iOS.CarouselViewHandler));
#elif MACCATALYST
                
#elif WINDOWS
                
#endif
            });

        return builder;
    }
}