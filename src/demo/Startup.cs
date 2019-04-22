﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using EntityGraphQL.Schema;

namespace demo
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<DemoContext>(opt => opt.UseSqlite("Filename=demo.db"));

            // add schema provider so we don't need to create it everytime
            services.AddSingleton(GraphQLSchema.MakeSchema());
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, DemoContext db)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            db.Database.EnsureCreated();
            // db.Database.Migrate();

            // add test data
            // db.Properties.Add(new Property {Id = 11, Name = "My House", Location = new Location {Id = 21, Name = "Australia"}});
            // db.Properties.Add(new Property {Id = 12, Name = "The White House", Location = new Location {Id = 22, Name = "America", SomeInt = 9999}});
            // db.SaveChanges();

            app.UseFileServer();

            app.UseMvc();
        }
    }
}
