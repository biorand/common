using System;

namespace IntelOrca.Biohazard.BioRand
{
    internal sealed class GeneratorAssetUploadOptions
    {
        public const int DefaultSmallAssetThresholdBytes = 32 * 1024 * 1024;
        public const int DefaultChunkSizeBytes = 8 * 1024 * 1024;
        public const int DefaultMaxParallelUploads = 4;
        public const int DefaultMaxChunkAttempts = 5;
        public const int DefaultMaxConsecutiveChunkFailures = 5;

        public int SmallAssetThresholdBytes { get; set; } = DefaultSmallAssetThresholdBytes;
        public int ChunkSizeBytes { get; set; } = DefaultChunkSizeBytes;
        public int MaxParallelUploads { get; set; } = DefaultMaxParallelUploads;
        public int MaxChunkAttempts { get; set; } = DefaultMaxChunkAttempts;
        public int MaxConsecutiveChunkFailures { get; set; } = DefaultMaxConsecutiveChunkFailures;

        internal void Validate()
        {
            if (SmallAssetThresholdBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(SmallAssetThresholdBytes));
            if (ChunkSizeBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(ChunkSizeBytes));
            if (MaxParallelUploads <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxParallelUploads));
            if (MaxChunkAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxChunkAttempts));
            if (MaxConsecutiveChunkFailures <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxConsecutiveChunkFailures));
        }
    }
}
