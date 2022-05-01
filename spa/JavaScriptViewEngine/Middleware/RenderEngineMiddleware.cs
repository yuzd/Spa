using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using spa.JavaScriptViewEngine.Utils;

namespace spa.JavaScriptViewEngine.Middleware
{
    public class RenderEngineMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RazorRenderEngine _engine;
        private readonly IWebHostEnvironment _hostingEnv;
        private readonly IOptions<StaticFileOptions> _options;
        private readonly ILogger _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public RenderEngineMiddleware(RequestDelegate next, RazorRenderEngine engine, IWebHostEnvironment hostingEnv, IOptions<StaticFileOptions> options,
            ILogger<RenderEngineMiddleware> logger)
        {
            _next = next;
            _engine = engine;
            _hostingEnv = hostingEnv;
            _options = options;
            _logger = logger;
            _contentTypeProvider = new FileExtensionContentTypeProvider();
        }

        /// <summary>
        /// Invokes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            try
            {
                var pathValue = context.Request.Path.Value!.ToString();
                if (string.IsNullOrEmpty(pathValue) || "/" == pathValue)
                {
                    await _next.Invoke(context);
                    return;
                }

                // 当前refer 或者 cookie
                var headersReferer = context.Request.Headers.Referer.ToString().Split('/').LastOrDefault();
                var cookieProject = context.Request.Cookies["spa_project"];
                var isFromSpa = (cookieProject != null && headersReferer != null && cookieProject == headersReferer);
                var isFile = _contentTypeProvider.TryGetContentType(pathValue.ToLower(), out var contentType);
                if (isFromSpa && isFile && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
                    !pathValue.ToLower().StartsWith("/" + cookieProject))
                {
                    // 来自spa的静态文件请求 但是又没有
                    SpaStaticFileContext staticFileContext = new SpaStaticFileContext(context, this._options.Value, this._logger,
                        _hostingEnv.WebRootFileProvider, contentType, "/" + cookieProject + pathValue);
                    if (staticFileContext.LookupFileInfo())
                    {
                        await staticFileContext.ServeStaticFile(context, this._next);
                        return;
                    }
                }
                else if (isFile)
                {
                    await _next.Invoke(context);
                    return;
                }

                var html = await _engine.RenderAsync(context);
                if (string.IsNullOrEmpty(html))
                {
                    await _next.Invoke(context);
                    return;
                }

                context.Response.ContentType = "text/html;charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(html);
            }
            catch
            {
                await _next.Invoke(context);
            }
        }
    }
}