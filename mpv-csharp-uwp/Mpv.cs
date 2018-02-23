using MPVLib;
using static MPVLib.client;
using static MPVLib.opengl_cb;
using static MPVLib.stream_cb;
using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Foundation;

namespace mpv_csharp_uwp
{
    public class Mpv
    {
        private MpvOpenglCbUpdateFn glcallback;
        private MpvHandle handle;
        private MpvOpenglCbContext glctx;
        private IAsyncAction worker;

        #region Methods
        public Mpv()
        {
            handle = MpvCreate();
            MpvInitialize(handle);
            glctx = MpvOpenglCbContext.__CreateInstance(MpvGetSubApi(handle, MpvSubApi.MPV_SUB_API_OPENGL_CB));
            SetOption("msg-level", "all=v");
            SetOption("vo", "opengl-cb");
            MpvRequestLogMessages(handle, "terminal-default");

            worker = Windows.System.Threading.ThreadPool.RunAsync((workItem) =>
            {
                while (workItem.Status == AsyncStatus.Started)
                {
                    var ev = MpvWaitEvent(handle, -1.0);
                    if (ev == null) continue;
                    if (ev.EventId != MpvEventId.MPV_EVENT_LOG_MESSAGE) continue;
                    var msg = MpvEventLogMessage.__CreateInstance(ev.Data);
                    Debug.Write("[" + msg.Prefix + "] " + msg.Text);
                }
            });
        }

        // Sets a an mpv option with the value being a string
        public MpvError SetOption(string option, string value)
        {
            return (MpvError)MpvSetOptionString(handle, option, value);
        }

        // Get an mpv property string value
        public unsafe string GetPropertyString(string property)
        {
            var val = MpvGetPropertyString(handle, property);
            if (val == null) return null;
            return Marshal.PtrToStringAnsi((IntPtr)val);
        }

        public unsafe bool GetPropertyBool(string property)
        {
            int val = 0;
            MpvGetProperty(handle, property, MpvFormat.MPV_FORMAT_FLAG, new IntPtr(&val));
            return val == 1;
        }

        // Sets an mpv property value
        public MpvError SetProperty(string property, string value)
        {
            return (MpvError)MpvSetPropertyString(handle, property, value);
        }

        // Executes a command through mpv
        public unsafe MpvError ExecuteCommand(params string[] args)
        {
            var list = new sbyte*[args.Length + 1];
            for (int i = 0; i < args.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(args[i] + "\0");
                var sbytes = (from b in bytes select Convert.ToSByte(b)).ToArray();
                fixed (sbyte* ptr = sbytes)
                {
                    list[i] = ptr;
                }
            }
            fixed (sbyte** ptr = list)
            {
                return (MpvError)MpvCommand(handle, ptr);
            }
        }

        // Initalizes the OpenGL Callbacks
        public MpvError OpenGLCallbackInitialize(string exts, MpvOpenglCbGetProcAddressFn get_proc_addr, IntPtr fn_context)
        {
            return (MpvError)MpvOpenglCbInitGl(glctx, exts, get_proc_addr, fn_context);
        }

        // Sets OpenGL Update Callback for mpv
        public void OpenGLCallbackSetUpdate(MpvOpenglCbUpdateFn callback, IntPtr ctx)
        {
            glcallback = callback;
            MpvOpenglCbSetUpdateCallback(glctx, callback, ctx);
        }

        // Executed when the OpenGL Update Callback is requested
        public MpvError OpenGLCallbackDraw(int fbo, int width, int height)
        {
            return (MpvError)MpvOpenglCbDraw(glctx, fbo, width, height);
        }

        // Reports to mpv that the frame has been rendered, entirely optional
        public MpvError OpenGLCallbackReportFlip()
        {
            return (MpvError)MpvOpenglCbReportFlip(glctx, 0);
        }

        public MpvError StreamCbAddReadOnly(String proto, IntPtr userdata, MpvStreamCbOpenRoFn open_fn)
        {
            return (MpvError)MpvStreamCbAddRo(handle, proto, userdata, open_fn);
        }
        #endregion Methods
    }
}
