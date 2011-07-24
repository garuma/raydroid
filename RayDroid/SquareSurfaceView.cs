using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace RayDroid
{
    public class SquareSurfaceView : SurfaceView
    {
        int realHeight;
        int realWidth;

        public SquareSurfaceView(Context context) :
            base (context)
        {
            Initialize ();
        }

        public SquareSurfaceView(Context context, IAttributeSet attrs) :
            base(context, attrs)
        {
            Initialize();
        }

        public SquareSurfaceView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {
            Initialize();
        }

        private void Initialize()
        {
            
        }

        public int RealHeight
        {
            get
            {
                return realHeight;
            }
        }

        public int RealWidth
        {
            get
            {
                return realWidth;
            }
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            realWidth = MeasureSpec.GetSize(widthMeasureSpec);
            realHeight = MeasureSpec.GetSize(heightMeasureSpec);

            realWidth = realHeight = Math.Min(realHeight, realWidth) * 2 / 3;
            Holder.SetFixedSize(realWidth, realHeight);
            base.OnMeasure(
                    MeasureSpec.MakeMeasureSpec(realWidth, MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(realHeight, MeasureSpecMode.Exactly)
            );
        }
    }
}