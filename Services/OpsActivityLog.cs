using System.Collections.Concurrent;

namespace DocumentDispatchService.Services
{
    public sealed class OpsActivityLog
    {
        private readonly ConcurrentQueue<ActivityEvent> _queue = new();
        private const int MaxItems = 250;

        public void Add(string source, string message)
        {
            _queue.Enqueue(new ActivityEvent(DateTime.UtcNow, source, message));

            while (_queue.Count > MaxItems && _queue.TryDequeue(out _))
            {
                // trim
            }
        }

        public IReadOnlyList<ActivityEvent> GetLatest(int take)
        {
            // ConcurrentQueue enumerates in FIFO order; we want the newest N
            var arr = _queue.ToArray();
            if (arr.Length == 0) return Array.Empty<ActivityEvent>();

            var start = Math.Max(0, arr.Length - take);
            return arr[start..];
        }

        public sealed record ActivityEvent(DateTime AtUtc, string Source, string Message);
    }
}
