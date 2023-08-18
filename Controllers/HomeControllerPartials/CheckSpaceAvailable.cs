using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UpLoader_For_ET.Models;
using Microsoft.AspNetCore.Authorization;
using UpLoader_For_ET.Data;
using UpLoader_For_ET.DBModels;
using static System.IO.Path;

using UpLoader_For_ET.StaticClasses;


namespace UpLoader_For_ET.Controllers;

public partial class HomeController : Controller
{
    /// <summary>
    /// Analyses the user file folder and returns false if it exceeeds alloted max space.
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    public bool CheckSpaceForUser(string? userName, long uploadedFileLength)
    {
        var userIDHash = GetUserIDHash(userName);
        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}");

        if (!Directory.Exists(FileBayPathForUser))
        {
            Directory.CreateDirectory(FileBayPathForUser);
        }

        //Calculate the amount of space used by user
        DirectoryInfo userDirectoryInfo = new DirectoryInfo(FileBayPathForUser);
        FileInfo[] userFileInfo = userDirectoryInfo.GetFiles();

        decimal totalBytes = uploadedFileLength;

        foreach (var file in userFileInfo)
        {
            totalBytes += file.Length;
        }

        decimal totalMegabytes = totalBytes / (1000 * 1000);

        return totalMegabytes <= userSpaceLimit.userLimitMB ? true : false; 
    }

    /// <summary>
    /// Returns a decimal specifying how many megabytes of space this user has used, will always return at least 0.
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    public decimal GetSpaceForUser(string? userName)
    {
        var userIDHash = GetUserIDHash(userName);
        string FileBayPathForUser = Combine(MainDirectory, $"FileBay/{userIDHash}");

        if (!Directory.Exists(FileBayPathForUser))
        {
            Directory.CreateDirectory(FileBayPathForUser);
        }

        //Calculate the amount of space used by user
        DirectoryInfo userDirectoryInfo = new DirectoryInfo(FileBayPathForUser);
        FileInfo[] userFileInfo = userDirectoryInfo.GetFiles();

        decimal totalBytes = 0;

        foreach (var file in userFileInfo)
        {
            totalBytes += file.Length;
        }

        decimal totalMegabytes = totalBytes / (1000 * 1000);

        return totalMegabytes;
    }

    /// <summary>
    /// Returns the amount of space used by userName as a percentage of available space
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    public decimal? PercentageSpaceUsed(string? userName)
    {
        decimal spaceUsedMB = GetSpaceForUser(userName);
        int? spacePermitted = userSpaceLimit.userLimitMB;

        return spacePermitted != null ? spaceUsedMB * 100 / spacePermitted : 0;
    }
}