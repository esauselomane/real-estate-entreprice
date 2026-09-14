using System.Threading.Channels;

namespace CRM.Microservice.Services.BackgroundJobs
{
    /// <summary>
    /// Generic in-memory job queue using System.Threading.Channels.
    /// Provides bounded queue with backpressure for background job processing.
    /// </summary>
    public interface IJobQueue<TJob>
    {
        ValueTask EnqueueAsync(TJob job, CancellationToken ct = default);
        ValueTask<TJob> DequeueAsync(CancellationToken ct = default);
    }

    public class JobQueue<TJob> : IJobQueue<TJob>
    {
        private readonly Channel<TJob> _channel;

        public JobQueue(int capacity = 1000)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<TJob>(options);
        }

        public ValueTask EnqueueAsync(TJob job, CancellationToken ct = default)
        {
            return _channel.Writer.WriteAsync(job, ct);
        }

        public ValueTask<TJob> DequeueAsync(CancellationToken ct = default)
        {
            return _channel.Reader.ReadAsync(ct);
        }
    }

    /// <summary>
    /// Job queue with completion tracking for long-running jobs.
    /// </summary>
    public interface ITrackedJobQueue<TJob, TResult>
    {
        ValueTask<TResult> EnqueueAndWaitAsync(TJob job, CancellationToken ct = default);
        ValueTask EnqueueAsync(TJob job, CancellationToken ct = default);
    }

    public class TrackedJobQueue<TJob, TResult> : ITrackedJobQueue<TJob, TResult>
    {
        private readonly Channel<(TJob Job, TaskCompletionSource<TResult> Tcs)> _channel;

        public TrackedJobQueue(int capacity = 1000)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<(TJob, TaskCompletionSource<TResult>)>(options);
        }

        public ValueTask EnqueueAsync(TJob job, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _channel.Writer.WriteAsync((job, tcs), ct);
        }

        public async ValueTask<TResult> EnqueueAndWaitAsync(TJob job, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _channel.Writer.WriteAsync((job, tcs), ct);
            return await tcs.Task.WaitAsync(ct);
        }

        internal bool TryComplete(TJob job, TResult result)
        {
            // This is a simplified version - in reality you'd need a dictionary to track by job
            return true;
        }

        internal ValueTask<(TJob Job, TaskCompletionSource<TResult> Tcs)> DequeueAsync(CancellationToken ct = default)
        {
            return _channel.Reader.ReadAsync(ct);
        }
    }
}