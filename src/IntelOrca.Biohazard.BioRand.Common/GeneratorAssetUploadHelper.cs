using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand
{
    internal sealed class GeneratorAssetUploadHelper
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUri;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly GeneratorAssetUploadOptions _uploadOptions;

        public GeneratorAssetUploadHelper(
            HttpClient httpClient,
            string baseUri,
            JsonSerializerOptions? jsonOptions = null,
            GeneratorAssetUploadOptions? uploadOptions = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUri = (baseUri ?? throw new ArgumentNullException(nameof(baseUri))).TrimEnd('/');
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _uploadOptions = uploadOptions ?? new GeneratorAssetUploadOptions();
            _uploadOptions.Validate();
        }

        public Task UploadAsync(Guid id, int randoId, RandomizerOutputAsset asset, CancellationToken cancellationToken = default)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            return asset.Data.Length > _uploadOptions.SmallAssetThresholdBytes
                ? UploadChunkedAsync(id, randoId, asset, cancellationToken)
                : UploadLegacyAsync(id, randoId, asset, cancellationToken);
        }

        private async Task UploadLegacyAsync(Guid id, int randoId, RandomizerOutputAsset asset, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, GetUri("generator/asset"));
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(id.ToString()), "id");
            form.Add(new StringContent(randoId.ToString()), "randoId");
            form.Add(new StringContent(asset.Key), "key");
            form.Add(new StringContent(asset.Title), "title");
            form.Add(new StringContent(asset.Description), "description");
            form.Add(new ByteArrayContent(asset.Data), "data", asset.FileName);
            request.Content = form;
            await SendAsync(request, cancellationToken);
        }

        private async Task UploadChunkedAsync(Guid id, int randoId, RandomizerOutputAsset asset, CancellationToken cancellationToken)
        {
            await SendJsonAsync(
                HttpMethod.Post,
                "generator/asset/begin",
                new GeneratorAssetUploadBeginRequest
                {
                    Id = id,
                    RandoId = randoId,
                    Key = asset.Key,
                    Title = asset.Title,
                    Description = asset.Description,
                    FileName = asset.FileName,
                    Length = asset.Data.LongLength
                },
                cancellationToken);

            var chunkCount = (asset.Data.Length + _uploadOptions.ChunkSizeBytes - 1) / _uploadOptions.ChunkSizeBytes;
            var workerCount = Math.Min(chunkCount, _uploadOptions.MaxParallelUploads);
            var firstFailureLock = new object();
            using var uploadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var nextChunkIndex = 0;
            var failureTracker = new FailureTracker();
            Exception? firstFailure = null;
            var tasks = new List<Task>(workerCount);
            for (var i = 0; i < workerCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (!uploadCts.IsCancellationRequested)
                    {
                        var chunkIndex = Interlocked.Increment(ref nextChunkIndex) - 1;
                        if (chunkIndex >= chunkCount)
                            break;

                        try
                        {
                            await UploadChunkWithRetryAsync(id, randoId, asset, chunkIndex, failureTracker, uploadCts.Token);
                        }
                        catch (OperationCanceledException) when (uploadCts.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            lock (firstFailureLock)
                            {
                                firstFailure ??= ex;
                            }
                            uploadCts.Cancel();
                            break;
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            if (firstFailure != null)
                throw firstFailure;

            await SendJsonAsync(
                HttpMethod.Post,
                "generator/asset/end",
                new GeneratorAssetUploadEndRequest
                {
                    Id = id,
                    RandoId = randoId,
                    Key = asset.Key
                },
                cancellationToken);
        }

        private async Task UploadChunkWithRetryAsync(
            Guid id,
            int randoId,
            RandomizerOutputAsset asset,
            int chunkIndex,
            FailureTracker failureTracker,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= _uploadOptions.MaxChunkAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await UploadChunkAsync(id, randoId, asset, chunkIndex, cancellationToken);
                    Interlocked.Exchange(ref failureTracker.ConsecutiveFailures, 0);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var failureCount = Interlocked.Increment(ref failureTracker.ConsecutiveFailures);
                    if (failureCount >= _uploadOptions.MaxConsecutiveChunkFailures)
                    {
                        throw new InvalidOperationException(
                            $"Aborting upload for asset '{asset.Key}' after {failureCount} consecutive chunk failures.",
                            ex);
                    }

                    if (attempt >= _uploadOptions.MaxChunkAttempts)
                    {
                        throw new InvalidOperationException(
                            $"Chunk {chunkIndex + 1} for asset '{asset.Key}' failed after {_uploadOptions.MaxChunkAttempts} attempts.",
                            ex);
                    }
                }
            }
        }

        private async Task UploadChunkAsync(
            Guid id,
            int randoId,
            RandomizerOutputAsset asset,
            int chunkIndex,
            CancellationToken cancellationToken)
        {
            var offset = chunkIndex * _uploadOptions.ChunkSizeBytes;
            var count = Math.Min(_uploadOptions.ChunkSizeBytes, asset.Data.Length - offset);
            var rangeStart = (long)offset;
            var rangeEnd = rangeStart + count - 1L;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                GetUri($"generator/asset/chunk?id={Uri.EscapeDataString(id.ToString())}&randoId={randoId}&key={Uri.EscapeDataString(asset.Key)}"));
            var content = new ByteArrayContent(asset.Data, offset, count);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentRange = new ContentRangeHeaderValue(rangeStart, rangeEnd, asset.Data.LongLength);
            request.Content = content;
            await SendAsync(request, cancellationToken);
        }

        private async Task SendJsonAsync(HttpMethod method, string path, object data, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, GetUri(path));
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            await SendAsync(request, cancellationToken);
        }

        private async Task SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{response.StatusCode} returned");
        }

        private string GetUri(string path) => $"{_baseUri}/{path}";

        private sealed class FailureTracker
        {
            public int ConsecutiveFailures;
        }

        private sealed class GeneratorAssetUploadBeginRequest
        {
            public Guid Id { get; set; }
            public int RandoId { get; set; }
            public string Key { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string FileName { get; set; } = "";
            public long Length { get; set; }
        }

        private sealed class GeneratorAssetUploadEndRequest
        {
            public Guid Id { get; set; }
            public int RandoId { get; set; }
            public string Key { get; set; } = "";
        }
    }
}
