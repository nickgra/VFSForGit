﻿using GVFS.Common.Tracing;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;

namespace GVFS.Common.Http
{
    public class ConfigHttpRequestor : HttpRequestor
    {
        private readonly string repoUrl;
        
        public ConfigHttpRequestor(ITracer tracer, Enlistment enlistment, RetryConfig retryConfig) 
            : base(tracer, retryConfig, enlistment.Authentication)
        {
            this.repoUrl = enlistment.RepoUrl;
        }

        public bool TryQueryGVFSConfig(out ServerGVFSConfig serverGVFSConfig)
        {
            serverGVFSConfig = null;

            Uri gvfsConfigEndpoint;
            string gvfsConfigEndpointString = this.repoUrl + GVFSConstants.Endpoints.GVFSConfig;
            try
            {
                gvfsConfigEndpoint = new Uri(gvfsConfigEndpointString);
            }
            catch (UriFormatException e)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Method", nameof(this.TryQueryGVFSConfig));
                metadata.Add("Exception", e.ToString());
                metadata.Add("Url", gvfsConfigEndpointString);
                this.Tracer.RelatedError(metadata, "UriFormatException when constructing Uri", Keywords.Network);

                return false;
            }

            long requestId = HttpRequestor.GetNewRequestId();
            RetryWrapper<ServerGVFSConfig> retrier = new RetryWrapper<ServerGVFSConfig>(this.RetryConfig.MaxAttempts, CancellationToken.None);
            retrier.OnFailure += RetryWrapper<ServerGVFSConfig>.StandardErrorHandler(this.Tracer, requestId, "QueryGvfsConfig");

            RetryWrapper<ServerGVFSConfig>.InvocationResult output = retrier.Invoke(
                tryCount =>
                {
                    using (GitEndPointResponseData response = this.SendRequest(
                        requestId,
                        gvfsConfigEndpoint,
                        HttpMethod.Get,
                        requestContent: null,
                        cancellationToken: CancellationToken.None))
                    {
                        if (response.HasErrors)
                        {
                            return new RetryWrapper<ServerGVFSConfig>.CallbackResult(response.Error, response.ShouldRetry);
                        }

                        try
                        {
                            string configString = response.RetryableReadToEnd();
                            ServerGVFSConfig config = JsonConvert.DeserializeObject<ServerGVFSConfig>(configString);
                            return new RetryWrapper<ServerGVFSConfig>.CallbackResult(config);
                        }
                        catch (JsonReaderException e)
                        {
                            return new RetryWrapper<ServerGVFSConfig>.CallbackResult(e, shouldRetry: false);
                        }
                    }
                });

            if (output.Succeeded)
            {
                serverGVFSConfig = output.Result;
                return true;
            }

            return false;
        }
    }
}
