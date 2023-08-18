using Microsoft.AspNetCore.Mvc;

namespace UpLoader_For_ET.Controllers;

public partial class RolesController : Controller
{
    /// <summary>
    /// Retrieves the userIDHash from the identity database, a partial class and method of Home controller FYI.
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    public string GetUserIDHash(string? userName)
    {
        string userIDHash = ApplicationDB.Users.Where(c => c.UserName == userName).Select(c => c.Id).First();

        return userIDHash;
    }
}