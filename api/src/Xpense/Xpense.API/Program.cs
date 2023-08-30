var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger
builder.Services.AddSwaggerGen();



var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("","v1");
        options.RoutePrefix=String.Empty;
    });
}

app.Run();