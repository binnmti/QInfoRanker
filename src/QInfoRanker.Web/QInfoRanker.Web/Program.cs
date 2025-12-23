using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces;
using QInfoRanker.Infrastructure.Services;
using QInfoRanker.Infrastructure.Collectors;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Infrastructure.Scoring;
using QInfoRanker.Web.Client.Pages;
using QInfoRanker.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<QInfoRankerDbContext>(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Add HttpClient for collectors
builder.Services.AddHttpClient();

// Add collectors
builder.Services.AddTransient<ICollector, HackerNewsCollector>();
builder.Services.AddSingleton<CollectorFactory>();

// Add scoring service
builder.Services.AddScoped<IScoringService, HybridScoringService>();

// Add article collection service
builder.Services.AddScoped<ArticleCollectionService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(QInfoRanker.Web.Client._Imports).Assembly);

app.Run();
