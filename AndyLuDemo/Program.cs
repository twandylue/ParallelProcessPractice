using ParallelProcessPractice.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace AndyLuDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            TaskRunnerBase run = 
                new AndyLuBasicTaskRunner1();
                // new AndyLuBasicTaskRunner2();
                // new AndyLuTaskThreadRunner1();
                // new AndyLuTaskThreadRunner2();
                // new AndyLuPipelineRunner1();
                // new AndyLuPipelineRunner2();
                // new AndyLuPipelineRunner3();
                // new AndyLuPipelineRunner4();
                // new AndyLuPipelineRunner5();
            run.ExecuteTasks(30);
        }
    }
}
