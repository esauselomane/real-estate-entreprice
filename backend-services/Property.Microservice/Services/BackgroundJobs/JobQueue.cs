using System.Threading.Channels;

namespace Property.Microservice.Services.BackgroundJobs
{
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
}