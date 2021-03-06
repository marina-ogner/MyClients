using System.IO;
using System.Text;
using Data.EF;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MyClientsBase.Helpers;
using MyClientsBase.Services;
using NLog.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;
using AutoMapper;

namespace MyClientsBase
{
  public class Startup
  {
    public IConfigurationRoot Configuration { get; }

    public Startup(IHostingEnvironment env)
    {
      var builder = new ConfigurationBuilder()
        .SetBasePath(env.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();
      Configuration = builder.Build();
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      var connection = Configuration["AppSettings:Connection"];
      services.AddDbContext<ApplicationDbContext>(options =>
                  options.UseMySQL(connection, b => b.MigrationsAssembly("MyClientsBase")));

      //inject app config
      services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));
      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
          {
            options.TokenValidationParameters = new TokenValidationParameters
            {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,
              ValidIssuer = Configuration["Jwt:Issuer"],
              ValidAudience = Configuration["Jwt:Issuer"],
              IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
            };
          });
      //inject services
      services.AddScoped<IUserService, UserService>();
      services.AddScoped<IClientService, ClientService>();

      services.AddCors(o => o.AddPolicy("MyPolicy", builder =>
      {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowCredentials()
               .AllowAnyHeader();
      }));

      // Register the Swagger generator, defining one or more Swagger documents
      services.AddSwaggerGen(c =>
      {
        c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
      });
      services.AddMvc();
      //inject automapper
      services.AddAutoMapper();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
    {
      loggerFactory.AddConsole(Configuration.GetSection("Logging"));
      loggerFactory.AddDebug();
      loggerFactory.AddNLog();
      //#if DEBUG
      //      loggerFactory.AddNLogWeb();
            //loggerFactory.AddFile("Logs/ts-{Date}");
      //#endif
      
      app.Use(async (context, next) =>
      {
        await next();
        if (context.Response.StatusCode == 404 &&
           !Path.HasExtension(context.Request.Path.Value) &&
           !context.Request.Path.Value.StartsWith("/api/"))
        {
          context.Request.Path = "/index.html";
          await next();
        }
      });

      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }
      app.UseAuthentication();

      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
      });
      app.UseCors("MyPolicy");
      //app.UseCors(x => x
      //        .AllowAnyOrigin()
      //        .AllowAnyMethod()
      //        .AllowAnyHeader()
      //        .AllowCredentials());

      app.UseMvcWithDefaultRoute();
      app.UseDefaultFiles();
      app.UseStaticFiles();
      app.UseMvc();
    }
  }
}
