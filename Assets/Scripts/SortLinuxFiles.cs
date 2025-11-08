using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class SortLinuxFiles : MonoBehaviour {

    // Extensions we should not touch (similar to Windows ProtectedExtensions)
    private static readonly string[] ProtectedExtensions = new string[]
    {
        "desktop", "sh", "appimage", "conf", "cfg", "ini",
        "service", "socket", "timer", "ko", "so", "bin",
        "elf", "run", "deb", "rpm", "tmp"
    };

    // Folder name variables (can be customized)
    private string imagesFolderName = "Images";
    private string musicFolderName = "Music";
    private string videoFolderName = "Videos";
    private string otherFolderName = "Other";
    private string duplicatesFolderName = "Duplicates";
    private bool duplicatesFolderEnable = true;

    // Storage for undo functionality
    private List<(string source, string destination)> movedFiles = new List<(string, string)>();
    private HashSet<string> createdFolders = new HashSet<string>();

    // Coroutine entry point to start sorting
    public void SortFilesByType(Slider progressSlider) => StartCoroutine(SortFilesCoroutine(progressSlider));

    private IEnumerator SortFilesCoroutine(Slider progressSlider) {
        progressSlider.gameObject.SetActive(true);

        string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Desktop");
        Dictionary<string, string> typeFolders = GetTypeFolders(); // Map extensions to main folders
        Dictionary<string, string> hashes = new Dictionary<string, string>(); // Store hashes for duplicate detection

        movedFiles.Clear();
        createdFolders.Clear();

        string[] files = Directory.GetFiles(desktop);
        progressSlider.maxValue = files.Length;
        progressSlider.value = 0;

        // Create duplicates folder if enabled
        string duplicatesFolder = Path.Combine(desktop, duplicatesFolderName);
        if (!Directory.Exists(duplicatesFolder) && duplicatesFolderEnable) {
            Directory.CreateDirectory(duplicatesFolder);
            createdFolders.Add(duplicatesFolder);
        }

        for (int i = 0; i < files.Length; i++) {
            string file = files[i];
            string ext = Path.GetExtension(file).TrimStart('.').ToLower();

            if (string.IsNullOrEmpty(ext) || ProtectedExtensions.Contains(ext))
                continue; // Skip files with no extension or protected extensions

            string fileHash = ComputeFileHash(file);
            string targetFile = "";

            // Check for duplicates by hash
            if (duplicatesFolderEnable && hashes.ContainsKey(fileHash)) {
                targetFile = Path.Combine(duplicatesFolder, Path.GetFileName(file));
            } else {
                hashes[fileHash] = file;
                string mainFolder = GetMainFolder(ext, desktop, typeFolders); // Main folder like Images, Music, Videos
                string subFolder = GetSubFolder(file, ext, mainFolder);        // Subfolder by extension or size
                targetFile = Path.Combine(subFolder, Path.GetFileName(file));
            }

            if (file != targetFile) {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)); // Ensure target folder exists
                File.Move(file, targetFile); // Move file
                movedFiles.Add((file, targetFile)); // Store for undo
                createdFolders.Add(Path.GetDirectoryName(targetFile)); // Track created folder
                RefreshLinuxDesktop(); // Refresh desktop to reflect changes
            }

            progressSlider.value += 1;
            yield return null;
        }

        progressSlider.gameObject.SetActive(false);
    }

    // Undo functionality (revert moved files)
    public void UndoSort(Slider progressSlider) => StartCoroutine(UndoCoroutine(progressSlider));

    private IEnumerator UndoCoroutine(Slider progressSlider) {
        progressSlider.gameObject.SetActive(true);
        progressSlider.maxValue = movedFiles.Count;
        progressSlider.value = 0;

        for (int i = movedFiles.Count - 1; i >= 0; i--) {
            var move = movedFiles[i];
            if (File.Exists(move.destination)) {
                Directory.CreateDirectory(Path.GetDirectoryName(move.source));
                File.Move(move.destination, move.source); // Move file back to original location
            }
            RefreshLinuxDesktop(); // Refresh desktop after each move
            progressSlider.value += 1;
            yield return null;
        }

        // Delete empty folders created during sorting
        foreach (var folder in createdFolders.OrderByDescending(f => f.Length)) {
            DeleteFolderLinux(folder);
        }

        movedFiles.Clear();
        progressSlider.gameObject.SetActive(false);
    }

    // Delete empty folders recursively (similar to Windows DeleteFolderWindowsAPI)
    private void DeleteFolderLinux(string folder) {
        if (!Directory.Exists(folder)) return;

        foreach (var sub in Directory.GetDirectories(folder)) {
            DeleteFolderLinux(sub);
        }

        if (!Directory.EnumerateFileSystemEntries(folder).Any()) {
            Directory.Delete(folder);
            RefreshLinuxDesktop(); // Refresh desktop after deletion
        }
    }

    // Compute SHA256 hash of file (used for duplicated files detection)
    private string ComputeFileHash(string filePath) {
        using (var sha = SHA256.Create()) {
            using (var stream = File.OpenRead(filePath)) {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    // Determine main folder based on extension (Images, Music, Videos, etc)
    private string GetMainFolder(string ext, string desktop, Dictionary<string, string> typeFolders) {
        string folderName = typeFolders.ContainsKey(ext) ? typeFolders[ext] : otherFolderName;
        string mainFolder = Path.Combine(desktop, folderName);
        if (!Directory.Exists(mainFolder)) {
            Directory.CreateDirectory(mainFolder);
            createdFolders.Add(mainFolder);
        }
        return mainFolder;
    }

    // Determine subfolder for file (by extension or size for 'Other' folder)
    private string GetSubFolder(string file, string ext, string mainFolder) {
        string subFolder;
        if (mainFolder.EndsWith(imagesFolderName) || mainFolder.EndsWith(musicFolderName) || mainFolder.EndsWith(videoFolderName)) {
            subFolder = Path.Combine(mainFolder, ext.ToUpper()); // Subfolder per extension
        } else {
            long size = new FileInfo(file).Length;
            string sizeFolder = size < 1024 * 1024 ? "Small" : "Large";
            subFolder = Path.Combine(mainFolder, sizeFolder, ext.ToLower()); // Subfolder by size
        }

        if (!Directory.Exists(subFolder)) {
            Directory.CreateDirectory(subFolder);
            createdFolders.Add(subFolder);
        }

        return subFolder;
    }

    // Refresh desktop environment (Linux equivalent to Windows SHChangeNotify)
    private void RefreshLinuxDesktop() {
        string desktopEnv = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").ToLower();

        // For GNOME, Unity, MATE
        if (desktopEnv.Contains("gnome") || desktopEnv.Contains("unity") || desktopEnv.Contains("mate"))
            RunCmd("bash", "-c \"xdg-open ~/Desktop\"");

        // For KDE
        else if (desktopEnv.Contains("kde"))
            RunCmd("bash", "-c \"kbuildsycoca5\"");

        // For XFCE
        else if (desktopEnv.Contains("xfce"))
            RunCmd("bash", "-c \"xfdesktop --reload\"");
    }

    private void RunCmd(string file, string args) => Process.Start(new ProcessStartInfo { FileName = file, Arguments = args, UseShellExecute = false });

    private Dictionary<string, string> GetTypeFolders() {
        return new Dictionary<string, string>() {
            { "png", imagesFolderName }, { "jpg", imagesFolderName }, { "jpeg", imagesFolderName },
            { "gif", imagesFolderName }, { "bmp", imagesFolderName }, { "tiff", imagesFolderName },
            { "mp4", videoFolderName }, { "mkv", videoFolderName }, { "avi", videoFolderName },
            { "mov", videoFolderName }, { "wmv", videoFolderName },
            { "mp3", musicFolderName }, { "wav", musicFolderName }, { "flac", musicFolderName }, { "aac", musicFolderName }
        };
    }

    public void SetImagesFolderName(string name) => imagesFolderName = name;
    public void SetMusicFolderName(string name) => musicFolderName = name;
    public void SetVideoFolderName(string name) => videoFolderName = name;
    public void SetOtherFolderName(string name) => otherFolderName = name;
    public void SetDuplicatesFolderName(string name) => duplicatesFolderName = name;
    public void SetDuplicatesFolderEnabled(bool value) => duplicatesFolderEnable = value;
}
