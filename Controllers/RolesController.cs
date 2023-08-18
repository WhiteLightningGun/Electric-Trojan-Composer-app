using Microsoft.AspNetCore.Identity; //Role manager, user manager
using Microsoft.AspNetCore.Mvc; // Controller, IAction Result
using Microsoft.AspNetCore.Authorization; //Authorisations
using UpLoader_For_ET.Models;
using UpLoader_For_ET.Data;
using static System.Console;
using static System.IO.Path;
using UpLoader_For_ET.DBModels;
using UpLoader_For_ET.Services;

namespace UpLoader_For_ET.Controllers;

public partial class RolesController : Controller
{
    private string AdminRole = "Administrators";
    private string UserRole = "User";
    private string PlebianRole = "Plebian";
    private readonly RoleManager<IdentityRole> roleManager;
    private readonly UserManager<IdentityUser> userManager;
    private readonly ApplicationDbContext ApplicationDB;
    private static string MainDirectory = Environment.CurrentDirectory;
    private ContactFormVisibility contactFormControl;

    //CONSTRUCTOR
    public RolesController(RoleManager<IdentityRole> roleManager,
    UserManager<IdentityUser> userManager, ApplicationDbContext injectedADB,
    ContactFormVisibility contactFormVisibility)
    {
        this.roleManager = roleManager;
        this.userManager = userManager;
        ApplicationDB = injectedADB;
        contactFormControl = contactFormVisibility;
    }

    public async Task<IActionResult> Index()
    {
        if (!(await roleManager.RoleExistsAsync(AdminRole)))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
        }

        if (!(await roleManager.RoleExistsAsync(UserRole)))
        {
            await roleManager.CreateAsync(new IdentityRole(UserRole));
        }

        if (!(await roleManager.RoleExistsAsync(PlebianRole)))
        {
            await roleManager.CreateAsync(new IdentityRole(PlebianRole));
        }

        return Redirect("/"); // Back to homepage
    }

    [Authorize(Roles="Administrators")]
    public IActionResult FrontPageReview()
    {
        FrontPageReviewModel frontPageEditModel = new();
        List<FrontPageEntry>? frontPageEntries = ApplicationDB.FrontPageEntries?.Select(x => x).ToList();
        frontPageEditModel.FrontPageEntries = frontPageEntries;

        return View(frontPageEditModel);
    }

    [Authorize(Roles = "Administrators")]
    public IActionResult FrontPageAdd(int id)
    {
        if(id <= 0) //The case where no useful argument is given in the url, this can include strings which ASP.Net will resolve to 0 if they do not parse to an int
        {
            return View();
        }

        FrontPageAddPost frontpageAdd = new();
        frontpageAdd.selectedFrontPageVideo = ApplicationDB.FrontPageEntries?.Select(x => x).Where(x => x.id == id).FirstOrDefault();
        
        if(frontpageAdd.selectedFrontPageVideo == null)
        {
            TempData["Message"] = "Video not found";
            return View();
        }

        TempData["Message"] = $"FrontPageAdd(int id = {id}) was used";
    
        return View(frontpageAdd);
    }


    [Authorize(Roles = "Administrators")]
    [HttpPost]
    public async Task<IActionResult> FrontPageAdd(string Title, string userDescription, string htmlEmbedURL, string? id)
    {
        if(ModelState.IsValid && id == null) // the case where we are adding a new video
        {
            Console.WriteLine($"Model state VALID with arguments Title: {Title}, userDescription: {userDescription} htmlEmbedUrl: {htmlEmbedURL} and id: {id}");
            
            //Create new entry in db

            FrontPageEntry latestFrontPageEntry = new();
            latestFrontPageEntry.description = userDescription;
            latestFrontPageEntry.title = Title;
            latestFrontPageEntry.htmlEmbedLink = htmlEmbedURL;

            ApplicationDB.FrontPageEntries?.Add(latestFrontPageEntry);
            await ApplicationDB.SaveChangesAsync();

            ModelState.Clear();
            TempData["Message"] = "Post Successful";
            return View();
        }

        if(ModelState.IsValid && id != null) // the edit existing entry case
        {
            int ID = Convert.ToInt32(id);

            FrontPageEntry? result = ApplicationDB.FrontPageEntries?.FirstOrDefault(x => x.id == ID);
            if(result != null)
            {
                result.description = userDescription;
                result.title = Title;
                result.htmlEmbedLink = htmlEmbedURL;
                await ApplicationDB.SaveChangesAsync();
                
                ModelState.Clear();
                TempData["Message"] = "Edit Successful";
                return FrontPageAdd(0); // The case where frontpageadd takes zero sends you to the add entry page
            }
            else
            {
                ModelState.Clear();
                TempData["Message"] = "id arg was not found, something has gone wrong.";
                return View();
            }
        }
        TempData["Message"] = "Front Page POST Used";
        return View();
    }

    [Authorize(Roles="Administrators")]
    public IActionResult Manager()
    {
        List<string?>? emailList = ApplicationDB.Users.Select(x => x.Email).ToList();
        List<IdentityRole> availableRoles = roleManager.Roles.ToList();
        ManagerPageModel managerPage = new();
        List<ManagerPageEntry> managerpageEntries = new();

        foreach(string? email in emailList)
        {
            ManagerPageEntry newManagerEntry = new();

            newManagerEntry.userEmail = email;
            // Get unique user ID associated with email
            string? uniqueID = ApplicationDB.Users.Where(c => c.UserName == email).Select(c => c.Id).First();

            // user role lookup, if it exists
            string? userRoleID = ApplicationDB.UserRoles.Where(x => x.UserId == uniqueID).Select(x => x.RoleId).FirstOrDefault();


            if(userRoleID != null)
            {
                string? roleName = ApplicationDB.Roles.Where(x => x.Id == userRoleID).Select(x => x.Name).FirstOrDefault();
                newManagerEntry.currentUserRole = roleName;
                //Console.WriteLine($"{email} has role {roleName}");
            }
            else
            {
                newManagerEntry.currentUserRole = "Not Set";
            }

            managerpageEntries.Add(newManagerEntry);
        }

        managerPage.managerEntries = managerpageEntries;
        
        List<string> rolesList = new();

        foreach(IdentityRole role in availableRoles)
        {
            if (role.Name != null)
            {
                rolesList.Add(role.Name);
            }
        }
        managerPage.interestingRoles = rolesList;
        managerPage.contactFormVisibility = contactFormControl.IsVisible;
        return View(managerPage);
    }

    [Authorize(Roles = "Administrators")]
    [HttpPost]
    public async Task<IActionResult> Manager(string userEmailSubmission, string selectedRole, string currentRole)
    {
        List<string?> allRolesList = roleManager.Roles.Select(x => x.Name).ToList();
        IdentityUser? userHere = await userManager.FindByEmailAsync(userEmailSubmission);
        
        if (userHere != null)
        {
            IdentityResult removedFromRole = await userManager.RemoveFromRoleAsync(userHere, currentRole);
            IdentityResult result = await userManager.AddToRoleAsync(userHere, selectedRole);
            await userManager.UpdateSecurityStampAsync(userHere);

            if (result.Succeeded)
            {
                WriteLine($"User {userHere.UserName} added to {selectedRole} successfully ");
            }
        }
        else
        {
            WriteLine($"Failed to change role associated with {userEmailSubmission}");
        }
        return Manager();
    }

    [Authorize(Roles = "Administrators")]
    [HttpPost]
    public async Task<IActionResult> DeleteUser(string userEmailSubmission, string selectedRole, string currentRole)
    {
        Console.WriteLine($"DeleteUser method was called with argument {userEmailSubmission} selectedRole {selectedRole} currentRole {currentRole}.");

        var user = await userManager.FindByEmailAsync(userEmailSubmission);

        if(user == null)
        {
            TempData["Alert"] = "User not found!!!";
            return View();
        }
        else
        {
            // deleteall files in filebay associated with this user
            var userIDHash = await userManager.GetUserIdAsync(user);
            string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}");

            //what if directory doesn't exist?
            if (Directory.Exists(FileBayPathForUser))
            {
                string[] files = Directory.GetFiles(FileBayPathForUser, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    System.IO.File.Delete(file);
                }

                Directory.Delete(FileBayPathForUser); //Deletes empty directory

                // deleting any entries in Uploads DB
                var uploadEntries = ApplicationDB.UploadDBEntries?.Select(x => x).Where(x => x.userHash == userIDHash).ToList();

                if (uploadEntries != null)
                {
                    foreach (var entry in uploadEntries)
                    {
                        ApplicationDB.Remove(entry);
                    }
                    await ApplicationDB.SaveChangesAsync();
                }
            }

            var result = await userManager.DeleteAsync(user);
            var userId = await userManager.GetUserIdAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unexpected error occurred deleting user.");
            }

            TempData["Message"] = $"Success, {userEmailSubmission} was deleted";
            return View();
        }
    }

    [Authorize(Roles = "Administrators")]
    [HttpPost]
    public IActionResult ManageUserFiles(string userEmailSubmission, string selectedRole, string currentRole)
    {
        var userIDHash = GetUserIDHash(userEmailSubmission);
        ManageUserFiles manageUserFiles = new();
        manageUserFiles.uploadDBEntries = ApplicationDB.UploadDBEntries?.Select(x => x).Where(x => x.userHash == userIDHash).ToList();
        manageUserFiles.selectedUserName = userEmailSubmission;
        return View(manageUserFiles);
    }

    [Authorize(Roles = "Administrators")]
    public IActionResult ManageUserFiles()
    {
        return Redirect("~/Roles/Manager");
    }

    [Authorize(Roles="User,Administrators")]
    [HttpPost]
    public async Task<IActionResult> DeleteAlert(string? argFileHashD, string argFileNameD, string argUserName)
    {
        Console.WriteLine("DeleteAlert was called via the ROLES CONTROLLER");
        string userIDHash = GetUserIDHash(argUserName);

        UploadDBEntry? toDelete = ApplicationDB.UploadDBEntries?.Select(x => x).Where(x => x.fileHash == argFileHashD).FirstOrDefault();

        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}/{argFileHashD}");

        if(toDelete != null)
        {
            ApplicationDB.UploadDBEntries?.Remove(toDelete);
            await ApplicationDB.SaveChangesAsync();
        }

        if (System.IO.File.Exists(FileBayPathForUser)) 
        { 
            System.IO.File.Delete(FileBayPathForUser);
        }

        DeleteAlertModel deletealertmodel = new (argFileNameD);

        return View(deletealertmodel);
    }

    [Authorize(Roles="Administrators")]
    public IActionResult ContactFormToggle()
    {
        contactFormControl.Toggle();

        ContactFormToggleView toggleView = new(contactFormControl.IsVisible);
        return View(toggleView);
    }

    [Authorize(Roles="Administrators")]
    public IActionResult MessageReviewPage()
    {
        MessageReviewModel messagesReview = new();

        //Messages ordered by most recent
        messagesReview.Messages = ApplicationDB.MessageDBEntries?.Select(x => x).OrderByDescending(x => x.TimeArrived).ToList();
        return View(messagesReview);
    }

    [HttpPost]
    [Authorize(Roles="Administrators")]
    public async Task<IActionResult> MessageReviewPage(int messageID)
    {

        MessageDBEntry? toDelete = ApplicationDB.MessageDBEntries?.Select(x => x).Where(x => x.id == messageID).FirstOrDefault();
        if(toDelete is not null)
        {
            ApplicationDB.Remove(toDelete);
            await ApplicationDB.SaveChangesAsync();
            TempData["Message"] = "Message Deleted, remember to re-check form visibility if required.";
        }
        else
        {
            TempData["Message"] = "Something went wrong.";
        }

        MessageReviewModel messagesReview = new();
        //Messages ordered by most recent
        messagesReview.Messages = ApplicationDB.MessageDBEntries?.Select(x => x).OrderByDescending(x => x.TimeArrived).ToList();
        return View(messagesReview);
    }
}