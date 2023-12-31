﻿
/*
 * Copyright (C) 2014 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// https://android.googlesource.com/platform/packages/apps/DeskClock/+/master/src/com/android/deskclock/VerticalViewPager.java

using System;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.ViewPager.Widget;
using View = Android.Views.View;

namespace CarouselView
{
	public class VerticalViewPager : ViewPager
	{
		public bool isSwipingEnabled = true;

		public VerticalViewPager(Context context) : base(context, null)
		{
		}

		public VerticalViewPager(Context context, IAttributeSet attrs) : base(context, attrs)
		{
			Init();
		}

		// Fix for #171 System.MissingMethodException: No constructor found
		public VerticalViewPager(IntPtr intPtr, JniHandleOwnership jni) : base(intPtr, jni)
		{
		}

		/**
		 * @return {@code false} since a vertical view pager can never be scrolled horizontally
		 */
		public override bool CanScrollHorizontally(int direction)
		{
			return false;
		}

		/**
		 * @return {@code true} iff a normal view pager would support horizontal scrolling at this time
		 */
		public override bool CanScrollVertically(int direction)
		{
			return base.CanScrollHorizontally(direction);
		}

		private void Init()
		{
			// Make page transit vertical
			SetPageTransformer(true, new VerticalPageTransformer());
			// Get rid of the overscroll drawing that happens on the left and right (the ripple)
			OverScrollMode = OverScrollMode.Never;
		}

		public override bool OnInterceptTouchEvent(MotionEvent ev)
		{
			if (this.isSwipingEnabled)
			{
				var toIntercept = base.OnInterceptTouchEvent(flipXY(ev));
				// Return MotionEvent to normal
				flipXY(ev);
				return toIntercept;
			}

			return false;
		}

		public override bool OnTouchEvent(MotionEvent ev)
		{
			if (this.isSwipingEnabled)
			{
				var toHandle = base.OnTouchEvent(flipXY(ev));
				//Return MotionEvent to normal
				flipXY(ev);
				return toHandle;
			}

			return false;
		}

		public void SetPagingEnabled(bool enabled)
		{
			this.isSwipingEnabled = enabled;
		}

		private MotionEvent flipXY(MotionEvent ev)
		{
			var width = Width;
			var height = Height;
			var x = (ev.GetY() / height) * width;
			var y = (ev.GetX() / width) * height;
			ev.SetLocation(x, y);
			return ev;
		}

		private class VerticalPageTransformer : Java.Lang.Object, ViewPager.IPageTransformer
		{
			public void TransformPage(Android.Views.View view, float position)
			{
				var pageWidth = view.Width;
				var pageHeight = view.Height;

				if (position < -1)
				{
					// This page is way off-screen to the left.
					view.Alpha = 0;
				}
				else if (position <= 1)
				{
					view.Alpha = 1;
					// Counteract the default slide transition
					view.TranslationX = pageWidth * -position;
					// set Y position to swipe in from top
					float yPosition = position * pageHeight;
					view.TranslationY = yPosition;
				}
				else
				{
					// This page is way off-screen to the right.
					view.Alpha = 0;
				}
			}
		}
	}
}