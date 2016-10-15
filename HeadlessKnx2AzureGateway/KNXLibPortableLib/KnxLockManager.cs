namespace KNXLibPortableLib
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class KnxLockManager
    {
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(0);
        private readonly object _connectedLock = new object();
        private bool _isConnected;

        public int LockCount
        {
            get { return this._sendLock.CurrentCount; }
        }

        public void LockConnection()
        {
            lock (this._connectedLock)
            {
                if (!this._isConnected)
                    return;

                this.SendLock();
                this._isConnected = false;
            }
        }

        public void UnlockConnection()
        {
            lock (this._connectedLock)
            {
                if (this._isConnected)
                    return;

                this._isConnected = true;
                this.SendUnlock();
            }
        }

        public void PerformLockedOperation(Action action)
        {
            // TODO: Shouldn't this check if we are connected?

            try
            {
                this.SendLock();
                action();
            }
            finally
            {
                this.SendUnlockPause();
            }
        }

        private void SendLock()
        {
            this._sendLock.Wait();
        }

        private void SendUnlock()
        {
            this._sendLock.Release();
        }

        private void SendUnlockPause()
        {
            var task = new Task(this.SendUnlockPauseThread);
            task.Start();
        }

        private void SendUnlockPauseThread()
        {
            Task.Delay(200);
            this._sendLock.Release();
        }
    }
}