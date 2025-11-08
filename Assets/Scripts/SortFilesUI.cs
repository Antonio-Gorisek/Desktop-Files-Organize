using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SortFilesUI : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField inputVideos;
    [SerializeField] private TMP_InputField inputImages;
    [SerializeField] private TMP_InputField inputMusic;
    [SerializeField] private TMP_InputField inputOther;
    [SerializeField] private TMP_InputField inputDuplicate;
    [Header("Toggle")]
    [SerializeField] private Toggle toggleDuplicates;
    [Header("Loading")]
    [SerializeField] private Slider sliderLoading;
    [Header("Buttons")]
    [SerializeField] private Button btnOrganize;
    [SerializeField] private Button btnUndo;

    [Header("Scripts")]
#if UNITY_STANDALONE_WIN
    private SortWindowsFiles sortWindowsFiles;
#elif UNITY_STANDALONE_LINUX
    private SortLinuxFiles sortLinuxFiles;
#endif

    private void Awake() {
#if UNITY_STANDALONE_WIN
        sortWindowsFiles = GetComponent<SortWindowsFiles>();
#elif UNITY_STANDALONE_LINUX
        sortLinuxFiles = GetComponent<SortLinuxFiles>();
#endif

        LoadSettings();

        toggleDuplicates.onValueChanged.AddListener(OnDuplicateToggle);

        btnUndo.onClick.AddListener(() => {
#if UNITY_STANDALONE_WIN
            sortWindowsFiles.UndoSort(sliderLoading);
#elif UNITY_STANDALONE_LINUX
            sortLinuxFiles.UndoSort(sliderLoading);
#endif
        });

        btnOrganize.onClick.AddListener(() => {
            SaveSettings();

#if UNITY_STANDALONE_WIN
            sortWindowsFiles.SetVideoFolderName(inputVideos.text);
            sortWindowsFiles.SetImagesFolderName(inputImages.text);
            sortWindowsFiles.SetMusicFolderName(inputMusic.text);
            sortWindowsFiles.SetOtherFolderName(inputOther.text);
            sortWindowsFiles.SetDuplicatesFolderName(inputDuplicate.text);
            sortWindowsFiles.SetDuplicatesFolderEnabled(toggleDuplicates.isOn);
            sortWindowsFiles.SortFilesByType(sliderLoading);
#elif UNITY_STANDALONE_LINUX
            sortLinuxFiles.SetVideoFolderName(inputVideos.text);
            sortLinuxFiles.SetImagesFolderName(inputImages.text);
            sortLinuxFiles.SetMusicFolderName(inputMusic.text);
            sortLinuxFiles.SetOtherFolderName(inputOther.text);
            sortLinuxFiles.SetDuplicatesFolderName(inputDuplicate.text);
            sortLinuxFiles.SetDuplicatesFolderEnabled(toggleDuplicates.isOn);
            sortLinuxFiles.SortFilesByType(sliderLoading);
#endif
        });
    }

    private void FixedUpdate() => SetUIInteractable(!sliderLoading.isActiveAndEnabled);

    private void OnDuplicateToggle(bool isOn) {
        inputDuplicate.interactable = isOn;
    }

    private void SaveSettings() {
        PlayerPrefs.SetString("FolderVideos", inputVideos.text);
        PlayerPrefs.SetString("FolderImages", inputImages.text);
        PlayerPrefs.SetString("FolderMusic", inputMusic.text);
        PlayerPrefs.SetString("FolderOther", inputOther.text);
        PlayerPrefs.SetString("FolderDuplicates", inputDuplicate.text);
        PlayerPrefs.SetInt("DuplicatesEnabled", toggleDuplicates.isOn ? 1 : 0);

        PlayerPrefs.Save();
    }

    private void LoadSettings() {
        if (PlayerPrefs.HasKey("FolderVideos")) inputVideos.text = PlayerPrefs.GetString("FolderVideos");
        if (PlayerPrefs.HasKey("FolderImages")) inputImages.text = PlayerPrefs.GetString("FolderImages");
        if (PlayerPrefs.HasKey("FolderMusic")) inputMusic.text = PlayerPrefs.GetString("FolderMusic");
        if (PlayerPrefs.HasKey("FolderOther")) inputOther.text = PlayerPrefs.GetString("FolderOther");
        if (PlayerPrefs.HasKey("FolderDuplicates")) inputDuplicate.text = PlayerPrefs.GetString("FolderDuplicates");
        if (PlayerPrefs.HasKey("DuplicatesEnabled")) toggleDuplicates.isOn = PlayerPrefs.GetInt("DuplicatesEnabled") == 1;

        inputDuplicate.interactable = toggleDuplicates.isOn;
    }

    private void SetUIInteractable(bool value) {
        inputVideos.interactable = value;
        inputImages.interactable = value;
        inputMusic.interactable = value;
        inputOther.interactable = value;
        inputDuplicate.interactable = value && toggleDuplicates.isOn;
        toggleDuplicates.interactable = value;
        btnOrganize.interactable = value;
        btnUndo.interactable = value;
    }
}
