using System;
using System.Collections.Generic;
using Android.Content;
using Android.Content.Res;
using AndroidX.ViewPager.Widget;
using CarouselView.Abstractions;
using Com.ViewPagerIndicator;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace CarouselView.Droid
{
    public partial class CarouselViewHandler : ViewHandler<CarouselViewControl, Android.Views.View>
    {
        private CarouselViewRenderer? _renderer = null;

        public static IPropertyMapper<CarouselViewControl, CarouselViewHandler> Mapper = new PropertyMapper<CarouselViewControl, CarouselViewHandler>(ViewHandler.ViewMapper)
        {
        };

        public CarouselViewHandler() : base(Mapper)
        {
        }

        public CarouselViewHandler(IPropertyMapper mapper) : base(mapper)
        {
        }

        public CarouselViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper)
        {
        }

        public override void SetVirtualView(IView view)
        {
            _renderer = new CarouselViewRenderer(Context);
            _renderer.SetControl(view as CarouselViewControl);
            base.SetVirtualView(view);
        }

        protected override Android.Views.View CreatePlatformView()
        {
            return _renderer.SetNativeView();
        }

        protected override void DisconnectHandler(Android.Views.View platformView)
        {
            base.DisconnectHandler(platformView);
            _renderer.Dispose(true);
        }
    }
}
