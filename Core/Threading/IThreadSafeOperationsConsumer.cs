using ExplorerPro.Core.Threading;

namespace ExplorerPro.Core.Threading
{
    public interface IThreadSafeOperationsConsumer
    {
        void SetThreadSafeOperations(ThreadSafeTabOperations threadSafeOperations);
    }
}


