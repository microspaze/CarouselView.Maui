using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Platform;
using Rect = Microsoft.Maui.Graphics.Rect;
using View = Microsoft.Maui.Controls.View;

namespace CarouselView.Droid
{
    public static class ViewExtensions
    {
        public static Android.Views.View ToAndroid(this View view, Rect size, IMauiContext mauiContext)
        {
            var viewHandler = view.ToHandler(mauiContext);
            var viewGroup = viewHandler.PlatformView as Android.Views.View;
            var layoutParams = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            viewGroup.LayoutParameters = layoutParams;
            view.Layout(size);
            viewGroup.Layout(0, 0, (int)size.Width, (int)size.Height);
            return viewGroup;
        }

        public static void UnbindDrawables(this Android.Views.View view)
        {
            if (view == null) { return; }

            if (view.Background != null)
            {
                view.Background.Callback = null;
            }
            if (view is ViewGroup)
            {
                for (int i = 0; i < ((ViewGroup)view).ChildCount; i++)
                {
                    UnbindDrawables(((ViewGroup)view).GetChildAt(i));
                }
                if (!(view is AdapterView))
                {
                    ((ViewGroup)view).RemoveAllViews();
                }
            }
        }
    }
}

