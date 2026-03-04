using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.Extensions;
using IntelOrca.Biohazard.REE;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public abstract class ReeRandomizerGenerator : IReeRandomizerGenerator
    {
        private readonly Dictionary<Type, object> _services = [];
        private readonly object _servicesLock = new();
        private readonly Dictionary<string, string> _logFiles = [];
        private FileRepository? _fileRepository;

        public IReeRandomizer Randomizer { get; }
        public RandomizerInput Input { get; }
        public RandomizerOptions Options { get; }
        public IRandomizerProgress Progress { get; }

        public ReeRandomizerGenerator(
            IReeRandomizer randomizer,
            RandomizerInput input,
            RandomizerOptions options,
            IRandomizerProgress progress)
        {
            Randomizer = randomizer;
            Input = input;
            Options = options;
            Progress = progress;
        }

        public Task<RandomizerOutput> GenerateAsync()
        {
            GenerateCampaigns();
            return Task.FromResult(BuildMod());
        }

        public virtual void GenerateCampaigns()
        {
        }

        protected void GenerateCampaign(string campaignName)
        {
            RunPatches();
            RunModifiers(campaignName);
        }

        private RandomizerOutput BuildMod()
        {
            RandomizerOutput? result = null;
            Progress.RunTask("Building mod", () =>
            {
                var modBuilder = CreateModBuilder();
                result = new RandomizerOutput(
                    [
                        new RandomizerOutputAsset(
                            "1-patch",
                            "Patch",
                            "Simply drop this file into your install folder.",
                            $"biorand-{Randomizer.GameMoniker}-{Input.Seed}.zip",
                            BuildPakZip(modBuilder)),
                        new RandomizerOutputAsset(
                            "2-fluffy",
                            "Fluffy Mod",
                            "Drop this zip file into Fluffy Mod Manager's mod folder and enable it.",
                            $"biorand-{Randomizer.GameMoniker}-{Input.Seed}-mod.zip",
                            BuildFluffyZip(modBuilder))
                    ],
                    Instructions);
            });
            return result ?? throw new Exception("No mod was built");
        }

        protected virtual string Instructions => "";

        private ModBuilder CreateModBuilder()
        {
            // var output = new ChainsawRandomizerOutput(input, _fileRepository.GetOutputPakFile(), _logFiles, PakVersion);
            var builder = new ModBuilder();
            builder.Name = $"BioRand - {Input.ProfileName} [{Input.Seed}]";
            builder.Description = $"{Input.ProfileName} by {Input.ProfileAuthor} [{Input.Seed}]\n{Input.ProfileDescription}";
            builder.Author = $"BioRand by {Randomizer.Author}";
            builder.Version = Randomizer.Version;
            FileRepository.AddFilesToModBuilder(builder);
            OnBuildMod(builder);
            return builder;
        }

        private byte[] BuildPakZip(ModBuilder modBuilder)
        {
            var zipFileBuilder = new ZipFileBuilder();
            zipFileBuilder.AddEntry(PakName, modBuilder.BuildPakFile());
            zipFileBuilder.AddEntry("config.json", Encoding.UTF8.GetBytes(Input.Configuration.ToJson()));
            foreach (var logFile in _logFiles)
            {
                zipFileBuilder.AddEntry(logFile.Key, Encoding.UTF8.GetBytes(logFile.Value));
            }
            return zipFileBuilder.Build();
        }

        private byte[] BuildFluffyZip(ModBuilder modBuilder)
        {
            modBuilder.AddFile("config.json", Encoding.UTF8.GetBytes(Input.Configuration.ToJson()));
            foreach (var logFile in _logFiles)
            {
                modBuilder.AddFile(logFile.Key, Encoding.UTF8.GetBytes(logFile.Value));
            }
            return modBuilder.BuildFluffyZipFile();
        }

        protected virtual void OnBuildMod(ModBuilder builder) { }

        public abstract RszTypeRepository TypeRepository { get; }
        public abstract string PakName { get; }

        public virtual byte[]? GetSupplementFile(string path) => null;
        protected virtual void OnBeforeModify() { }
        protected virtual void OnAfterModify() { }

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

        private void RunPatches()
        {
            Progress.RunTask("Applying patches", () =>
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
                l.LogVersionTimeInfo($"BioRand {Randomizer.GameMoniker.ToUpperInvariant()} {Randomizer.Version}", $"By {Randomizer.Author}");
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
                Progress.RunTask($"Running modifier: {n}", () => m.Apply(this, logger.Process));
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
                        if (!t.IsAbstract && typeof(Modifier).IsAssignableFrom(t))
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
                    .Select(x => (Modifier)Activator.CreateInstance(x.Item1)!)
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

        public byte[]? TryGetFile(string path) => FileRepository.GetFile(path);
        public void SetFile(string path, byte[] data) => FileRepository.SetFile(path, data);

        private FileRepository FileRepository
        {
            get
            {
                if (_fileRepository == null)
                {
                    _fileRepository = new FileRepository(Options.GameInputPath);
                }
                return _fileRepository;
            }
        }

        bool IReeRandomizerContext.ExportingMod => throw new NotImplementedException();

        private sealed class RandomizerLoggerIO
        {
            public RandomizerLogger Input { get; } = new();
            public RandomizerLogger Process { get; } = new();
            public RandomizerLogger Output { get; } = new();
        }
    }
}
