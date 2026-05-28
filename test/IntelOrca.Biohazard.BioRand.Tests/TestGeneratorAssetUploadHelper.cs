using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestGeneratorAssetUploadHelper
    {
        [Fact]
        public async Task SmallAssetUsesLegacyEndpoint()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new ConcurrentQueue<RequestRecord>();
            using var httpClient = CreateHttpClient(async request =>
            {
                requests.Enqueue(await RequestRecord.CreateAsync(request));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var helper = new GeneratorAssetUploadHelper(httpClient, "https://example.test");
            await helper.UploadAsync(Guid.NewGuid(), 7, CreateAsset("legacy", 8), cancellationToken);

            var request = Assert.Single(requests);
            Assert.Equal("/generator/asset", request.Path);
            Assert.StartsWith("multipart/form-data", request.ContentType);
        }

        [Fact]
        public async Task AssetAtThresholdStillUsesLegacyEndpoint()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new ConcurrentQueue<RequestRecord>();
            using var httpClient = CreateHttpClient(async request =>
            {
                requests.Enqueue(await RequestRecord.CreateAsync(request));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var helper = new GeneratorAssetUploadHelper(
                httpClient,
                "https://example.test",
                uploadOptions: new GeneratorAssetUploadOptions
                {
                    SmallAssetThresholdBytes = 8
                });

            await helper.UploadAsync(Guid.NewGuid(), 8, CreateAsset("threshold", 8), cancellationToken);

            var request = Assert.Single(requests);
            Assert.Equal("/generator/asset", request.Path);
        }

        [Fact]
        public async Task LargeAssetUsesChunkedProtocolWithContentRange()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new ConcurrentQueue<RequestRecord>();
            var activeChunkRequests = 0;
            var maxChunkRequests = 0;
            using var httpClient = CreateHttpClient(async request =>
            {
                var record = await RequestRecord.CreateAsync(request);
                requests.Enqueue(record);
                if (record.Path == "/generator/asset/chunk")
                {
                    var concurrent = Interlocked.Increment(ref activeChunkRequests);
                    UpdateMax(ref maxChunkRequests, concurrent);
                    await Task.Delay(75, cancellationToken);
                    Interlocked.Decrement(ref activeChunkRequests);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var helper = new GeneratorAssetUploadHelper(
                httpClient,
                "https://example.test/",
                uploadOptions: new GeneratorAssetUploadOptions
                {
                    SmallAssetThresholdBytes = 8,
                    ChunkSizeBytes = 4,
                    MaxParallelUploads = 2
                });

            await helper.UploadAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), 42, CreateAsset("chunked", 10), cancellationToken);

            var recordedRequests = requests.ToArray();
            Assert.Equal("/generator/asset/begin", recordedRequests.First().Path);
            Assert.Equal("/generator/asset/end", recordedRequests.Last().Path);

            var beginRequest = recordedRequests.First();
            using var beginJson = JsonDocument.Parse(beginRequest.Body);
            Assert.Equal(10, beginJson.RootElement.GetProperty("length").GetInt32());

            var chunkRequests = recordedRequests.Where(x => x.Path == "/generator/asset/chunk").OrderBy(x => x.ContentRange).ToArray();
            Assert.Equal(new[] { "bytes 0-3/10", "bytes 4-7/10", "bytes 8-9/10" }, chunkRequests.Select(x => x.ContentRange));
            Assert.True(maxChunkRequests > 1, "Expected chunk uploads to overlap.");
            Assert.True(maxChunkRequests <= 2, "Chunk upload parallelism exceeded the configured limit.");
        }

        [Fact]
        public async Task MultipleAssetsCanUploadConcurrently()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new ConcurrentQueue<RequestRecord>();
            using var httpClient = CreateHttpClient(async request =>
            {
                requests.Enqueue(await RequestRecord.CreateAsync(request));
                if (request.RequestUri!.AbsolutePath == "/generator/asset/chunk")
                    await Task.Delay(25, cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var helper = new GeneratorAssetUploadHelper(
                httpClient,
                "https://example.test",
                uploadOptions: new GeneratorAssetUploadOptions
                {
                    SmallAssetThresholdBytes = 1,
                    ChunkSizeBytes = 4,
                    MaxParallelUploads = 2
                });

            await Task.WhenAll(
                helper.UploadAsync(Guid.NewGuid(), 3, CreateAsset("asset-a", 8), cancellationToken),
                helper.UploadAsync(Guid.NewGuid(), 4, CreateAsset("asset-b", 8), cancellationToken));

            var recordedRequests = requests.ToArray();
            Assert.Equal(2, recordedRequests.Count(x => x.Path == "/generator/asset/begin"));
            Assert.Equal(4, recordedRequests.Count(x => x.Path == "/generator/asset/chunk"));
            Assert.Equal(2, recordedRequests.Count(x => x.Path == "/generator/asset/end"));

            var assetAChunks = recordedRequests
                .Where(x => x.Path == "/generator/asset/chunk" && x.Query.Contains("key=asset-a"))
                .OrderBy(x => x.ContentRange)
                .Select(x => x.ContentRange)
                .ToArray();
            var assetBChunks = recordedRequests
                .Where(x => x.Path == "/generator/asset/chunk" && x.Query.Contains("key=asset-b"))
                .OrderBy(x => x.ContentRange)
                .Select(x => x.ContentRange)
                .ToArray();

            Assert.Equal(new[] { "bytes 0-3/8", "bytes 4-7/8" }, assetAChunks);
            Assert.Equal(new[] { "bytes 0-3/8", "bytes 4-7/8" }, assetBChunks);
        }

        [Fact]
        public async Task ChunkUploadRetriesUpToConfiguredLimit()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var chunkAttempts = 0;
            using var httpClient = CreateHttpClient(request =>
            {
                if (request.RequestUri!.AbsolutePath == "/generator/asset/chunk")
                {
                    chunkAttempts++;
                    return Task.FromResult(new HttpResponseMessage(chunkAttempts < 5 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var helper = new GeneratorAssetUploadHelper(
                httpClient,
                "https://example.test",
                uploadOptions: new GeneratorAssetUploadOptions
                {
                    SmallAssetThresholdBytes = 1,
                    ChunkSizeBytes = 4,
                    MaxParallelUploads = 1,
                    MaxChunkAttempts = 5,
                    MaxConsecutiveChunkFailures = 10
                });

            await helper.UploadAsync(Guid.NewGuid(), 1, CreateAsset("retry", 4), cancellationToken);

            Assert.Equal(5, chunkAttempts);
        }

        [Fact]
        public async Task UploadAbortsAfterFiveConsecutiveChunkFailuresOverall()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var failedRanges = new ConcurrentBag<string>();
            var totalChunkAttempts = 0;
            using var httpClient = CreateHttpClient(async request =>
            {
                if (request.RequestUri!.AbsolutePath == "/generator/asset/chunk")
                {
                    failedRanges.Add(request.Content!.Headers.ContentRange!.ToString());
                    Interlocked.Increment(ref totalChunkAttempts);
                    await Task.Delay(25, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var helper = new GeneratorAssetUploadHelper(
                httpClient,
                "https://example.test",
                uploadOptions: new GeneratorAssetUploadOptions
                {
                    SmallAssetThresholdBytes = 1,
                    ChunkSizeBytes = 4,
                    MaxParallelUploads = 2,
                    MaxChunkAttempts = 5,
                    MaxConsecutiveChunkFailures = 5
                });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.UploadAsync(Guid.NewGuid(), 9, CreateAsset("abort", 8), cancellationToken));

            Assert.Contains("Aborting upload", ex.Message);
            Assert.True(totalChunkAttempts >= 5, "Expected at least five failed chunk attempts before aborting.");
            Assert.True(failedRanges.Distinct().Count() > 1, "Expected failures from more than one chunk.");
        }

        private static RandomizerOutputAsset CreateAsset(string key, int size)
        {
            var data = Enumerable.Range(0, size).Select(x => (byte)x).ToArray();
            return new RandomizerOutputAsset(key, key, $"{key}-description", $"{key}.bin", data);
        }

        private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> onSend)
        {
            return new HttpClient(new CallbackHttpMessageHandler(onSend));
        }

        private static void UpdateMax(ref int currentMax, int candidate)
        {
            while (true)
            {
                var snapshot = currentMax;
                if (candidate <= snapshot)
                    return;
                if (Interlocked.CompareExchange(ref currentMax, candidate, snapshot) == snapshot)
                    return;
            }
        }

        private sealed class CallbackHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> onSend) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return onSend(request);
            }
        }

        private sealed class RequestRecord
        {
            public string Path { get; private set; } = "";
            public string Query { get; private set; } = "";
            public string Body { get; private set; } = "";
            public string ContentType { get; private set; } = "";
            public string ContentRange { get; private set; } = "";

            public static async Task<RequestRecord> CreateAsync(HttpRequestMessage request)
            {
                return new RequestRecord
                {
                    Path = request.RequestUri!.AbsolutePath,
                    Query = request.RequestUri!.Query,
                    Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(),
                    ContentType = request.Content?.Headers.ContentType?.ToString() ?? "",
                    ContentRange = request.Content?.Headers.ContentRange?.ToString() ?? ""
                };
            }
        }
    }
}
