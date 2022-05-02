//-----------------------------------------------------------------------
// <copyright file="SpaStaticFileContext .cs" company="Company">
// Copyright (C) Company. All Rights Reserved.
// </copyright>
// <author>nainaigu</author>
// <create>$Date$</create>
// <summary></summary>
//-----------------------------------------------------------------------

using System.Linq;
using Microsoft.AspNetCore.StaticFiles;

namespace spa.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Internal;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// 
/// </summary>
  internal struct SpaStaticFileContext
  {

    #nullable disable
    private readonly HttpContext _context;
    private readonly StaticFileOptions _options;
    private readonly HttpRequest _request;
    private readonly HttpResponse _response;
    private readonly IFileProvider _fileProvider;
    private readonly string _method;
    private readonly string _contentType;
    private IFileInfo _fileInfo;
    private EntityTagHeaderValue _etag;
    private RequestHeaders _requestHeaders;
    private ResponseHeaders _responseHeaders;
    private RangeItemHeaderValue _range;
    private long _length;
    private readonly PathString _subPath;
    private DateTimeOffset _lastModified;
    private PreconditionState _ifMatchState;
    private PreconditionState _ifNoneMatchState;
    private PreconditionState _ifModifiedSinceState;
    private PreconditionState _ifUnmodifiedSinceState;
    private RequestType _requestType;


    #nullable enable
    public SpaStaticFileContext(
      HttpContext context,
      StaticFileOptions options,
      IFileProvider fileProvider,
      string? contentType,
      PathString subPath)
    {
      this._context = context;
      this._options = options;
      this._request = context.Request;
      this._response = context.Response;
      this._fileProvider = fileProvider;
      this._method = this._request.Method;
      this._contentType = contentType;
      this._fileInfo = null;
      this._etag =  null;
      this._requestHeaders =null;
      this._responseHeaders = null;
      this._range =  null;
      this._length = 0L;
      this._subPath = subPath;
      this._lastModified = new DateTimeOffset();
      this._ifMatchState = PreconditionState.Unspecified;
      this._ifNoneMatchState = PreconditionState.Unspecified;
      this._ifModifiedSinceState = PreconditionState.Unspecified;
      this._ifUnmodifiedSinceState = PreconditionState.Unspecified;
      if (HttpMethods.IsGet(this._method))
        this._requestType = RequestType.IsGet;
      else if (HttpMethods.IsHead(this._method))
        this._requestType = RequestType.IsHead;
      else
        this._requestType = RequestType.Unspecified;
    }

    private RequestHeaders RequestHeaders => this._requestHeaders ?? (this._requestHeaders = this._request.GetTypedHeaders());

    private ResponseHeaders ResponseHeaders => this._responseHeaders ?? (this._responseHeaders = this._response.GetTypedHeaders());

    public bool IsHeadMethod => this._requestType.HasFlag((Enum) SpaStaticFileContext.RequestType.IsHead);

    public bool IsGetMethod => this._requestType.HasFlag((Enum) SpaStaticFileContext.RequestType.IsGet);

    public bool IsRangeRequest
    {
      get => this._requestType.HasFlag((Enum) SpaStaticFileContext.RequestType.IsRange);
      private set
      {
        if (value)
          this._requestType |= SpaStaticFileContext.RequestType.IsRange;
        else
          this._requestType &= ~SpaStaticFileContext.RequestType.IsRange;
      }
    }

    public string SubPath => this._subPath.Value;

    public string PhysicalPath => this._fileInfo.PhysicalPath;

    public bool LookupFileInfo()
    {
      this._fileInfo = this._fileProvider.GetFileInfo(this._subPath.Value);
      if (this._fileInfo.Exists)
      {
        this._length = this._fileInfo.Length;
        DateTimeOffset lastModified = this._fileInfo.LastModified;
        this._lastModified = new DateTimeOffset(lastModified.Year, lastModified.Month, lastModified.Day, lastModified.Hour, lastModified.Minute, lastModified.Second, lastModified.Offset).ToUniversalTime();
        this._etag = new EntityTagHeaderValue((StringSegment) ("\"" + Convert.ToString(this._lastModified.ToFileTime() ^ this._length, 16) + "\""));
      }
      return this._fileInfo.Exists;
    }

    public void ComprehendRequestHeaders()
    {
      this.ComputeIfMatch();
      this.ComputeIfModifiedSince();
      this.ComputeRange();
      this.ComputeIfRange();
    }

    private void ComputeIfMatch()
    {
      RequestHeaders requestHeaders = this.RequestHeaders;
      IList<EntityTagHeaderValue> ifMatch = requestHeaders.IfMatch;
      if (ifMatch != null && ifMatch.Count > 0)
      {
        this._ifMatchState = SpaStaticFileContext.PreconditionState.PreconditionFailed;
        foreach (EntityTagHeaderValue entityTagHeaderValue in (IEnumerable<EntityTagHeaderValue>) ifMatch)
        {
          if (entityTagHeaderValue.Equals((object) EntityTagHeaderValue.Any) || entityTagHeaderValue.Compare(this._etag, true))
          {
            this._ifMatchState = SpaStaticFileContext.PreconditionState.ShouldProcess;
            break;
          }
        }
      }
      IList<EntityTagHeaderValue> ifNoneMatch = requestHeaders.IfNoneMatch;
      if (ifNoneMatch == null || ifNoneMatch.Count <= 0)
        return;
      this._ifNoneMatchState = SpaStaticFileContext.PreconditionState.ShouldProcess;
      foreach (EntityTagHeaderValue entityTagHeaderValue in (IEnumerable<EntityTagHeaderValue>) ifNoneMatch)
      {
        if (entityTagHeaderValue.Equals((object) EntityTagHeaderValue.Any) || entityTagHeaderValue.Compare(this._etag, true))
        {
          this._ifNoneMatchState = SpaStaticFileContext.PreconditionState.NotModified;
          break;
        }
      }
    }

    private void ComputeIfModifiedSince()
    {
      RequestHeaders requestHeaders = this.RequestHeaders;
      DateTimeOffset utcNow = DateTimeOffset.UtcNow;
      DateTimeOffset? ifModifiedSince = requestHeaders.IfModifiedSince;
      DateTimeOffset? nullable;
      if (ifModifiedSince.HasValue)
      {
        nullable = ifModifiedSince;
        DateTimeOffset dateTimeOffset = utcNow;
        if ((nullable.HasValue ? (nullable.GetValueOrDefault() <= dateTimeOffset ? 1 : 0) : 0) != 0)
        {
          nullable = ifModifiedSince;
          DateTimeOffset lastModified = this._lastModified;
          this._ifModifiedSinceState = nullable.HasValue && nullable.GetValueOrDefault() < lastModified ? SpaStaticFileContext.PreconditionState.ShouldProcess : PreconditionState.NotModified;
        }
      }
      DateTimeOffset? ifUnmodifiedSince = requestHeaders.IfUnmodifiedSince;
      if (!ifUnmodifiedSince.HasValue)
        return;
      nullable = ifUnmodifiedSince;
      DateTimeOffset dateTimeOffset1 = utcNow;
      if ((nullable.HasValue ? (nullable.GetValueOrDefault() <= dateTimeOffset1 ? 1 : 0) : 0) == 0)
        return;
      nullable = ifUnmodifiedSince;
      DateTimeOffset lastModified1 = this._lastModified;
      this._ifUnmodifiedSinceState = nullable.HasValue && nullable.GetValueOrDefault() >= lastModified1 ? SpaStaticFileContext.PreconditionState.ShouldProcess : PreconditionState.PreconditionFailed;
    }

    private void ComputeIfRange()
    {
      RangeConditionHeaderValue ifRange = this.RequestHeaders.IfRange;
      if (ifRange == null)
        return;
      DateTimeOffset? lastModified1 = ifRange.LastModified;
      if (lastModified1.HasValue)
      {
        DateTimeOffset lastModified2 = this._lastModified;
        lastModified1 = ifRange.LastModified;
        if ((lastModified1.HasValue ? (lastModified2 > lastModified1.GetValueOrDefault() ? 1 : 0) : 0) == 0)
          return;
        this.IsRangeRequest = false;
      }
      else
      {
        if (this._etag == null || ifRange.EntityTag == null || ifRange.EntityTag.Compare(this._etag, true))
          return;
        this.IsRangeRequest = false;
      }
    }

    private void ComputeRange()
    {
      if (!this.IsGetMethod)
        return;
      bool isRangeRequest;
      (isRangeRequest, this._range) = RangeHelper.ParseRange(this._context, this.RequestHeaders, this._length);
      this.IsRangeRequest = isRangeRequest;
    }

    public void ApplyResponseHeaders(int statusCode)
    {
      this._response.StatusCode = statusCode;
      if (statusCode < 400)
      {
        if (!string.IsNullOrEmpty(this._contentType))
          this._response.ContentType = this._contentType;
        ResponseHeaders responseHeaders = this.ResponseHeaders;
        responseHeaders.LastModified = new DateTimeOffset?(this._lastModified);
        responseHeaders.ETag = this._etag;
        responseHeaders.Headers.AcceptRanges = (StringValues) "bytes";
      }
      if (statusCode == 200)
        this._response.ContentLength = new long?(this._length);
      this._options.OnPrepareResponse(new StaticFileResponseContext(this._context, this._fileInfo));
    }

    public PreconditionState GetPreconditionState() => GetMaxPreconditionState(this._ifMatchState, this._ifNoneMatchState, this._ifModifiedSinceState, this._ifUnmodifiedSinceState);


    #nullable disable
    private static PreconditionState GetMaxPreconditionState(
      params PreconditionState[] states)
    {
      PreconditionState preconditionState = PreconditionState.Unspecified;
      for (int index = 0; index < states.Length; ++index)
      {
        if (states[index] > preconditionState)
          preconditionState = states[index];
      }
      return preconditionState;
    }


    #nullable enable
    public Task SendStatusAsync(int statusCode)
    {
      this.ApplyResponseHeaders(statusCode);
      return Task.CompletedTask;
    }

    public async Task ServeStaticFile(HttpContext context, RequestDelegate next)
    {
      this.ComprehendRequestHeaders();
      switch (this.GetPreconditionState())
      {
        case PreconditionState.Unspecified:
        case PreconditionState.ShouldProcess:
          if (this.IsHeadMethod)
          {
            await this.SendStatusAsync(200);
            break;
          }
          try
          {
            if (this.IsRangeRequest)
            {
              await this.SendRangeAsync();
              break;
            }
            await this.SendAsync();
            break;
          }
          catch (FileNotFoundException ex)
          {
            context.Response.Clear();
          }
          await next(context);
          break;
        case PreconditionState.NotModified:
          await this.SendStatusAsync(304);
          break;
        case PreconditionState.PreconditionFailed:
          await this.SendStatusAsync(412);
          break;
        default:
          throw new NotImplementedException(this.GetPreconditionState().ToString());
      }
    }

    public async Task SendAsync()
    {
      this.SetCompressionMode();
      this.ApplyResponseHeaders(200);
      try
      {
        await this._context.Response.SendFileAsync(this._fileInfo, 0L, new long?(this._length), this._context.RequestAborted);
      }
      catch (OperationCanceledException ex)
      {
      }
    }

    internal async Task SendRangeAsync()
    {
      if (this._range == null)
      {
        this.ResponseHeaders.ContentRange = new ContentRangeHeaderValue(this._length);
        this.ApplyResponseHeaders(416);
      }
      else
      {
        long start;
        long length;
        this.ResponseHeaders.ContentRange = this.ComputeContentRange(this._range, out start, out length);
        this._response.ContentLength = new long?(length);
        this.SetCompressionMode();
        this.ApplyResponseHeaders(206);
        try
        {
          await this._context.Response.SendFileAsync(this._fileInfo, start, new long?(length), this._context.RequestAborted);
        }
        catch (OperationCanceledException ex)
        {
        }
      }
    }


    #nullable disable
    private ContentRangeHeaderValue ComputeContentRange(
      RangeItemHeaderValue range,
      out long start,
      out long length)
    {
      start = range.From.Value;
      long to = range.To.Value;
      length = to - start + 1L;
      return new ContentRangeHeaderValue(start, to, this._length);
    }

    private void SetCompressionMode()
    {
      IHttpsCompressionFeature compressionFeature = this._context.Features.Get<IHttpsCompressionFeature>();
      if (compressionFeature == null)
        return;
      compressionFeature.Mode = this._options.HttpsCompression;
    }


    #nullable enable
    internal enum PreconditionState : byte
    {
      Unspecified,
      NotModified,
      ShouldProcess,
      PreconditionFailed,
    }


    #nullable disable
    [Flags]
    private enum RequestType : byte
    {
      Unspecified = 0,
      IsHead = 1,
      IsGet = 2,
      IsRange = 4,
    }
  }
  
  internal static class RangeHelper
  {
    /// <summary>
    /// Returns the normalized form of the requested range if the Range Header in the <see cref="P:Microsoft.AspNetCore.Http.HttpContext.Request" /> is valid.
    /// </summary>
    /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Http.HttpContext" /> associated with the request.</param>
    /// <param name="requestHeaders">The <see cref="T:Microsoft.AspNetCore.Http.Headers.RequestHeaders" /> associated with the given <paramref name="context" />.</param>
    /// <param name="length">The total length of the file representation requested.</param>
    /// <param name="logger">The <see cref="T:Microsoft.Extensions.Logging.ILogger" />.</param>
    /// <returns>A boolean value which represents if the <paramref name="requestHeaders" /> contain a single valid
    /// range request. A <see cref="T:Microsoft.Net.Http.Headers.RangeItemHeaderValue" /> which represents the normalized form of the
    /// range parsed from the <paramref name="requestHeaders" /> or <c>null</c> if it cannot be normalized.</returns>
    /// <remark>If the Range header exists but cannot be parsed correctly, or if the provided length is 0, then the range request cannot be satisfied (status 416).
    /// This results in (<c>true</c>,<c>null</c>) return values.</remark>
    public static (bool isRangeRequest, RangeItemHeaderValue? range) ParseRange(
      HttpContext context,
      RequestHeaders requestHeaders,
      long length)
    {
      StringValues range1 = context.Request.Headers.Range;
      if (StringValues.IsNullOrEmpty(range1))
      {
        return (false, (RangeItemHeaderValue) null);
      }
      if (range1.Count > 1 || range1[0].IndexOf(',') >= 0)
      {
        return (false, (RangeItemHeaderValue) null);
      }
      RangeHeaderValue range2 = requestHeaders.Range;
      if (range2 == null)
      {
        return (false, (RangeItemHeaderValue) null);
      }
      ICollection<RangeItemHeaderValue> ranges = range2.Ranges;
      if (ranges == null)
      {
        return (false, (RangeItemHeaderValue) null);
      }
      if (ranges.Count == 0)
        return (true, (RangeItemHeaderValue) null);
      return length == 0L ? (true, (RangeItemHeaderValue) null) : (true, NormalizeRange(ranges.Single<RangeItemHeaderValue>(), length));
    }

    internal static RangeItemHeaderValue? NormalizeRange(
      RangeItemHeaderValue range,
      long length)
    {
      long? from = range.From;
      long? to = range.To;
      if (from.HasValue)
      {
        if (from.Value >= length)
          return (RangeItemHeaderValue) null;
        if (!to.HasValue || to.Value >= length)
          to = new long?(length - 1L);
      }
      else if (to.HasValue)
      {
        if (to.Value == 0L)
          return (RangeItemHeaderValue) null;
        long num1 = Math.Min(to.Value, length);
        from = new long?(length - num1);
        long? nullable1 = from;
        long num2 = num1;
        long? nullable2 = nullable1.HasValue ? new long?(nullable1.GetValueOrDefault() + num2) : new long?();
        long num3 = 1;
        long? nullable3;
        if (!nullable2.HasValue)
        {
          nullable1 = new long?();
          nullable3 = nullable1;
        }
        else
          nullable3 = new long?(nullable2.GetValueOrDefault() - num3);
        to = nullable3;
      }
      return new RangeItemHeaderValue(from, to);
    }
  }