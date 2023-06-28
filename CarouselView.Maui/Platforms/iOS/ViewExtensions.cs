using UIKit;
using CoreGraphics;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
using Platform = Microsoft.Maui.Controls.Compatibility.Platform.iOS.Platform;

namespace CarouselView.iOS
{
    public static class ViewExtensions
    {
        public static UIView ToiOS(this View view, CGRect size)
        {
			if (Platform.GetRenderer(view) == null)
				Platform.SetRenderer(view, Platform.CreateRenderer(view));
            
			var vRenderer = Platform.GetRenderer(view);

			vRenderer.NativeView.Frame = size;

			vRenderer.NativeView.AutoresizingMask = UIViewAutoresizing.All;
			vRenderer.NativeView.ContentMode = UIViewContentMode.ScaleToFill;

			vRenderer.Element?.Layout (size.ToRectangle());

			var nativeView = vRenderer.NativeView;

            nativeView.SetNeedsLayout ();

            return nativeView;
        }
    }
}

