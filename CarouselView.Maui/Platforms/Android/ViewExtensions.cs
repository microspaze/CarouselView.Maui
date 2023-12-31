﻿using Android.Views;
using Android.Content;
using Android.Widget;
using Android.Graphics;
using Platform = Microsoft.Maui.Controls.Compatibility.Platform.Android.Platform;
using Rect = Microsoft.Maui.Graphics.Rect;
using View = Microsoft.Maui.Controls.View;

namespace CarouselView.Droid
{
    public static class ViewExtensions
    {
        public static Android.Views.View ToAndroid(this View view, Rect size, Context _context)
        {
            // NullReferenceException during swiping #314 (ScrollView)
            if (Platform.GetRenderer(view) == null || Platform.GetRenderer(view)?.Tracker == null || view is Microsoft.Maui.Controls.ListView)
                Platform.SetRenderer(view, Platform.CreateRendererWithContext(view, _context));

            var vRenderer = Platform.GetRenderer(view);
            
            var viewGroup = vRenderer.View;

            vRenderer.Tracker?.UpdateLayout ();
            var layoutParams = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            viewGroup.LayoutParameters = layoutParams;
            view.Layout(size);
            viewGroup.Layout(0, 0, (int)size.Width, (int)size.Height);

            /*if (view is Xamarin.Forms.ListView)
            {
                var list = (Xamarin.Forms.ListView)view;
                if (list.SelectedItem != null)
                {
                    list.ScrollTo(list.SelectedItem, ScrollToPosition.Center, false);
                }
            }*/

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

