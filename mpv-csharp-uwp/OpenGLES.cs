using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.Foundation.Collections;
using System.Runtime.InteropServices;

using EGLDisplay = System.IntPtr;
using EGLContext = System.IntPtr;
using EGLConfig = System.IntPtr;
using EGLSurface = System.IntPtr;
using EGLNativeDisplayType = System.IntPtr;
using EGLNativeWindowType = System.Object;
using glbool = System.Int32;

namespace mpv_csharp_uwp
{

    public class OpenGLES : IDisposable
    {

        // Out-of-band handle values
        public static readonly EGLNativeDisplayType EGL_DEFAULT_DISPLAY = IntPtr.Zero;
        public static readonly IntPtr EGL_NO_DISPLAY = IntPtr.Zero;
        public static readonly IntPtr EGL_NO_CONTEXT = IntPtr.Zero;
        public static readonly IntPtr EGL_NO_SURFACE = IntPtr.Zero;

        public const glbool EGL_FALSE = 0;
        public const glbool EGL_TRUE = 1;

        // Config attributes
        public const int EGL_BUFFER_SIZE = 0x3020;
        public const int EGL_ALPHA_SIZE = 0x3021;
        public const int EGL_BLUE_SIZE = 0x3022;
        public const int EGL_GREEN_SIZE = 0x3023;
        public const int EGL_RED_SIZE = 0x3024;
        public const int EGL_DEPTH_SIZE = 0x3025;
        public const int EGL_STENCIL_SIZE = 0x3026;

        // QuerySurface / SurfaceAttrib / CreatePbufferSurface targets
        public const int EGL_HEIGHT = 0x3056;
        public const int EGL_WIDTH = 0x3057;

        // Attrib list terminator
        public const int EGL_NONE = 0x3038;

        // CreateContext attributes
        public const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;

        // ANGLE
        public const int EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER = 0x320B;
        public const int EGL_ANGLE_SURFACE_RENDER_TO_BACK_BUFFER = 0x320C;

        public const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
        public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MAJOR_ANGLE = 0x3204;
        public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MINOR_ANGLE = 0x3205;
        public const int EGL_PLATFORM_ANGLE_TYPE_DEFAULT_ANGLE = 0x3206;

        public const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;

        public const int EGL_PLATFORM_ANGLE_TYPE_D3D9_ANGLE = 0x3207;
        public const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 0x3208;
        public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 0x3209;
        public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 0x320A;
        public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_WARP_ANGLE = 0x320B;
        public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_REFERENCE_ANGLE = 0x320C;
        public const int EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE = 0x320F;


        // fields

        private EGLDisplay mEglDisplay;
        private EGLContext mEglContext;
        private EGLConfig mEglConfig;

        public OpenGLES()
        {
            mEglDisplay = EGL_NO_DISPLAY;
            mEglContext = EGL_NO_CONTEXT;
            mEglConfig = default(EGLConfig);

            Initialize();
        }

        public void Dispose()
        {
            Cleanup();
        }

        public void Initialize()
        {
            int[] configAttributes =
            {
                    EGL_RED_SIZE, 8,
                    EGL_GREEN_SIZE, 8,
                    EGL_BLUE_SIZE, 8,
                    EGL_ALPHA_SIZE, 8,
                    EGL_DEPTH_SIZE, 8,
                    EGL_STENCIL_SIZE, 8,
                    EGL_NONE
                };

            int[] contextAttributes =
            {
                    EGL_CONTEXT_CLIENT_VERSION, 2,
                    EGL_NONE
                };

            int[] defaultDisplayAttributes =
            {
					// These are the default display attributes, used to request ANGLE's D3D11 renderer.
					// eglInitialize will only succeed with these attributes if the hardware supports D3D11 Feature Level 10_0+.
					EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,

					// EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER is an optimization that can have large performance benefits on mobile devices.
					// Its syntax is subject to change, though. Please update your Visual Studio templates if you experience compilation issues with it.
					EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER, EGL_TRUE, 

					// EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE is an option that enables ANGLE to automatically call 
					// the IDXGIDevice3.Trim method on behalf of the application when it gets suspended. 
					// Calling IDXGIDevice3.Trim when an application is suspended is a Windows Store application certification requirement.
					EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE, EGL_TRUE,
                    EGL_NONE,
                };

            int[] fl9_3DisplayAttributes =
            {
					// These can be used to request ANGLE's D3D11 renderer, with D3D11 Feature Level 9_3.
					// These attributes are used if the call to eglInitialize fails with the default display attributes.
					EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
                    EGL_PLATFORM_ANGLE_MAX_VERSION_MAJOR_ANGLE, 9,
                    EGL_PLATFORM_ANGLE_MAX_VERSION_MINOR_ANGLE, 3,
                    EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER, EGL_TRUE,
                    EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE, EGL_TRUE,
                    EGL_NONE,
                };

            int[] warpDisplayAttributes =
            {
					// These attributes can be used to request D3D11 WARP.
					// They are used if eglInitialize fails with both the default display attributes and the 9_3 display attributes.
					EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
                    EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_DEVICE_TYPE_WARP_ANGLE,
                    EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER, EGL_TRUE,
                    EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE, EGL_TRUE,
                    EGL_NONE,
                };

            //
            // To initialize the display, we make three sets of calls to eglGetPlatformDisplayEXT and eglInitialize, with varying 
            // parameters passed to eglGetPlatformDisplayEXT:
            // 1) The first calls uses "defaultDisplayAttributes" as a parameter. This corresponds to D3D11 Feature Level 10_0+.
            // 2) If eglInitialize fails for step 1 (e.g. because 10_0+ isn't supported by the default GPU), then we try again 
            //    using "fl9_3DisplayAttributes". This corresponds to D3D11 Feature Level 9_3.
            // 3) If eglInitialize fails for step 2 (e.g. because 9_3+ isn't supported by the default GPU), then we try again 
            //    using "warpDisplayAttributes".  This corresponds to D3D11 Feature Level 11_0 on WARP, a D3D11 software rasterizer.
            //

            // This tries to initialize EGL to D3D11 Feature Level 10_0+. See above comment for details.
            mEglDisplay = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, EGL_DEFAULT_DISPLAY, defaultDisplayAttributes);
            if (mEglDisplay == EGL_NO_DISPLAY)
            {
                throw new Exception("Failed to get EGL display (D3D11 10.0+).");
            }

            int major, minor;
            if (eglInitialize(mEglDisplay, out major, out minor) == EGL_FALSE)
            {
                // This tries to initialize EGL to D3D11 Feature Level 9_3, if 10_0+ is unavailable (e.g. on some mobile devices).
                mEglDisplay = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, EGL_DEFAULT_DISPLAY, fl9_3DisplayAttributes);
                if (mEglDisplay == EGL_NO_DISPLAY)
                {
                    throw new Exception("Failed to get EGL display (D3D11 9.3).");
                }

                if (eglInitialize(mEglDisplay, out major, out minor) == EGL_FALSE)
                {
                    // This initializes EGL to D3D11 Feature Level 11_0 on WARP, if 9_3+ is unavailable on the default GPU.
                    mEglDisplay = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, EGL_DEFAULT_DISPLAY, warpDisplayAttributes);
                    if (mEglDisplay == EGL_NO_DISPLAY)
                    {
                        throw new Exception("Failed to get EGL display (D3D11 11.0 WARP)");
                    }

                    if (eglInitialize(mEglDisplay, out major, out minor) == EGL_FALSE)
                    {
                        // If all of the calls to eglInitialize returned EGL_FALSE then an error has occurred.
                        throw new Exception("Failed to initialize EGL");
                    }
                }
            }

            int numConfigs = 0;
            EGLDisplay[] configs = new EGLDisplay[1];
            if (eglChooseConfig(mEglDisplay, configAttributes, configs, configs.Length, out numConfigs) == EGL_FALSE || numConfigs == 0)
            {
                throw new Exception("Failed to choose first EGLConfig");
            }
            mEglConfig = configs[0];

            mEglContext = eglCreateContext(mEglDisplay, mEglConfig, EGL_NO_CONTEXT, contextAttributes);
            if (mEglContext == EGL_NO_CONTEXT)
            {
                throw new Exception("Failed to create EGL context");
            }
        }

        public void Cleanup()
        {
            if (mEglDisplay != EGL_NO_DISPLAY && mEglContext != EGL_NO_CONTEXT)
            {
                eglDestroyContext(mEglDisplay, mEglContext);
                mEglContext = EGL_NO_CONTEXT;
            }

            if (mEglDisplay != EGL_NO_DISPLAY)
            {
                eglTerminate(mEglDisplay);
                mEglDisplay = EGL_NO_DISPLAY;
            }
        }

        public EGLSurface CreateSurface(SwapChainPanel panel)
        {
            if (panel == null)
            {
                throw new ArgumentNullException("SwapChainPanel parameter is invalid");
            }

            EGLSurface surface = EGL_NO_SURFACE;

            int[] surfaceAttributes =
            {
					// EGL_ANGLE_SURFACE_RENDER_TO_BACK_BUFFER is part of the same optimization as EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER (see above).
					// If you have compilation issues with it then please update your Visual Studio templates.
					EGL_ANGLE_SURFACE_RENDER_TO_BACK_BUFFER, EGL_TRUE,
                    EGL_NONE
                };

            // Create a PropertySet and initialize with the EGLNativeWindowType.
            PropertySet surfaceCreationProperties = new PropertySet();
            surfaceCreationProperties.Add("EGLNativeWindowTypeProperty", panel);

            surface = eglCreateWindowSurface(mEglDisplay, mEglConfig, surfaceCreationProperties, surfaceAttributes);
            if (surface == EGL_NO_SURFACE)
            {
                throw new Exception("Failed to create EGL surface");
            }

            return surface;
        }

        public Size GetSurfaceDimensions(EGLSurface surface)
        {
            int width = 0;
            int height = 0;
            eglQuerySurface(mEglDisplay, surface, EGL_WIDTH, out width);
            eglQuerySurface(mEglDisplay, surface, EGL_HEIGHT, out height);
            return new Size(width, height);
        }

        public void DestroySurface(EGLSurface surface)
        {
            if (mEglDisplay != EGL_NO_DISPLAY && surface != EGL_NO_SURFACE)
            {
                eglDestroySurface(mEglDisplay, surface);
            }
        }

        public void MakeCurrent(EGLSurface surface)
        {
            if (eglMakeCurrent(mEglDisplay, surface, surface, mEglContext) == EGL_FALSE)
            {
                throw new Exception("Failed to make EGLSurface current");
            }
        }

        public void MakeCurrentAlt(EGLSurface surfDraw, EGLSurface surfRead)
        {
            if (eglMakeCurrent(mEglDisplay, surfDraw, surfRead, mEglContext) == EGL_FALSE)
            {
                throw new Exception("Failed to make EGLSurface current");
            }
        }

        public EGLContext GetCurrentContext()
        {
            return eglGetCurrentContext();
        }

        public int SwapBuffers(EGLSurface surface)
        {
            return eglSwapBuffers(mEglDisplay, surface);
        }

        public IntPtr GetProcAddress(string procname)
        {
            return eglGetProcAddress(procname);
        }

        public void Reset()
        {
            Cleanup();
            Initialize();
        }

        // C API

        private const string libEGL = "libEGL.dll";

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        private static extern IntPtr eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern EGLDisplay eglGetPlatformDisplayEXT(int platform, EGLNativeDisplayType native_display, int[] attrib_list);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglInitialize(EGLDisplay dpy, out int major, out int minor);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglChooseConfig(EGLDisplay dpy, int[] attrib_list, [In, Out] EGLConfig[] configs, int config_size, out int num_config);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern EGLContext eglCreateContext(EGLDisplay dpy, EGLConfig config, EGLContext share_context, int[] attrib_list);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern EGLSurface eglCreateWindowSurface(EGLDisplay dpy, EGLConfig config, [MarshalAs(UnmanagedType.IInspectable)] EGLNativeWindowType win, int[] attrib_list);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglQuerySurface(EGLDisplay dpy, EGLSurface surface, int attribute, out int value);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglDestroySurface(EGLDisplay dpy, EGLSurface surface);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglMakeCurrent(EGLDisplay dpy, EGLSurface draw, EGLSurface read, EGLContext ctx);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglSwapBuffers(EGLDisplay dpy, EGLSurface surface);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglDestroyContext(EGLDisplay dpy, EGLContext ctx);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern glbool eglTerminate(EGLDisplay dpy);

        [DllImport(libEGL, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern EGLContext eglGetCurrentContext();
    }
}
