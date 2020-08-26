﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using IdentityServer4.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using IdentityServer.Data;
using IdentityServer4.Services;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;
using System.Net;

namespace IdentityServer
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }
        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            // uncomment, if you want to add an MVC-based UI
            services.AddControllersWithViews();

            IdentityModelEventSource.ShowPII = true;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));
            services.AddDbContext<Data.ConfigurationDbContext>(options => options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
            })
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddDefaultTokenProviders();

            services.AddAuthentication()
                .AddOpenIdConnect("azuread", "Azure AD", options => Configuration.Bind("AzureAd", options));
            services.Configure<OpenIdConnectOptions>("azuread", options =>
            {
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Events = new OpenIdConnectEvents()
                {
                    OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/Account/Logout");
                        return Task.FromResult(0);
                    }
                };
            });

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.UserInteraction.LoginUrl = "/Account/Login";
                options.UserInteraction.LogoutUrl = "/Account/Logout";
                options.Authentication = new AuthenticationOptions()
                {
                    CookieLifetime = TimeSpan.FromHours(10), // ID server cookie timeout set to 10 hours
                    CookieSlidingExpiration = true
                };
            })
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
                options.EnableTokenCleanup = true;
            })
            .AddAspNetIdentity<ApplicationUser>();

            // not recommended for production - you need to store your key material somewhere secure
            builder.AddDeveloperSigningCredential();

                        
            services.AddScoped<IProfileService, ProfileService>();

        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // uncomment if you want to add MVC
            app.UseStaticFiles();
            app.UseRouting();

            app.UseIdentityServer();

            // uncomment, if you want to add MVC
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
