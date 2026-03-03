using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.BioRand.Cryptography;

namespace IntelOrca.Biohazard.BioRand
{
    public abstract class ReeRandomizer(RandomizerInput input, IRandomizerProgress? progress) : IPatchContext
    {
        private readonly Dictionary<Type, object> _services = [];
        private readonly object _servicesLock = new();
        private readonly Dictionary<string, string> _logFiles = [];

        public RandomizerInput Input { get; } = input;
        public IRandomizerProgress Reporter { get; } = progress ?? new DummyRandomizerProgress();

        public abstract string Version { get; }
        public abstract string Author { get; }

        protected virtual void OnBeforeModify()
        {
        }

        protected virtual void OnAfterModify()
        {
        }

        public T GetService<T>()
        {
            lock (_servicesLock)
            {
                var type = typeof(T);
                _services.TryGetValue(type, out var service);
                if (service == null)
                {
                    service = Activator.CreateInstance(type, [this])!;
                    _services[type] = service;
                }
                return (T)service;
            }
        }

        public T? GetConfigOption<T>(string key, T? defaultValue = default)
        {
            if (Input.Configuration == null)
                return defaultValue;
            return Input.Configuration.GetValueOrDefault<T>(key, defaultValue);
        }

        public Rng GetRng(params object[] key)
        {
            var hashInput = string.Concat([Input.Seed, .. key]);
            var seed = MurMur3.HashData(hashInput);
            return new Rng(seed);
        }

        public void AddLogFile(string name, string content)
        {
            _logFiles[name] = content;
        }

        public abstract RandomizerOutput OnRandomize();

        protected void RandomizeCampaign(string campaignName)
        {
            RunPatches();
            RunModifiers(campaignName);
        }

        private void RunPatches()
        {
            Reporter.RunTask("Applying patches", () =>
            {
                var patcher = new Patcher();
                patcher.ApplyAll(this);
            });
        }

        private void RunModifiers(string campaignName)
        {
            var modifiers = CreateModifiers();

            var logger = new RandomizerLoggerIO();
            foreach (var l in new[] { logger.Input, logger.Process, logger.Output })
            {
                l.LogHr();
                l.LogVersionTimeInfo(Version, Author);
                l.LogLine($"Seed = {Input.Seed}");
                l.LogLine($"Campaign = {campaignName}");
                l.LogHr();
            }

            // Input
            IterateModifiers((n, m) =>
            {
                logger.Input.Push(n);
                m.LogState(this, logger.Input);
                logger.Input.Pop();
                logger.Input.LogHr();
            });

            // Apply modifiers
            OnBeforeModify();
            IterateModifiers((n, m) =>
            {
                logger.Process.Push(n);
                Reporter.RunTask($"Running modifier: {n}", () => m.Apply(this, logger.Process));
                logger.Process.Pop();
                logger.Process.LogHr();
            });
            OnAfterModify();

            // Output
            IterateModifiers((n, m) =>
            {
                logger.Output.Push(n);
                m.LogState(this, logger.Output);
                logger.Output.Pop();
                logger.Output.LogHr();
            });

            AddLogFile($"input_{campaignName}.log", logger.Input.Output);
            AddLogFile($"process_{campaignName}.log", logger.Process.Output);
            AddLogFile($"output_{campaignName}.log", logger.Output.Output);

            ImmutableArray<Modifier> CreateModifiers()
            {
                var modifiers = new List<(Type, int)>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (typeof(Modifier).IsAssignableFrom(t))
                        {
                            var order = 0;
                            var orderAttribute = t.GetCustomAttribute<OrderAttribute>();
                            if (orderAttribute != null)
                            {
                                order = orderAttribute.Order;
                            }
                            modifiers.Add((t, order));
                        }
                    }
                }
                return modifiers
                    .OrderBy(x => x.Item2)
                    .Select(x => (Modifier)Activator.CreateInstance(x.Item1))
                    .ToImmutableArray();
            }

            void IterateModifiers(Action<string, Modifier> action)
            {
                foreach (var modifier in modifiers)
                {
                    var name = modifier.GetType().Name.Replace("Modifier", "");
                    action(name, modifier);
                }
            }
        }

        public byte[]? GetFile(string path)
        {
            throw new NotImplementedException();
        }

        public void SetFile(string path, byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[]? GetSupplementFile(string path)
        {
            throw new NotImplementedException();
        }

        bool IPatchContext.ExportingMod => throw new NotImplementedException();

        private sealed class RandomizerLoggerIO
        {
            public RandomizerLogger Input { get; } = new();
            public RandomizerLogger Process { get; } = new();
            public RandomizerLogger Output { get; } = new();
        }
    }
}
