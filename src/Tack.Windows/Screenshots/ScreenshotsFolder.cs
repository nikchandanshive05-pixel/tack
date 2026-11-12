using System;
using System.IO;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Tack.Windows.Screenshots;

/// <summary>
/// The Screenshots folder, where Win+Shift+S and Print Screen save. Tack has
/// no capture tool of its own: whatever lands here ends up on the board.
/// </summary>
public static unsafe class ScreenshotsFolder
{
    /// <summary>
    /// The Screenshots known folder, created if it does not exist yet. Falls
    /// back to Pictures\Screenshots in the user's profile if Windows will not say.
    /// </summary>
    public static string Find()
    {
        var folder = FromRegistry()
            ?? Known()
            ?? FromMyPictures()
            ?? Fallback(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Watching it will say so.
        }
        return folder;
    }

    static string? FromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
            if (key == null) return null;
            var val = key.GetValue("{B7BEDE81-DF94-4783-8285-86586CB0A175}")
                ?? key.GetValue("{B7BEDE81-DF94-4682-A7D8-57A52620B86F}");
            if (val is string raw && !string.IsNullOrWhiteSpace(raw))
            {
                var expanded = Environment.ExpandEnvironmentVariables(raw);
                if (Directory.Exists(expanded)) return expanded;
            }
        }
        catch {}
        return null;
    }

    static string? FromMyPictures()
    {
        var myPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrEmpty(myPictures)) return null;
        var cand = Path.Combine(myPictures, "Screenshots");
        return Directory.Exists(cand) ? cand : null;
    }

    static string Fallback(string profile) => Path.Combine(profile, "Pictures", "Screenshots");

    static string? Known()
    {
        var id = PInvoke.FOLDERID_Screenshots;
        PWSTR path;
        var result = PInvoke.SHGetKnownFolderPath(&id, KNOWN_FOLDER_FLAG.KF_FLAG_CREATE, HANDLE.Null, &path);
        try
        {
            var text = result.Succeeded ? path.ToString() : null;
            return string.IsNullOrEmpty(text) ? null : text;
        }
        finally
        {
            // Windows may hand back memory even when it fails.
            PInvoke.CoTaskMemFree(path);
        }
    }
}
