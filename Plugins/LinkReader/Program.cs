using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JocysCom.VS.AiCompanion.Plugins.PowerShellExecutor.Controllers
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
