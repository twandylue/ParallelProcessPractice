using ParallelProcessPractice.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AndyLuDemo
{
    // 串流
    public class AndyLuBasicTaskRunner1 : TaskRunnerBase
    {
        public override void Run(IEnumerable<MyTask> tasks)
        {
            foreach (var t in tasks)
            {
                t.DoStepN(1);
                t.DoStepN(2);
                t.DoStepN(3);
                Console.WriteLine($"exec job: {t.ID} completed.");
            }
        }
    }

    // 批次
    public class AndyLuBasicTaskRunner2 : TaskRunnerBase
    {
        public override void Run(IEnumerable<MyTask> tasks)
        {
            var tasklist = tasks.ToArray();
            foreach (var task in tasklist) task.DoStepN(1);
            foreach (var task in tasklist) task.DoStepN(2);
            foreach (var task in tasklist) task.DoStepN(3);
        }
    }
}