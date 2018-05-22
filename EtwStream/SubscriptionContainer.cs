#region Using Statements

using System;
using System.Reactive.Disposables;

#endregion

// ReSharper disable UnusedMember.Global
namespace EtwStream
{
    public interface ISubscriptionContainer
    {
        void Add(IDisposable subscription);
    }

    public class SubscriptionContainer : ISubscriptionContainer
    {
        private readonly CompositeDisposable subscriptions = new CompositeDisposable();

        public void Add(IDisposable subscription)
        {
            this.subscriptions.Add(subscription);
        }

        public void Dispose()
        {
            this.subscriptions.Dispose();
        }
    }

    public static class EtwStreamSubscriptionContainerExtensions
    {
        public static void AddTo(this IDisposable subscription, ISubscriptionContainer container)
        {
            container.Add(subscription);
        }
    }
}
