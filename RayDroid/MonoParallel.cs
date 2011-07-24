using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Mono.Threading.Tasks
{
    public static class Parallel
    {
        static readonly bool sixtyfour = IntPtr.Size == 8;

        internal static int GetBestWorkerNumber()
        {
            return GetBestWorkerNumber(TaskScheduler.Current);
        }

        internal static int GetBestWorkerNumber(TaskScheduler scheduler)
        {
            return scheduler.MaximumConcurrencyLevel;
        }

        static int GetBestWorkerNumber(int from, int to, ParallelOptions options, out int step)
        {
            int num = GetBestWorkerNumber(options.TaskScheduler);
            if (options != null && options.MaxDegreeOfParallelism != -1)
                num = options.MaxDegreeOfParallelism;
            // Integer range that each task process
            if ((step = (to - from) / num) < 5)
            {
                step = 5;
                num = (to - from) / 5;
                if (num < 1)
                    num = 1;
            }

            return num;
        }

        static void HandleExceptions(IEnumerable<Task> tasks)
        {
            List<Exception> exs = new List<Exception>();
            foreach (Task t in tasks)
            {
                if (t.Exception != null)
                    exs.Add(t.Exception);
            }

            if (exs.Count > 0)
            {
                throw new AggregateException(exs);
            }
        }

        static void InitTasks(Task[] tasks, int count, Action action, ParallelOptions options)
        {
            TaskCreationOptions creation = TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent;

            for (int i = 0; i < count; i++)
            {
                if (options == null)
                    tasks[i] = Task.Factory.StartNew(action, creation);
                else
                    tasks[i] = Task.Factory.StartNew(action, options.CancellationToken, creation, options.TaskScheduler);
            }
        }

        public static void For<TLocal>(int fromInclusive,
                                                      int toExclusive,
                                                      ParallelOptions parallelOptions,
                                                      Func<TLocal> localInit,
                                                      Func<int, ParallelLoopState, TLocal, TLocal> body,
                                                      Action<TLocal> localFinally)
        {
            if (body == null)
                throw new ArgumentNullException("body");
            if (localInit == null)
                throw new ArgumentNullException("localInit");
            if (localFinally == null)
                throw new ArgumentNullException("localFinally");
            if (parallelOptions == null)
                throw new ArgumentNullException("options");
            if (fromInclusive >= toExclusive)
                return;

            // Number of task toExclusive be launched (normally == Env.ProcessorCount)
            int step;
            int num = GetBestWorkerNumber(fromInclusive, toExclusive, parallelOptions, out step);

            Task[] tasks = new Task[num];

            StealRange[] ranges = new StealRange[num];
            for (int i = 0; i < num; i++)
                ranges[i] = new StealRange(fromInclusive, i, step);

            //ParallelLoopState.ExternalInfos infos = new ParallelLoopState.ExternalInfos();

            int currentIndex = -1;

            Action workerMethod = delegate
            {
                int localWorker = Interlocked.Increment(ref currentIndex);
                StealRange range = ranges[localWorker];
                int index = range.V64.Actual;
                int stopIndex = localWorker + 1 == num ? toExclusive : System.Math.Min(toExclusive, index + step);
                TLocal local = localInit();

                CancellationToken token = parallelOptions.CancellationToken;

                try
                {
                    for (int i = index; i < stopIndex; )
                    {
                        token.ThrowIfCancellationRequested();

                        if (i >= stopIndex - range.V64.Stolen)
                            break;

                        local = body(i, null, local);

                        if (i + 1 >= stopIndex - range.V64.Stolen)
                            break;

                        range.V64.Actual = ++i;
                    }

                    // Try toExclusive steal fromInclusive our right neighbor (cyclic)
                    int len = num + localWorker;
                    for (int sIndex = localWorker + 1; sIndex < len; ++sIndex)
                    {
                        int extWorker = sIndex % num;
                        range = ranges[extWorker];

                        stopIndex = extWorker + 1 == num ? toExclusive : System.Math.Min(toExclusive, fromInclusive + (extWorker + 1) * step);
                        int stolen = -1;

                        do
                        {
                            do
                            {
                                long old;
                                StealValue64 val = new StealValue64();

                                old = sixtyfour ? range.V64.Value : Interlocked.CompareExchange(ref range.V64.Value, 0, 0);
                                val.Value = old;

                                if (val.Actual >= stopIndex - val.Stolen - 2)
                                    goto next;
                                stolen = (val.Stolen += 1);

                                if (Interlocked.CompareExchange(ref range.V64.Value, val.Value, old) == old)
                                    break;
                            } while (true);

                            stolen = stopIndex - stolen;

                            if (stolen > range.V64.Actual)
                                local = body(stolen, null, local);
                            else
                                goto next;

                            /*for (int j = stealAmout - 1; j >= 0; --j)
                            {
                                int steal = stopIndex - stolen + j;

                                if (steal > range.V64.Actual)
                                    local = body(steal, null, local);
                                else
                                    goto next;
                            }*/
                        } while (true);

                    next:
                        continue;
                    }
                }
                finally
                {
                    localFinally(local);
                }
            };

            InitTasks(tasks, num, workerMethod, parallelOptions);

            try
            {
                Task.WaitAll(tasks);
            }
            catch
            {
                HandleExceptions(tasks);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct StealValue64
        {
            [FieldOffset(0)]
            public long Value;
            [FieldOffset(0)]
            public int Actual;
            [FieldOffset(4)]
            public int Stolen;
        }

        class StealRange
        {
            public StealValue64 V64 = new StealValue64();

            public StealRange(int fromInclusive, int i, int step)
            {
                V64.Actual = fromInclusive + i * step;
            }
        }
    }
}