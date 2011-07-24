using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;

using Microsoft.ParallelComputingPlatform.ParallelExtensions.Samples;

namespace RayDroid
{
    [Activity(Label = "RayDroid", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity, ISurfaceHolderCallback
    {
        bool showThreads;
        bool parallel;
        int degreeOfParallelism = 2;
        CancellationTokenSource cancellation;

        int width, height;
        int[] rgb;

        Button startStopBtn;
        CheckBox parallelBtn;
        CheckBox showBtn;
        TextView fpsText;
        EditText numThreadEntry;

        SquareSurfaceView view;
        ISurfaceHolder holder;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            startStopBtn = FindViewById<Button>(Resource.Id.startStopBtn);
            startStopBtn.Click += delegate { LaunchProcess(); };

            parallelBtn = FindViewById<CheckBox>(Resource.Id.parallelBtn);
            parallelBtn.CheckedChange += delegate { showBtn.Enabled = parallel = parallelBtn.Checked; };

            showBtn = FindViewById<CheckBox>(Resource.Id.showBtn);
            fpsText = FindViewById<TextView>(Resource.Id.fpsText);
            numThreadEntry = FindViewById<EditText>(Resource.Id.numThreadEntry);

            LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.viewLayout);
            view = new SquareSurfaceView(this);
            view.Holder.AddCallback(this);
            layout.AddView(view);

            ThreadPool.SetMinThreads(2, 2);
        }

        private void LaunchProcess ()
        {
            // If we already have the rendering task created, then we're currently running.
            // In that case, stop the renderer.
            if (cancellation != null) {
                startStopBtn.Enabled = false;
                cancellation.Cancel();
            }
            else {
                showThreads = showBtn.Checked;
                parallel = parallelBtn.Checked;
                degreeOfParallelism = string.IsNullOrWhiteSpace(numThreadEntry.Text) ? 2 : int.Parse (numThreadEntry.Text);
                cancellation = new CancellationTokenSource();
                Task.Factory.StartNew (RenderLoop, cancellation.Token, cancellation.Token)
                    .ContinueWith(t => 
                    {
                        RunOnUiThread(() =>
                        {
                            parallelBtn.Enabled = true;
                            showBtn.Enabled = parallelBtn.Checked;
                            startStopBtn.Enabled = true;
                            startStopBtn.Text = "Start";
                            cancellation = null;
                        });
                    }, TaskContinuationOptions.ExecuteSynchronously);

                showBtn.Enabled = false;
                parallelBtn.Enabled = false;
                startStopBtn.Text = "Stop";
            }
        }

        private void RenderLoop(object boxedToken)
        {
            try
            {
                var cancellationToken = (CancellationToken)boxedToken;

                // Create a ray tracer, and create a reference to "sphere2" that we are going to bounce
                var rayTracer = new RayTracer(width, height);
                var scene = rayTracer.DefaultScene;
                var sphere2 = (Sphere)scene.Things[0]; // The first item is assumed to be our sphere
                var baseY = sphere2.Radius;
                sphere2.Center.Y = sphere2.Radius;

                // Timing determines how fast the ball bounces as well as diagnostics frames/second info
                var renderingTime = new Stopwatch();
                ParallelOptions options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = degreeOfParallelism,
                    CancellationToken = cancellation.Token
                };
                Random rnd = new Random();

                // Keep rendering until the rendering task has been canceled
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Determine the new position of the sphere based on the current time elapsed
                    //float dy2 = 0.8f * Math.Abs((float)Math.Sin(++ticks * Math.PI / 3000));
                    sphere2.Center.Y = baseY + 0.8f * (float)rnd.NextDouble ();

                    // Render the scene
                    renderingTime.Reset();
                    renderingTime.Start();

                    if (!parallel) rayTracer.RenderSequential(scene, rgb);
                    else if (showThreads) rayTracer.RenderParallelShowingThreads(scene, rgb, options);
                    else rayTracer.RenderParallel(scene, rgb, options);

                    renderingTime.Stop();

                    // Update the bitmap in the UI thread
                    var framesPerSecond = (1000.0 / renderingTime.ElapsedMilliseconds);
                    if (holder != null)
                    {
                        var canvas = holder.LockCanvas();
                        canvas.DrawBitmap(rgb, 0, width, 0, 0, width, height, false, null);
                        holder.UnlockCanvasAndPost(canvas);
                    }

                    RunOnUiThread(() => fpsText.Text = framesPerSecond.ToString("F1") + " FPS");
                }
            }
            catch (Exception e)
            {
                Android.Util.Log.Error("Process exception", e.Message);
            }
        }

        public void SurfaceChanged(ISurfaceHolder holder, int format, int w, int h)
        {
            this.holder = holder;

            if (width == w && height == h)
                return;
            width = view.RealWidth;
            height = view.RealHeight;
            rgb = new int[width * height];

            var canvas = holder.LockCanvas();
            canvas.DrawColor(Android.Graphics.Color.Crimson);
            holder.UnlockCanvasAndPost(canvas);
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            this.holder = holder;
            
            var canvas = holder.LockCanvas();
            canvas.DrawColor(Android.Graphics.Color.Aquamarine);
            holder.UnlockCanvasAndPost(canvas);
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            this.holder = null;
        }
    }
}
