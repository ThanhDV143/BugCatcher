using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ThanhDV.BugCatcher
{
    internal readonly struct EncodedFrame
    {
        public readonly byte[] Jpg;
        public readonly double Time;
        public readonly long Sequence;
        public readonly int Generation;

        public EncodedFrame(byte[] jpg, double time, long sequence, int generation)
        {
            Jpg = jpg;
            Time = time;
            Sequence = sequence;
            Generation = generation;
        }
    }

    /// <summary>
    /// One background thread that encodes JPGs and does every file operation, in the order they were queued.
    /// The first I/O failure (disk full, no permission) is kept in <see cref="Error"/>; later writes are skipped.
    /// </summary>
    internal sealed class JpegWorker : IDisposable
    {
        private const int Quality = 60;

        private enum JobKind { Encode, CreateDirectory, WriteBytes, WriteText }

        private readonly struct Job
        {
            public readonly JobKind Kind;
            public readonly CapturedFrame Frame;
            public readonly string Path;
            public readonly byte[] Bytes;
            public readonly string Text;

            public Job(JobKind kind, CapturedFrame frame = default, string path = null, byte[] bytes = null, string text = null)
            {
                Kind = kind;
                Frame = frame;
                Path = path;
                Bytes = bytes;
                Text = text;
            }
        }

        private readonly BlockingCollection<Job> jobs = new BlockingCollection<Job>();
        private readonly ConcurrentQueue<EncodedFrame> encoded = new ConcurrentQueue<EncodedFrame>();
        private readonly Action<byte[]> returnPixels;
        private readonly Thread thread;
        private volatile string error;
        private int pendingEncodes;

        public JpegWorker(Action<byte[]> returnPixels)
        {
            this.returnPixels = returnPixels;
            thread = new Thread(Run) { IsBackground = true, Name = "BugCatcher JPG" };
            thread.Start();
        }

        /// <summary>Frames queued for encoding that have not come out yet.</summary>
        public int PendingEncodes => Volatile.Read(ref pendingEncodes);

        /// <summary>Message of the first failed file operation, or null.</summary>
        public string Error => error;

        public void ClearError() => error = null;

        public void Encode(CapturedFrame frame)
        {
            Interlocked.Increment(ref pendingEncodes);
            jobs.Add(new Job(JobKind.Encode, frame));
        }

        public void CreateDirectory(string path) => jobs.Add(new Job(JobKind.CreateDirectory, path: path));

        public void Write(string path, byte[] bytes) => jobs.Add(new Job(JobKind.WriteBytes, path: path, bytes: bytes));

        public void WriteText(string path, string text) => jobs.Add(new Job(JobKind.WriteText, path: path, text: text));

        public bool TryTakeEncoded(out EncodedFrame frame) => encoded.TryDequeue(out frame);

        private void Run()
        {
            foreach (Job job in jobs.GetConsumingEnumerable())
            {
                if (job.Kind == JobKind.Encode)
                {
                    try
                    {
                        CapturedFrame f = job.Frame;
                        // EncodeArrayToJPG is thread-safe; this is the only expensive step and it stays off the main thread.
                        byte[] jpg = ImageConversion.EncodeArrayToJPG(f.Pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)f.Width, (uint)f.Height, 0, Quality);
                        encoded.Enqueue(new EncodedFrame(jpg, f.Time, f.Sequence, f.Generation));
                    }
                    catch (Exception e)
                    {
                        error ??= "encode: " + e.Message;
                    }
                    finally
                    {
                        returnPixels(job.Frame.Pixels);
                        Interlocked.Decrement(ref pendingEncodes);
                    }
                    continue;
                }

                if (error != null) continue;
                try
                {
                    switch (job.Kind)
                    {
                        case JobKind.CreateDirectory: Directory.CreateDirectory(job.Path); break;
                        case JobKind.WriteBytes: File.WriteAllBytes(job.Path, job.Bytes); break;
                        case JobKind.WriteText: File.WriteAllText(job.Path, job.Text); break;
                    }
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            }
        }

        public void Dispose()
        {
            jobs.CompleteAdding();
            // Let queued writes finish (incident info, last frames), but never hang the app on exit.
            thread.Join(2000);
        }
    }
}
