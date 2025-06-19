using CarouselView.Abstractions;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Platform;
using System.Collections.Specialized;
using System.ComponentModel;
using UIKit;

/*
 * Significant Memory Leak for iOS when using custom layout for page content #125
 * 
 * The problem:
 * 
 * To facilitate smooth swiping, UIPageViewController keeps a ghost copy of the pages in a collection named
 * ChildViewControllers.
 * This collection is handled internally by UIPageViewController to keep a maximun of 3 items, but
 * when a custom view is used from Xamarin.Forms side, the views hang in memory and are not collected no matter if
 * internally the UIViewController is disposed by UIPageViewController.
 * 
 * Fix explained:
 * 
 * Some code has been added to CreateViewController to return
 * a child controller if exists in ChildViewControllers.
 * Also Dispose has been implemented in ViewContainer to release the custom views.
 * Dispose is called in the finalizer thread (UI) so the code to release the views from memory has been
 * wrapped in InvokeOnMainThread.
 */

namespace CarouselView.iOS
{
    /// <summary>
    /// CarouselView Renderer
    /// </summary>
    public class CarouselViewRenderer
    {
        bool carouselOrientationChanged;

        UIPageViewController pageController;
        UIPageControl pageControl;
        UIScrollView scrollView;

        UIButton prevBtn;
        UIButton nextBtn;

        CarouselViewControl _control;
        IMauiContext? _mauiContext => Application.Current?.Windows[0]?.Handler?.MauiContext;
        bool _disposed;

        // A local copy of ItemsSource so we can use CollectionChanged events
        List<object> Source;

        List<ViewContainer> ChildViewControllers;

        int Count => Source?.Count ?? 0;

        double ElementWidth;
        double ElementHeight;

        // To avoid triggering Position changed more than once
        bool isChangingPosition;
        bool isChangingSelectedItem;

        public CarouselViewRenderer(CarouselViewControl control)
        {
            if (_control == null)
            {
                // Instantiate the native control and assign it to the Control property with
                // the SetNativeControl method (called when Height BP changes)
                carouselOrientationChanged = true;
            }

            _control = control;
            _control.PropertyChanged += OnElementPropertyChanged;
            _control.SizeChanged += OnElementSizeChanged;

            // Configure the control and subscribe to event handlers
            if (_control.ItemsSource != null && _control.ItemsSource is INotifyCollectionChanged)
            {
                ((INotifyCollectionChanged)_control.ItemsSource).CollectionChanged += ItemsSource_CollectionChanged;
            }
        }

        public void LayoutSubviews()
        {
            //base.LayoutSubviews();
            
            //Reset safeAreaInsets to UIEdgeInsets.Zero
            if (pageController == null || pageController.View == null) { return; }
            var safeAreaInsets = pageController.View.SafeAreaInsets;
            if (safeAreaInsets != UIEdgeInsets.Zero)
            {
                pageController.AdditionalSafeAreaInsets = new UIEdgeInsets(-safeAreaInsets.Top, -safeAreaInsets.Left, -safeAreaInsets.Bottom, -safeAreaInsets.Right);
            }
        }

        void AddAutoplayBehavior()
        {
            if (_control.InfiniteScrolling && _control.AutoplayInterval > 0 && _control.ItemsSource?.GetCount() > 1)
            {
                _control.Behaviors.Add(new AutoplayBehavior() { Delay = _control.AutoplayInterval * 1000 });
            }
        }

        void RemoveAutoplayBehavior()
        {
            if (_control.Behaviors.FirstOrDefault((arg) => arg is AutoplayBehavior) is AutoplayBehavior autoplay)
            {
                autoplay.StopTimer();
                _control.Behaviors.Remove(autoplay);
            }
        }

        async void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_control == null || pageController == null || Source == null) return;

            RemoveAutoplayBehavior();

            // NewItems contains the item that was added.
            // If NewStartingIndex is not -1, then it contains the index where the new item was added.
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                InsertPage(_control?.ItemsSource?.GetItem(e.NewStartingIndex), e.NewStartingIndex);
            }

            // OldItems contains the item that was removed.
            // If OldStartingIndex is not -1, then it contains the index where the old item was removed.
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                await RemovePage(e.OldStartingIndex);
            }

            // OldItems contains the moved item.
            // OldStartingIndex contains the index where the item was moved from.
            // NewStartingIndex contains the index where the item was moved to.
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                Source.RemoveAt(e.OldStartingIndex);

                Source.InsertRange(e.NewStartingIndex, e.OldItems.Cast<object>());

                var firstViewController = CreateViewController(e.NewStartingIndex);

                pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
                {
                    isChangingPosition = true;
                    _control.Position = e.NewStartingIndex;
                    isChangingPosition = false;

                    SetArrowsVisibility();
                    SetIndicatorsCurrentPage();

                    SendPositionSelected();
                });
            }

            // NewItems contains the replacement item.
            // NewStartingIndex and OldStartingIndex are equal, and if they are not -1,
            // then they contain the index where the item was replaced.
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                // Remove controller from ChildViewControllers
                if (ChildViewControllers != null)
                {
                    ChildViewControllers.RemoveAll(c => c.Tag == Source[e.OldStartingIndex]);
                }

                Source[e.OldStartingIndex] = e.NewItems[0];

                var firstViewController = CreateViewController(_control.Position);

                pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
                {
                });
            }

            // No other properties are valid.
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                SetPosition();
                SetNativeView(); // ResetAdapter won't work as last view controller won't be removed from ChildViewControllers
                SendPositionSelected();
            }

            AddAutoplayBehavior();
        }

        void SendPositionSelected()
        {
            isChangingSelectedItem = true;
            _control.SelectedItem = _control.ItemsSource?.GetItem(_control.Position);
            isChangingSelectedItem = false;
            _control.SendPositionSelected();
            _control.PositionSelectedCommand?.Execute(new PositionSelectedEventArgs() { NewValue = _control.Position });
        }

        void ResetAdapter()
        {
            CleanUpPageController();

            Source = _control.ItemsSource != null ? new List<object>(_control.ItemsSource.GetList()) : null;

            SetArrowsVisibility();

            SetIndicators();

            if (Source != null && Source?.Count > 0)
            {
                var firstViewController = CreateViewController(_control.Position);

                pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
                {
                });
            }
        }

        void OnElementSizeChanged(object? sender, EventArgs e)
        {
            if (_control == null) return;

            var rect = this._control.Bounds;
            // To avoid extra DataTemplate instantiations #158
            if (rect.Height > 0)
            {
                RemoveAutoplayBehavior();

                ElementWidth = rect.Width;
                ElementHeight = rect.Height;
                SetNativeView();
                SendPositionSelected();

                AddAutoplayBehavior();
            }
        }

        // Fix #129 CarouselViewControl not rendered when loading a page from memory bug
        // Fix #157 CarouselView Binding breaks when returning to Page bug duplicate
        public void MovedToSuperview()
        {
            //if (Control == null)
            //{
            //    Element_SizeChanged(_control, null);
            //}

            //base.MovedToSuperview();
        }

        public void MovedToWindow()
        {
            //if (Control == null)
            //{
            //    Element_SizeChanged(_control, null);
            //}

            //base.MovedToWindow();
        }

        protected void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            //base.OnElementPropertyChanged(sender, e);

            if (_control == null || pageController == null) return;

            switch (e.PropertyName)
            {
                case "Renderer":
                    // Fix for issues after recreating the control #86
                    _prevPosition = _control.Position;
                    break;
                case "IsVisible":
                    pageController.View.Hidden = !_control.IsVisible;
                    break;
                case "Orientation":
                    RemoveAutoplayBehavior();
                    carouselOrientationChanged = true;
                    SetNativeView();
                    SendPositionSelected();
                    AddAutoplayBehavior();
                    break;
                case "BackgroundColor":
                    pageController.View.BackgroundColor = _control.BackgroundColor?.ToPlatform();
                    break;
                case "IsSwipeEnabled":
                    SetIsSwipeEnabled();
                    break;
                case "IndicatorsTintColor":
                    SetIndicatorsTintColor();
                    break;
                case "CurrentPageIndicatorTintColor":
                    SetCurrentPageIndicatorTintColor();
                    break;
                case "IndicatorsShape":
                    SetIndicatorsShape();
                    break;
                case "ShowIndicators":
                    SetIndicatorsVisibility();
                    break;
                case "ItemsSource":
                    RemoveAutoplayBehavior();
                    SetPosition();
                    ResetAdapter();
                    SendPositionSelected();
                    if (_control.ItemsSource != null && _control.ItemsSource is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)_control.ItemsSource).CollectionChanged += ItemsSource_CollectionChanged;
                    }
                    AddAutoplayBehavior();
                    break;
                case "ItemTemplate":
                    RemoveAutoplayBehavior();
                    ResetAdapter();
                    SendPositionSelected();
                    AddAutoplayBehavior();
                    break;
                case "Position":
                    if (!isChangingPosition)
                    {
                        SetCurrentPage(_control.Position);
                    }
                    break;
                case "SelectedItem":
                    if (!isChangingSelectedItem)
                    {
                        // NEW
                        _control.Position = _control.ItemsSource.GetList().IndexOf(_control.SelectedItem);
                    }
                    break;
                case "ShowArrows":
                    SetArrowsVisibility();
                    break;
                case "ArrowsBackgroundColor":
                    if (prevBtn == null || nextBtn == null) return;
                    prevBtn.BackgroundColor = _control.ArrowsBackgroundColor?.ToPlatform();
                    nextBtn.BackgroundColor = _control.ArrowsBackgroundColor?.ToPlatform();
                    break;
                case "ArrowsTintColor":
                    if (prevBtn == null || nextBtn == null) return;
                    var prevArrow = (UIImageView)prevBtn.Subviews[0];
                    prevArrow.TintColor = _control.ArrowsTintColor?.ToPlatform();
                    var nextArrow = (UIImageView)nextBtn.Subviews[0];
                    nextArrow.TintColor = _control.ArrowsTintColor?.ToPlatform();
                    break;
                case "ArrowsTransparency":
                    if (prevBtn == null || nextBtn == null) return;
                    prevBtn.Alpha = _control.ArrowsTransparency;
                    nextBtn.Alpha = _control.ArrowsTransparency;
                    break;
                case "InfiniteScrolling":
                    // Do nothing
                    RemoveAutoplayBehavior();
                    AddAutoplayBehavior();
                    break;
                case "AutoplayInterval":
                    RemoveAutoplayBehavior();
                    AddAutoplayBehavior();
                    break;
                case "HorizontalIndicatorsPosition":
                    SetIndicators();
                    break;
                case "VerticalIndicatorsPosition":
                    SetIndicators();
                    break;
                case "ArrowsSize":
                    SetArrows();
                    break;
                case "ArrowsParentMargin":
                    SetArrows();
                    break;
                case "HorizontalArrowsPosition":
                    SetArrows();
                    break;
                case "VerticalArrowsPosition":
                    SetArrows();
                    break;
                case "PrevArrowTemplate":
                    SetArrows();
                    break;
                case "NextArrowTemplate":
                    SetArrows();
                    break;
            }
        }

        #region adapter callbacks

        void PageController_DidFinishAnimating(object sender, UIPageViewFinishedAnimationEventArgs e)
        {
            if (e.Completed)
            {
                var controller = (ViewContainer)pageController.ViewControllers[0];
                var position = Source.IndexOf(controller.Tag);
                isChangingPosition = true;
                _control.Position = position;
                isChangingPosition = false;
                _prevPosition = position;
                SetArrowsVisibility();
                SetIndicatorsCurrentPage();
                SendPositionSelected();
            }
        }

        #endregion

        public UIView SetNativeView()
        {
            // Rotation bug(iOS) #115 Fix
            CleanUpPageController();

            if (carouselOrientationChanged)
            {
                var interPageSpacing = (float)_control.InterPageSpacing;

                // Orientation BP
                var orientation = (UIPageViewControllerNavigationOrientation)_control.Orientation;

                // InterPageSpacing BP
                pageController = new UIPageViewController(UIPageViewControllerTransitionStyle.Scroll,
                                                          orientation, UIPageViewControllerSpineLocation.None, interPageSpacing);
                pageController.View.ClipsToBounds = true;
            }

            Source = _control.ItemsSource != null ? new List<object>(_control.ItemsSource.GetList()) : null;

            // BackgroundColor BP
            pageController.View.BackgroundColor = _control.BackgroundColor?.ToPlatform();

            #region adapter

            pageController.GetPreviousViewController = (pageViewController, referenceViewController) =>
            {
                var controller = (ViewContainer)referenceViewController;

                if (controller != null)
                {
                    var position = Source.IndexOf(controller.Tag);

                    // Determine if we are on the first page
                    if (position == 0)
                    {
                        if (_control.InfiniteScrolling && _control.ItemsSource.GetCount() > 1)
                        {
                            int previousPageIndex = Source.Count - 1;
                            return CreateViewController(previousPageIndex);
                        }
                        else
                        {
                            // We are on the first page, so there is no need for a controller before that
                            return null;
                        }
                    }
                    else
                    {
                        int previousPageIndex = position - 1;
                        return CreateViewController(previousPageIndex);
                    }
                }
                else
                {
                    return null;
                }
            };

            pageController.GetNextViewController = (pageViewController, referenceViewController) =>
            {
                var controller = (ViewContainer)referenceViewController;

                if (controller != null)
                {
                    var position = Source.IndexOf(controller.Tag);

                    // Determine if we are on the last page
                    if (position == Count - 1)
                    {
                        if (_control.InfiniteScrolling && _control.ItemsSource.GetCount() > 1)
                        {
                            int nextPageIndex = 0;
                            return CreateViewController(nextPageIndex);
                        }
                        else
                        {
                            // We are on the last page, so there is no need for a controller after that
                            return null;
                        }
                    }
                    else
                    {
                        int nextPageIndex = position + 1;
                        return CreateViewController(nextPageIndex);
                    }
                }
                else
                {
                    return null;
                }
            };

            pageController.DidFinishAnimating += PageController_DidFinishAnimating;

            foreach (var view in pageController?.View.Subviews)
            {
                scrollView = view as UIScrollView;

                if (scrollView != null)
                {
                    scrollView.Scrolled += Scroller_Scrolled;
                    scrollView.DraggingStarted += Scroller_DraggingStarted;
                    scrollView.DraggingEnded += Scroller_DraggingEnded;

                    if (_control.GestureRecognizers.FirstOrDefault((arg) => arg is SwipeGestureRecognizer) is SwipeGestureRecognizer swipe)
                    {
                        if (_control.IsSwipeEnabled)
                        {
                            var command = swipe.Command;
                            var param = swipe.CommandParameter;

                            var swipe1 = new UISwipeGestureRecognizer((s1) =>
                            {
                                if (swipe.Direction.ToString().Contains(s1.Direction.ToString()))
                                {
                                    command?.Execute(param);
                                }
                            });

                            swipe1.Direction = _control.Orientation == CarouselViewOrientation.Horizontal ? UISwipeGestureRecognizerDirection.Up : UISwipeGestureRecognizerDirection.Left;

                            var swipe2 = new UISwipeGestureRecognizer((s2) =>
                            {
                                if (swipe.Direction.ToString().Contains(s2.Direction.ToString()))
                                {
                                    command?.Execute(param);
                                }
                            });

                            swipe2.Direction = _control.Orientation == CarouselViewOrientation.Horizontal ? UISwipeGestureRecognizerDirection.Down : UISwipeGestureRecognizerDirection.Right;

                            scrollView.AddGestureRecognizer(swipe1);
                            scrollView.AddGestureRecognizer(swipe2); 
                        }

                        _control.GestureRecognizers.Remove(swipe);
                    }
                }
            }

            #endregion

            // IsSwipeEnabled BP
            SetIsSwipeEnabled();

            if (Source != null && Source?.Count > 0)
            {
                var firstViewController = CreateViewController(_control.Position);

                pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
                {
                });
            }

            //SetNativeControl(pageController.View);

            // ARROWS
            SetArrows();

            // INDICATORS
            SetIndicators();

            SetIndicatorsVisibility();

            return pageController.View;
        }

        void SetIsSwipeEnabled()
        {
            if (scrollView != null)
            {
                scrollView.ScrollEnabled = _control.IsSwipeEnabled;
                //scrollView.Bounces = _control.IsSwipeEnabled;
            }
        }

        double percentCompleted;
        nfloat prevPoint;
        ScrollDirection direction;

        void Scroller_Scrolled(object sender, EventArgs e)
        {
            // Added safety to help resolve issue #404
            if (_control == null) return;

            //var scrollView = (UIScrollView)sender;
            var point = scrollView.ContentOffset;

            double currentPercentCompleted;

            if (_control.Orientation == CarouselViewOrientation.Horizontal)
            {
                currentPercentCompleted = Math.Floor((Math.Abs(point.X - pageController.View.Frame.Size.Width) / pageController.View.Frame.Size.Width) * 100);
                direction = prevPoint > point.X ? ScrollDirection.Left : ScrollDirection.Right;
                prevPoint = point.X;
            }
            else
            {
                currentPercentCompleted = Math.Floor((Math.Abs(point.Y - pageController.View.Frame.Size.Height) / pageController.View.Frame.Size.Height) * 100);
                direction = prevPoint > point.Y ? ScrollDirection.Up : ScrollDirection.Down;
                prevPoint = point.Y;
            }

            if (currentPercentCompleted <= 100 && currentPercentCompleted > percentCompleted)
            {
                var reportedPercentCompleted = currentPercentCompleted;

                if (direction == ScrollDirection.Left || direction == ScrollDirection.Up)
                {
                    reportedPercentCompleted = -reportedPercentCompleted;
                }

                _control.SendScrolled(reportedPercentCompleted, direction);
                _control.ScrolledCommand?.Execute(new Abstractions.ScrolledEventArgs()
                {
                    NewValue = reportedPercentCompleted,
                    Direction = direction
                });
                percentCompleted = currentPercentCompleted;
            }
            else
            {
                percentCompleted = 0;
            }
        }

        void Scroller_DraggingStarted(object sender, EventArgs e)
        {
            RemoveAutoplayBehavior();
        }

        // No executing if IsSwipeEnabled = false
        void Scroller_DraggingEnded(object sender, DraggingEventArgs e)
        {
            AddAutoplayBehavior();

            if (_control.Position == _control.ItemsSource.GetCount() - 1 && !_control.InfiniteScrolling && (direction == ScrollDirection.Right || direction == ScrollDirection.Down))
            {
                _control.SendLoadMore();
                _control.LoadMoreCommand?.Execute(null);
            }
        }

        void SetPosition()
        {
            isChangingPosition = true;
            if (_control.ItemsSource != null)
            {
                if (_control.Position > _control.ItemsSource.GetCount() - 1)
                {
                    _control.Position = _control.ItemsSource.GetCount() - 1;
                }
                if (_control.Position == -1)
                {
                    _control.Position = 0;
                }
            }
            else
            {
                _control.Position = 0;
            }
            _prevPosition = _control.Position;
            isChangingPosition = false;
        }

        void SetArrows()
        {
            CleanUpArrows();

            if (_control.ShowArrows)
            {
                var o = _control.Orientation == CarouselViewOrientation.Horizontal ? "H" : "V";
                var formatOptions = _control.Orientation == CarouselViewOrientation.Horizontal ? NSLayoutFormatOptions.AlignAllCenterY : NSLayoutFormatOptions.AlignAllCenterX;

                var w = _control.Orientation == CarouselViewOrientation.Horizontal ? _control.ArrowsSize + 3 : _control.ArrowsSize + 19;
                var h = _control.Orientation == CarouselViewOrientation.Horizontal ? _control.ArrowsSize + 19 : _control.ArrowsSize + 3;
                var margin = _control.ArrowsParentMargin;

                if (prevBtn == null)
                {
                    prevBtn = new UIButton();
                    prevBtn.TranslatesAutoresizingMaskIntoConstraints = false;
                    prevBtn.Alpha = _control.ArrowsTransparency;

                    if (_control.PrevArrowTemplate == null)
                    { 
                        prevBtn.BackgroundColor = _control.ArrowsBackgroundColor?.ToPlatform();
                        
                        var prevArrow = new UIImageView();
                        var prevArrowImage = new UIImage(_control.Orientation == CarouselViewOrientation.Horizontal ? "prev.png" : "up.png");
                        prevArrow.Image = prevArrowImage.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
                        prevArrow.TranslatesAutoresizingMaskIntoConstraints = false;
                        prevArrow.TintColor = _control.ArrowsTintColor?.ToPlatform();

                        prevBtn.AddSubview(prevArrow);

                        var prevViewsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { prevBtn, prevArrow }, new NSObject[] { new NSString("superview"), new NSString("prevArrow") });
                        prevBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("[prevArrow(==" + _control.ArrowsSize + ")]", 0, new NSDictionary(), prevViewsDictionary));
                        prevBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[prevArrow(==" + _control.ArrowsSize + ")]", 0, new NSDictionary(), prevViewsDictionary));
                        prevBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[prevArrow]-(2)-|", 0, new NSDictionary(), prevViewsDictionary));
                        prevBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[superview]-(<=1)-[prevArrow]", formatOptions, new NSDictionary(), prevViewsDictionary));
                    }
                    else
                    {
                        var template = (View)_control.PrevArrowTemplate.CreateContent();
                        w = (int)template.WidthRequest;
                        h = (int)template.HeightRequest;

                        var rect = new CGRect(0, 0, w, h);
                        var prevArrow = template.ToiOS(rect, _mauiContext);
                        prevArrow.TranslatesAutoresizingMaskIntoConstraints = false;

                        prevBtn.AddSubview(prevArrow);

                        var prevViewsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { prevBtn, prevArrow }, new NSObject[] { new NSString("superview"), new NSString("prevArrow") });
                        prevBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("[prevArrow(==" + w + ")]", 0, new NSDictionary(), prevViewsDictionary));
                        prevBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[prevArrow(==" + h + ")]", 0, new NSDictionary(), prevViewsDictionary));
                    }

                    prevBtn.TouchUpInside += PrevBtn_TouchUpInside;

                    pageController.View.AddSubview(prevBtn);

                    var btnsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { pageController.View, prevBtn }, new NSObject[] { new NSString("superview"), new NSString("prevBtn") });

                    pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:[prevBtn(==" + w + ")]", 0, new NSDictionary(), btnsDictionary));
                    pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[prevBtn(==" + h + ")]", 0, new NSDictionary(), btnsDictionary));
                    pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":|-" + margin + "-[prevBtn]", 0, new NSDictionary(), btnsDictionary));

                    if (_control.Orientation == CarouselViewOrientation.Horizontal)
                    {
                        switch (_control.HorizontalArrowsPosition)
                        {
                            case HorizontalArrowsPosition.Center:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[superview]-(<=1)-[prevBtn]", formatOptions, new NSDictionary(), btnsDictionary));
                                break;
                            case HorizontalArrowsPosition.Bottom:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[prevBtn]-" + margin + "-|", NSLayoutFormatOptions.AlignAllBottom, new NSDictionary(), btnsDictionary));
                                break;
                            case HorizontalArrowsPosition.Top:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:|-" + margin + "-[prevBtn]", NSLayoutFormatOptions.AlignAllTop, new NSDictionary(), btnsDictionary));
                                break;
                        }
                    }
                    else
                    {
                        switch (_control.VerticalArrowsPosition)
                        {
                            case VerticalArrowsPosition.Center:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[superview]-(<=1)-[prevBtn]", formatOptions, new NSDictionary(), btnsDictionary));
                                break;
                            case VerticalArrowsPosition.Left:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:|-" + margin + "-[prevBtn]", NSLayoutFormatOptions.AlignAllLeft, new NSDictionary(), btnsDictionary));
                                break;
                            case VerticalArrowsPosition.Right:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:[prevBtn]-" + margin + "-|", NSLayoutFormatOptions.AlignAllRight, new NSDictionary(), btnsDictionary));
                                break;
                        }
                    }
                }

                if (nextBtn == null)
                {
                    nextBtn = new UIButton();
                    nextBtn.TranslatesAutoresizingMaskIntoConstraints = false;
                    nextBtn.Alpha = _control.ArrowsTransparency;

                    if (_control.NextArrowTemplate == null)
                    {
                        nextBtn.BackgroundColor = _control.ArrowsBackgroundColor?.ToPlatform();

                        var nextArrow = new UIImageView();
                        var nextArrowImage = new UIImage(_control.Orientation == CarouselViewOrientation.Horizontal ? "next.png" : "down.png");
                        nextArrow.Image = nextArrowImage.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
                        nextArrow.TranslatesAutoresizingMaskIntoConstraints = false;
                        nextArrow.TintColor = _control.ArrowsTintColor?.ToPlatform();

                        nextBtn.AddSubview(nextArrow);

                        var nextViewsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { nextBtn, nextArrow }, new NSObject[] { new NSString("superview"), new NSString("nextArrow") });
                        nextBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("[nextArrow(==" + _control.ArrowsSize + ")]", 0, new NSDictionary(), nextViewsDictionary));
                        nextBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[nextArrow(==" + _control.ArrowsSize + ")]", 0, new NSDictionary(), nextViewsDictionary));
                        nextBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":|-(2)-[nextArrow]", 0, new NSDictionary(), nextViewsDictionary));
                        nextBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[superview]-(<=1)-[nextArrow]", formatOptions, new NSDictionary(), nextViewsDictionary));
                    }
                    else
                    {
                        var template = (View)_control.NextArrowTemplate.CreateContent();
                        w = (int)template.WidthRequest;
                        h = (int)template.HeightRequest;

                        var rect = new CGRect(0, 0, w, h);
                        var nextArrow = template.ToiOS(rect, _mauiContext);
                        nextArrow.TranslatesAutoresizingMaskIntoConstraints = false;

                        nextBtn.AddSubview(nextArrow);

                        var nextViewsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { nextBtn, nextArrow }, new NSObject[] { new NSString("superview"), new NSString("nextArrow") });
                        nextBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("[nextArrow(==" + w + ")]", 0, new NSDictionary(), nextViewsDictionary));
                        nextBtn.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[nextArrow(==" + h + ")]", 0, new NSDictionary(), nextViewsDictionary));
                    }

                    nextBtn.TouchUpInside += NextBtn_TouchUpInside;

                    pageController.View.AddSubview(nextBtn);

                    var btnsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { pageController.View, nextBtn }, new NSObject[] { new NSString("superview"), new NSString("nextBtn") });

                    pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:[nextBtn(==" + w + ")]", 0, new NSDictionary(), btnsDictionary));
                    pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[nextBtn(==" + h + ")]", 0, new NSDictionary(), btnsDictionary));
                    pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[nextBtn]-" + margin + "-|", 0, new NSDictionary(), btnsDictionary));

                    if (_control.Orientation == CarouselViewOrientation.Horizontal)
                    {
                        switch (_control.HorizontalArrowsPosition)
                        {
                            case HorizontalArrowsPosition.Center:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[superview]-(<=1)-[nextBtn]", formatOptions, new NSDictionary(), btnsDictionary));
                                break;
                            case HorizontalArrowsPosition.Bottom:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[nextBtn]-" + margin + "-|", NSLayoutFormatOptions.AlignAllBottom, new NSDictionary(), btnsDictionary));
                                break;
                            case HorizontalArrowsPosition.Top:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:|-" + margin + "-[nextBtn]", NSLayoutFormatOptions.AlignAllTop, new NSDictionary(), btnsDictionary));
                                break;
                        }
                    }
                    else
                    {
                        switch (_control.VerticalArrowsPosition)
                        {
                            case VerticalArrowsPosition.Center:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat(o + ":[superview]-(<=1)-[nextBtn]", formatOptions, new NSDictionary(), btnsDictionary));
                                break;
                            case VerticalArrowsPosition.Left:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:|-" + margin + "-[nextBtn]", NSLayoutFormatOptions.AlignAllLeft, new NSDictionary(), btnsDictionary));
                                break;
                            case VerticalArrowsPosition.Right:
                                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:[nextBtn]-" + margin + "-|", NSLayoutFormatOptions.AlignAllRight, new NSDictionary(), btnsDictionary));
                                break;
                        }
                    }
                }

                SetArrowsVisibility();
            }
            else
            {
                if (prevBtn == null || nextBtn == null) return;
                prevBtn.Hidden = true;
                nextBtn.Hidden = true;
            }
        }

        private bool? _prevBtnClicked = null;

        void PrevBtn_TouchUpInside(object sender, EventArgs e)
        {
            RemoveAutoplayBehavior();

            if (_control.Position > 0)
            {
                _prevBtnClicked = true;
                _control.Position--;
            }
            else if (_control.InfiniteScrolling)
            {
                var position = _control.ItemsSource.GetCount() - 1;
                var lastViewController = CreateViewController(position);
                _prevPosition = position;

                pageController.SetViewControllers(new[] { lastViewController }, UIPageViewControllerNavigationDirection.Reverse, true, s =>
                {
                    isChangingPosition = true;
                    _control.Position = position;
                    isChangingPosition = false;

                    SetIndicatorsCurrentPage();

                    // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
                    SendPositionSelected();
                });
            }

            AddAutoplayBehavior();
        }

        void NextBtn_TouchUpInside(object sender, EventArgs e)
        {
            RemoveAutoplayBehavior();

            if (_control.Position < _control.ItemsSource?.GetCount() - 1)
            {
                _prevBtnClicked = false;
                _control.Position++;
            }
            else if (_control.InfiniteScrolling)
            {
                var firstViewController = CreateViewController(0);
                _prevPosition = 0;

                pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, true, s =>
                {
                    isChangingPosition = true;
                    _control.Position = 0;
                    isChangingPosition = false;

                    SetIndicatorsCurrentPage();

                    // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
                    SendPositionSelected();
                });
            }

            AddAutoplayBehavior();
        }

        void SetArrowsVisibility()
        {
            if (prevBtn == null || nextBtn == null) return;
            prevBtn.Hidden = (_control.Position == 0 && !_control.InfiniteScrolling) || (_control.InfiniteScrolling && _control.ItemsSource?.GetCount() < 2) || _control.ItemsSource?.GetCount() == 0 || _control.ItemsSource == null || !_control.ShowArrows;
            nextBtn.Hidden = (_control.Position == _control.ItemsSource?.GetCount() - 1 && !_control.InfiniteScrolling) || (_control.InfiniteScrolling && _control.ItemsSource?.GetCount() < 2) || _control.ItemsSource?.GetCount() == 0 || _control.ItemsSource == null || !_control.ShowArrows;
        }

        void SetIndicators()
        {
            pageControl = new UIPageControl();
            pageControl.TranslatesAutoresizingMaskIntoConstraints = false;
            pageControl.Enabled = false;
            pageController.View.AddSubview(pageControl);
            var viewsDictionary = NSDictionary.FromObjectsAndKeys(new NSObject[] { pageControl }, new NSObject[] { new NSString("pageControl") });

            if (_control.Orientation == CarouselViewOrientation.Horizontal)
            {
                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:|[pageControl]|", NSLayoutFormatOptions.AlignAllCenterX, new NSDictionary(), viewsDictionary));

                switch (_control.HorizontalIndicatorsPosition)
                {
                    case HorizontalIndicatorsPosition.Top:
                        pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:|[pageControl]", 0, new NSDictionary(), viewsDictionary));
                        break;
                    case HorizontalIndicatorsPosition.Bottom:
                        pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:[pageControl]|", 0, new NSDictionary(), viewsDictionary));
                        break;
                }
            }

            if (_control.Orientation == CarouselViewOrientation.Vertical)
            {
                pageControl.Transform = CGAffineTransform.MakeRotation(3.14159265f / 2);
                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:[pageControl(==36)]", 0, new NSDictionary(), viewsDictionary));
                pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("V:|[pageControl]|", NSLayoutFormatOptions.AlignAllTop, new NSDictionary(), viewsDictionary));

                switch (_control.VerticalIndicatorsPosition)
                {
                    case VerticalIndicatorsPosition.Left:
                        pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:|[pageControl]", 0, new NSDictionary(), viewsDictionary));
                        break;
                    case VerticalIndicatorsPosition.Right:
                        pageController.View.AddConstraints(NSLayoutConstraint.FromVisualFormat("H:[pageControl]|", 0, new NSDictionary(), viewsDictionary));
                        break;
                }
            }

            pageControl.Pages = Count;
            // IndicatorsTintColor BP
            pageControl.PageIndicatorTintColor = _control.IndicatorsTintColor?.ToPlatform();
            // CurrentPageIndicatorTintColor BP
            pageControl.CurrentPageIndicatorTintColor = _control.CurrentPageIndicatorTintColor?.ToPlatform();
            pageControl.CurrentPage = _control.Position;
            // IndicatorsShape BP
            SetIndicatorsShape();
        }

        void SetIndicatorsVisibility()
        {
            pageControl.Hidden = !_control.ShowIndicators;
        }

        void SetIndicatorsTintColor()
        {
            if (pageControl == null) return;

            pageControl.PageIndicatorTintColor = _control.IndicatorsTintColor?.ToPlatform();
            SetIndicatorsShape();
        }

        void SetCurrentPageIndicatorTintColor()
        {
            if (pageControl == null) return;

            pageControl.CurrentPageIndicatorTintColor = _control.CurrentPageIndicatorTintColor?.ToPlatform();
            SetIndicatorsShape();
        }

        void SetIndicatorsCurrentPage()
        {
            if (pageControl == null) return;

            pageControl.Pages = Count;
            pageControl.CurrentPage = _control.Position;
            SetIndicatorsShape();
        }

        void SetIndicatorsShape()
        {
            if (pageControl == null) return;

            if (_control.IndicatorsShape == IndicatorsShape.Square)
            {
                foreach (var view in pageControl.Subviews)
                {
                    if (view.Frame.Width == 7)
                    {
                        view.Layer.CornerRadius = 0;
                        var frame = new CGRect(view.Frame.X, view.Frame.Y, view.Frame.Width - 1, view.Frame.Height - 1);
                        view.Frame = frame;
                    }
                }
            }
            else
            {
                foreach (var view in pageControl.Subviews)
                {
                    if (view.Frame.Width == 6)
                    {
                        view.Layer.CornerRadius = 3.5f;
                        var frame = new CGRect(view.Frame.X, view.Frame.Y, view.Frame.Width + 1, view.Frame.Height + 1);
                        view.Frame = frame;
                    }
                }
            }
        }

        void InsertPage(object item, int position)
        {
            if (_control == null || pageController == null || Source == null) return;

            Source.Insert(position, item);

            try
            {
                // Because we maybe inserting into an empty PageController
                UIViewController firstViewController;
                if (pageController.ViewControllers.Count() > 0)
                {
                    firstViewController = pageController.ViewControllers[0];
                }
                else
                {
                    firstViewController = CreateViewController(_control.Position);
                }

                pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
                {
                    // To keep the same view visible when inserting in a position <= current (like Android ViewPager)
                    if (position <= _control.Position && Source.Count > 1)
                    {
                        isChangingPosition = true;
                        _control.Position++;
                        isChangingPosition = false;

                        _prevPosition = _control.Position;
                    }

                    SetArrowsVisibility();

                    SetIndicatorsCurrentPage();

                    SendPositionSelected();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                SetPosition();
                SetNativeView();
                SendPositionSelected();
            }  
        }

        async Task RemovePage(int position)
        {
            if (_control == null || pageController == null || Source == null) return;

            if (Source?.Count > 0)
            {
                // To remove latest page, rebuild pageController or the page wont disappear
                if (Source.Count == 1)
                {
                    Source.RemoveAt(position);
                    SetNativeView();
                }
                else
                {
                    // Remove controller from ChildViewControllers
                    if (ChildViewControllers != null)
                    {
                        ChildViewControllers.RemoveAll(c => c.Tag == Source[position]);
                    }

                    Source.RemoveAt(position);

                    // To remove current page
                    if (position == _control.Position)
                    {
                        var newPos = position - 1;
                        if (newPos == -1)
                        {
                            newPos = 0;
                        }

                        // With a swipe transition
                        if (_control.AnimateTransition)
                        {
                            await Task.Delay(100);
                        }

                        var navdirection = position == 0 ? UIPageViewControllerNavigationDirection.Forward : UIPageViewControllerNavigationDirection.Reverse;
                        var firstViewController = CreateViewController(newPos);

                        pageController.SetViewControllers(new[] { firstViewController }, navdirection, _control.AnimateTransition, s =>
                        {
                            isChangingPosition = true;
                            _control.Position = newPos;
                            isChangingPosition = false;

                            SetArrowsVisibility();

                            SetIndicatorsCurrentPage();

                            // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
                            SendPositionSelected();
                        });
                    }
                    else
                    {
                        var firstViewController = pageController.ViewControllers[0];

                        pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
                        {
                            SetArrowsVisibility();

                            SetIndicatorsCurrentPage();

                            // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
                            SendPositionSelected();
                        });
                    }
                }

                _prevPosition = _control.Position;
            }
        }

        private int _prevPosition;

        void SetCurrentPage(int position)
        {
            if (_control == null || position < 0 || position > _control.ItemsSource?.GetCount() - 1) return;

            if (_control.ItemsSource?.GetCount() > 0)
            {
                // Transition direction based on prevPosition or if prevBtn has been clicked
                // Fix wrong animation direction when position changed from code behind
                var isForward = (position >= _prevPosition && position - _prevPosition == 1) || _prevBtnClicked == false || (position == 0 && _prevPosition == _control.ItemsSource?.GetCount() - 1 && _control.InfiniteScrolling);
                var navDirection = isForward ? UIPageViewControllerNavigationDirection.Forward : UIPageViewControllerNavigationDirection.Reverse;
                var firstViewController = CreateViewController(position);

                pageController.SetViewControllers(new[] { firstViewController }, navDirection, _control.AnimateTransition, s =>
                {
                    SetArrowsVisibility();
                    SetIndicatorsCurrentPage();

                    // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
                    SendPositionSelected();
                });

                _prevBtnClicked = null;
                _prevPosition = position;
            }
        }

        #region adapter

        UIViewController CreateViewController(int index)
        {
            // Significant Memory Leak for iOS when using custom layout for page content #125
            var newTag = Source[index];
            foreach (ViewContainer child in pageController.ChildViewControllers)
            {
                if (child.Tag == newTag)
                {
                    return child;
                }
            }

            View formsView = null;

            object bindingContext = null;

            if (Source != null && Source?.Count > 0)
            {
                bindingContext = Source.ElementAt(index);
            }
            
            // Return from the local copy of controllers
            if (ChildViewControllers == null)
            {
                ChildViewControllers = new List<ViewContainer>();
            }

            foreach (ViewContainer controller in ChildViewControllers)
            {
                if (controller.Tag == bindingContext)
                {
                    return controller;
                }
            }

            // Support for List<DataTemplate> as ItemsSource
            if (bindingContext is DataTemplate dt)
            {
                formsView = (View)dt.CreateContent();
            }
            else
            {
                // Support for List<View> as ItemsSource
                if (bindingContext is View view)
                {
                    formsView = view;
                }
                else
                {
                    var selector = _control.ItemTemplate as DataTemplateSelector;
                    if (selector != null)
                    {
                        formsView = (View)selector.SelectTemplate(bindingContext, _control).CreateContent();
                    }
                    else
                    {
                        // So ItemsSource can be ViewModels
                        if (_control.ItemTemplate != null)
                        {
                            formsView = (View)_control.ItemTemplate.CreateContent();
                        }
                        else
                        {
                            formsView = new Label()
                            {
                                Text = "Please provide an ItemTemplate or a DataTemplateSelector"
                            };
                        }
                    }

                    formsView.BindingContext = bindingContext;
                }
            }

            // HeightRequest fix
            formsView.Parent = _control;
            _control.AddItemView(index, formsView);

            var rect = new CGRect(_control.X, _control.Y, ElementWidth, ElementHeight);
            var nativeConverted = formsView.ToiOS(rect, _mauiContext);

            // TODO: Add tap gesture if any in the forms view

            var viewController = new ViewContainer();
            viewController.Tag = bindingContext;
            viewController.View = nativeConverted;

            if (ChildViewControllers != null)
            {
                ChildViewControllers.Add(viewController);
            }

            return viewController;
        }

        #endregion

        void CleanUpArrows()
        {
            if (prevBtn != null)
            {
                prevBtn.TouchUpInside -= PrevBtn_TouchUpInside;
                prevBtn.RemoveFromSuperview();
                prevBtn.Dispose();
                prevBtn = null;
            }

            if (nextBtn != null)
            {
                nextBtn.TouchUpInside -= NextBtn_TouchUpInside;
                nextBtn.RemoveFromSuperview();
                nextBtn.Dispose();
                nextBtn = null;
            }
        }

        void CleanUpPageControl()
        {
            if (pageControl == null) return;

            pageControl.RemoveFromSuperview();
			pageControl.Dispose();
			pageControl = null;
        }

		void CleanUpPageController()
		{
			CleanUpPageControl();

            if (pageController == null) return;

            foreach (var child in pageController.ChildViewControllers)
            {
                child.Dispose();
            }

            foreach (var child in pageController.ViewControllers)
            {
                child.Dispose();
            }

            // Cleanup ChildViewControllers
			if (ChildViewControllers != null)
			{
                foreach (var child in ChildViewControllers)
				{
					child.Dispose();
				}

				ChildViewControllers = null;
			}
		}

		public void Dispose(bool disposing)
		{
			if (disposing && !_disposed)
			{
                // CarouselViewRenderer.Dispose Null reference Unhandled Exception: #210
                // Exception thrown on Dispose #233
				try
                {
                    pageController.DidFinishAnimating -= PageController_DidFinishAnimating;
                    pageController.GetPreviousViewController = null;
                    pageController.GetNextViewController = null;

                    CleanUpPageController();

                    pageController.View.RemoveFromSuperview();
                    pageController.View.Dispose();

                    pageController.Dispose();
                    pageController = null;

				} catch (Exception ex) {
                    Console.Write(ex.Message);
				}

                if (scrollView != null)
                {
                    scrollView.Scrolled -= Scroller_Scrolled;
                    scrollView.DraggingStarted -= Scroller_DraggingStarted;
                    scrollView.DraggingEnded -= Scroller_DraggingEnded;

                    if (scrollView.GestureRecognizers.FirstOrDefault((arg) => arg is UISwipeGestureRecognizer) is UISwipeGestureRecognizer swipe)
                    {
                        scrollView.RemoveGestureRecognizer(swipe);
                    }

                    scrollView.Dispose();
                    scrollView = null;
                }

                if (_control != null)
				{
                    _control.PropertyChanged -= OnElementPropertyChanged;
                    _control.SizeChanged -= OnElementSizeChanged;
                    if (_control.ItemsSource != null && _control.ItemsSource is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)_control.ItemsSource).CollectionChanged -= ItemsSource_CollectionChanged;
                    }
                    RemoveAutoplayBehavior();
                }

				Source = null;

				_disposed = true;
			}
		}

        /// <summary>
		/// Used for registration with dependency service
		/// </summary>
		public static new bool Init()
        {
            var temp = DateTime.Now;
            return true;
        }
    }
}