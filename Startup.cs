﻿using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using incrementally.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace incrementally_backend
{
  public class Startup
  {
    public Startup(IConfiguration configuration, IHostingEnvironment env)
    {
      Configuration = configuration;
      Env = env;
      if (env.IsDevelopment())
      {
          IdentityModelEventSource.ShowPII = true; 
      }
    }

    public IConfiguration Configuration { get; }
    public IHostingEnvironment Env { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddAuthentication(options =>
      {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
      })
        .AddJwtBearer(jwtOptions =>
        {
          jwtOptions.Authority = $"https://login.microsoftonline.com/tfp/{Configuration["AzureAdB2C:Tenant"]}.onmicrosoft.com/{Configuration["AzureAdB2C:Policy"]}/v2.0/";
          jwtOptions.Audience = Configuration["AzureAdB2C:ClientId"];
          jwtOptions.TokenValidationParameters = new TokenValidationParameters {
            ValidIssuer = $"https://{Configuration["AzureAdB2C:Tenant"]}.b2clogin.com/{Configuration["AzureAdB2C:TenantId"]}/v2.0/"
          };
          jwtOptions.Events = new JwtBearerEvents
          {
            // OnAuthenticationFailed = AuthenticationFailed
          };
        });
      services.AddCors(options =>
        {
          options.AddPolicy("AllowCors",
          builder =>
          {
            if (Env.IsDevelopment()) {
              builder.WithOrigins(
                "http://localhost:8080",
                "http://localhost:8080/editor"
              )
                .AllowAnyHeader()
                .AllowAnyMethod();
            } else {
              builder.WithOrigins(
                "https://incrementally.xyz",
                "https://incrementally.xyz/editor"
              )
                .AllowAnyHeader()
                .AllowAnyMethod();
            }
          });
        });
      services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
      services.AddSingleton<CosmosDbService>(InitializeCosmosClientInstanceAsync(Configuration.GetSection("CosmosDb"), Configuration).GetAwaiter().GetResult());
      services.AddSingleton<SqlKata.Compilers.PostgresCompiler>();
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

      app.UseCors("AllowCors");

      app.UseAuthentication();
      app.UseHttpsRedirection();
      app.UseMvc();
    }

    private static async Task<CosmosDbService> InitializeCosmosClientInstanceAsync(IConfigurationSection configurationSection, IConfiguration configuration)
    {
      string databaseName = configurationSection.GetSection("DatabaseName").Value;
      var containerNames = new List<string>();
      configurationSection.GetSection("ContainerNames").Bind(containerNames);
      string account = configurationSection.GetSection("Account").Value;
      // string key = configurationSection.GetSection("Key").Value;
      string key = configuration["CosmosDBKey"];
      CosmosClientBuilder clientBuilder = new CosmosClientBuilder(account, key);
      CosmosClient client = clientBuilder
        .WithConnectionModeDirect()
        .Build();
      CosmosDbService cosmosDbService = new CosmosDbService(client, databaseName, containerNames);
      var database = await client.CreateDatabaseIfNotExistsAsync(databaseName);
      foreach (var containerName in containerNames)
      {
        await database.Database.CreateContainerIfNotExistsAsync(containerName, "/id");
      }

      return cosmosDbService;
    }

    private Task AuthenticationFailed(AuthenticationFailedContext arg)
    {
      // For debugging purposes only!
      var s = $"AuthenticationFailed: {arg.Exception.Message}";
      arg.Response.ContentLength = s.Length;
      arg.Response.Body.Write(Encoding.UTF8.GetBytes(s), 0, s.Length);
      return Task.FromResult(0);
    }
  }
}
