using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Reflection;

namespace JocysCom.VS.AiCompanion.Plugins.LinkReader
{

	/// <summary>Program</summary>
	public class Program
	{
		/// <summary>Main</summary>
		public static void Main(string[] args)
		{
			CreateWebHostBuilder(args).Build().Run();
		}

		/// <summary>Create web host builder</summary>
		public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
				.ConfigureServices((context, services) =>
				{
					// Add services to the container.
					services.AddControllers();
					services.AddEndpointsApiExplorer();
					services.AddSwaggerGen(c =>
					{
						c.SwaggerDoc("v1", new OpenApiInfo { Title = Assembly.GetExecutingAssembly().GetName().Name, Version = "1.0" });
						// Set the comments path for the Swagger JSON and UI.
						var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
						var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
						c.IncludeXmlComments(xmlPath);
					});
				})
				.Configure(app =>
				{
					// Configure the HTTP request pipeline.
					app.UseSwagger();
					if (app.ApplicationServices.GetService<IWebHostEnvironment>().IsDevelopment())
					{
						app.UseSwaggerUI();
					}
					app.UseHttpsRedirection();
					app.UseRouting(); // UseRouting is required to use UseEndpoints
					app.UseAuthorization();
					app.UseEndpoints(endpoints =>
					{
						endpoints.MapControllers();
					});
				});
	}
}
