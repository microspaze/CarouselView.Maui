using CoreGraphics;
using Microsoft.Maui;
using Microsoft.Maui.Platform;
using UIKit;

namespace CarouselView.iOS
{
    public static class ViewExtensions
    {
        public static UIView? ToiOS(this View view, CGRect size, IMauiContext mauiContext)
        {
            var viewHandler = view.ToHandler(mauiContext);
            var nativeView = viewHandler?.PlatformView as UIView;
            if (nativeView != null)
            {
                nativeView.Frame = size;
                nativeView.AutoresizingMask = UIViewAutoresizing.All;
                nativeView.ContentMode = UIViewContentMode.ScaleToFill;
                
                view.Layout(size.ToRectangle());
                
                nativeView.SetNeedsLayout();
            }

            return nativeView;
        }
    }
}

