using System.Threading.Channels;

namespace PRN222_FINAL.BLL.Services.Benchmarks;

public interface IBenchmarkJobQueue
{
    ValueTask EnqueueBenchmarkAsync(Guid runId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueBenchmarkAsync(CancellationToken cancellationToken = default);
}

public sealed class BenchmarkJobQueue : IBenchmarkJobQueue
{
    private readonly Channel<Guid> _queue;

    public BenchmarkJobQueue()
    {
        _queue = Channel.CreateUnbounded<Guid>();
    }

    public ValueTask EnqueueBenchmarkAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(runId, cancellationToken);
    }

    public ValueTask<Guid> DequeueBenchmarkAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
