using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuessMyNumber.Model;
using GuessMyNumber.Provider;
using GuessMyNumber.Web.Handler;
using GuessMyNumber.Web.Hubs;
using GuessMyNumber.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GuessMyNumber.Web
{
    public class Startup
    {
        private IConfigurationRoot _configuration;

        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            builder.AddEnvironmentVariables();
            _configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(_configuration.GetSection("AppSettings"));

            services.AddRazorPages();
            services.AddSignalR();

            // configure DI for application services
            services.AddSingleton<IGameProvider, GameProvider>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            bool useHttps = _configuration.GetValue<bool>("AppSettings:RedirectHttps");
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                if (useHttps)
                {
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }
            }

            if (useHttps)
            {
                app.UseHttpsRedirection(); 
            }
            
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            { 
                // Enable when using razor pages
                // endpoints.MapRazorPages();

                endpoints.MapHub<GameHub>("/gameHub");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action}/{id?}",
                    defaults: new { controller = "Game", action = "Index" });
            });


            //our custom middleware
            app.Use((context, next) =>
            {
                var endpointFeature = context.Features[typeof(IEndpointFeature)] as IEndpointFeature;
                var endpoint = endpointFeature?.Endpoint;

                //note: endpoint will be null, if there was no
                //route match found for the request by the endpoint route resolver middleware
                if (endpoint != null)
                {
                    var routePattern = (endpoint as RouteEndpoint)?.RoutePattern
                                                                  ?.RawText;

                    Console.WriteLine("Name: " + endpoint.DisplayName);
                    Console.WriteLine($"Route Pattern: {routePattern}");
                    Console.WriteLine("Metadata Types: " + string.Join(", ", endpoint.Metadata));
                }
                return next();
            });

        }
    }
}
