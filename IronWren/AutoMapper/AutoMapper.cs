﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace IronWren.AutoMapper
{
    /// <summary>
    /// Contains extension methods for the <see cref="WrenVM"/> class to automatically map C# classes to Wren.
    /// </summary>
    public static class AutoMapper
    {
        /// <summary>
        /// Dictionary of generated modules mapped to their names and VMs.
        /// <para/>
        /// Also used for the VMs to which the lookup functions for the AutoMapper have been added.
        /// </summary>
        private static readonly Dictionary<WrenVM, Dictionary<string, ForeignModule>> generatedModules =
            new Dictionary<WrenVM, Dictionary<string, ForeignModule>>();

        private static readonly Dictionary<WrenVM, Dictionary<string, ForeignClass>> mainModuleClasses =
            new Dictionary<WrenVM, Dictionary<string, ForeignClass>>();

        /// <summary>
        /// Gets or sets whether a module getting modified after being loaded throws an Exception.
        /// <para/>
        /// On by default.
        /// </summary>
        public static bool TreatModificationAfterLoadAsError { get; set; } = true;

        /// <summary>
        /// Automatically maps the given type to make its public interface accessible from Wren. Optionally places it into a module
        /// other than the <see cref="WrenVM.InterpetModule"/>.
        /// <para/>
        /// If no other module is specified, the generated code will be interpreted immediately.
        /// </summary>
        /// <typeparam name="TTarget">The type to map.</typeparam>
        /// <param name="vm">The <see cref="WrenVM"/> to make the type available to.</param>
        /// <param name="moduleName">The name of the module to place the type into.</param>
        public static void AutoMap<TTarget>(this WrenVM vm, string moduleName = WrenVM.InterpetModule)
        {
            vm.AutoMap(moduleName, typeof(TTarget));
        }

        /// <summary>
        /// Automatically maps the given types to make their public interfaces accessible from Wren. Optionally places them into a module
        /// other than the <see cref="WrenVM.InterpetModule"/>.
        /// <para/>
        /// The generated code will be interpreted immediately.
        /// </summary>
        /// <param name="vm">The <see cref="WrenVM"/> to make the types available to.</param>
        /// <param name="targets">The types to map.</param>
        public static void AutoMap(this WrenVM vm, params Type[] targets)
        {
            vm.AutoMap(WrenVM.InterpetModule, targets);
        }

        /// <summary>
        /// Automatically maps the given types to make their public interfaces accessible from Wren. Optionally places them into a module
        /// other than the <see cref="WrenVM.InterpetModule"/>.
        /// <para/>
        /// If no other module is specified, the generated code will be interpreted immediately.
        /// </summary>
        /// <param name="vm">The <see cref="WrenVM"/> to make the types available to.</param>
        /// <param name="moduleName">The name of the module to place the types into.</param>
        /// <param name="targets">The types to map.</param>
        public static void AutoMap(this WrenVM vm, string moduleName, params Type[] targets)
        {
            checkInitialization(vm);

            if (moduleName == WrenVM.InterpetModule)
            {
                if (!mainModuleClasses.ContainsKey(vm))
                    mainModuleClasses.Add(vm, new Dictionary<string, ForeignClass>());

                foreach (var target in targets)
                {
                    var foreignClass = new ForeignClass(target.GetTypeInfo());
                    var classAttribute = target.GetTypeInfo().GetCustomAttribute<WrenClassAttribute>();

                    mainModuleClasses[vm].Add(classAttribute?.Name ?? target.Name, foreignClass);

                    vm.Interpret(foreignClass.GetSource());
                }

                return;
            }

            ForeignModule module;
            if (generatedModules.ContainsKey(vm) && generatedModules[vm].ContainsKey(moduleName))
            {
                module = generatedModules[vm][moduleName];

                if (module.Used && TreatModificationAfterLoadAsError)
                    throw new LoadedModuleModifiedException(moduleName);
            }
            else
            {
                module = new ForeignModule();

                if (moduleName != WrenVM.InterpetModule)
                    generatedModules[vm].Add(moduleName, module);
            }

            foreach (var target in targets)
                module.Add(target);
        }

        private static void checkInitialization(WrenVM vm)
        {
            if (generatedModules.ContainsKey(vm))
                return;

            vm.Config.LoadModule += loadAutoMapperModule;
            vm.Config.BindForeignMethod += bindAutoMapperMethod;
            vm.Config.BindForeignClass += bindAutoMapperClass;

            generatedModules.Add(vm, new Dictionary<string, ForeignModule>());
        }

        #region VM Config Methods

        private static WrenForeignClassMethods bindAutoMapperClass(WrenVM vm, string module, string className)
        {
            if (!generatedModules.ContainsKey(vm)
                || !generatedModules[vm].ContainsKey(module)
                || !generatedModules[vm][module].Classes.ContainsKey(className))
                return null;

            return generatedModules[vm][module].Classes[className].Bind();
        }

        private static WrenForeignMethod bindAutoMapperMethod(WrenVM vm, string module, string className, bool isStatic, string signature)
        {
            if (module == WrenVM.InterpetModule
                && mainModuleClasses.ContainsKey(vm)
                && mainModuleClasses[vm].ContainsKey(className)
                && mainModuleClasses[vm][className].Functions.ContainsKey(signature))
                return mainModuleClasses[vm][className].Functions[signature];

            if (generatedModules.ContainsKey(vm)
                && generatedModules[vm].ContainsKey(module)
                && generatedModules[vm][module].Classes.ContainsKey(className)
                && generatedModules[vm][module].Classes[className].Functions.ContainsKey(signature))
                return generatedModules[vm][module].Classes[className].Functions[signature];

            return null;
        }

        private static string loadAutoMapperModule(WrenVM vm, string name)
        {
            if (!generatedModules.ContainsKey(vm) || !generatedModules[vm].ContainsKey(name))
                return null;

            return generatedModules[vm][name].GetSource();
        }

        #endregion VM Config Methods
    }
}