using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal sealed class DdfWinFormsEventDispatcher : IDdfEditorEventDispatcher
    {
        private readonly Control owner;
        private readonly int ownerThreadId;
        private readonly SynchronizationContext synchronizationContext;
        private readonly object syncRoot = new object();
        private readonly Queue<Action> pendingCallbacks = new Queue<Action>();

        public DdfWinFormsEventDispatcher(Control owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ownerThreadId = Environment.CurrentManagedThreadId;
            synchronizationContext = SynchronizationContext.Current;
            owner.HandleCreated += owner_HandleCreated;
            owner.Disposed += owner_Disposed;
        }

        public bool HasAccess =>
            Environment.CurrentManagedThreadId == ownerThreadId;

        public void Post(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (owner.IsDisposed || owner.Disposing) return;
            if (HasAccess)
            {
                callback();
                return;
            }
            if (owner.IsHandleCreated)
            {
                try
                {
                    owner.BeginInvoke(new Action<Action>(InvokeIfAlive), callback);
                    return;
                }
                catch (InvalidOperationException)
                {
                    if (owner.IsDisposed || owner.Disposing) return;
                }
            }
            if (synchronizationContext != null)
            {
                synchronizationContext.Post(static state =>
                {
                    var invocation = ((DdfWinFormsEventDispatcher Dispatcher,
                        Action Callback))state;
                    invocation.Dispatcher.InvokeIfAlive(invocation.Callback);
                }, (this, callback));
                return;
            }
            lock (syncRoot)
            {
                if (!owner.IsDisposed && !owner.Disposing)
                    pendingCallbacks.Enqueue(callback);
            }
        }

        private void owner_HandleCreated(object sender, EventArgs e)
        {
            Action[] pending;
            lock (syncRoot)
            {
                pending = pendingCallbacks.ToArray();
                pendingCallbacks.Clear();
            }
            foreach (Action callback in pending) InvokeIfAlive(callback);
        }

        private void owner_Disposed(object sender, EventArgs e)
        {
            lock (syncRoot) pendingCallbacks.Clear();
        }

        private void InvokeIfAlive(Action callback)
        {
            if (!owner.IsDisposed && !owner.Disposing) callback();
        }
    }
}
