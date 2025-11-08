using System.Runtime.InteropServices; // Enables calling Windows API functions (native DLL calls)
using System.Security.Cryptography; // For file hashing (am using for duplicated files checking)
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using System.Collections;
using UnityEditor;

public class SortWindowsFiles : MonoBehaviour {

    // The structure that Windows uses for the SHFileOperation API call, more here: https://pinvoke.net/default.aspx/wininet/InternetPerConnOptionList.html
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT {
        public IntPtr hwnd;
        public uint wFunc; // The operation we want (move, copy, delete...)
        public string pFrom; // Source file path
        public string pTo; // Destination path
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
        // The rest of the variables must be present here even though we don't use them for the code to work.
    }

    // Windows API functions https://pinvoke.net/default.aspx/shell32/api.html check documentation there you will find everything
    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    // Constant id (for "MOVE" operation) every operation (delete, copy, paste, etc) have own ID
    const uint FO_MOVE = 0x0001;
    const uint FO_DELETE = 0x0003;

    // Constant iD (notify the system that associations and content have changed)
    const uint SHCNE_ASSOCCHANGED = 0x08000000;

    // Constant Id (standard for refresh)
    const uint SHCNF_IDLIST = 0x0000;

    // Flags to control the behavior of SHFileOperation
    const ushort FOF_SILENT = 0x0004; // Suppress progress UI
    const ushort FOF_NOCONFIRMATION = 0x0010; // Do not ask the user for confirmation
    const ushort FOF_NOCONFIRMMKDIR = 0x0200; // Do not confirm directory creation

    // Extensions that we don't touch because they can be critical for moving (especially desktop ini)
    private static readonly string[] ProtectedExtensions = new string[] {
        "ini", "lnk", "sys", "dll", "url", "msi",
        "bat", "cmd", "reg", "scr", "drv", "tmp", "config"
    };

    // Folder name variables (can be easily changed)
    private string imagesFolderName = "Images";
    private string musicFolderName = "Music";
    private string videoFolderName = "Videos";
    private string otherFolderName = "Other";
    private string duplicatesFolderName = "Duplicates";
    private bool duplicatesFolderEnable = true;

    // Undo storage
    private List<(string source, string destination)> movedFiles = new List<(string, string)>();
    private HashSet<string> createdFolders = new HashSet<string>();


    // Main coroutine to sort files
    public void SortFilesByType(Slider progressSlider) => StartCoroutine(SortFilesCoroutine(progressSlider));

    private IEnumerator SortFilesCoroutine(Slider progressSlider) {
        progressSlider.gameObject.SetActive(true);
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        Dictionary<string, string> typeFolders = GetTypeFolders(); // Maps extensions to main folder
        Dictionary<string, string> hashes = new Dictionary<string, string>(); // hash to original file path
        movedFiles.Clear();
        createdFolders.Clear();

        string[] files = Directory.GetFiles(desktopPath);
        progressSlider.maxValue = files.Length;
        progressSlider.value = 0;

        // Create duplicates folder if it doesn't exist
        string duplicatesFolder = Path.Combine(desktopPath, duplicatesFolderName);
        if (!Directory.Exists(duplicatesFolder) && duplicatesFolderEnable) {
            Directory.CreateDirectory(duplicatesFolder);
            createdFolders.Add(duplicatesFolder);
        }
        for (int i = 0; i < files.Length; i++) {
            string file = files[i];
            string ext = Path.GetExtension(file).TrimStart('.').ToLower(); // Takes the file extension

            if (string.IsNullOrEmpty(ext) || ProtectedExtensions.Contains(ext)) // Skips files with no extension or those on the ProtectedExtensions list
                continue;

            string fileHash = ComputeFileHash(file);

            string targetFile = "";

            // Check for duplicates by hash only if duplicates folder is enabled
            if (duplicatesFolderEnable && hashes.ContainsKey(fileHash)) {
                targetFile = Path.Combine(duplicatesFolder, Path.GetFileName(file));
            } else {
                hashes[fileHash] = file;
                string mainFolder = GetMainFolder(ext, desktopPath, typeFolders);
                string subFolder = GetSubFolder(file, ext, mainFolder);
                targetFile = Path.Combine(subFolder, Path.GetFileName(file));
            }

            if (file != targetFile) {
                MoveFile(file, targetFile);
                movedFiles.Add((source: file, destination: targetFile));
                createdFolders.Add(duplicatesFolder);
                RefreshDesktop();
            }

            progressSlider.value += 1;
            yield return null;
        }
        progressSlider.gameObject.SetActive(false);

    }

    public void UndoSort(Slider progressSlider) => StartCoroutine(UndoCoroutine(progressSlider));

    private IEnumerator UndoCoroutine(Slider progressSlider) {
        progressSlider.gameObject.SetActive(true);
        progressSlider.maxValue = movedFiles.Count;
        progressSlider.value = 0;

        for (int i = movedFiles.Count - 1; i >= 0; i--) {
            var move = movedFiles[i];
            MoveFile(move.destination, move.source);
            RefreshDesktop();
            progressSlider.value += 1;
            yield return null;
        }

        foreach (var folder in createdFolders.OrderByDescending(f => f.Length)) {
            DeleteFolderWindowsAPI(folder);
        }
        movedFiles.Clear();
        progressSlider.gameObject.SetActive(false);
    }

    private void DeleteFolderWindowsAPI(string folderPath) {
        if (!Directory.Exists(folderPath)) return;

        // Delete empty subfolders
        foreach (var subFolder in Directory.GetDirectories(folderPath)) {
            DeleteFolderWindowsAPI(subFolder);
        }

        // After cleaning subfolders, delete this folder if it is empty
        if (!Directory.EnumerateFileSystemEntries(folderPath).Any()) {
            SHFILEOPSTRUCT op = new SHFILEOPSTRUCT {
                wFunc = FO_DELETE, // We want to delete folder so value for that must be 0x0003 in FO_DELETE variable
                pFrom = folderPath + '\0' + '\0',
                fFlags = FOF_SILENT | FOF_NOCONFIRMATION | FOF_NOCONFIRMMKDIR
            };
            SHFileOperation(ref op);
            RefreshDesktop();
        }
    }

    // Compute SHA256 hash of file
    private string ComputeFileHash(string filePath) {
        using (var sha = SHA256.Create()) {
            using (var stream = File.OpenRead(filePath)) {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    // Getting main folder name by file type
    private string GetMainFolder(string ext, string desktopPath, Dictionary<string, string> typeFolders) {
        string folderName = typeFolders.ContainsKey(ext) ? typeFolders[ext] : otherFolderName;
        string mainFolder = Path.Combine(desktopPath, folderName);
        if (!Directory.Exists(mainFolder)) {
            Directory.CreateDirectory(mainFolder);
            createdFolders.Add(mainFolder);
        }
        return mainFolder;
    }

    // Getting subfolder (Images/Music/Videos by extension, 'Other' folder is sorting by size)
    private string GetSubFolder(string file, string ext, string mainFolder) {
        string subFolder;

        if (mainFolder.EndsWith(imagesFolderName) || mainFolder.EndsWith(musicFolderName) || mainFolder.EndsWith(videoFolderName)) {
            subFolder = Path.Combine(mainFolder, ext.ToUpper());
        } else {
            long size = new FileInfo(file).Length;
            string sizeFolder = size < 1024 * 1024 ? "Small" : "Large";
            subFolder = Path.Combine(mainFolder, sizeFolder, ext.ToLower());
        }


        if (!Directory.Exists(subFolder)) {
            Directory.CreateDirectory(subFolder);
            createdFolders.Add(subFolder);
        }
        return subFolder;
    }

    // Moves file using Windows Explorer API
    private void MoveFile(string source, string destination) {
        SHFILEOPSTRUCT op = new SHFILEOPSTRUCT {
            wFunc = FO_MOVE, // We want to move file so value for that must be 0x0001 in FO_MOVE variable
            pFrom = source + '\0' + '\0',
            pTo = destination + '\0' + '\0'
        };
        op.fFlags = FOF_SILENT | FOF_NOCONFIRMATION | FOF_NOCONFIRMMKDIR;
        SHFileOperation(ref op);
    }

    private void RefreshDesktop() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

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
