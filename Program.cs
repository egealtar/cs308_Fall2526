using e_commerce.Data;
using e_commerce.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// MongoDB bağlantısı
builder.Services.AddSingleton<IMongoClient>(s =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDB") 
        ?? "mongodb://localhost:27017";
    return new MongoClient(connectionString);
});

builder.Services.AddScoped(s =>
{
    var client = s.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "BookeryDB";
    return client.GetDatabase(databaseName);
});

// Repository'leri ekle
builder.Services.AddScoped<IMongoDBRepository<Product>>(s =>
{
    var database = s.GetRequiredService<IMongoDatabase>();
    return new MongoDBRepository<Product>(database, "Products");
});

builder.Services.AddScoped<IMongoDBRepository<Order>>(s =>
{
    var database = s.GetRequiredService<IMongoDatabase>();
    return new MongoDBRepository<Order>(database, "Orders");
});

builder.Services.AddScoped<IMongoDBRepository<Category>>(s =>
{
    var database = s.GetRequiredService<IMongoDatabase>();
    return new MongoDBRepository<Category>(database, "Categories");
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();