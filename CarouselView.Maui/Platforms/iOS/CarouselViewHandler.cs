using CarouselView.Abstractions;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Handlers;
using UIKit;

namespace CarouselView.iOS
{
    public partial class CarouselViewHandler : ViewHandler<CarouselViewControl, UIView>
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
            _renderer = new CarouselViewRenderer(view as CarouselViewControl);
            base.SetVirtualView(view);
        }

        protected override UIView CreatePlatformView()
        {
            return _renderer.SetNativeView();
        }

        protected override void DisconnectHandler(UIView platformView)
        {
            base.DisconnectHandler(platformView);
            _renderer.Dispose(true);
        }
    }
}
