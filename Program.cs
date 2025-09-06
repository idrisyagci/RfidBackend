using RfidBackend.Hubs;
using RfidBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "RFID Backend API", Version = "v1" });
    
    // XML dokümantasyon dosyası varsa dahil et
    var xmlFile = "RfidBackend.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, true);
    }
});

// SignalR ekleme
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// HTTP Client Factory
builder.Services.AddHttpClient();

// RFID Service ekleme
builder.Services.AddSingleton<IRfidService, RfidService>();

// CORS policy ekleme (mobil uygulama için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
    
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:8080", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Logging konfigürasyonu
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RFID Backend API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// CORS kullanma
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// SignalR Hub mapping
app.MapHub<RfidHub>("/rfidHub").RequireCors("SignalRPolicy");

// Health check endpoint
app.MapGet("/health", () => new { 
    status = "healthy", 
    timestamp = DateTime.Now,
    version = "1.0.0"
});

app.Run();
