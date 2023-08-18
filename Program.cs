using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using UpLoader_For_ET.Data;
using UpLoader_For_ET;
using UpLoader_For_ET.Services;
using UpLoader_For_ET.Configuration;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
/*
builder.Services.AddDbContext<UploadsDBContext>(
    options => options.UseSqlite(builder.Configuration.GetConnectionString("UploadsDBContext"))
    );
*/

//builder.Services.AddTransient<IEmailSender, EmailServiceDevelopmentMode>(); //Offline version, saves mail to TempMail folder

builder.Services.AddTransient<IEmailSender, MailService>(); // This is the more recent email sender, relies on settings in appsettings.json via MailSettings configuration below

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(
    options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

builder.Services.Configure<MailSettings>(builder.Configuration.GetSection(nameof(MailSettings)));
builder.Services.Configure<UserSpaceLimitSetting>(builder.Configuration.GetSection(nameof(UserSpaceLimitSetting)));
builder.Services.Configure<MessageLimitSetting>(builder.Configuration.GetSection(nameof(MessageLimitSetting)));

builder.Services.AddSingleton(new ContactFormVisibility());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
Console.WriteLine("Program Ended");
