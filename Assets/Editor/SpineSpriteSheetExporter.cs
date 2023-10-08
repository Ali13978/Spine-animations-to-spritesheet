using UnityEngine;
using UnityEditor;
using Spine.Unity;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using static UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle;
using System;
using Unity.Mathematics;
using static Codice.CM.Common.Serialization.PacketFileReader;

public class SpineSpriteSheetExporter : EditorWindow
{
    private GameObject selectedGameObject;
    private float framesPerSecond = 20f;
    private string exportPath = "Assets/SpriteSheets";
    private string selectedAnimationName;
    private string[] animationNames;
    private Camera captureCamera;
    private Camera[] sceneCameras;
    private bool isRecording = false;
    private int frameCount = 0;
    private int currentFrameIndex = 0;

    private bool showProgressBar = false;
    private Vector2 maxRes = Vector2.zero;

    private bool isFirstTime = true;

    [MenuItem("Tools/Spine Sprite Sheet Exporter")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(SpineSpriteSheetExporter));
    }

    private void OnGUI()
    {
        GUILayout.Label("Spine Sprite Sheet Exporter", EditorStyles.boldLabel); 

        selectedGameObject = EditorGUILayout.ObjectField("Select GameObject with Spine Animation", selectedGameObject, typeof(GameObject), true) as GameObject;
        framesPerSecond = EditorGUILayout.FloatField("Frames Per Second", framesPerSecond);

        exportPath = EditorGUILayout.TextField("Export Folder", exportPath);

        // Retrieve available animation names from the selected Spine GameObject
        if (selectedGameObject != null)
        {
            var skeletonAnimation = selectedGameObject.GetComponent<SkeletonAnimation>();
            var skeletonData = skeletonAnimation.SkeletonDataAsset.GetSkeletonData(true);
            animationNames = skeletonData.Animations.Select(animation => animation.Name).ToArray();

            // Display a dropdown list for selecting the animation
            if (animationNames.Length > 0)
            {
                // Create a variable to store the selected index
                int selectedAnimationIndex = 0; // Initialize with 0 or any default value

                // Check if selectedAnimationName is in the animationNames array and get its index
                if (!string.IsNullOrEmpty(selectedAnimationName))
                {
                    selectedAnimationIndex = Array.IndexOf(animationNames, selectedAnimationName);
                    if (selectedAnimationIndex == -1)
                    {
                        // The selectedAnimationName is not in the array, reset to default index (0)
                        selectedAnimationIndex = 0;
                    }
                }

                // Display the popup with the selected index
                selectedAnimationIndex = EditorGUILayout.Popup("Select Animation", selectedAnimationIndex, animationNames);

                // Update the selectedAnimationName based on the selected index
                selectedAnimationName = animationNames[selectedAnimationIndex];
            }
            else
            {
                EditorGUILayout.HelpBox("No animations found for the selected Spine GameObject.", MessageType.Warning);
            }
        }
        else
        {
            animationNames = new string[0];
            selectedAnimationName = "";
        }

        // Capture Camera Selection
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Capture Camera (Optional)", EditorStyles.boldLabel);
        captureCamera = EditorGUILayout.ObjectField("Capture Camera", captureCamera, typeof(Camera), true) as Camera;

        // Get a list of all active cameras in the scene
        sceneCameras = Camera.allCameras;

        // Display a dropdown list for selecting a camera
        string[] cameraNames = sceneCameras.Select(cam => cam.name).ToArray();
        int selectedCameraIndex = EditorGUILayout.Popup("Select Capture Camera", GetSelectedCameraIndex(), cameraNames);
        if (selectedCameraIndex >= 0 && selectedCameraIndex < sceneCameras.Length)
        {
            captureCamera = sceneCameras[selectedCameraIndex];
        }

        if (!isRecording)
        {
            if (GUILayout.Button("Create SpriteSheets"))
            {
                isFirstTime = true;
                ExportFramesBtn();
            }
        }
        else
        {
            GUILayout.Label("Recording Progress: " + Mathf.RoundToInt(currentFrameIndex / (float)frameCount * 100) + "%");
        }

        // Display progress bar if needed
        if (showProgressBar)
        {
            float progress = (float)currentFrameIndex / frameCount;
            EditorUtility.DisplayProgressBar("Exporting Frames", "Capturing frames...", progress);

            if (currentFrameIndex >= frameCount)
            {
                showProgressBar = false;
                EditorUtility.ClearProgressBar();
            }
        }

        bool canCreateSpriteSheet = selectedGameObject != null && Directory.Exists(Path.Combine(exportPath, selectedGameObject.name, selectedAnimationName));

        GUI.enabled = true;
    }

    private void ExportFramesBtn()
    {
        if (selectedGameObject == null)
        {
            Debug.LogError("Please select a GameObject with a Spine animation.");
            return;
        }

        if (string.IsNullOrEmpty(selectedAnimationName))
        {
            Debug.LogError("Please select an animation to export frames.");
            return;
        }

        // Check if the export folder exists
        string gameObjectFolder = Path.Combine(exportPath, selectedGameObject.name);
        string animationFolder = Path.Combine(gameObjectFolder, selectedAnimationName);

        bool folderExists = Directory.Exists(animationFolder);
        maxRes = Vector2.zero;

        if (folderExists)
        {
            // Delete existing PNG files in the folder

            string[] existingFiles = Directory.GetFiles(animationFolder, "*.png");
            int maxWidth = 0;
            int maxHeight = 0;

            if (!isFirstTime)
            {
                foreach (var filePath in existingFiles)
                {
                    // Load each image to get its dimensions
                    Texture2D image = new Texture2D(2, 2); // Create a new Texture2D
                    byte[] fileData = File.ReadAllBytes(filePath);
                    if (image.LoadImage(fileData)) // Load the image data
                    {
                        maxWidth = Mathf.Max(maxWidth, image.width);
                        maxHeight = Mathf.Max(maxHeight, image.height);
                    }
                }
            }

            // Update max resolution
            maxRes = new Vector2(maxWidth, maxHeight);
            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }
        }
        else
        {
            // Create folders for the export path
            if (!Directory.Exists(gameObjectFolder))
                Directory.CreateDirectory(gameObjectFolder);

            if (!Directory.Exists(animationFolder))
                Directory.CreateDirectory(animationFolder);
        }

        frameCount = Mathf.CeilToInt(selectedGameObject.GetComponent<SkeletonAnimation>().Skeleton.Data.FindAnimation(selectedAnimationName).Duration * framesPerSecond);
        currentFrameIndex = 0;
        isRecording = true;
        showProgressBar = true;
        StartRecording();
    }    

    private void CreateSpriteSheet()
    {
        string gameObjectFolder = Path.Combine(exportPath, selectedGameObject.name);
        string animationFolder = Path.Combine(gameObjectFolder, selectedAnimationName);
        string[] existingFiles = Directory.GetFiles(animationFolder, "*.png");
        Array.Sort(existingFiles);

        // Calculate the number of columns and rows based on your requirements
        int columns = 6;
        int rows = Mathf.CeilToInt((float)existingFiles.Length / columns);

        // Calculate the width and height of the sprite sheet based on the dimensions of the input images
        int spriteSheetWidth = columns * GetMaxImageWidth(existingFiles);
        int spriteSheetHeight = rows * GetMaxImageHeight(existingFiles);

        // Create a new texture for the sprite sheet
        Texture2D spriteSheet = new Texture2D(spriteSheetWidth, spriteSheetHeight);

        int currentImageIndex = 0;

        foreach (var filePath in existingFiles)
        {
            Texture2D image = new Texture2D(2, 2); // Create a new Texture2D
            byte[] fileData = File.ReadAllBytes(filePath);
            if (image.LoadImage(fileData))
            {
                int colIndex = currentImageIndex % columns;
                int rowIndex = currentImageIndex / columns;

                int xPos = colIndex * image.width;
                int yPos = (rows - rowIndex - 1) * image.height;

                spriteSheet.SetPixels(xPos, yPos, image.width, image.height, image.GetPixels());

                currentImageIndex++;
            }
        }

        spriteSheet.Apply();

        // Encode the sprite sheet to PNG
        byte[] spriteSheetBytes = spriteSheet.EncodeToPNG();
        string savePath = Path.Combine(gameObjectFolder, selectedAnimationName + ".png");
        // Save the sprite sheet as an image file in the same directory
        File.WriteAllBytes(savePath, spriteSheetBytes);

        // Destroy temporary textures to free up memory
        foreach (var filePath in existingFiles)
        {
            Texture2D image = new Texture2D(2, 2); // Create a new Texture2D
            byte[] fileData = File.ReadAllBytes(filePath);
            if (image.LoadImage(fileData))
                Texture2D.DestroyImmediate(image);
        }

        // Destroy the sprite sheet texture
        Texture2D.DestroyImmediate(spriteSheet);
        Debug.Log("Sprite sheet generate at: " + savePath);

        File.Delete(gameObjectFolder + selectedAnimationName + ".meta");
        DeleteFolder(animationFolder);

        AssetDatabase.Refresh();

    }

    private void DeleteFolder(string folderPath)
    {
        // Check if the folder exists
        if (Directory.Exists(folderPath))
        {
            // Delete all files and subdirectories inside the folder
            string[] allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (string file in allFiles)
            {
                File.Delete(file);
            }

            // Delete the folder itself
            Directory.Delete(folderPath, true);

            // Check if the folder was successfully deleted
            if (!Directory.Exists(folderPath))
            {
                Debug.Log("Folder deleted successfully.");
            }
            else
            {
                Debug.LogWarning("Folder deletion failed.");
            }
        }
    }


    int GetMaxImageWidth(string[] files)
    {
        int maxWidth = 0;
        foreach (var filePath in files)
        {
            Texture2D image = new Texture2D(2, 2);
            byte[] fileData = File.ReadAllBytes(filePath);
            if (image.LoadImage(fileData))
            {
                maxWidth = Mathf.Max(maxWidth, image.width);
            }
        }
        return maxWidth;
    }
    int GetMaxImageHeight(string[] files)
    {
        int maxHeight = 0;
        foreach (var filePath in files)
        {
            Texture2D image = new Texture2D(2, 2);
            byte[] fileData = File.ReadAllBytes(filePath);
            if (image.LoadImage(fileData))
            {
                maxHeight = Mathf.Max(maxHeight, image.height);
            }
        }
        return maxHeight;
    }

    private int GetSelectedCameraIndex()
    {
        if (captureCamera == null)
            return -1;

        for (int i = 0; i < sceneCameras.Length; i++)
        {
            if (sceneCameras[i] == captureCamera)
            {
                return i;
            }
        }
        return -1;
    }

    private void StartRecording()
    {
        EditorApplication.isPlaying = true;
        SceneView.FrameLastActiveSceneView();
        SceneView.lastActiveSceneView.Repaint();
        isRecording = true;
        currentFrameIndex = 0;
        Repaint();
    }

    private void Update()
    {
        if (isRecording && currentFrameIndex < frameCount)
        {
            float time = (float)currentFrameIndex / framesPerSecond;

            // Set the animation time
            selectedGameObject.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, selectedAnimationName, false);
            selectedGameObject.GetComponent<SkeletonAnimation>().AnimationState.Update(time);
            selectedGameObject.GetComponent<SkeletonAnimation>().AnimationState.Apply(selectedGameObject.GetComponent<SkeletonAnimation>().Skeleton);

            // Capture the frame
            CaptureFrame(currentFrameIndex);

            currentFrameIndex++;

            if (currentFrameIndex >= frameCount)
            {
                isRecording = false;
                EditorApplication.isPlaying = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
                AssetDatabase.Refresh();
                
                if (!isFirstTime)
                {
                    CreateSpriteSheet();
                }
                if (isFirstTime)
                {
                    isFirstTime = false;
                    ExportFramesBtn();
                }
            }
        }
    }

    private void CaptureFrame(int frameIndex)
    {
        // Render the current frame to a texture
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 32);
        captureCamera.targetTexture = renderTexture;
        captureCamera.Render();

        // Read pixels from the RenderTexture
        RenderTexture.active = renderTexture;
        Texture2D frameTexture = new Texture2D(renderTexture.width, renderTexture.height);
        frameTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        frameTexture.Apply();

        // Crop the frame to remove empty space
        Rect cropRect = CalculateCropRect(frameTexture);
        Texture2D croppedTexture = CropTexture(frameTexture, cropRect);

        // Save the frame texture as a PNG file
        string framePath = Path.Combine(exportPath, selectedGameObject.name, selectedAnimationName, $"Frame_{frameIndex:0000}.png");
        File.WriteAllBytes(framePath, croppedTexture.EncodeToPNG());

        // Clean up
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(frameTexture);
        UnityEngine.Object.DestroyImmediate(croppedTexture);
        captureCamera.targetTexture = null;
    }

    // Function to calculate the crop rect to remove empty space from the frame
    private Rect CalculateCropRect(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;

        int left = width;
        int right = 0;
        int top = height;
        int bottom = 0;

        // Iterate through the pixels to find the boundaries of the non-empty area
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color32 pixel = pixels[index];

                if (pixel.a > 0)
                {
                    // Update boundaries
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    top = Mathf.Min(top, y);
                    bottom = Mathf.Max(bottom, y);
                }
            }
        }

        // Calculate the width and height of the cropped area
        int croppedWidth = right - left + 1;
        int croppedHeight = bottom - top + 1;

        Rect cropRect = new Rect();
        // Create a rect for cropping
        if (maxRes == Vector2.zero)
            cropRect = new Rect(left, top, croppedWidth, croppedHeight);
        else
            cropRect = new Rect(left, top, maxRes.x, maxRes.y);
        return cropRect;
    }

    // Function to crop a texture using a given rect
    private Texture2D CropTexture(Texture2D texture, Rect cropRect)
    {
        int x = (int)cropRect.x;
        int y = (int)cropRect.y;
        int width = (int)cropRect.width;
        int height = (int)cropRect.height;

        Color[] pixels = texture.GetPixels(x, y, width, height);
        Texture2D croppedTexture = new Texture2D(width, height);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();

        return croppedTexture;
    }

    private void OnDestroy()
    {
        // Ensure that the progress bar is cleared when the window is closed
        EditorUtility.ClearProgressBar();
    }
}

