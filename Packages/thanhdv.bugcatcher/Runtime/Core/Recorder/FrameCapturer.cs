using System;
using System.Collections.Concurrent;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThanhDV.BugCatcher
{
    /// <summary>A raw frame read back from the GPU. <see cref="Pixels"/> is RGBA32 and comes from the capturer's pool.</summary>
    internal readonly struct CapturedFrame
    {
        public readonly byte[] Pixels;
        public readonly int Width;
        public readonly int Height;
        /// <summary>Realtime (seconds since startup) of the frame that was captured.</summary>
        public readonly double Time;
        public readonly long Sequence;
        /// <summary>Recorder generation the frame belongs to; frames of an older generation are dropped.</summary>
        public readonly int Generation;

        public CapturedFrame(byte[] pixels, int width, int height, double time, long sequence, int generation)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
            Time = time;
            Sequence = sequence;
            Generation = generation;
        }
    }

    /// <summary>
    /// Copies the finished frame into a small render texture on the GPU and reads it back without stalling.
    /// Call <see cref="Capture"/> at the end of a frame (after <c>WaitForEndOfFrame</c>). Main thread only.
    /// </summary>
    internal sealed class FrameCapturer : IDisposable
    {
        private readonly ConcurrentBag<byte[]> pool = new ConcurrentBag<byte[]>();
        private RenderTexture screen;
        private RenderTexture small;
        private Texture2D fallback;
        private int pooledSize;
        private bool disposed;

        /// <summary>Raised on the main thread when a frame's pixels are on the CPU.</summary>
        public event Action<CapturedFrame> FrameReady;

        /// <summary>Readbacks requested but not delivered yet.</summary>
        public int InFlight { get; private set; }

        public void Capture(int shortEdge, double time, long sequence, int generation)
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            if (disposed || screenWidth <= 0 || screenHeight <= 0) return;

            float scale = Mathf.Min(1f, shortEdge / (float)Mathf.Min(screenWidth, screenHeight));
            int width = Mathf.Max(2, Mathf.RoundToInt(screenWidth * scale));
            int height = Mathf.Max(2, Mathf.RoundToInt(screenHeight * scale));
            Ensure(ref screen, screenWidth, screenHeight);
            Ensure(ref small, width, height);

            ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
            // The captured texture is upside down where UVs start at the top (D3D, Metal, Vulkan).
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(screen, small, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else Graphics.Blit(screen, small);

            if (SystemInfo.supportsAsyncGPUReadback)
            {
                InFlight++;
                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, request =>
                {
                    InFlight--;
                    if (disposed || request.hasError) return;
                    Deliver(request.GetData<byte>(), width, height, time, sequence, generation);
                });
            }
            else
            {
                // Blocking fallback (GLES2, WebGL); the image is small, so the stall is short.
                if (fallback == null || fallback.width != width || fallback.height != height)
                {
                    if (fallback != null) Object.Destroy(fallback);
                    fallback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                }
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = small;
                fallback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                RenderTexture.active = previous;
                Deliver(fallback.GetRawTextureData<byte>(), width, height, time, sequence, generation);
            }
        }

        private void Deliver(NativeArray<byte> data, int width, int height, double time, long sequence, int generation)
        {
            int size = width * height * 4;
            if (data.Length < size) return;

            byte[] pixels = Rent(size);
            NativeArray<byte>.Copy(data, pixels, size);
            FrameReady?.Invoke(new CapturedFrame(pixels, width, height, time, sequence, generation));
        }

        /// <summary>Gives a pixel buffer back once it is encoded. Thread-safe.</summary>
        public void Return(byte[] pixels)
        {
            if (pixels.Length == pooledSize) pool.Add(pixels);
        }

        private byte[] Rent(int size)
        {
            if (size != pooledSize)
            {
                // Resolution changed: buffers of the old size are useless.
                while (pool.TryTake(out _)) { }
                pooledSize = size;
            }
            return pool.TryTake(out byte[] pixels) ? pixels : new byte[size];
        }

        private static void Ensure(ref RenderTexture texture, int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height) return;
            if (texture != null) texture.Release();
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "BugCatcher Capture" };
            texture.Create();
        }

        /// <summary>Frees the textures and pixel buffers; captures can start again later.</summary>
        public void Release()
        {
            if (screen != null) screen.Release();
            if (small != null) small.Release();
            screen = null;
            small = null;
            if (fallback != null) Object.Destroy(fallback);
            fallback = null;
            while (pool.TryTake(out _)) { }
        }

        public void Dispose()
        {
            disposed = true;
            Release();
        }
    }
}
