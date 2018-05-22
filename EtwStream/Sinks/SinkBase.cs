#region Using Statements

using System;
using System.Collections.Generic;
using System.Threading;

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    public abstract class SinkBase<T> : IObserver<T>, IObserver<IList<T>>, IDisposable
    {
        public abstract void Dispose();

        public abstract void OnNext(IList<T> value);

        public virtual void OnNext(T value)
            => OnNext(new[] { value });

        public virtual void OnError(Exception error)
            => Dispose();

        public virtual void OnCompleted()
            => Dispose();

        public IDisposable CreateLinkedDisposable(IDisposable subscription) => new BinaryCompositeDisposable(subscription, this);

        private class BinaryCompositeDisposable : IDisposable
        {
            private volatile IDisposable disposable1;

            private volatile IDisposable disposable2;

            public BinaryCompositeDisposable(IDisposable disposable1, IDisposable disposable2)
            {
                this.disposable1 = disposable1;
                this.disposable2 = disposable2;
            }

            public void Dispose()
            {
#pragma warning disable 0420
                var old1 = Interlocked.Exchange(ref this.disposable1, null);
#pragma warning restore 0420
                old1?.Dispose();

#pragma warning disable 0420
                var old2 = Interlocked.Exchange(ref this.disposable2, null);
#pragma warning restore 0420
                old2?.Dispose();
            }
        }
    }
}
