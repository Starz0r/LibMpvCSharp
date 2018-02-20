using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace mpv_csharp_uwp
{
    public class Mpv
    {
        #region Definitions
        // Delegates
        public delegate IntPtr MyGetProcAddress(IntPtr context, String name);
        public delegate void MyOpenGLCallbackUpdate(IntPtr context);
        public delegate Int64 MyStreamCbReadFn(IntPtr cookie, IntPtr buf, UInt64 numbytes);
        public delegate Int64 MyStreamCbSeekFn(IntPtr cookie, Int64 offset);
        public delegate void MyStreamCbCloseFn(IntPtr cookie);
        public delegate Int64 MyStreamCbSizeFn(IntPtr cookie);
        public delegate int MyStreamCbOpenFn(String userdata, String uri, ref MPV_STREAM_CB_INFO info);

        // Structs
        public struct MPV_STREAM_CB_INFO
        {
            public IntPtr Cookie;
            public IntPtr ReadFn;
            public IntPtr SeekFn;
            public IntPtr SizeFn;
            public IntPtr CloseFn;
        }

        // Members
        private MyOpenGLCallbackUpdate callback_method;
        private GCHandle callback_gc;
        private IntPtr callback_ptr;
        private IntPtr libmpv_handle;
        private IntPtr libmpv_gl_context;
        #endregion Definitions

        #region Methods
        public Mpv()
        {
            libmpv_handle = mpv_create();
            Initalize(libmpv_handle);
            libmpv_gl_context = GetSubApi(1);
        }

        // Creates a new mpv object
        private IntPtr Create()
        {
            return mpv_create();
        }


        // Initalizes the mpv object, runs right after getting created
        private MpvErrorCode Initalize(IntPtr mpv_handle)
        {
            return (MpvErrorCode)mpv_initialize(mpv_handle);
        }

        // Sets a an mpv option with the value being a string
        public MpvErrorCode SetOptionString(string option, string value)
        {
            return (MpvErrorCode)mpv_set_option_string(libmpv_handle, GetUtf8Bytes(option), GetUtf8Bytes(value));
        }

        // Gets API Contextes, currently only used to get mpv's OpenGL context, internal use only
        private IntPtr GetSubApi(int value)
        {
            return mpv_get_sub_api(libmpv_handle, value);
        }

        // Executes a command through mpv
        public MpvErrorCode ExecuteCommand(params string[] args)
        {
            IntPtr[] byteArrayPointers;
            var mainPtr = AllocateUtf8IntPtrArrayWithSentinel(args, out byteArrayPointers);
            MpvErrorCode result = (MpvErrorCode)mpv_command(libmpv_handle, mainPtr);
            foreach (var ptr in byteArrayPointers)
            {
                Marshal.FreeHGlobal(ptr);
            }
            Marshal.FreeHGlobal(mainPtr);
            return result;
        }

        // Returns a corresponding property tuple from mpv
        public Tuple<MpvErrorCode, String> GetProperty(string property)
        {
            IntPtr lpBuffer = IntPtr.Zero;
            int err = mpv_get_property_string(libmpv_handle, GetUtf8Bytes(property), (int)MpvFormat.MPV_FORMAT_STRING, ref lpBuffer);
            String result = Marshal.PtrToStringAnsi(lpBuffer);
            return Tuple.Create((MpvErrorCode)err, result);
        }

        // Sets a mpv property with the specified value
        public MpvErrorCode SetProperty(string property, MpvFormat format, string value)
        {
            var temp = GetUtf8Bytes(value);
            return (MpvErrorCode)mpv_set_property(libmpv_handle, GetUtf8Bytes(property), (int)format, ref temp);
        }

        // Initalizes the OpenGL Callbacks
        public MpvErrorCode OpenGLCallbackInitialize(byte[] exts, MyGetProcAddress callback, IntPtr fn_context)
        {
            return (MpvErrorCode)mpv_opengl_cb_init_gl(libmpv_gl_context, exts, callback, fn_context);

        }

        // Sets OpenGL Update Callback for mpv
        public MpvErrorCode OpenGLCallbackSetUpdate(MyOpenGLCallbackUpdate callback, IntPtr callback_context)
        {
            // Set class members
            callback_method = callback;
            callback_ptr = Marshal.GetFunctionPointerForDelegate<MyOpenGLCallbackUpdate>(callback);

            // Allocate the pointer so it doesn't get garbage collected
            callback_gc = GCHandle.Alloc(callback_ptr, GCHandleType.Pinned);

            return (MpvErrorCode)mpv_opengl_cb_set_update_callback(libmpv_gl_context, callback_ptr, callback_context);
        }

        // Executed when the OpenGL Update Callback is requested
        public MpvErrorCode OpenGLCallbackDraw(int framebuffer_object, int width, int height)
        {
            // callback_ptr might get garbage collected if it isn't used too much, so we have to keep it alive this way
            callback_ptr = Marshal.GetFunctionPointerForDelegate<MyOpenGLCallbackUpdate>(callback_method);
            return (MpvErrorCode)mpv_opengl_cb_draw(libmpv_gl_context, framebuffer_object, width, height);
        }

        // Reports to mpv that the frame has been rendered, entirely optional
        public MpvErrorCode OpenGLCallbackReportFlip()
        {
            return (MpvErrorCode)mpv_opengl_cb_report_flip(libmpv_gl_context);
        }

        // Renders the OpenGL Callback frame that was returned
        public MpvErrorCode OpenGLCallbackRender()
        {
            return (MpvErrorCode)mpv_opengl_cb_render(libmpv_gl_context);
        }

        public MpvErrorCode StreamCbAddReadOnly(String proto, String userdata, MyStreamCbOpenFn open_fn)
        {
            return (MpvErrorCode)mpv_stream_cb_add_ro(libmpv_handle, proto, userdata, open_fn);
        }

        #endregion Methods

        #region Helpers
        private byte[] GetUtf8Bytes(String s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }

        private IntPtr AllocateUtf8IntPtrArrayWithSentinel(string[] arr, out IntPtr[] byteArrayPointers)
        {
            int numberOfStrings = arr.Length + 1; // add extra element for extra null pointer last (sentinel)
            byteArrayPointers = new IntPtr[numberOfStrings];
            IntPtr rootPointer = Marshal.AllocCoTaskMem(IntPtr.Size * numberOfStrings);
            for (int index = 0; index < arr.Length; index++)
            {
                var bytes = GetUtf8Bytes(arr[index]);
                IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                byteArrayPointers[index] = unmanagedPointer;
            }
            Marshal.Copy(byteArrayPointers, 0, rootPointer, numberOfStrings);
            return rootPointer;
        }
        #endregion Helpers

        #region Enumeration
        public enum MpvErrorCode
        {
            MPV_ERROR_SUCCESS = 0,
            MPV_ERROR_EVENT_QUEUE_FULL = -1,
            MPV_ERROR_NOMEM = -2,
            MPV_ERROR_UNINITIALIZED = -3,
            MPV_ERROR_INVALID_PARAMETER = -4,
            MPV_ERROR_OPTION_NOT_FOUND = -5,
            MPV_ERROR_OPTION_FORMAT = -6,
            MPV_ERROR_OPTION_ERROR = -7,
            MPV_ERROR_PROPERTY_NOT_FOUND = -8,
            MPV_ERROR_PROPERTY_FORMAT = -9,
            MPV_ERROR_PROPERTY_UNAVAILABLE = -10,
            MPV_ERROR_PROPERTY_ERROR = -11,
            MPV_ERROR_COMMAND = -12,
            MPV_ERROR_LOADING_FAILED = -13,
            MPV_ERROR_AO_INIT_FAILED = -14,
            MPV_ERROR_VO_INIT_FAILED = -15,
            MPV_ERROR_NOTHING_TO_PLAY = -16,
            MPV_ERROR_UNKNOWN_FORMAT = -17,
            MPV_ERROR_UNSUPPORTED = -18,
            MPV_ERROR_NOT_IMPLEMENTED = -19,
            MPV_ERROR_GENERIC = -20
        }

        public enum MpvFormat
        {
            MPV_FORMAT_NONE,
            MPV_FORMAT_STRING,
            MPV_FORMAT_OSD_STRING,
            MPV_FORMAT_FLAG,
            MPV_FORMAT_INT64,
            MPV_FORMAT_DOUBLE,
            MPV_FORMAT_NODE,
            MPV_FORMAT_NODE_ARRAY,
            MPV_FORMAT_NODE_MAP,
            MPV_FORMAT_BYTE_ARRAY
        }
        #endregion Enumeration

        #region Imports
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr LoadPackagedLibrary([MarshalAs(UnmanagedType.LPWStr)]string libraryName, int reserved = 0);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string librayName);

        private const string libmpv = "mpv.dll";

        [DllImport(libmpv, EntryPoint = "mpv_create", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mpv_create();

        [DllImport(libmpv, EntryPoint = "mpv_initialize", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_initialize(IntPtr mpv_handle);

        [DllImport(libmpv, EntryPoint = "mpv_set_option_string", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_set_option_string(IntPtr mpv_handle, byte[] option, byte[] value);

        [DllImport(libmpv, EntryPoint = "mpv_get_sub_api", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mpv_get_sub_api(IntPtr mpv_handle, int value);

        [DllImport(libmpv, EntryPoint = "mpv_opengl_cb_init_gl", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_opengl_cb_init_gl(IntPtr gl_context, byte[] exts, MyGetProcAddress callback, IntPtr fn_context);

        [DllImport(libmpv, EntryPoint = "mpv_opengl_cb_set_update_callback", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_opengl_cb_set_update_callback(IntPtr gl_context, IntPtr callback, IntPtr callback_context);

        [DllImport(libmpv, EntryPoint = "mpv_command", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_command(IntPtr mpv_handle, IntPtr strings);

        [DllImport(libmpv, EntryPoint = "mpv_opengl_cb_draw", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_opengl_cb_draw(IntPtr gl_context, int fbo, int width, int height);

        [DllImport(libmpv, EntryPoint = "mpv_opengl_cb_report_flip", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_opengl_cb_report_flip(IntPtr gl_context, Int64 time = 0);

        [DllImport(libmpv, EntryPoint = "mpv_opengl_cb_render", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_opengl_cb_render(IntPtr gl_context, int vp = 0);

        [DllImport(libmpv, EntryPoint = "mpv_get_property_string", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_get_property_string(IntPtr mpv_handle, byte[] name, int format, ref IntPtr data);

        [DllImport(libmpv, EntryPoint = "mpv_set_property", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_set_property(IntPtr mpv_handle, byte[] name, int format, ref byte[] data);

        [DllImport(libmpv, EntryPoint = "mpv_stream_cb_add_ro", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mpv_stream_cb_add_ro(IntPtr mpv_handle, String protocol, String userdata, MyStreamCbOpenFn openfn);
        #endregion Imports
    }
}
