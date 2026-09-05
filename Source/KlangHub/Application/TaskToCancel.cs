using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KlangHub.Application
{
    public class TaskToCancel
    {
        public Task Task { get; set; } = null!;
        public CancellationTokenSource TokenSource { get; set; } = null!;
    }

    public class TasksToCancel
    {
        /// <summary>
        /// Never reassigned. It used to be set to null at the end of Dispose, while Add tested it for
        /// null and then locked on it - so a task starting as the program closed could pass the test,
        /// have the field cleared underneath it, and take a NullReferenceException on the lock. A field
        /// you lock on must be the same object for the life of the instance.
        /// </summary>
        private readonly List<TaskToCancel> taskList = new List<TaskToCancel>();
        private volatile bool IsDisposed = false;

        public void Add(Action action, CancellationTokenSource? cancellationTokenSource = null)
        {
            if (action == null || IsDisposed)
                return;

            lock(taskList)
            {
                if (cancellationTokenSource == null)
                {
                    cancellationTokenSource = new CancellationTokenSource();
                }

                // LongRunning, because nearly every action handed to this blocks: a reconnect waits out
                // its backoff, a status poll waits on a device that may be switched off. Without it these
                // sit on thread-pool threads, and enough unreachable devices at once starve the pool that
                // the rest of the program needs.
                var task = Task.Factory.StartNew(action, cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
                taskList.Add(new TaskToCancel { Task = task, TokenSource = cancellationTokenSource });

                taskList.RemoveAll(x => x?.Task == null || x.Task.IsCompleted);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            lock(taskList)
            {
                foreach (var item in taskList)
                {
                    if (item.Task != null)
                    {
                        if (!item.Task.IsCompleted)
                        {
                            item.TokenSource.Cancel();
                            item.TokenSource.Dispose();
                            if (item.Task.Status == TaskStatus.RanToCompletion
                                || item.Task.Status == TaskStatus.Canceled
                                || item.Task.Status == TaskStatus.Faulted)
                            {
                                item.Task.Dispose();
                            }
                        }
                    }
                }
                taskList.RemoveAll(x => x?.Task == null || x.Task.IsCompleted);
            }
            Task[] stillRunning;
            lock (taskList)
            {
                stillRunning = taskList.Select(x => x.Task).Where(t => t != null).ToArray();
            }

            Task.WaitAll(stillRunning, 4000);

            lock (taskList)
            {
                taskList.RemoveAll(x => x?.Task == null || x.Task.IsCompleted);
            }
        }
    }
}