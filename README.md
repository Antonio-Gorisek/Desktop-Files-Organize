# 🗂 Desktop File Organizer (Unity)

This tool is made in Unity and automatically organizes all files on the Desktop by type (videos, images, music, others) by creating folders and moving the files into them. Optionally, it can detect duplicate files and place them into a separate folder. It also includes an Undo feature to revert the sorting.

[Watch the video](https://youtu.be/2DsQs7TeUCo)

GIF:

![qhmUaef](https://github.com/user-attachments/assets/c3311b8f-efd5-4899-8ac9-649fa41b15b3)

## How Sorting Works (Visual Overview)

**Before Sorting (Desktop)**

```
Desktop
│
├── video1.mp4
├── video2.mov
├── photo1.png
├── image2.jpg
├── song1.mp3
├── document.pdf
└── random-big-file.zip
```

**After Sorting**

```
Desktop
│
├── Videos
│   ├── MP4
│   │   └── video1.mp4
│   └── MOV
│       └── video2.mov
│
├── Images
│   ├── PNG
│   │   └── photo1.png
│   └── JPG
│       └── image2.jpg
│
├── Music
│   └── MP3
│       └── song1.mp3
│
└── Other
    ├── Small
    │   └── PDF
    │       └── document.pdf
    └── Large
        └── ZIP
            └── random-big-file.zip
```

**If duplicate detection is enabled:**

```
Duplicates
│
└── (files that have the same SHA256 hash)
```
