using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.REE;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public class Patcher
    {
        private ImmutableArray<Type> _patches;
        private ImmutableArray<ExportModAttribute> _all;

        private ImmutableArray<Type> PatchTypes
        {
            get
            {
                if (_patches.IsDefault)
                {
                    var patches = new List<(Type, int)>();
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var t in assembly.GetTypes())
                        {
                            if (!t.IsAbstract && t.GetInterfaces().Any(x => x == typeof(IPatch)))
                            {
                                var order = 0;
                                var orderAttribute = t.GetCustomAttribute<OrderAttribute>();
                                if (orderAttribute != null)
                                {
                                    order = orderAttribute.Order;
                                }
                                patches.Add((t, order));
                            }
                        }
                    }
                    _patches = patches
                        .OrderBy(x => x.Item2)
                        .Select(x => x.Item1)
                        .ToImmutableArray();
                }
                return _patches;
            }
        }

        public ImmutableArray<ExportModAttribute> Mods
        {
            get
            {
                if (_all.IsDefault)
                {
                    _all = PatchTypes
                        .Select(x => x.GetCustomAttribute<ExportModAttribute>()!)
                        .Where(x => x != null)
                        .ToImmutableArray();
                }
                return _all;
            }
        }

        public ModBuilder ExportMod(IReeRandomizerContext context, string name)
        {
            var exportedModAttribute = Mods.FirstOrDefault(x => x.Name == name)
                ?? throw new ArgumentException($"{name} not found", nameof(name));
            var type = PatchTypes.FirstOrDefault(x => x.GetCustomAttribute<ExportModAttribute>()?.Name == exportedModAttribute.Name)
                ?? throw new Exception($"Type for {name} not found");

            var modBuilder = new ModBuilder
            {
                Name = exportedModAttribute.Name,
                Description = exportedModAttribute.Description,
                Version = exportedModAttribute.Version,
                Author = exportedModAttribute.Author
            };

            ApplyPatch(context, type);

            return modBuilder;
        }

        internal void ApplyAll(IReeRandomizerContext context)
        {
            foreach (var patchType in PatchTypes)
            {
                ApplyPatch(context, patchType);
            }
        }

        private static void ApplyPatch(IReeRandomizerContext context, Type type)
        {
            var ctors = type.GetConstructors();
            if (ctors.Length > 1)
                throw new NotSupportedException($"Only one constructor supported for {nameof(IPatch)}");

            var ctor = ctors[0];
            var ctorParameters = ctor.GetParameters().ToArray();
            var ctorArguments = new object?[ctorParameters.Length];
            for (var i = 0; i < ctorArguments.Length; i++)
            {
                if (ctorParameters[i].ParameterType == typeof(IReeRandomizerContext))
                {
                    ctorArguments[i] = context;
                }
            }

            var patchInstance = (IPatch)Activator.CreateInstance(type, ctorArguments)!;
            patchInstance.Apply();
        }
    }
}
