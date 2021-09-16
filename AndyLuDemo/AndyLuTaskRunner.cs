using ParallelProcessPractice.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;

namespace AndyLuDemo
{
    // using linq 11 = 5+3+3 但不能精確掌控step1 最多被5個thread 執行
    public class AndyLuTaskThreadRunner1 : TaskRunnerBase
    {
        private volatile int count = 0;
        private object _lock = new object();
        public override void Run(IEnumerable<MyTask> tasks)
        {

            tasks.AsParallel()
                .WithDegreeOfParallelism(11)
                .ForAll((task) =>
                {

                    lock (_lock)
                    {
                        count += 1;
                        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: {count}"); // 測試thread 最大數量
                    }

                    task.DoStepN(1);
                    task.DoStepN(2);
                    task.DoStepN(3);

                    lock (_lock)
                    {
                        count -= 1;
                    }

                });
        }
    }

    // using TPL + linq
    public class AndyLuTaskThreadRunner2 : TaskRunnerBase
    {
        public override void Run(IEnumerable<MyTask> tasks)
        {
            tasks.AsParallel().WithDegreeOfParallelism(5 + 3 + 3)
                .ForAll((t) =>
                {
                    // Task 會自己維護一個Thread pool 有空閒就會接著做
                    Task.Run(() => { t.DoStepN(1); })
                        .ContinueWith((x) => { t.DoStepN(2); })
                        .ContinueWith((x) => { t.DoStepN(3); })
                        .Wait();
                });
        }
    }

    // pipline + counts(精準) + thread pool + release list
    public class AndyLuPipelineRunner1 : TaskRunnerBase
    {
        public override void Run(IEnumerable<MyTask> tasks)
        {
            List<Thread> threads = new List<Thread>();
            Thread t = null;
            int[] counts = {
                0,
                5,
                3,
                3
            };
            foreach (var task in tasks) this.queues[1].Add(task);

            for (int step = 1; step <= 3; step++)
            {
                for (int i = 0; i < counts[step]; i++)
                {
                    threads.Add(t = new Thread(this.RunAllStepN));
                    t.Start(step);
                }
            }

            for (int step = 1; step <= 3; step++)
            {
                this.queues[step].CompleteAdding();
                for (int i = 0; i < counts[step]; i++)
                {
                    threads[0].Join();
                    threads.RemoveAt(0); // 釋放資源
                }
            }
        }
        private BlockingCollection<MyTask>[] queues = new BlockingCollection<MyTask>[3 + 1] {
            null,
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>()
        };
        private void RunAllStepN(object step_value)
        {
            int step = (int)step_value;
            // bool _first = (step == 1);
            bool _last = (step == 3);
            foreach (var task in this.queues[step].GetConsumingEnumerable())
            {
                task.DoStepN(step);
                if (!_last) this.queues[step + 1].Add(task);
            }
        }
    }

    // pipline + CocurrentQueue + thread pool + lock + counter
    public class AndyLuPipelineRunner2 : TaskRunnerBase
    {
        private readonly object _lock = new Object();
        private volatile int[] task_step_done = { 0, 0, 0, 0 };
        private int total_tasks = 30;
        private ConcurrentQueue<MyTask>[] queues = new ConcurrentQueue<MyTask>[3 + 1] {
            null,
            new ConcurrentQueue<MyTask>(),
            new ConcurrentQueue<MyTask>(),
            new ConcurrentQueue<MyTask>()
        };
        private void DoAllStepN(object step_value)
        {
            int step = (int)step_value;
            while (task_step_done[step] < total_tasks)
            {
                if (this.queues[step].TryDequeue(out MyTask task))
                {
                    task.DoStepN(step);
                    lock (_lock)
                    {
                        task_step_done[step] += 1;
                    }
                    if (step < 3) this.queues[step + 1].Enqueue(task);
                }
            }
        }

        public override void Run(IEnumerable<MyTask> tasks)
        {
            List<Thread> threads = new List<Thread>();
            int[] step_limits = { 0, 5, 3, 3 };
            foreach (var task in tasks) this.queues[1].Enqueue(task);
            for (int step = 1; step <= 3; step++)
            {
                for (int i = 0; i < step_limits[step]; i++)
                {
                    Thread t = new Thread(this.DoAllStepN);
                    t.Start(step);
                    threads.Add(t);
                }
            }
            foreach (var t in threads) t.Join(); // 關鍵 回到 calling thread

        }
    }

    // pipline + TPL
    public class AndyLuPipelineRunner3 : TaskRunnerBase
    {
        private BlockingCollection<MyTask>[] queues = new BlockingCollection<MyTask>[3 + 1] {
            null,
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>()
        };

        private void DoAllStepN(int step)
        {
            foreach (var task in this.queues[step].GetConsumingEnumerable())
            {
                task.DoStepN(step);
                if (step < 3) this.queues[step + 1].Add(task);
            }
            if (step < 3) this.queues[step + 1].CompleteAdding();
        }

        public override void Run(IEnumerable<MyTask> tasks)
        {
            List<Task> tasks_list = new List<Task>();
            foreach (var task in tasks) this.queues[1].Add(task);
            this.queues[1].CompleteAdding();

            for (int step = 1; step <= 3; step++)
            {
                int temp = step;
                Task t = Task.Run(() => this.DoAllStepN(temp));
                tasks_list.Add(t);
            }

            foreach (var t in tasks_list) t.Wait();
        }
    }

    // pipeline + counts(精準) + TPL + release list
    public class AndyLuPipelineRunner4 : TaskRunnerBase
    {
        private BlockingCollection<MyTask>[] queues = new BlockingCollection<MyTask>[3 + 1] {
            null,
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>()
        };

        private void DoAllStepN(int step)
        {
            foreach (var task in this.queues[step].GetConsumingEnumerable())
            {
                task.DoStepN(step);
                if (step < 3) this.queues[step + 1].Add(task);
            }
        }

        public override void Run(IEnumerable<MyTask> tasks)
        {
            List<Task> tasks_list = new List<Task>();
            int[] counts = {
                0,
                5,
                3,
                3
            };
            foreach (var task in tasks) this.queues[1].Add(task);

            for (int step = 1; step <= 3; step++)
            {
                for (int i = 0; i < counts[step]; i++)
                {
                    int temp = step;
                    Task t = Task.Run(() => this.DoAllStepN(temp));
                    tasks_list.Add(t);
                }
            }

            for (int step = 1; step <= 3; step++)
            {
                this.queues[step].CompleteAdding();
                for (int i = 0; i < counts[step]; i++)
                {
                    tasks_list[0].Wait();
                    tasks_list.RemoveAt(0);
                }
            }
        }
    }

    // pipeline + counts(不精準) + TPL + lock + counter
    public class AndyLuPipelineRunner5 : TaskRunnerBase
    {
        private int[] task_step_done = { 0, 0, 0, 0 };
        private int total_task = 30;
        private object _lock = new object();
        private BlockingCollection<MyTask>[] queues = new BlockingCollection<MyTask>[3 + 1] {
            null,
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>(),
            new BlockingCollection<MyTask>()
        };

        private void DoAllStepN(int step)
        {
            foreach (var task in this.queues[step].GetConsumingEnumerable())
            {
                task.DoStepN(step);
                lock (_lock)
                {
                    task_step_done[step]++;
                    if (task_step_done[step] >= total_task) this.queues[step].CompleteAdding();
                }
                if (step < 3) this.queues[step + 1].Add(task);
            }
        }

        public override void Run(IEnumerable<MyTask> tasks)
        {
            List<Task> tasks_list = new List<Task>();
            foreach (var task in tasks) this.queues[1].Add(task);
            this.queues[1].CompleteAdding();

            for (int i = 0; i < 3; i++)
            {
                for (int step = 1; step <= 3; step++)
                {
                    int temp = step;
                    Task t = Task.Run(() => this.DoAllStepN(temp));
                    tasks_list.Add(t);
                }
            }

            foreach (var t in tasks_list) t.Wait();
        }
    }

    public class AndyLuPipelineRunner6 : TaskRunnerBase
    {
        private ConcurrentQueue<MyTask>[] queues = new ConcurrentQueue<MyTask>[1 + 3] {
            null,
            new ConcurrentQueue<MyTask>(),
            new ConcurrentQueue<MyTask>(),
            new ConcurrentQueue<MyTask>()
        };
        private ManualResetEvent[] waithandles = new ManualResetEvent[1 + 3] {
            null,
            new ManualResetEvent(false),
            new ManualResetEvent(false),
            new ManualResetEvent(false)
        };
        private int totalTask;
        private int[] taskDone = { 0, 0, 0, 0 };
        private object[] _locks = { null, new Object(), new Object(), new Object() };
        private void DoAllStep(int step)
        {
            while (this.waithandles[step].WaitOne()) // 等待開啟
            {
                if (this.queues[step].TryDequeue(out MyTask task))
                {
                    task.DoStepN(step);
                    lock (_locks[step]) taskDone[step]++;
                    if (step < 3)
                    {
                        if (this.waithandles[step + 1].WaitOne(0) == false) this.waithandles[step + 1].Set(); // 開啟下一個step 的訊號
                        this.queues[step + 1].Enqueue(task);
                    }
                }
                else if (taskDone[step] >= totalTask) break; // 事情做完後要停止等待
            }
        }
        public override void Run(IEnumerable<MyTask> tasks)
        {
            List<Task> taskPool = new List<Task>();
            int[] counts = { 0, 5, 3, 3 };
            foreach (var task in tasks) this.queues[1].Enqueue(task);
            totalTask = this.queues[1].Count;
            this.waithandles[1].Set(); // 開始訊號
            Parallel.For(1, counts.Length, (step) =>
            {
                for (int i = 0; i < counts[step]; i++)
                {
                    Task t = Task.Run(() => { this.DoAllStep(step); });
                    taskPool.Add(t);
                }
            });
            foreach (var t in taskPool) t.Wait();
        }
    }
}