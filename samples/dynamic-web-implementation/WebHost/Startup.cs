﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Library;
using McMaster.NETCore.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebHost
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add MVC Services
            var builder = services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            var sharedTyes = new List<Type> { typeof(IServiceProvider), typeof(IServiceCollection), typeof(IPluginFactory) };

            ////Load plugin contract
            var assLib = Path.Combine(AppContext.BaseDirectory, @"PluginContract\PluginContract\PluginContract.dll");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(assLib);

            //Load plugin implementation
            var assImpl = Path.Combine(AppContext.BaseDirectory, @"PluginImpl\PluginImpl\PluginImpl.dll");
            var implLoader = PluginLoader.CreateFromAssemblyFile(
                       assImpl,
                       sharedTyes.ToArray(),
                       conf => {
                           conf.PreferSharedTypes = true;
                       });
            var implLoadedLib = implLoader.LoadDefaultAssembly();

            // Invoke configuration for implementation
            var configType = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.Contains("PluginImpl")).First().GetTypes()
                .Where(t => typeof(IPluginFactory).IsAssignableFrom(t) && !t.IsAbstract).FirstOrDefault();
            var plugin = Activator.CreateInstance(configType) as IPluginFactory;
            plugin.Configure(services);

            //Load plugin implementation Override/Exstencion
            var assImplOverride = Path.Combine(AppContext.BaseDirectory, @"PluginImplOverride\PluginImplOverride\PluginImplOverride.dll");
            var implLoaderOverride = PluginLoader.CreateFromAssemblyFile(
                       assImplOverride,
                       sharedTyes.ToArray(),
                       conf => {
                           conf.PreferSharedTypes = true;
                       });
            var implOverrideLoadedLib = implLoaderOverride.LoadDefaultAssembly();



            // Invoke configuration for Override or extend capability
            var configOverrideType = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.Contains("PluginImplOverride")).First().GetTypes()
                .Where(t => typeof(IPluginFactory).IsAssignableFrom(t) && !t.IsAbstract).FirstOrDefault();
            var pluginOverride = Activator.CreateInstance(configOverrideType) as IPluginFactory;
            pluginOverride.Configure(services);

            // Register controllers
            var ass = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var a in ass)
            {
                builder.AddApplicationPart(a).AddControllersAsServices();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();
        }

        private static List<Assembly> GetAllAssembly()
        {
            var allAssembly = new List<Assembly>();
            allAssembly.AddRange(AppDomain.CurrentDomain.GetAssemblies());
            allAssembly.Add(Assembly.GetExecutingAssembly());
            allAssembly.Add(Assembly.GetEntryAssembly());
            return allAssembly;
        }
    }
}
