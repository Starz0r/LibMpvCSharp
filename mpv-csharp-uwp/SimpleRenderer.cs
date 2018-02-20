using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Windows.Foundation;

namespace mpv_csharp_uwp
{
    class SimpleRenderer : IDisposable
    {
        private Size size = Size.Empty;

        private uint renderBuffer;

        public SimpleRenderer()
        {
            //uint[] renderBuffers = new uint[1];
            //glGenRenderbuffers(renderBuffers.Length, renderBuffers);
            //renderBuffer = renderBuffers[0];

            //glBindRenderbuffer(GL_RENDERBUFFER, renderBuffer);



        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            glClearColor(1, 0, 0, 1);
            glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        }

        public void UpdateWindowSize(Size size)
        {
            glViewport(0, 0, (int)size.Width, (int)size.Height);
        }

        private const string libGLESv2 = "libGLESv2.dll";


        // ClearBufferMask
        public const int GL_DEPTH_BUFFER_BIT = 0x00000100;
        public const int GL_STENCIL_BUFFER_BIT = 0x00000400;
        public const int GL_COLOR_BUFFER_BIT = 0x00004000;

        // Framebuffer Object
        public const int GL_FRAMEBUFFER = 0x8D40;
        public const int GL_RENDERBUFFER = 0x8D41;

        [DllImport(libGLESv2)]
        public static extern void glGenRenderbuffers(int n, [In, Out] uint[] buffers);
        [DllImport(libGLESv2)]
        public static extern void glBindRenderbuffer(int n, uint buffer);
        [DllImport(libGLESv2)]
        public static extern void glViewport(int x, int y, int width, int height);
        [DllImport(libGLESv2)]
        public static extern void glClearColor(float red, float green, float blue, float alpha);
        [DllImport(libGLESv2)]
        public static extern void glClear(int mask);
    }
}
