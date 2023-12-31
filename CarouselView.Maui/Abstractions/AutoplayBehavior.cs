﻿using System;

namespace CarouselView.Abstractions
{
    public sealed class AutoplayBehavior : Behavior<CarouselViewControl>
    {
        private CarouselViewControl attachedCarousel;

        Timer timer;

        public int Delay { get; set; }

        protected override void OnAttachedTo(CarouselViewControl bindable)
        {
            base.OnAttachedTo(bindable);

            attachedCarousel = bindable;

            StartTimer();
        }

        public void StartTimer()
        {
            timer = new Timer(Autoplay, attachedCarousel.Position, Delay, Delay);
        }

        public void StopTimer()
        {
            if (timer != null)
            {
                timer.Cancel();
                timer.Dispose();
            }
            timer = null;
        }

        protected override void OnDetachingFrom(CarouselViewControl bindable)
        {
            StopTimer();
            base.OnDetachingFrom(bindable);
        }

        private void Autoplay(object args)
        {
            attachedCarousel.Dispatcher.Dispatch(() =>
            {
                if (attachedCarousel.ItemsSource != null && attachedCarousel.AutoplayInterval > 0 && attachedCarousel.ItemsSource?.GetCount() > 1)
                {
                    if (attachedCarousel.Position < attachedCarousel.ItemsSource.GetCount() - 1 || DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.WinUI)
                    {
                        attachedCarousel.Position++;
                    }
                    else
                    {
                        attachedCarousel.Position = 0;
                    }
                }
            });
        }
    }
}
