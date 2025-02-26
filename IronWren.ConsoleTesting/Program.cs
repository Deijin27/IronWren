﻿using IronWren.AutoMapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IronWren.ConsoleTesting
{
    internal class Program
    {
        private static void alloc(WrenVM vm)
        {
            Console.WriteLine("Allocator called!");
            vm.SetSlotNewForeign(0, 1);
        }

        private static void Main(string[] args)
        {
            var config = new WrenConfig();
            config.Write += (vm, text) => Console.Write(text);
            config.Error += (vm, type, module, line, message) => Console.WriteLine($"Error [{type}] in module [{module}] at line {line}:{Environment.NewLine}{message}");

            config.LoadModule += (vm, module) => new WrenLoadModuleResult { Source = $"System.print(\"Module [{module}] loaded!\")" };

            config.BindForeignMethod += (vm, module, className, isStatic, signature) =>
            {
                Console.WriteLine($"BindForeignMethod called: It's called {signature}, is part of {className} and is {(isStatic ? "static" : "not static")}.");
                return (signature == "sayHi(_)" ? sayHi : (WrenForeignMethod)null);
            };

            config.BindForeignClass += (vm, module, className) => className == "Test" ? new WrenForeignClassMethods { Allocate = alloc } : null;

            using (var vm = new WrenVM(config))
            {
                var result = vm.Interpret("System.print(\"Hi from Wren!\")");

                result = vm.Interpret("var helloTo = Fn.new { |name|\n" +
                    "System.print(\"Hello, %(name)!\")\n" +
                    "}");

                result = vm.Interpret("helloTo.call(\"IronWren\")");

                var someFnHandle = vm.MakeCallHandle("call(_)");

                vm.EnsureSlots(2);
                vm.GetVariable(WrenVM.MainModule, "helloTo", 0);
                vm.SetSlotString(1, "foreign method");
                result = vm.Call(someFnHandle);

                result = vm.Interpret("foreign class Test {\n" +
                    "construct new() { }\n" +
                    "isForeign { true }\n" +
                    "foreign sayHi(to)\n" +
                    "}\n" +
                    "var test = Test.new()\n" +
                    "test.sayHi(\"wren\")\n" +
                    "\n" +
                    "import \"TestModule\"\n");

                vm.EnsureSlots(1);
                vm.GetVariable(WrenVM.MainModule, "test", 0);
                result = vm.Call(vm.MakeCallHandle("isForeign"));
                var isTestClassForeign = vm.GetSlotBool(0);

                Console.WriteLine("Test class is foreign: " + isTestClassForeign);

                vm.AutoMap(typeof(WrenMath));
                vm.Interpret("System.print(\"The sine of pi is: %(Math.sin(Math.pi))!\")");
                Console.WriteLine($"And C# says it's: {Math.Sin(Math.PI)}");

                Console.WriteLine();

                var sw = new Stopwatch();

                for (var i = 0; i < 3; ++i)
                {
                    sw.Restart();
                    vm.Interpret("for (i in 1..1000000) Math.sin(Math.pi)");
                    sw.Stop();

                    Console.WriteLine("1000000 iterations took " + sw.ElapsedMilliseconds + "ms.");
                }

                var results = new double[1000000];
                sw.Restart();
                for (var i = 0; i < 1000000; ++i)
                {
                    results[i] = Math.Sin(Math.PI);
                }
                sw.Stop();

                Console.WriteLine("1000000 iterations in C# took " + sw.ElapsedMilliseconds + "ms.");

                vm.AutoMap<WrenVector>();
                vm.Interpret("var vec = Vector.new(1, 2)");
                vm.Interpret("vec.print()");
                vm.Interpret("System.print(\"Vector's X is: %(vec.x)\")");
                vm.Interpret("System.print(\"Vector's Y is: %(vec.y)\")");

                Console.ReadLine();
                Console.Clear();
                Console.WriteLine("You may now write Wren code that will be interpreted!");
                Console.WriteLine("Use file:[path] to interpret a file!");
                Console.WriteLine();

                while (true)
                {
                    var input = Console.ReadLine();

                    if (input.StartsWith("file:"))
                        vm.Interpret(File.ReadAllText(input.Substring(5)));
                    else
                        vm.Interpret(input);
                }
            }
        }

        private static void sayHi(WrenVM vm)
        {
            var to = vm.GetSlotString(1);
            Console.WriteLine("Foreign method says hi to " + to + "!");
        }
    }
}