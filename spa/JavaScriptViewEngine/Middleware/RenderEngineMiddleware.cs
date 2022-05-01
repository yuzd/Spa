using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace spa.JavaScriptViewEngine.Middleware
{
    public class RenderEngineMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RazorRenderEngine _engine;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public RenderEngineMiddleware(RequestDelegate next, RazorRenderEngine engine)
        {
            _next = next;
            _engine = engine;
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

                if (_contentTypeProvider.TryGetContentType(pathValue.ToLower(), out _))
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
