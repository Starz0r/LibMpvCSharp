using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace mpv_csharp_uwp.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private IntPtr mRenderSurface;
        private OpenGLES mOpenGLES;
        Mpv mpv;
        private MPVLib.MpvStreamCbInfo streamcb_info;


        #region Events
        public MainPage()
        {
            InitializeComponent();

            mOpenGLES = new OpenGLES();
            mRenderSurface = OpenGLES.EGL_NO_SURFACE;

            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // The SwapChainPanel has been created and arranged in the page layout, so EGL can be initialized. 
            CreateRenderSurface();
            InitalizeMpvDynamic();
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            DestroyRenderSurface();
            Window.Current.CoreWindow.KeyDown -= OnKeyDown;
        }

        private void OnKeyDown(CoreWindow window, KeyEventArgs e)
        {
            switch (e.VirtualKey)
            {
                case Windows.System.VirtualKey.Space:
                    if (mpv.GetPropertyBool("pause"))
                        mpv.SetProperty("pause", "no");
                    else
                        mpv.SetProperty("pause", "yes");
                    break;
            }
        }

        private void CreateRenderSurface()
        {
            if (mOpenGLES != null && mRenderSurface == OpenGLES.EGL_NO_SURFACE)
            {
                mRenderSurface = mOpenGLES.CreateSurface(videoBox);
            }
        }

        private void DestroyRenderSurface()
        {
            if (mOpenGLES == null)
            {
                mOpenGLES.DestroySurface(mRenderSurface);
            }
            mRenderSurface = OpenGLES.EGL_NO_SURFACE;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion Events

        #region Delegates
        private IntPtr MyProcAddress(IntPtr context, string name)
        {
            return mOpenGLES.GetProcAddress(name);
        }

        private async void DrawNextFrame(IntPtr context)
        {
            // Wait for the UI Thread to run to render the next frame.
            await videoBox.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            {
                // Get the Width and Height of the Window (it can change at anytime)
                int w = (int)((Frame)Window.Current.Content).ActualWidth;
                int h = (int)((Frame)Window.Current.Content).ActualHeight;

                // Draw the next frame, swap buffers, and report the frame has been flipped
                mpv.OpenGLCallbackDraw(0, w, -h);
                mOpenGLES.SwapBuffers(mRenderSurface);
                mpv.OpenGLCallbackReportFlip();
            }));
        }

        public unsafe long StreamCbReadFn(IntPtr cookie, sbyte* buf, ulong nbytes)
        {
            return 0;
        }

        public unsafe long StreamCbSeekFn(IntPtr cookie, long offset)
        {
            return 0;
        }

        public long StreamCbSizeFn(IntPtr cookie)
        {
            return 0;
        }

        public void StreamCbCloseFn(IntPtr cookie)
        {
        }

        public unsafe int StreamCbOpenFn(IntPtr user_data, sbyte *uri, IntPtr info)
        {
            streamcb_info = MPVLib.MpvStreamCbInfo.__CreateInstance(info);
            streamcb_info.ReadFn = StreamCbReadFn;
            streamcb_info.SeekFn = StreamCbSeekFn;
            streamcb_info.SizeFn = StreamCbSizeFn;
            streamcb_info.CloseFn = StreamCbCloseFn;
            return 0;
        }

        #endregion Delegates

        private void InitalizeMpvDynamic()
        {
            mOpenGLES.MakeCurrent(mRenderSurface);

            mpv = new Mpv();
            mpv.OpenGLCallbackInitialize(null, MyProcAddress, IntPtr.Zero);
            mpv.OpenGLCallbackSetUpdate(DrawNextFrame, IntPtr.Zero);
            mpv.ExecuteCommand("loadfile", "http://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_480p_h264.mov");
        }
    }
}
