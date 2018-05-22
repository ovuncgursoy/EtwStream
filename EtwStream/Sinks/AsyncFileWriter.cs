#region Using Statements

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    internal class AsyncFileWriter
    {
        private readonly bool autoFlush;

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private readonly Encoding encoding;

        private readonly FileStream fileStream;

        private readonly object gate = new object();

        private readonly byte[] newLine;

        private readonly Task processingTask;

        private readonly BlockingCollection<string> q = new BlockingCollection<string>();

        private readonly string sinkName;

        private int isDisposed;

        public AsyncFileWriter(string sinkName, string fileName, Encoding encoding, bool autoFlush)
        {
            {
                var fi = new FileInfo(fileName);

                if (fi.Directory != null && !fi.Directory.Exists)
                {
                    fi.Directory.Create();
                }
            }

            FileName = fileName;
            this.sinkName = sinkName;
            this.fileStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: false);
            this.encoding = encoding;
            this.autoFlush = autoFlush;
            this.newLine = encoding.GetBytes(Environment.NewLine);
            CurrentStreamLength = this.fileStream.Length;
            this.processingTask = Task.Factory.StartNew(ConsumeQueue, TaskCreationOptions.LongRunning);
        }

        public string FileName { get; }

        public long CurrentStreamLength { get; private set; }

        private void ConsumeQueue()
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (this.q.TryTake(out var nextString, Timeout.Infinite, this.cancellationTokenSource.Token))
                    {
                        try
                        {
                            var bytes = this.encoding.GetBytes(nextString);

                            CurrentStreamLength += bytes.Length + this.newLine.Length;

                            if (!this.autoFlush)
                            {
                                this.fileStream.Write(bytes, 0, bytes.Length);
                                this.fileStream.Write(this.newLine, 0, this.newLine.Length);
                            }
                            else
                            {
                                this.fileStream.Write(bytes, 0, bytes.Length);
                                this.fileStream.Write(this.newLine, 0, this.newLine.Length);
                                this.fileStream.Flush();
                            }
                        }
                        catch (Exception ex)
                        {
                            EtwStreamEventSource.Log.SinkError(this.sinkName, "FileStream Write/Flush failed", ex.ToString());
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        public void Enqueue(string value)
        {
            this.q.Add(value);
        }

        public string[] MakeFinal()
        {
            if (Interlocked.Increment(ref this.isDisposed) == 1)
            {
                this.cancellationTokenSource.Cancel();
                this.processingTask.Wait();
                try
                {
                    this.fileStream.Close();
                }
                catch (Exception ex)
                {
                    EtwStreamEventSource.Log.SinkError(this.sinkName, "FileStream Dispose failed", ex.ToString());
                }

                // rest line...
                return this.q.ToArray();
            }

            return null;
        }
    }
}
