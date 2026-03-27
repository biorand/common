#nullable enable
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using IntelOrca.Biohazard.BioRand.REE;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestModifier
    {
        // --- Modifier subclasses ---

        private class ContextlessModifier : Modifier
        {
            public bool LogStateCalled { get; private set; }
            public bool ApplyCalled { get; private set; }

            public override void LogState(RandomizerLogger logger) => LogStateCalled = true;
            public override void Apply(RandomizerLogger logger) => ApplyCalled = true;
        }

        public class ContextAwareModifier : Modifier
        {
            public static ContextAwareModifier? LastInstance { get; set; }

            public bool LogStateCalled { get; private set; }
            public bool ApplyCalled { get; private set; }
            public IReeRandomizerContext? ReceivedContext { get; private set; }

            public ContextAwareModifier() => LastInstance = this;

            public override void LogState(IReeRandomizerContext context, RandomizerLogger logger)
            {
                ReceivedContext = context;
                LogStateCalled = true;
            }

            public override void Apply(IReeRandomizerContext context, RandomizerLogger logger)
            {
                ReceivedContext = context;
                ApplyCalled = true;
            }
        }

        /// <summary>
        /// Modifier that uses a context constructor and captures the context in a static so
        /// the test can inspect it after generation without needing any special generator hook.
        /// </summary>
        public class ContextConstructorModifier : Modifier
        {
            public static IReeRandomizerContext? CapturedContext { get; set; }

            public ContextConstructorModifier(IReeRandomizerContext context)
            {
                CapturedContext = context;
            }
        }

        // --- Default delegation tests ---

        [Fact]
        public void LogState_WithContext_DelegatesToContextlessOverload()
        {
            var modifier = new ContextlessModifier();
            modifier.LogState(new StubContext(), new RandomizerLogger());
            Assert.True(modifier.LogStateCalled);
        }

        [Fact]
        public void Apply_WithContext_DelegatesToContextlessOverload()
        {
            var modifier = new ContextlessModifier();
            modifier.Apply(new StubContext(), new RandomizerLogger());
            Assert.True(modifier.ApplyCalled);
        }

        [Fact]
        public async Task ContextAwareOverride_ReceivesContext()
        {
            ContextAwareModifier.LastInstance = null;

            var generator = new StubGenerator();
            await generator.GenerateAsync();

            var modifier = ContextAwareModifier.LastInstance;
            Assert.NotNull(modifier);
            Assert.True(modifier.LogStateCalled);
            Assert.True(modifier.ApplyCalled);
            Assert.Same(generator, modifier.ReceivedContext);
        }

        // --- Generator auto-discovery test ---

        [Fact]
        public async Task GenerateAsync_ContextConstructorModifier_IsConstructedWithGenerator()
        {
            ContextConstructorModifier.CapturedContext = null;

            var generator = new StubGenerator();
            await generator.GenerateAsync();

            Assert.Same(generator, ContextConstructorModifier.CapturedContext);
        }

        // --- Stubs ---

        private class StubGenerator : ReeRandomizerGenerator
        {
            public StubGenerator() : base(
                new StubReeRandomizer(),
                new RandomizerInput(),
                new RandomizerOptions { GameInputPath = "" },
                DummyRandomizerProgress.Default)
            {
            }

            public override void GenerateCampaigns() => GenerateCampaign("test");
            public override RszTypeRepository TypeRepository { get; } = new RszTypeRepository();
            public override string PakName => "test.pak";
        }

        private class StubReeRandomizer : IReeRandomizer
        {
            public string Version => "0.0";
            public string Author => "Test";
            public string GameMoniker => "test";
            public string? ProcessName => null;
            public PakList PakList { get; } = new PakList([]);
            public ImmutableArray<string> PakExtractFileNamePatterns => [];
            public Task<RandomizerConfigurationDefinition> GetConfigurationDefinitionAsync(RandomizerOptions options) => throw new NotImplementedException();
            public Task<IReeRandomizerGenerator> CreateGeneratorAsync(RandomizerInput input, RandomizerOptions options, IRandomizerProgress progress) => throw new NotImplementedException();
        }

        private class StubContext : IReeRandomizerContext
        {
            public IReeRandomizer Randomizer => throw new NotImplementedException();
            public bool ExportingMod => false;
            public PakList PakList => throw new NotImplementedException();
            public RszTypeRepository TypeRepository => throw new NotImplementedException();
            public T GetService<T>() => throw new NotImplementedException();
            public T? GetConfigOption<T>(string key, T? defaultValue = default) => defaultValue;
            public byte[]? TryGetFile(string path) => null;
            public void SetFile(string path, byte[] data) { }
            public byte[]? GetSupplementFile(string path) => null;
            public Rng GetRng(params object[] key) => throw new NotImplementedException();
        }
    }
}
