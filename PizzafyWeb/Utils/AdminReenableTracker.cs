using System.Collections.Concurrent;

namespace PizzafyWeb.Utils
{
    // Simple in-memory tracker. For production replace with persistent storage.
    public static class AdminReenableTracker
    {
        private static readonly ConcurrentDictionary<int, byte> _pending = new();
        public static void Mark(int userId) => _pending[userId] = 1;
        public static bool Consume(int userId) => _pending.TryRemove(userId, out _);
    }
}
