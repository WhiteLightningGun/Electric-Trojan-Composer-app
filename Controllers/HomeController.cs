using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UpLoader_For_ET.Models;
using Microsoft.AspNetCore.Authorization;
using UpLoader_For_ET.Data;
using UpLoader_For_ET.DBModels;
using static System.IO.Path;
using UpLoader_For_ET.Configuration;
using Microsoft.AspNetCore.Identity.UI.Services;
using UpLoader_For_ET.Services;
using System.Text;
using System.Text.Encodings.Web;


using Microsoft.Extensions.Options;

using UpLoader_For_ET.StaticClasses;


namespace UpLoader_For_ET.Controllers;

public partial class HomeController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext ApplicationDB;
    //private UploadsDBContext uploadsDB;
    public UserSpaceLimitSetting userSpaceLimit;
    private MessageLimitSetting messageSettings;
    private ContactFormVisibility contactFormControl;
    private readonly IEmailSender emailSender;
    private static string MainDirectory = Environment.CurrentDirectory;
    
    public HomeController(ILogger<HomeController> logger, 
    ApplicationDbContext injectedADB, 
    IWebHostEnvironment env,
    IOptions<UserSpaceLimitSetting> _userSpaceLimit,
    IEmailSender _emailSender,
    ContactFormVisibility contactFormVisibility,
    IOptions<MessageLimitSetting> _messageSettings)
    {
        _logger = logger;
        ApplicationDB = injectedADB;
        _env = env;
        userSpaceLimit = _userSpaceLimit.Value;
        emailSender = _emailSender;
        contactFormControl = contactFormVisibility;
        messageSettings = _messageSettings.Value;
    }

    public IActionResult Index()
    {
        
        List<FrontPageEntry>? entries = ApplicationDB.FrontPageEntries?.Select(x => x).ToList();

        if(entries is not null)
        {
            IndexPageModel indexpagemodel = new(entries);

            return View(indexpagemodel);
        }

        return View();
    }
    
    public IActionResult Privacy()
    {

        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    [Authorize]
    public IActionResult UserSummary()
    {
        string? userName = User.Identity?.Name;
        string userIDHash = GetUserIDHash(userName);
        decimal spaceUsed = GetSpaceForUser(userName);

        UserSummaryModel userSummaryModel = new();
        userSummaryModel.userName = userName;
        userSummaryModel.uploadDBEntries = ApplicationDB.UploadDBEntries?.Select(x => x).Where(x => x.userHash == userIDHash).ToList();
        userSummaryModel.percentageUsed = PercentageSpaceUsed(userName);
        userSummaryModel.spaceUsedMB = spaceUsed;
        userSummaryModel.spaceRemaining = userSpaceLimit.userLimitMB - spaceUsed;

        return View(userSummaryModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }


    [Authorize(Roles="User,Administrators")]
    [HttpPost]
    [RequestSizeLimit(134212233)] // ~134 MB
    public async Task<IActionResult> UploadPage(IFormFile uploadedFile, string comment)
    {
        //string QuarantinePath = Combine(MainDirectory, "Quarantine");
        string userIDHash = GetUserIDHash(User.Identity?.Name);

        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}/");
        string? userComment = comment; // the original argument (string comment) is non-nullable to enforce mandatoryness

        //For monitoring data usage it will be necessary to create a record of the amount of space used by the user and measuring folder usage

        if(ModelState.IsValid)
        {
            if(!CheckSpaceForUser(User.Identity?.Name, uploadedFile.Length))
            {
                TempData["Alert"] = $"Upload blocked! {uploadedFile.FileName} is {(decimal)uploadedFile.Length/(1000*1000):0.0} megabytes - too big! ";
                return View();
            }
            using var fileStream = uploadedFile.OpenReadStream();
            byte[] eightByteHeader = new byte[8];
            fileStream.Read(eightByteHeader, 0, 8);

            if (UploadCheck.CheckFormat(eightByteHeader) != true)
            {
                //Consider banning user for uploading deliberately misnamed files. 
                List<string> errorList = ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage).ToList();
                errorList.Add("Wrong file format, permitted formats are OGG vorbis and MP3 with ID3v2 tags");
                UploadModel uploadModelWithErrors = new(hasErrors: true, validationErrors: errorList);
                return View(uploadModelWithErrors);
            }

            // for now, we assume successful virus scan takes place here
            string extension = System.IO.Path.GetExtension(uploadedFile.FileName);
            string fileHashForNewFile = Guid.NewGuid().ToString() + extension;
            
            Directory.CreateDirectory(FileBayPathForUser);
            string savePath = Combine(FileBayPathForUser, $"{fileHashForNewFile}");
            
            using (var newFile = System.IO.File.Create(savePath))
            {
                await uploadedFile.CopyToAsync(newFile);
            }

            //Add file name and location information to DB
            UploadDBEntry newDBEntry = new UploadDBEntry
            {
                userHash = userIDHash,
                userDescription = userComment,
                userFileTitle = uploadedFile.FileName,
                fileHash = fileHashForNewFile
            };
            await ApplicationDB.AddAsync(newDBEntry);
            await ApplicationDB.SaveChangesAsync();
        }
        else
        {
            List<string> errorList = ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage).ToList();
            UploadModel uploadModelWithErrors = new(hasErrors: true, validationErrors: errorList);
            TempData["Alert"] = "Upload not successful.";
            return View(uploadModelWithErrors);
        }

        TempData["Message"] = "Upload Successful.";
        return View();
    }

    [Authorize(Roles="User,Administrators")]
    public IActionResult UploadPage()
    {
        TempData["Report"] = $"Hello {User.Identity?.Name}, you have used {GetSpaceForUser(User.Identity?.Name):0.00} megabytes so far. Maximum {userSpaceLimit.userLimitMB} megabytes.";
        return View();
    }
    
    [Authorize]
    [Authorize(Roles="Plebian,User,Administrators")]
    public IActionResult UserDownloads()
    {
        string userIDHash = GetUserIDHash(User.Identity?.Name);
        UserDownloadModel userdownloadmodel = new();
        userdownloadmodel.uploadDBEntries = ApplicationDB.UploadDBEntries?.Select(x => x).Where(x => x.userHash == userIDHash).ToList();
        return View(userdownloadmodel);
    }

    [Authorize(Roles="Plebian,User,Administrators")]
    [HttpPost]
    public async Task<IActionResult> UserDownloads(string argFileHash, string argFileName)
    {
        string userIDHash = GetUserIDHash(User.Identity?.Name);
        string fileNameHash = argFileHash;
        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}/{fileNameHash}");

        //get file as bytes, save with user-friendly name parameter
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(FileBayPathForUser);
        return File(fileBytes, "application/force-download", argFileName); 
    }

    [Authorize(Roles="Plebian,User,Administrators")]
    [HttpPost]
    public async Task<IActionResult> SpecificUserDownload(string argFileHash, string argFileName, string argUserName)
    {
        string userIDHash = GetUserIDHash(argUserName);
        string fileNameHash = argFileHash;
        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}/{fileNameHash}");
        Console.WriteLine($"FileBayPathForUser was {FileBayPathForUser}");

        //get file as bytes, save with user-friendly name parameter
        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(FileBayPathForUser);
        return File(fileBytes, "application/force-download", argFileName); 
    }


    [Authorize(Roles="User,Administrators")]
    [HttpPost]
    public async Task<IActionResult> DeleteAlert(string? argFileHashD, string argFileNameD)
    {
        string userIDHash = GetUserIDHash(User.Identity?.Name);

        UploadDBEntry? toDelete = ApplicationDB.UploadDBEntries?.Select(x => x).Where(x => x.fileHash == argFileHashD).FirstOrDefault();

        string FileBayTrackPath = Combine(MainDirectory, $"FileBay/{userIDHash}/{argFileHashD}");

        if(toDelete != null)
        {
            ApplicationDB.UploadDBEntries?.Remove(toDelete);
            await ApplicationDB.SaveChangesAsync();
        }

        if (System.IO.File.Exists(FileBayTrackPath)) 
        { 
            System.IO.File.Delete(FileBayTrackPath);
        }

        DeleteAlertModel deletealertmodel = new (argFileNameD);

        return View(deletealertmodel);
    }

    [Authorize(Roles="Administrators")]
    public IActionResult AssignTrack()
    {
        AssignTrackModel assignTrackModel = new();
        assignTrackModel.listOfUsers = ApplicationDB.Users.Select(x => x.Email).ToList();

        return View(assignTrackModel);
    }

    [Authorize(Roles="Administrators")]
    [HttpPost]
    public async Task<IActionResult> AssignTrack(IFormFile uploadedFile, string comment, string selecteduser)
    {
        AssignTrackModel assignTrackModel = new();
        assignTrackModel.listOfUsers = ApplicationDB.Users.Select(x => x.Email).ToList();

        if(!CheckSpaceForUser(selecteduser, uploadedFile.Length))
        {
            TempData["Alert"] = $"Upload blocked! {uploadedFile.FileName} is {(decimal)uploadedFile.Length/(1000*1000):0.0} megabytes - too big! {selecteduser} has used {GetSpaceForUser(selecteduser):0.00} mb. Maximum limit {userSpaceLimit.userLimitMB}";

            return View(assignTrackModel);
        }

        string userIDHash = GetUserIDHash(selecteduser);

        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}/");
        string? userComment = comment;

        //For monitoring data usage it will be necessary to create a record of the amount of space used by the user and measuring folder usage

        if(ModelState.IsValid)
        {
            Console.WriteLine("Valid ModelState.");

            using var fileStream = uploadedFile.OpenReadStream();
            byte[] eightByteHeader = new byte[8];
            fileStream.Read(eightByteHeader, 0, 8);

            if (UploadCheck.CheckFormat(eightByteHeader) != true)
            {
                //Consider banning user for uploading deliberately misnamed files. 
                List<string> errorList = ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage).ToList();
                errorList.Add("Wrong file format, permitted formats are OGG vorbis and MP3 with ID3v2 tags");

                AssignTrackModel uploadModelWithErrors = new();
                uploadModelWithErrors.HasErrors = true;
                uploadModelWithErrors.ValidationErrors = errorList;
                uploadModelWithErrors.listOfUsers =ApplicationDB.Users.Select(x => x.Email).ToList();

                return View(uploadModelWithErrors);
            }

            // for now, we assume successful virus scan takes place here

            string extension = System.IO.Path.GetExtension(uploadedFile.FileName);

            string fileHashForNewFile = Guid.NewGuid().ToString() + extension;
            string originalFileName = uploadedFile.FileName;
            
            Directory.CreateDirectory(FileBayPathForUser);
            string savePath = Combine(FileBayPathForUser, $"{fileHashForNewFile}"); //needs correct file extension
            
            using (var newFile = System.IO.File.Create(savePath))
            {
                uploadedFile.CopyTo(newFile);
            }

            //Add file name and location information to DB
            UploadDBEntry newDBEntry = new UploadDBEntry
            {
                userHash = userIDHash,
                userDescription = userComment,
                userFileTitle = originalFileName,
                fileHash = fileHashForNewFile
            };
            await ApplicationDB.AddAsync(newDBEntry);
            await ApplicationDB.SaveChangesAsync();
        }
        else
        {
            List<string> errorList = ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage).ToList();
            AssignTrackModel uploadModelWithErrors = new();
            uploadModelWithErrors.HasErrors = true;
            uploadModelWithErrors.ValidationErrors = errorList;
            uploadModelWithErrors.listOfUsers =ApplicationDB.Users.Select(x => x.Email).ToList();
            return View(uploadModelWithErrors);
        }

        TempData["Message"] = $"It worked... {selecteduser} has used {GetSpaceForUser(selecteduser):0.00} mb so far.";
        return View(assignTrackModel);
    }

    
    public IActionResult Contact()
    {
        if(!contactFormControl.IsVisible)
        {
            return Redirect("~/Home/ContactFormClosed");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Contact(string userEmailSubmission, string userMessageSubmission, bool Consent)
    {  
        int? MessageCount = ApplicationDB.MessageDBEntries?.Count();       
        
        if(!contactFormControl.IsVisible || MessageCount >= messageSettings.MaxMessages)
        {
            contactFormControl.SetToOff();
            return Redirect("~/Home/ContactFormClosed");
        }

        if(ModelState.IsValid && Consent == true)
        {
            //Begin db access here, remember to add current date to db entry

            MessageDBEntry messageDBEntry = new();
            messageDBEntry.Email = userEmailSubmission;
            messageDBEntry.Message = userMessageSubmission;
            messageDBEntry.TimeArrived = DateTime.Now;

            ApplicationDB.Add(messageDBEntry);
            await ApplicationDB.SaveChangesAsync();

            //send email alerting admin to new email
            string? callbackUrl = Url.ActionLink("About", "Home", null); 

            if(callbackUrl is not null)
            {
                await emailSender.SendEmailAsync("admin@electrictrojan.com", "Alert! New message.", $"You have a new message, read it now: <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{callbackUrl}</a>");
            }
            
            ModelState.Clear();
            TempData["Alert"] = true;

            return View();
        }
        else
        {

            return View();
        }

    }
    public IActionResult ContactFormClosed()
    {
        return View();
    }

}
