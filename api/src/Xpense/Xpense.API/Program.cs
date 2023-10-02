using Microsoft.EntityFrameworkCore;
using Xpense.API.Data;

var builder = WebApplication.CreateBuilder(args);

// Register Controllers
builder.Services.AddControllers();

// Register XpenseDbContext, XpenseDbContextFactory
builder.Services.AddPooledDbContextFactory<XpenseDbContext>(optionsBuilder =>
{
    optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});




var app = builder.Build();

app.UseRouting();
app.MapControllers();
app.Run();