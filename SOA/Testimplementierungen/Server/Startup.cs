using BlobsServer;
using BlobsServer.Services;
using BlueDotsServer.Configurations;
using BlueDotsServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlueDotsServer
{
    public class Startup
    {
        private readonly IConfiguration appsettings;

        public Startup(IConfiguration appsettings)
        {
            this.appsettings = appsettings;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var meshConfigOptions = new MeshConfigOptions();
            appsettings.Bind(MeshConfigOptions.SectionName, meshConfigOptions);
            services.AddSingleton(meshConfigOptions);
            services.AddSingleton<BlueDotsService>();
            services.AddSingleton<MeshClient>();
            services.AddSingleton<BlobsService>();
            services.AddSingleton<BlobsMeshClient>();
            services.AddSingleton<MyName>();
            services.AddGrpc(options => 
            {
                options.MaxReceiveMessageSize = 6 * 1024 * 1024; // 6 MB
                //options.MaxSendMessageSize = 6 * 1024 * 1024;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<BlueDotsService>();
                endpoints.MapGrpcService<MeshService>();
                endpoints.MapGrpcService<BlobsService>();
                endpoints.MapGrpcService<BlobMeshService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
