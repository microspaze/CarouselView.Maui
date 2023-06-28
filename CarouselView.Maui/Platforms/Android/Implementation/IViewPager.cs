using System;
using CarouselView.Abstractions;

namespace CarouselView.Droid
{
    internal interface IViewPager
    {
        void SetPagingEnabled(bool enabled);
        void SetElement(CarouselViewControl element);
    }
}
