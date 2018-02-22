using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace mpv_csharp_uwp.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        #region Definitions

        private IntPtr mRenderSurface;
        private OpenGLES mOpenGLES;
        private object mRenderSurfaceCriticalSection = new object();
        private IAsyncAction mRenderLoopWorker;
        Mpv mpv;

        private GCHandle streamcb_current_file_allocated;
        private StorageFile streamcb_file;
        private IntPtr streamcb_file_pointer = IntPtr.Zero;
        private String streamcb_userdata;
        private Stream streamcb_stream;
        private Byte[] streamcb_buffer;
        private UInt64 streamcb_buffer_size;
        private IntPtr streamcb_buffer_reference;

        private Mpv.MyStreamCbReadFn streamcb_callback_read_method;
        private IntPtr streamcb_callback_read_ptr;
        private GCHandle streamcb_callback_read_gc;

        private Mpv.MyStreamCbSeekFn streamcb_callback_seek_method;
        private IntPtr streamcb_callback_seek_ptr;
        private GCHandle streamcb_callback_seek_gc;

        private Mpv.MyStreamCbSizeFn streamcb_callback_size_method;
        private IntPtr streamcb_callback_size_ptr;
        private GCHandle streamcb_callback_size_gc;

        private Mpv.MyStreamCbCloseFn streamcb_callback_close_method;
        private IntPtr streamcb_callback_close_ptr;
        private GCHandle streamcb_callback_close_gc;

        #endregion Definitions

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
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            DestroyRenderSurface();
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

        public Int64 StreamCbReadFn(IntPtr cookie, IntPtr buf, UInt64 numbytes)
        {
            IntPtr fp = cookie;
            streamcb_buffer_reference = buf;
            streamcb_buffer_size = numbytes;

            // * Not 64bit safe :(
            streamcb_buffer = new Byte[numbytes];
            int result = streamcb_stream.Read(streamcb_buffer, 0, Convert.ToInt32(numbytes));
            Marshal.Copy(streamcb_buffer, 0, streamcb_buffer_reference, result);

            // End of File
            if (result == 0)
            {
                return 0;
            }

            return result;

            // TODO: Add a try/catch to return a -1 on an exception
        }

        public Int64 StreamCbSeekFn(IntPtr cookie, Int64 offset)
        {
            IntPtr fp = cookie;

            Int64 result = streamcb_stream.Seek(offset, SeekOrigin.Begin);

            return result < 0 ? (Int64)Mpv.MpvErrorCode.MPV_ERROR_GENERIC : result;
        }

        public Int64 StreamCbSizeFn(IntPtr cookie)
        {
            return streamcb_stream.Length;
        }

        public void StreamCbCloseFn(IntPtr cookie)
        {
            streamcb_stream.Dispose();
        }

        public int StreamCbOpenFn(String userdata, String uri, ref Mpv.MPV_STREAM_CB_INFO info)
        {
            // Store File Path
            streamcb_userdata = userdata;

            // Allocate Methods
            streamcb_callback_read_method = StreamCbReadFn;
            streamcb_callback_read_ptr = Marshal.GetFunctionPointerForDelegate(streamcb_callback_read_method);
            streamcb_callback_read_gc = GCHandle.Alloc(streamcb_callback_read_ptr, GCHandleType.Pinned);

            streamcb_callback_seek_method = StreamCbSeekFn;
            streamcb_callback_seek_ptr = Marshal.GetFunctionPointerForDelegate(streamcb_callback_seek_method);
            streamcb_callback_seek_gc = GCHandle.Alloc(streamcb_callback_seek_ptr, GCHandleType.Pinned);

            streamcb_callback_size_method = StreamCbSizeFn;
            streamcb_callback_size_ptr = Marshal.GetFunctionPointerForDelegate(streamcb_callback_size_method);
            streamcb_callback_size_gc = GCHandle.Alloc(streamcb_callback_size_ptr, GCHandleType.Pinned);

            streamcb_callback_close_method = StreamCbCloseFn;
            streamcb_callback_close_ptr = Marshal.GetFunctionPointerForDelegate(streamcb_callback_close_method);
            streamcb_callback_close_gc = GCHandle.Alloc(streamcb_callback_close_ptr, GCHandleType.Pinned);

            // Set Struct Methods
            info.Cookie = streamcb_file_pointer;
            info.ReadFn = streamcb_callback_read_ptr;
            info.SeekFn = streamcb_callback_seek_ptr;
            info.SizeFn = streamcb_callback_size_ptr;
            info.CloseFn = streamcb_callback_close_ptr;

            // TODO: Return a MPV_ERROR_LOADING_FAILED if we aren't able to allocate the file to memory

            return 0;
        }

        #endregion Delegates

        private void InitalizeMpvDynamic()
        {
            mOpenGLES.MakeCurrent(mRenderSurface);

            mpv = new Mpv();

            Windows.Storage.StorageFolder installationPath = Windows.Storage.ApplicationData.Current.LocalFolder;
            mpv.SetOptionString("log-file", @installationPath.Path + @"\koala.log");
            mpv.SetOptionString("msg-level", "all=v");
            mpv.SetOptionString("vo", "opengl-cb");

            mpv.OpenGLCallbackInitialize(null, MyProcAddress, IntPtr.Zero);
            mpv.OpenGLCallbackSetUpdate(DrawNextFrame, IntPtr.Zero);

            mpv.ExecuteCommand("loadfile", "https://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_480p_h264.mov");
        }
    }
}
