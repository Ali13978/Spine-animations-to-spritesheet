# Spine 2D Animation to Sprite Sheet Converter for Unity

Easily convert your **Spine** skeletal animations into **sprite sheets** for use in Unity or any 2D game pipeline.  
Perfect for developers who want the smoothness of Spine animations but need them in a *frame-by-frame* format.

> **Note:** Make sure your camera is large enough so that **every frame** of the animation is fully inside it.

---

## ✨ Features
- **Direct Spine Export** → Capture animations from Spine directly into a sprite sheet.
- **Unity-Ready Output** → Import your generated sprite sheets straight into Unity’s animation workflow.
- **Custom Frame Settings** → Define frame count, resolution, and export format.
- **Fast & Lightweight** → No unnecessary overhead—just import, configure, export.

---

## 📂 How It Works
1. **Prepare your animation** in Spine (make sure it’s final and ready to export).
2. **Load** the Spine file into the tool.
3. **Set export options**:
   - Output resolution
   - Frame count per animation
   - Padding between frames
4. **Click Export** → The tool generates a sprite sheet (`.png`) and an optional `.json`/`.atlas` for reference.
5. **Import into Unity** and slice your sprite sheet to create frame-by-frame animations.

---

## 🛠 Requirements
- **Spine 2D** (Professional or Essential version)
- **Unity 2019+** (tested, but should work on newer versions)

---

## 📦 Installation
1. **Clone or download** this repository.
2. Open the project in Unity or place the tool scripts in your existing Unity project’s `Assets/Editor` folder.
3. Follow the usage steps below.

---

## 🚀 Usage in Unity
1. Open the **Spine to Sprite Sheet** window from Unity’s top menu:  
