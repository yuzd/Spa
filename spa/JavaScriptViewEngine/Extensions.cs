using JavaScriptViewEngine.Middleware;
using System;
using JavaScriptViewEngine.Pool;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using AppBuilder = Microsoft.AspNetCore.Builder.IApplicationBuilder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JavaScriptViewEngine
{
    /// <summary>
    /// Some extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Added the middlware that creates and disposes a <see cref="IRenderEngine"/> for each request
        /// </summary>
        /// <param name="app">The application.</param>
        public static void UseJsEngine(
            this AppBuilder app
            )
        {
            app.UseMiddleware<RenderEngineMiddleware>();
        }


        /// <summary>
        /// Add the services required to use a render engine, a pool, etc.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="action"></param>
        public static void AddJsEngine(this IServiceCollection services, Action<JsEngineServiceBuilder> action)
        {
            var builder = new JsEngineServiceBuilder();
            action(builder);
            builder.Register(services);
        }
        
        /// <summary>
        /// This will add <see cref="JsViewEngine"/> to a collection used by mvc to render
        /// </summary>
        internal class JavaScriptViewEngineMvcViewOptionsSetup : ConfigureOptions<MvcViewOptions>
        {
            /// <summary>
            /// Initializes a new instance of <see cref="JavaScriptViewEngineMvcViewOptionsSetup"/>.
            /// </summary>
            /// <param name="serviceProvider">The application's <see cref="IServiceProvider"/>.</param>
            public JavaScriptViewEngineMvcViewOptionsSetup(IServiceProvider serviceProvider)
                : base(options => ConfigureMvc(serviceProvider, options))
            {
            }

            /// <summary>
            /// Configures <paramref name="options"/> to use <see cref="JsViewEngine"/>.
            /// </summary>
            /// <param name="serviceProvider">The application's <see cref="IServiceProvider"/>.</param>
            /// <param name="options">The <see cref="MvcViewOptions"/> to configure.</param>
            public static void ConfigureMvc(
                IServiceProvider serviceProvider,
                MvcViewOptions options)
            {
                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                if (options == null)
                    throw new ArgumentNullException(nameof(options));

                var jsViewEngine = serviceProvider.GetRequiredService<IJsViewEngine>();
                options.ViewEngines.Add(jsViewEngine);
            }
        }

        public class JsEngineServiceBuilder
        {
            private Action<IServiceCollection> _engineFactoryAction;
            private Action<IServiceCollection> _renderEngineBuilderAction;

            internal JsEngineServiceBuilder() {  }

            public JsEngineServiceBuilder UseSingletonEngineFactory()
            {
                if (_engineFactoryAction != null)
                    throw new Exception("You have already registered an engine factory.");

                _engineFactoryAction = services =>
                {
                    services.AddSingleton<IRenderEngineFactory, SingletonRenderEngineFactory>();
                };

                return this;
            }

            public JsEngineServiceBuilder UseCustomEngineFactory<T>() where T: class, IRenderEngineFactory
            {
                if (_engineFactoryAction != null)
                    throw new Exception("You have already registered an engine factory.");

                _engineFactoryAction = services =>
                {
                    services.AddSingleton<IRenderEngineFactory, T>();
                };

                return this;
            }
            
            public JsEngineServiceBuilder UseRenderEngine<T>(Action<RazorEngineOption> rezorEngineOption = null) where T: class, IRenderEngineBuilder
            {
                if (_renderEngineBuilderAction != null)
                    throw new Exception("You have already registered an engine builder.");

                _renderEngineBuilderAction = services =>
                {
                    services.AddSingleton<IRenderEngineBuilder, T>();
                    if (rezorEngineOption != null)
                        services.Configure(rezorEngineOption);
                };

                return this;
            }

            internal void Register(IServiceCollection services)
            {
                if(_engineFactoryAction == null)
                    throw new Exception("You must specific an engine factory using 'Use(.*)EngineFactory()' methods.");

                if (_renderEngineBuilderAction == null)
                    throw new Exception("You must specific an engine builder using 'Use(.*)RenderEngine()' methods.");

                _engineFactoryAction(services);
                _renderEngineBuilderAction(services);
                
                services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcViewOptions>, JavaScriptViewEngineMvcViewOptionsSetup>());
                services.TryAddSingleton<IFolderWatcher, FolderWatcher>();
                services.TryAddSingleton<IFileWatcher, FileWatcher>();
                services.TryAddTransient<IJsViewEngine, JsViewEngine>();
            }
        }

    }
}
