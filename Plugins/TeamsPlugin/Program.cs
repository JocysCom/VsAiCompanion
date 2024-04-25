using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System;
using System.IO;
using System.Reflection;

namespace JocysCom.VS.AiCompanion.Plugins.TeamsPlugin
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
				 .ConfigureAppConfiguration((hostingContext, config) =>
				 {
					 // Get the environment name, like Development, Staging, or Production
					 var env = hostingContext.HostingEnvironment;
					 // Define the path to the %APPDATA% folder and specify your custom configuration file
					 var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
					 var company = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyCompanyAttribute)))?.Company;
					 var product = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute)))?.Product;
					 var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
					 var customConfigPath = Path.Combine(appDataFolder, company, product, "appsettings.json");
					 // Add the custom configuration file
					 config.AddJsonFile(customConfigPath, optional: true, reloadOnChange: true);
					 // Optionally, load a different configuration file based on the environment
					 var environmentSpecificPath = Path.Combine(appDataFolder, company, product, $"appsettings.{env.EnvironmentName}.json");
					 config.AddJsonFile(environmentSpecificPath, optional: true, reloadOnChange: true);
				 })
				.ConfigureServices((context, services) =>
				{
					services.AddMicrosoftIdentityWebAppAuthentication(context.Configuration, "AzureAd")
		.EnableTokenAcquisitionToCallDownstreamApi(new[] { "https://graph.microsoft.com/Calendars.Read" })
		.AddInMemoryTokenCaches();

					services.AddControllersWithViews(options =>
					{
						var policy = new AuthorizationPolicyBuilder()
							.RequireAuthenticatedUser()
							.Build();
						options.Filters.Add(new AuthorizeFilter(policy));
					}).AddMicrosoftIdentityUI();


					// Add services to the container.
					services.AddControllers();
					services.AddEndpointsApiExplorer();
					services.AddSwaggerGen();
				})
				.Configure(app =>
				{
					// Configure the HTTP request pipeline.
					if (app.ApplicationServices.GetService<IWebHostEnvironment>().IsDevelopment())
					{
						app.UseSwagger();
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
